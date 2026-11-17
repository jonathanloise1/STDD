import { describe, expect, it, vi } from 'vitest';
import { createEngineRegistry, runAssistant, tierOrder } from './engine.ts';
import type { AnswerEngine, EngineAvailability } from './engine.ts';
import { createServerEngine } from './serverEngine.ts';
import { SECTIONS, testPack } from './fixtures/pack.ts';
import type { GenerationTier } from './types.ts';

const pack = testPack();
const ranked = SECTIONS.slice(2, 6);

function fakeEngine(
	tier: GenerationTier,
	availability: EngineAvailability,
	generate: AnswerEngine['generate'] = async () => 'Click "New Task".\nSource: [1]',
	timeoutMs = 1000,
): AnswerEngine {
	return { tier, timeoutMs, availability: async () => availability, generate };
}

const run = (engines: AnswerEngine[], extra: object = {}) =>
	runAssistant({
		engines,
		lang: 'en',
		question: 'How do I create a task',
		ranked,
		pack,
		packSha256: 'abc',
		...extra,
	});

describe('tier order and registry (manual.md 7.1)', () => {
	it('maps the setting to a chain', () => {
		expect(tierOrder('browser-first')).toEqual(['browser', 'server']);
		expect(tierOrder('server-first')).toEqual(['server', 'browser']);
		expect(tierOrder('retrieval-only')).toEqual([]);
	});

	it('returns the engines of the registered tiers in the order of the setting', () => {
		const registry = createEngineRegistry();
		registry.register('server', () => fakeEngine('server', { state: 'ready' }));
		registry.register('browser', () => fakeEngine('browser', { state: 'ready' }));
		const status = {
			order: 'server-first' as const,
			server: { available: true, timeoutSeconds: 1 },
			browser: { enabled: true, askBeforeDownloadMb: 100, timeoutSeconds: 1 },
		};
		expect(registry.enginesFor({ status, pack }).map((e) => e.tier)).toEqual([
			'server',
			'browser',
		]);
		expect(
			registry.enginesFor({ status: { ...status, order: 'retrieval-only' }, pack }),
		).toEqual([]);
	});

	it('uses no generation tier with an incompatible pack', () => {
		const registry = createEngineRegistry();
		registry.register('server', () => fakeEngine('server', { state: 'ready' }));
		const status = {
			order: 'browser-first' as const,
			server: { available: true, timeoutSeconds: 1 },
			browser: { enabled: true, askBeforeDownloadMb: 100, timeoutSeconds: 1 },
		};
		expect(registry.enginesFor({ status, pack: testPack({ compatible: false }) })).toEqual([]);
	});

	it('lets a factory decline the tier', () => {
		const registry = createEngineRegistry();
		registry.register('server', () => null);
		const status = {
			order: 'server-first' as const,
			server: { available: false, timeoutSeconds: 1 },
			browser: { enabled: false, askBeforeDownloadMb: 100, timeoutSeconds: 1 },
		};
		expect(registry.enginesFor({ status, pack })).toEqual([]);
	});
});

describe('assistant chain and fallback (manual.md 7.3)', () => {
	it('returns a valid answer with the cited positions', async () => {
		const result = await run([fakeEngine('server', { state: 'ready' })]);
		expect(result.outcome).toEqual({
			kind: 'answered',
			tier: 'server',
			body: 'Click "New Task".',
			cited: [1],
		});
	});

	it('falls back to retrieval without any engine', async () => {
		const result = await run([]);
		expect(result.outcome).toEqual({ kind: 'retrieval', failed: false });
	});

	it('shows the refusal as declined and never calls the next tier', async () => {
		const next = vi.fn(async () => 'x\nSource: [1]');
		const result = await run([
			fakeEngine(
				'browser',
				{ state: 'ready' },
				async () => "I can't find this in the manual.",
			),
			fakeEngine('server', { state: 'ready' }, next),
		]);
		expect(result.outcome).toEqual({ kind: 'declined', tier: 'browser', reason: 'refused' });
		expect(next).not.toHaveBeenCalled();
	});

	it('treats an answer without a source as declined (invalid)', async () => {
		const result = await run([
			fakeEngine('server', { state: 'ready' }, async () => 'Click New Task.'),
		]);
		expect(result.outcome).toEqual({ kind: 'declined', tier: 'server', reason: 'invalid' });
	});

	it('passes the question to the next tier when a tier errors', async () => {
		const result = await run([
			fakeEngine('browser', { state: 'ready' }, async () => {
				throw new Error('boom');
			}),
			fakeEngine('server', { state: 'ready' }),
		]);
		expect(result.outcome).toMatchObject({ kind: 'answered', tier: 'server' });
	});

	it('reports a timeout when every tier that ran failed', async () => {
		const never = () => new Promise<string>(() => {});
		const result = await run([fakeEngine('server', { state: 'ready' }, never, 20)]);
		expect(result.outcome).toEqual({ kind: 'retrieval', failed: true });
	});

	it('skips an unavailable tier without counting it as a failure', async () => {
		const result = await run([fakeEngine('browser', { state: 'unavailable' })]);
		expect(result.outcome).toEqual({ kind: 'retrieval', failed: false });
	});

	it('answers with the next tier while the first one prepares, and keeps the notice', async () => {
		const notices: string[] = [];
		const result = await run(
			[
				fakeEngine('browser', { state: 'preparing', doneMb: 30, totalMb: 120 }),
				fakeEngine('server', { state: 'ready' }),
			],
			{ onNotice: (n: { kind: string }) => notices.push(n.kind) },
		);
		expect(result.outcome).toMatchObject({ kind: 'answered', tier: 'server' });
		expect(result.notice).toEqual({ kind: 'preparing', doneMb: 30, totalMb: 120 });
		expect(notices).toEqual(['preparing']);
	});

	it('offers the download and falls back to the cards while consent is open', async () => {
		const result = await run([fakeEngine('browser', { state: 'consent', sizeMb: 120 })]);
		expect(result.notice).toEqual({ kind: 'consent', sizeMb: 120 });
		expect(result.outcome).toEqual({ kind: 'retrieval', failed: false });
	});

	it('streams text without the source line', async () => {
		const seen: string[] = [];
		await run(
			[
				fakeEngine('server', { state: 'ready' }, async (req) => {
					req.onText('Click "New Task".\nSou');
					return 'Click "New Task".\nSource: [1]';
				}),
			],
			{ onText: (text: string) => seen.push(text) },
		);
		expect(seen).toEqual(['Click "New Task".']);
	});
});

describe('server engine', () => {
	it('sends the section ids in context order and returns the output', async () => {
		const answer = vi.fn(async () => 'Body.\nSource: [1]');
		const engine = createServerEngine(20, { answer });
		const controller = new AbortController();
		const out = await engine.generate({
			lang: 'en',
			question: 'q',
			sections: SECTIONS.slice(2, 4),
			packSha256: 'abc',
			signal: controller.signal,
			onText: () => undefined,
		});
		expect(out).toBe('Body.\nSource: [1]');
		expect(answer).toHaveBeenCalledWith(
			{
				lang: 'en',
				question: 'q',
				sectionIds: [SECTIONS[2].id, SECTIONS[3].id],
				packSha256: 'abc',
			},
			expect.objectContaining({ signal: controller.signal }),
		);
		expect(engine.timeoutMs).toBeGreaterThan(20000);
	});
});
