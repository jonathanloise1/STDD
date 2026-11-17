import { describe, expect, it, vi } from 'vitest';
import { askQuestion, buildGap, rateNo } from './askController.ts';
import type { AskDeps, AskSession } from './askController.ts';
import { askReducer, initialAskState, orderCards } from './askState.ts';
import type { AskAction, AskState } from './askState.ts';
import { createEngineRegistry } from './engine.ts';
import type { AnswerEngine } from './engine.ts';
import { testPack } from './fixtures/pack.ts';
import type { GapRecord, HelpStatus } from './types.ts';

const pack = testPack();
const loaded = { pack, sha256: 'packsha' };
const serverStatus: HelpStatus = {
	order: 'server-first',
	server: { available: true, timeoutSeconds: 1 },
	browser: { enabled: false, askBeforeDownloadMb: 100, timeoutSeconds: 1 },
};

function setup(generate?: AnswerEngine['generate'], status: HelpStatus = serverStatus) {
	const registry = createEngineRegistry();
	if (generate)
		registry.register('server', () => ({
			tier: 'server',
			timeoutMs: 1000,
			availability: async () => ({ state: 'ready' }),
			generate,
		}));
	const gaps: GapRecord[] = [];
	const deps: AskDeps = {
		lang: 'en',
		route: '/tasks',
		loadPack: async () => loaded,
		loadStatus: async () => status,
		registry,
		recordGap: async (record) => void gaps.push(record),
		now: () => new Date('2026-11-06T10:15:00Z'),
	};
	let state: AskState = initialAskState;
	const sessions: AskSession[] = [];
	const callbacks = {
		dispatch: (a: AskAction) => (state = askReducer(state, a)),
		onSession: (s: AskSession) => void sessions.push(s),
		onNoticeEngine: () => undefined,
	};
	return {
		deps,
		gaps,
		sessions,
		callbacks,
		get state() {
			return state;
		},
	};
}

describe('asking a question (manual.md 7.2)', () => {
	it('shows at most three cards headed "best matches" when no model answers, and records no gap', async () => {
		const t = setup();
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		expect(t.state).toMatchObject({
			phase: 'results',
			header: 'best',
			done: true,
			answer: null,
		});
		if (t.state.phase !== 'results') throw new Error('unreachable');
		expect(t.state.cards.length).toBeLessThanOrEqual(3);
		expect(t.state.cards[0].pageTitle).toBe('Create a task');
		expect(t.gaps).toEqual([]);
	});

	it('answers out of scope with the fixed reply and records a no_match gap with masked text', async () => {
		const t = setup();
		await askQuestion(t.deps, 'Can I pay invoice 12345678 from here?', 1, t.callbacks);
		expect(t.state.phase).toBe('outOfScope');
		expect(t.gaps).toHaveLength(1);
		expect(t.gaps[0]).toMatchObject({
			schema_version: 1,
			outcome: 'no_match',
			tier: 'retrieval',
			route: '/tasks',
			lang: 'en',
			question: 'Can I pay invoice [number] from here?',
			pack: { project_version: '0.1.0', sha256: 'packsha' },
		});
		expect(t.sessions).toHaveLength(1);
	});

	it('shows the answer with source chips and the cited section first', async () => {
		const t = setup(async () => 'Open the Tasks page and click "New Task".\nSource: [2]');
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		if (t.state.phase !== 'results') throw new Error('unreachable');
		expect(t.state.answer).toMatchObject({ tier: 'server', complete: true });
		expect(t.state.answer?.sources).toHaveLength(1);
		expect(t.state.answer?.sources[0].n).toBe(2);
		expect(t.state.cards[0].url).toBe(t.state.answer?.sources[0].url);
		expect(t.gaps).toEqual([]);
	});

	it('shows the declined message and records refused for the refusal sentence', async () => {
		const t = setup(async () => "I can't find this in the manual.");
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		expect(t.state).toMatchObject({ phase: 'results', header: 'declined', answer: null });
		expect(t.gaps.map((g) => [g.outcome, g.tier])).toEqual([['refused', 'server']]);
	});

	it('records invalid for an answer without a source', async () => {
		const t = setup(async () => 'Just click it.');
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		expect(t.state).toMatchObject({ header: 'declined' });
		expect(t.gaps.map((g) => g.outcome)).toEqual(['invalid']);
	});

	it('falls back to the best matches and records timeout when the only tier fails', async () => {
		const t = setup(async () => {
			throw new Error('slow');
		});
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		expect(t.state).toMatchObject({ phase: 'results', header: 'best', answer: null });
		expect(t.gaps.map((g) => [g.outcome, g.tier])).toEqual([['timeout', 'retrieval']]);
	});

	it('runs retrieval only when the status call fails', async () => {
		const t = setup(async () => 'x\nSource: [1]');
		t.deps.loadStatus = async () => {
			throw new Error('down');
		};
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		expect(t.state).toMatchObject({ phase: 'results', header: 'best', answer: null });
	});

	it('says help is unavailable when the pack cannot be loaded, with no card and no gap', async () => {
		const t = setup();
		t.deps.loadPack = async () => {
			throw new Error('missing');
		};
		await askQuestion(t.deps, 'How do I create a task', 1, t.callbacks);
		expect(t.state.phase).toBe('unavailable');
		expect(t.gaps).toEqual([]);
	});

	it('keeps working when the gap cannot be recorded', async () => {
		const t = setup();
		t.deps.recordGap = vi.fn(async () => {
			throw new Error('down');
		});
		await expect(
			askQuestion(t.deps, 'Can I pay my invoice from here?', 1, t.callbacks),
		).resolves.toBeUndefined();
		expect(t.state.phase).toBe('outOfScope');
	});
});

describe('rating (US-HELP-06 behaviour)', () => {
	it('records not_helpful for No with the question context', async () => {
		const t = setup();
		await askQuestion(t.deps, 'How do I create a task with a title', 1, t.callbacks);
		await rateNo(t.deps, loaded, t.sessions[0]);
		expect(t.gaps.map((g) => g.outcome)).toEqual(['not_helpful']);
		expect(t.gaps[0].top_sections.length).toBeGreaterThan(0);
	});

	it('builds the record of 7.4', () => {
		const gap = buildGap(
			{ lang: 'it', route: '/tasks', now: () => new Date('2026-11-06T10:15:00Z') },
			loaded,
			{ question: 'mail a@b.it', tier: 'retrieval', topSections: ['a#b'], coverage: 0.2249 },
			'no_match',
		);
		expect(gap).toEqual({
			schema_version: 1,
			recorded_at: '2026-11-06T10:15:00.000Z',
			lang: 'it',
			route: '/tasks',
			question: 'mail [email]',
			outcome: 'no_match',
			tier: 'retrieval',
			pack: { project_version: '0.1.0', sha256: 'packsha' },
			top_sections: ['a#b'],
			coverage: 0.22,
		});
	});
});

describe('panel reducer', () => {
	it('ignores actions of an earlier question', () => {
		let s = askReducer(initialAskState, { type: 'started', seq: 2 });
		s = askReducer(s, { type: 'outOfScope', seq: 1, question: 'old' });
		expect(s.phase).toBe('idle');
	});

	it('hides the cards header while an answer streams and keeps the vote', () => {
		let s = askReducer(initialAskState, { type: 'started', seq: 1 });
		s = askReducer(s, { type: 'retrieved', seq: 1, question: 'q', cards: [] });
		s = askReducer(s, { type: 'text', seq: 1, text: 'Click', tier: 'server' });
		expect(s).toMatchObject({ header: 'none', answer: { text: 'Click', complete: false } });
		s = askReducer(s, { type: 'rated', seq: 1, rating: 'no' });
		expect(s).toMatchObject({ rating: 'no' });
	});

	it('moves the cited sections first and keeps at most three cards', () => {
		const view = (id: string) => ({
			id,
			pageId: id,
			pageTitle: id,
			heading: null,
			snippet: '',
			url: `/${id}`,
		});
		const ranked = ['a', 'b', 'c', 'd'].map(view);
		expect(orderCards(ranked, ['c']).map((c) => c.id)).toEqual(['c', 'a', 'b']);
		expect(orderCards(ranked, []).map((c) => c.id)).toEqual(['a', 'b', 'c']);
	});
});
