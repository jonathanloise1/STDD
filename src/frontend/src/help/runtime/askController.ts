/**
 * One question of the "Ask" tab, from retrieval to the gap record (manual.md 7.2, 7.3, 7.4). Framework-free: the
 * panel passes its dispatch function and the services as dependencies.
 */
import type { HelpLang, HelpPack } from '../index.ts';
import { orderCards } from './askState.ts';
import type { AskAction } from './askState.ts';
import { runAssistant, tierOfOutcome } from './engine.ts';
import type { AnswerEngine, AssistantOutcome, EngineRegistry } from './engine.ts';
import { maskQuestion } from './mask.ts';
import { MAX_CARDS, createPackSearcher, retrieve, viewOf } from './searcher.ts';
import type { PackSearcher } from './searcher.ts';
import { RETRIEVAL_ONLY_STATUS } from './types.ts';
import type { AssistantTier, GapOutcome, GapRecord, HelpStatus, LoadedPack } from './types.ts';

export interface AskDeps {
	lang: HelpLang;
	/** The route pattern of the screen, or its path when no pattern matches. */
	route: string;
	loadPack(): Promise<LoadedPack<HelpPack>>;
	loadStatus(): Promise<HelpStatus>;
	registry: EngineRegistry;
	recordGap(record: GapRecord): Promise<void>;
	now(): Date;
}

/** What the rating needs to remember about the question that was answered. */
export interface AskSession {
	question: string;
	tier: AssistantTier;
	topSections: string[];
	coverage: number;
	/** The engine that offered a download, so the panel can pass on the user's choice. */
	noticeEngine: AnswerEngine | null;
	engines: AnswerEngine[];
}

const TOP_SECTIONS = 3;

/** The record of 7.4; the question is masked here and again by the backend. */
export function buildGap(
	deps: Pick<AskDeps, 'lang' | 'route' | 'now'>,
	loaded: LoadedPack<HelpPack>,
	session: Pick<AskSession, 'question' | 'tier' | 'topSections' | 'coverage'>,
	outcome: GapOutcome,
): GapRecord {
	return {
		schema_version: 1,
		recorded_at: deps.now().toISOString(),
		lang: deps.lang,
		route: deps.route,
		question: maskQuestion(session.question),
		outcome,
		tier: session.tier,
		pack: { project_version: loaded.pack.project.version, sha256: loaded.sha256 },
		top_sections: session.topSections,
		coverage: Math.round(session.coverage * 100) / 100,
	};
}

/** Recording a gap never disturbs the user: a failure is dropped. */
async function record(deps: AskDeps, gap: GapRecord): Promise<void> {
	try {
		await deps.recordGap(gap);
	} catch {
		/* the documentation owners lose one data point; the user loses nothing */
	}
}

const gapOutcomeOf = (outcome: AssistantOutcome): GapOutcome | null => {
	if (outcome.kind === 'declined') return outcome.reason;
	if (outcome.kind === 'retrieval' && outcome.failed) return 'timeout';
	return null;
};

/** What the panel hands to `askQuestion` to receive the progress of a question. */
export interface AskCallbacks {
	dispatch: (action: AskAction) => void;
	/** The session the rating needs, given before the result that offers the rating is dispatched. */
	onSession: (session: AskSession, loaded: LoadedPack<HelpPack>) => void;
	/** The engine that offered a download, given with the notice, so the user's choice reaches it. */
	onNoticeEngine: (engine: AnswerEngine) => void;
}

/**
 * Asks one question. Dispatches the actions of the reducer as results become known; resolves when the question
 * has been answered and its gap, if any, has been recorded.
 */
export async function askQuestion(
	deps: AskDeps,
	question: string,
	seq: number,
	callbacks: AskCallbacks,
	signal?: AbortSignal,
): Promise<void> {
	const { dispatch, onSession, onNoticeEngine } = callbacks;
	dispatch({ type: 'started', seq });
	let loaded: LoadedPack<HelpPack>;
	let searcher: PackSearcher;
	try {
		loaded = await deps.loadPack();
		searcher = createPackSearcher(loaded.pack, loaded.embeddings ?? null);
	} catch {
		dispatch({ type: 'unavailable', seq });
		return;
	}

	const { ranked, decision } = retrieve(searcher, question);
	const topSections = ranked.slice(0, TOP_SECTIONS).map((s) => s.id);
	const base = { question, topSections, coverage: decision.coverage };

	if (!decision.inScope) {
		const session: AskSession = { ...base, tier: 'retrieval', noticeEngine: null, engines: [] };
		onSession(session, loaded);
		dispatch({ type: 'outOfScope', seq, question });
		await record(deps, buildGap(deps, loaded, session, 'no_match'));
		return;
	}

	const views = ranked.map((s) => viewOf(searcher, s));
	dispatch({ type: 'retrieved', seq, question, cards: views.slice(0, MAX_CARDS) });

	let status: HelpStatus;
	try {
		status = await deps.loadStatus();
	} catch {
		status = RETRIEVAL_ONLY_STATUS;
	}
	const engines = deps.registry.enginesFor({ status, pack: loaded.pack });

	const result = await runAssistant({
		engines,
		lang: deps.lang,
		question,
		ranked,
		pack: loaded.pack,
		packSha256: loaded.sha256,
		signal,
		onText: (text, tier) => dispatch({ type: 'text', seq, text, tier }),
		onNotice: (notice, engine) => {
			onNoticeEngine(engine);
			dispatch({ type: 'notice', seq, notice });
		},
	});

	const outcome = result.outcome;
	const session: AskSession = {
		...base,
		tier: tierOfOutcome(outcome),
		noticeEngine: result.noticeEngine,
		engines,
	};
	onSession(session, loaded);
	if (outcome.kind === 'answered') {
		const cited = outcome.cited.map((n) => result.context[n - 1]);
		dispatch({
			type: 'finished',
			seq,
			cards: orderCards(
				views,
				cited.map((s) => s.id),
			),
			header: 'none',
			answer: {
				text: outcome.body,
				tier: outcome.tier,
				sources: outcome.cited.map((n, i) => ({ n, url: cited[i].url })),
				complete: true,
			},
		});
	} else {
		dispatch({
			type: 'finished',
			seq,
			cards: views.slice(0, MAX_CARDS),
			header: outcome.kind === 'declined' ? 'declined' : 'best',
			answer: null,
		});
	}

	const gap = gapOutcomeOf(outcome);
	if (gap) await record(deps, buildGap(deps, loaded, session, gap));
}

/** "No" under a result records the question as `not_helpful`; "Yes" is not passed on. */
export async function rateNo(
	deps: AskDeps,
	loaded: LoadedPack<HelpPack>,
	session: AskSession,
): Promise<void> {
	await record(deps, buildGap(deps, loaded, session, 'not_helpful'));
}
