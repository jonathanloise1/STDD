/**
 * The tiers above retrieval (manual.md 7.1, 7.3). An `AnswerEngine` turns a question and its sections into the raw
 * model output; this module decides which engines run, in which order, and what the panel shows when they fail.
 * Pure: no React, no network. The server engine is in `serverEngine.ts`; the browser engine registers in `engines.ts`.
 */
import { selectContext } from '../index.ts';
import type { HelpLang, HelpPack, PackSection, ProtocolContext } from '../index.ts';
import { parseAnswer, streamingText } from './parseAnswer.ts';
import type { AssistantTier, GenerationTier, HelpStatus } from './types.ts';

/** The state of an engine for the next question. */
export type EngineAvailability =
	| { state: 'ready' }
	/** `reason` is a device reason code (`help:deviceReason.*`) that the panel shows in its status line. */
	| { state: 'unavailable'; reason?: string }
	/** A first download larger than the threshold needs the user's consent (manual.md 7.2, models.md 7.3). */
	| { state: 'consent'; sizeMb: number }
	/** Weights are downloading; the next tier answers meanwhile. */
	| { state: 'preparing'; doneMb: number; totalMb: number };

export interface EngineRequest {
	lang: HelpLang;
	question: string;
	/** The sections of the context in rank order; `[n]` in the output is the position in this list. */
	sections: PackSection[];
	packSha256: string;
	signal: AbortSignal;
	/** Called with the whole output so far while it streams. */
	onText: (partial: string) => void;
}

export interface AnswerEngine {
	readonly tier: GenerationTier;
	/** Time allowed per question; after it the engine is abandoned for this question. */
	readonly timeoutMs: number;
	availability(): Promise<EngineAvailability>;
	/** Resolves with the complete raw output; rejects on error or abort. */
	generate(request: EngineRequest): Promise<string>;
	/** The user accepted the download offered by the `consent` state. */
	acceptDownload?(): void;
	/** The user declined it; the engine remembers the choice (manual.md 7.2). */
	declineDownload?(): void;
	/** Availability changes while the panel is open (download progress); returns the unsubscribe function. */
	subscribe?(listener: (availability: EngineAvailability) => void): () => void;
}

export interface EngineFactoryContext {
	status: HelpStatus;
	pack: HelpPack;
}

/** Returns the engine of a tier, or null when the tier is not eligible. */
export type EngineFactory = (context: EngineFactoryContext) => AnswerEngine | null;

export interface EngineRegistry {
	register(tier: GenerationTier, factory: EngineFactory): void;
	/** The eligible engines in the order of the tier setting; empty without a compatible pack. */
	enginesFor(context: EngineFactoryContext): AnswerEngine[];
}

export function createEngineRegistry(): EngineRegistry {
	const factories = new Map<GenerationTier, EngineFactory>();
	return {
		register: (tier, factory) => void factories.set(tier, factory),
		enginesFor(context) {
			if (!context.pack.assistant.compatible) return [];
			const order = tierOrder(context.status.order);
			return order
				.map((tier) => factories.get(tier)?.(context) ?? null)
				.filter((e): e is AnswerEngine => e !== null);
		},
	};
}

/** `MyApp:Assistant:Order` as the chain of generation tiers (models.md 7.1). */
export function tierOrder(order: HelpStatus['order']): GenerationTier[] {
	if (order === 'browser-first') return ['browser', 'server'];
	if (order === 'server-first') return ['server', 'browser'];
	return [];
}

/** A notice the panel shows above the cards while another tier answers. */
export type EngineNotice =
	| { kind: 'consent'; sizeMb: number }
	| { kind: 'preparing'; doneMb: number; totalMb: number }
	/** The tier cannot run on this device; shown in the status line while the next tier answers. */
	| { kind: 'device'; reason: string };

export type AssistantOutcome =
	/** A valid answer with sources. */
	| { kind: 'answered'; tier: GenerationTier; body: string; cited: number[] }
	/** The model refused or its output was unusable (7.3). */
	| { kind: 'declined'; tier: GenerationTier; reason: 'refused' | 'invalid' }
	/** No generation tier answered: none was eligible, or every one that ran failed. */
	| { kind: 'retrieval'; failed: boolean };

export interface AssistantResult {
	outcome: AssistantOutcome;
	/** The sections that were sent to the model, in order. */
	context: PackSection[];
	/** The notice of the engine that could not answer yet, if any. */
	notice: EngineNotice | null;
	/** The engine that has the notice, so the panel can forward the user's choice. */
	noticeEngine: AnswerEngine | null;
}

export interface AssistantRun {
	engines: readonly AnswerEngine[];
	lang: HelpLang;
	question: string;
	ranked: readonly PackSection[];
	pack: HelpPack;
	packSha256: string;
	signal?: AbortSignal;
	/** The streamed text that may be shown, with the tier that writes it. */
	onText?: (text: string, tier: GenerationTier) => void;
	/** Raised as soon as an engine reports consent or preparation. */
	onNotice?: (notice: EngineNotice, engine: AnswerEngine) => void;
}

const DEFAULT_CONTEXT: Pick<ProtocolContext, 'max_sections' | 'max_context_chars' | 'scan_limit'> =
	{
		max_sections: 4,
		max_context_chars: 4000,
		scan_limit: 8,
	};

/** The refusal sentence of a language from the pack's protocol. */
export function refusalOf(pack: HelpPack, lang: HelpLang): string {
	const languages = (
		pack.assistant.protocol.content as { languages?: Record<string, { refusal?: string }> }
	).languages;
	return languages?.[lang]?.refusal ?? '';
}

function answerLimits(pack: HelpPack, lang: HelpLang, sources: number) {
	const answer = (
		pack.assistant.protocol.content as {
			answer?: { max_body_chars?: number; max_body_lines?: number };
		}
	).answer;
	return {
		sources,
		maxBodyChars: answer?.max_body_chars ?? 600,
		maxBodyLines: answer?.max_body_lines ?? 7,
		refusal: refusalOf(pack, lang),
	};
}

/** Runs `generate` with its time limit; the caller's signal also aborts it. */
async function generateWithin(
	engine: AnswerEngine,
	request: Omit<EngineRequest, 'signal'>,
	outer?: AbortSignal,
) {
	const controller = new AbortController();
	const abort = () => controller.abort();
	outer?.addEventListener('abort', abort);
	const timer = setTimeout(abort, engine.timeoutMs);
	const aborted = new Promise<never>((_, reject) => {
		controller.signal.addEventListener('abort', () => reject(new Error('aborted')));
	});
	try {
		return await Promise.race([
			engine.generate({ ...request, signal: controller.signal }),
			aborted,
		]);
	} finally {
		clearTimeout(timer);
		outer?.removeEventListener('abort', abort);
	}
}

/**
 * The chain of manual.md 7.1 and 7.3: try each eligible engine in order; one that is not ready leaves a notice and
 * the next answers; one that errors or times out passes the question on; a refusal or an unusable output ends the
 * chain. The sections of retrieval are always the caller's to show.
 */
export async function runAssistant(run: AssistantRun): Promise<AssistantResult> {
	const context = selectContext(
		run.ranked,
		run.pack.assistant.protocol.content.context ?? DEFAULT_CONTEXT,
	);
	const refusal = refusalOf(run.pack, run.lang);
	let notice: EngineNotice | null = null;
	let noticeEngine: AnswerEngine | null = null;
	let attempted = 0;

	if (context.length > 0) {
		for (const engine of run.engines) {
			let availability: EngineAvailability;
			try {
				availability = await engine.availability();
			} catch {
				continue;
			}
			if (availability.state === 'unavailable') {
				if (availability.reason && !notice) {
					notice = { kind: 'device', reason: availability.reason };
					noticeEngine = null;
					run.onNotice?.(notice, engine);
				}
				continue;
			}
			if (availability.state === 'consent' || availability.state === 'preparing') {
				if (!notice || notice.kind === 'device') {
					notice =
						availability.state === 'consent'
							? { kind: 'consent', sizeMb: availability.sizeMb }
							: {
									kind: 'preparing',
									doneMb: availability.doneMb,
									totalMb: availability.totalMb,
								};
					noticeEngine = engine;
					run.onNotice?.(notice, engine);
				}
				continue;
			}
			attempted += 1;
			try {
				const raw = await generateWithin(
					engine,
					{
						lang: run.lang,
						question: run.question,
						sections: context,
						packSha256: run.packSha256,
						onText: (partial) =>
							run.onText?.(streamingText(partial, refusal), engine.tier),
					},
					run.signal,
				);
				const parsed = parseAnswer(raw, answerLimits(run.pack, run.lang, context.length));
				if (parsed.kind === 'answered')
					return {
						outcome: {
							kind: 'answered',
							tier: engine.tier,
							body: parsed.body,
							cited: parsed.cited,
						},
						context,
						notice,
						noticeEngine,
					};
				return {
					outcome: { kind: 'declined', tier: engine.tier, reason: parsed.kind },
					context,
					notice,
					noticeEngine,
				};
			} catch {
				if (run.signal?.aborted) break;
			}
		}
	}
	return { outcome: { kind: 'retrieval', failed: attempted > 0 }, context, notice, noticeEngine };
}

/** The tier recorded in a gap for an outcome. */
export function tierOfOutcome(outcome: AssistantOutcome): AssistantTier {
	return outcome.kind === 'retrieval' ? 'retrieval' : outcome.tier;
}
