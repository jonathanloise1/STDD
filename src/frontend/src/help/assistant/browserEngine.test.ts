import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { beforeEach, describe, expect, it } from 'vitest';
import { createBrowserEngineFactory } from './browserEngine.ts';
import type { BrowserEngineDeps } from './browserEngine.ts';
import { goodAdapter } from './gpuCheck.test.ts';
import type { GpuAdapterLike, KeyValueStore } from './gpuCheck.ts';
import type { FromWorker, ToWorker, WorkerPort } from './messages.ts';
import { createWorkerCore } from './workerCore.ts';
import type { CacheLike, TextModel, TransformersLib } from './workerCore.ts';
import { createEngineRegistry, runAssistant } from '../runtime/engine.ts';
import type { AnswerEngine } from '../runtime/engine.ts';
import { SECTIONS, testPack } from '../runtime/fixtures/pack.ts';
import { normalizeStatus } from '../runtime/status.ts';
import type { HelpStatus } from '../runtime/types.ts';

const MB = 1024 * 1024;
const ANSWER = 'Click "New Task".\nSource: [1]';
const BASE = '/models/assistant/';
const FILE = `http://localhost${BASE}1.0.0/onnx/model_q4f16.onnx`;

interface World {
	adapter: GpuAdapterLike | null;
	deviceMemory?: number;
	/** Size of the weights in bytes; null means the file does not exist. */
	sizeBytes: number | null;
	cacheKeys: Set<string>;
	storage: Map<string, string>;
	storageThrows: boolean;
	tokensPerSecond: number;
	generate: (opts: { signal: AbortSignal; onText: (t: string) => void }) => Promise<string>;
	loadError: boolean;
	clock: number;
	env: Record<string, unknown>;
	persisted: number;
	loads: number;
	checks: number;
	prompts: string[];
	deleted: string[];
	workerStarts: number;
}

function newWorld(): World {
	return {
		adapter: goodAdapter(),
		sizeBytes: 50 * MB,
		cacheKeys: new Set(),
		storage: new Map(),
		storageThrows: false,
		tokensPerSecond: 40,
		generate: async ({ onText }) => {
			onText('Click "New');
			onText(ANSWER);
			return ANSWER;
		},
		loadError: false,
		clock: 0,
		env: {},
		persisted: 0,
		loads: 0,
		checks: 0,
		prompts: [],
		deleted: [],
		workerStarts: 0,
	};
}

function deps(w: World): BrowserEngineDeps {
	const model: TextModel = {
		renderPrompt: (messages, kwargs) => JSON.stringify({ messages, kwargs }),
		async generate(prompt, opts) {
			w.prompts.push(prompt);
			if (opts.minNewTokens) {
				w.clock += (opts.minNewTokens / w.tokensPerSecond) * 1000;
				return { text: 'one two', tokens: opts.minNewTokens };
			}
			const text = await w.generate(opts);
			return { text, tokens: 20 };
		},
	};
	const lib: TransformersLib = {
		env: w.env,
		async loadModel(id, options) {
			w.loads += 1;
			if (w.loadError) throw new Error('boom');
			expect(id).toBe('1.0.0');
			options.onProgress('onnx/model_q4f16.onnx', 25 * MB, 50 * MB);
			options.onProgress('tokenizer.json', 1 * MB, 1 * MB);
			w.cacheKeys.add(FILE);
			return model;
		},
	};
	const cache: CacheLike = {
		match: async (url) => (w.cacheKeys.has(url) ? {} : undefined),
		keys: async () => [...w.cacheKeys].map((url) => ({ url })),
		delete: async (request) => {
			const url = typeof request === 'string' ? request : request.url;
			w.deleted.push(url);
			return w.cacheKeys.delete(url);
		},
	};
	return {
		store: (): KeyValueStore => {
			if (w.storageThrows) throw new Error('denied');
			return {
				getItem: (k) => w.storage.get(k) ?? null,
				setItem: (k, v) => void w.storage.set(k, v),
			};
		},
		createWorker(): WorkerPort {
			w.workerStarts += 1;
			let listener: (m: FromWorker) => void = () => undefined;
			const core = createWorkerCore({
				loadLibrary: async () => lib,
				gpuEnv: () => ({
					gpu: {
						requestAdapter: async () => {
							w.checks += 1;
							return w.adapter;
						},
					},
					deviceMemory: w.deviceMemory,
				}),
				fetch: async () => ({
					ok: w.sizeBytes !== null,
					headers: { get: () => (w.sizeBytes === null ? null : String(w.sizeBytes)) },
				}),
				openCache: async () => cache,
				persist: async () => {
					w.persisted += 1;
					return true;
				},
				origin: 'http://localhost',
				now: () => w.clock,
				post: (m) => queueMicrotask(() => listener(m)),
			});
			return {
				post: (message: ToWorker) => void queueMicrotask(() => void core.handle(message)),
				onMessage: (l) => void (listener = l),
				onError: () => undefined,
				terminate: () => undefined,
			};
		},
	};
}

/** A pack with the real protocol of training and a model entry, as the pack builder writes them. */
function assistantPack(compatible = true) {
	const content = JSON.parse(
		readFileSync(resolve(process.cwd(), '../../training/assistant/protocol.json'), 'utf8'),
	);
	const pack = testPack({ compatible });
	pack.assistant.protocol = { sha256: 'p', content };
	pack.assistant.model = {
		id: 'assistant',
		version: '1.0.0',
		protocol_sha256: 'p',
		retrieval_sha256: 'r',
	};
	return pack;
}
const pack = assistantPack();
const status = (
	over: Partial<HelpStatus['browser']> = {},
	order: HelpStatus['order'] = 'browser-first',
): HelpStatus => ({
	order,
	server: { available: false, timeoutSeconds: 1 },
	browser: { enabled: true, askBeforeDownloadMb: 100, timeoutSeconds: 5, ...over },
});

let w: World;
beforeEach(() => {
	w = newWorld();
});

const engineOf = (over: Partial<HelpStatus['browser']> = {}, p = pack) => {
	const engine = createBrowserEngineFactory(deps(w))({ status: status(over), pack: p });
	if (!engine) throw new Error('not eligible');
	return engine;
};

/** Polls availability like the next question would, until it is not `preparing`. */
async function settle(engine: AnswerEngine) {
	for (let i = 0; i < 50; i += 1) {
		const a = await engine.availability();
		if (a.state !== 'preparing') return a;
		await new Promise((r) => setTimeout(r, 0));
	}
	throw new Error('never settled');
}

const request = (
	engine: AnswerEngine,
	signal = new AbortController().signal,
	onText = () => undefined,
) =>
	engine.generate({
		lang: 'en',
		question: 'How do I create a task',
		sections: SECTIONS.slice(2, 4),
		packSha256: 'abc',
		signal,
		onText,
	});

describe('eligibility (models.md 7.5)', () => {
	const factory = (p = pack, s = status()) =>
		createBrowserEngineFactory(deps(w))({ status: s, pack: p });

	it('is eligible with a compatible pack and the setting on', () =>
		expect(factory()).not.toBeNull());
	it('is not eligible when the setting is off', () =>
		expect(factory(pack, status({ enabled: false }))).toBeNull());
	it('is not eligible when the pack says not compatible', () =>
		expect(factory(assistantPack(false))).toBeNull());
	it('is not eligible when the pack has no model (no export)', () => {
		const p = assistantPack();
		p.assistant.model = null;
		expect(factory(p)).toBeNull();
	});
	it('is not eligible when the model was trained with another protocol', () => {
		const p = assistantPack();
		p.assistant.model = { ...p.assistant.model!, protocol_sha256: 'other' };
		expect(factory(p)).toBeNull();
	});
	it('is not eligible without Worker support and no injected deps', () => {
		expect(createBrowserEngineFactory()({ status: status(), pack })).toBeNull();
	});
	it('is unavailable with reason notFound when the weights are missing', async () => {
		w.sizeBytes = null;
		expect(await engineOf().availability()).toEqual({
			state: 'unavailable',
			reason: 'notFound',
		});
	});
});

describe('device check in the engine (models.md 7.2)', () => {
	it('keeps the reason of a failed check and the panel can show it', async () => {
		w.adapter = goodAdapter({ info: { device: 'SwiftShader' } });
		const a = await engineOf().availability();
		expect(a).toEqual({ state: 'unavailable', reason: 'softwareAdapter' });
		expect(w.storage.get('help.assistant.webgpu.1.0.0')).toBe('fail:softwareAdapter');
		expect(w.loads).toBe(0);
	});

	it('does not check again once a failure is stored', async () => {
		w.storage.set('help.assistant.webgpu.1.0.0', 'fail:noShaderF16');
		const a = await engineOf().availability();
		expect(a).toEqual({ state: 'unavailable', reason: 'noShaderF16' });
		expect(w.checks).toBe(0);
		expect(w.workerStarts).toBe(0);
	});

	it('checks again for a new model version', async () => {
		w.storage.set('help.assistant.webgpu.0.9.0', 'fail:noShaderF16');
		const a = await engineOf().availability();
		expect(a.state).toBe('preparing');
		expect(w.checks).toBe(1);
	});

	it('checks again when storage throws, and still works', async () => {
		w.storageThrows = true;
		const engine = engineOf();
		expect((await engine.availability()).state).toBe('preparing');
		expect((await settle(engine)).state).toBe('ready');
		expect(w.checks).toBe(1);
	});

	it('fails the whole check when the warm-up is too slow (step 8)', async () => {
		w.tokensPerSecond = 3;
		const engine = engineOf({ minTokensPerSecond: 8 });
		await engine.availability();
		expect(await settle(engine)).toEqual({ state: 'unavailable', reason: 'tooSlow' });
		expect(w.storage.get('help.assistant.webgpu.1.0.0')).toBe('fail:tooSlow');
	});

	it('stores ok after a fast warm-up and skips step 8 the next time', async () => {
		w.tokensPerSecond = 40;
		const engine = engineOf({ minTokensPerSecond: 8 });
		await engine.availability();
		expect((await settle(engine)).state).toBe('ready');
		expect(w.storage.get('help.assistant.webgpu.1.0.0')).toBe('ok');
		const warmups = w.prompts.length;
		expect(warmups).toBe(1);
		// A new page: stored ok, no check, no warm-up
		const second = newWorld();
		second.storage = w.storage;
		second.cacheKeys = w.cacheKeys;
		const again = createBrowserEngineFactory(deps(second))({ status: status(), pack })!;
		await again.availability();
		expect((await settle(again)).state).toBe('ready');
		expect(second.checks).toBe(0);
		expect(second.prompts).toEqual([]);
	});
});

describe('weights (models.md 7.3)', () => {
	it('sets the same-origin rules on the library', async () => {
		const engine = engineOf();
		await engine.availability();
		await settle(engine);
		expect(w.env).toMatchObject({
			allowRemoteModels: false,
			allowLocalModels: true,
			localModelPath: BASE,
			useBrowserCache: true,
		});
	});

	it('loads without asking below the threshold, shows progress and requests persistent storage', async () => {
		const engine = engineOf({ askBeforeDownloadMb: 100 });
		const seen: string[] = [];
		engine.subscribe?.((a) =>
			seen.push(a.state === 'preparing' ? `${a.doneMb}/${a.totalMb}` : a.state),
		);
		expect(await engine.availability()).toMatchObject({ state: 'preparing', totalMb: 50 });
		expect((await settle(engine)).state).toBe('ready');
		expect(seen).toContain('26/51');
		expect(seen.at(-1)).toBe('ready');
		expect(w.persisted).toBe(1);
	});

	it('asks for consent before a first download above the threshold', async () => {
		const engine = engineOf({ askBeforeDownloadMb: 10 });
		expect(await engine.availability()).toEqual({ state: 'consent', sizeMb: 50 });
		expect(w.loads).toBe(0);
		expect(await engine.availability()).toEqual({ state: 'consent', sizeMb: 50 });
	});

	it('downloads after "Download"', async () => {
		const engine = engineOf({ askBeforeDownloadMb: 10 });
		await engine.availability();
		engine.acceptDownload?.();
		expect((await settle(engine)).state).toBe('ready');
		expect(w.loads).toBe(1);
		expect(w.persisted).toBe(1);
	});

	it('remembers "Not now" per model version', async () => {
		const engine = engineOf({ askBeforeDownloadMb: 10 });
		await engine.availability();
		engine.declineDownload?.();
		expect(w.storage.get('help.assistant.download.1.0.0')).toBe('declined');
		expect(await engine.availability()).toEqual({ state: 'unavailable' });
		expect(w.loads).toBe(0);
	});

	it('does not ask again when the weights are already cached, and does not persist again', async () => {
		w.cacheKeys.add(FILE);
		const engine = engineOf({ askBeforeDownloadMb: 10 });
		expect((await engine.availability()).state).toBe('preparing');
		expect((await settle(engine)).state).toBe('ready');
		expect(w.persisted).toBe(0);
	});

	it('removes cache entries of other versions after the new one loads', async () => {
		const old = `http://localhost${BASE}0.9.0/onnx/model_q4f16.onnx`;
		const foreign = 'http://localhost/other/file.bin';
		w.cacheKeys.add(old);
		w.cacheKeys.add(foreign);
		const engine = engineOf();
		await engine.availability();
		await settle(engine);
		expect(w.deleted).toEqual([old]);
		expect(w.cacheKeys.has(FILE)).toBe(true);
		expect(w.cacheKeys.has(foreign)).toBe(true);
	});

	it('reports loadFailed when the load fails, without storing it', async () => {
		w.loadError = true;
		const engine = engineOf();
		await engine.availability();
		expect(await settle(engine)).toEqual({ state: 'unavailable', reason: 'loadFailed' });
		expect(w.storage.has('help.assistant.webgpu.1.0.0')).toBe(false);
	});
});

describe('generation', () => {
	async function ready() {
		const engine = engineOf();
		await engine.availability();
		await settle(engine);
		w.prompts.length = 0;
		return engine;
	}

	it('renders the prompt of the pack protocol and streams the output', async () => {
		const engine = await ready();
		const seen: string[] = [];
		const out = await request(engine, undefined, ((t: string) => seen.push(t)) as never);
		expect(out).toBe(ANSWER);
		expect(seen).toEqual(['Click "New', ANSWER]);
		const sent = JSON.parse(w.prompts[0]);
		expect(sent.messages[0].role).toBe('system');
		expect(sent.messages[0].content).toContain('You answer questions from users of');
		expect(sent.messages[1].content).toContain('Question: How do I create a task');
		expect(sent.messages[1].content).toContain('[1] ');
		expect(sent.kwargs).toEqual({ enable_thinking: false });
	});

	it('rejects and stops the worker when the signal aborts', async () => {
		let aborted = false;
		w.generate = ({ signal }) =>
			new Promise((_, reject) =>
				signal.addEventListener('abort', () => {
					aborted = true;
					reject(new Error('aborted'));
				}),
			);
		const engine = await ready();
		const controller = new AbortController();
		const p = request(engine, controller.signal);
		await new Promise((r) => setTimeout(r, 0));
		controller.abort();
		await expect(p).rejects.toThrow('aborted');
		await new Promise((r) => setTimeout(r, 0));
		expect(aborted).toBe(true);
	});

	it('rejects when the model throws', async () => {
		w.generate = async () => {
			throw new Error('gpu lost');
		};
		const engine = await ready();
		await expect(request(engine)).rejects.toThrow('gpu lost');
	});
});

describe('the ladder (models.md 7.1)', () => {
	const run = (engines: AnswerEngine[]) =>
		runAssistant({
			engines,
			lang: 'en',
			question: 'How do I create a task',
			ranked: SECTIONS.slice(2, 6),
			pack,
			packSha256: 'abc',
		});
	const server = (generate: AnswerEngine['generate']): AnswerEngine => ({
		tier: 'server',
		timeoutMs: 1000,
		availability: async () => ({ state: 'ready' }),
		generate,
	});

	async function readyBrowser(timeoutSeconds = 5) {
		const engine = engineOf({ timeoutSeconds });
		await engine.availability();
		await settle(engine);
		return engine;
	}

	it('answers on the device when the browser tier is first and ready', async () => {
		const result = await run([await readyBrowser(), server(async () => 'x')]);
		expect(result.outcome).toMatchObject({ kind: 'answered', tier: 'browser', cited: [1] });
	});

	it('moves to the server with the same question and sections after an error', async () => {
		w.generate = async () => {
			throw new Error('gpu lost');
		};
		let seen: unknown;
		const result = await run([
			await readyBrowser(),
			server(async (r) => {
				seen = [r.question, r.sections.map((s) => s.id)];
				return ANSWER;
			}),
		]);
		expect(result.outcome).toMatchObject({ kind: 'answered', tier: 'server' });
		expect(seen).toEqual(['How do I create a task', result.context.map((s) => s.id)]);
	});

	it('moves on after a timeout', async () => {
		w.generate = () => new Promise(() => undefined);
		const browser = await readyBrowser();
		const slow = { ...browser, timeoutMs: 20 };
		const result = await run([slow, server(async () => ANSWER)]);
		expect(result.outcome).toMatchObject({ kind: 'answered', tier: 'server' });
	});

	it('ends in retrieval with failed=true when the browser is the only tier and fails', async () => {
		w.generate = async () => {
			throw new Error('x');
		};
		const result = await run([await readyBrowser()]);
		expect(result.outcome).toEqual({ kind: 'retrieval', failed: true });
	});

	it('gives the next tier the question and shows the device reason while the browser is unavailable', async () => {
		w.adapter = goodAdapter({ info: { description: 'llvmpipe' } });
		const notices: unknown[] = [];
		const result = await runAssistant({
			engines: [engineOf(), server(async () => ANSWER)],
			lang: 'en',
			question: 'How do I create a task',
			ranked: SECTIONS.slice(2, 6),
			pack,
			packSha256: 'abc',
			onNotice: (n) => notices.push(n),
		});
		expect(result.outcome).toMatchObject({ tier: 'server' });
		expect(notices).toEqual([{ kind: 'device', reason: 'softwareAdapter' }]);
		expect(result.notice).toEqual({ kind: 'device', reason: 'softwareAdapter' });
	});

	it('offers consent through the chain while the server answers', async () => {
		const engine = engineOf({ askBeforeDownloadMb: 10 });
		const result = await run([engine, server(async () => ANSWER)]);
		expect(result.outcome).toMatchObject({ tier: 'server' });
		expect(result.notice).toEqual({ kind: 'consent', sizeMb: 50 });
		expect(result.noticeEngine).toBe(engine);
	});

	describe('order setting from the status endpoint', () => {
		const registry = () => {
			const r = createEngineRegistry();
			r.register('browser', createBrowserEngineFactory(deps(w)));
			r.register('server', () => server(async () => ANSWER));
			return r;
		};
		const statusOf = (order: string) =>
			normalizeStatus({
				order,
				browser: {
					enabled: true,
					model_base_url: BASE,
					min_tokens_per_second: 8,
					ask_before_download_mb: 100,
					timeout_seconds: 20,
				},
				server: { available: true, timeout_seconds: 20 },
			});
		const tiers = (order: string) =>
			registry()
				.enginesFor({ status: statusOf(order), pack })
				.map((e) => e.tier);

		it('browser-first', () => expect(tiers('browser-first')).toEqual(['browser', 'server']));
		it('server-first', () => expect(tiers('server-first')).toEqual(['server', 'browser']));
		it('retrieval-only', () => expect(tiers('retrieval-only')).toEqual([]));
		it('reads the browser values of the status', () => {
			expect(statusOf('browser-first').browser).toMatchObject({
				modelBaseUrl: BASE,
				minTokensPerSecond: 8,
				askBeforeDownloadMb: 100,
				timeoutSeconds: 20,
			});
		});
		it('uses no browser tier with an incompatible pack', () => {
			expect(
				registry().enginesFor({
					status: statusOf('browser-first'),
					pack: assistantPack(false),
				}),
			).toEqual([]);
		});
	});
});
