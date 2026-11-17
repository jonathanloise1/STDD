/**
 * The logic of the assistant worker, free of `self` and of the library, so it runs under test with a fake pipeline.
 * `assistant.worker.ts` wires it to the real worker scope and to `@huggingface/transformers`, which is imported
 * dynamically there and so stays out of the main bundle.
 */
import { WARMUP_PROMPT, WARMUP_TOKENS, checkGpu } from './gpuCheck.ts';
import type { GpuEnv } from './gpuCheck.ts';
import type { FromWorker, ModelLocation, ToWorker } from './messages.ts';

/** The ONNX file of the `q4f16` export (models.md 6.5); its size stands for the download. */
export const MODEL_FILE = 'onnx/model_q4f16.onnx';
export const DEFAULT_CACHE_NAME = 'transformers-cache';

export interface GenerateOptions {
	maxNewTokens: number;
	/** Set for the warm-up so that exactly this many tokens are produced. */
	minNewTokens?: number;
	onText: (accumulated: string) => void;
	signal: AbortSignal;
}

/** The seam: a loaded model that renders the chat template and decodes greedily (models.md 3.7, 3.8). */
export interface TextModel {
	renderPrompt(
		messages: { role: string; content: string }[],
		templateKwargs: Record<string, unknown>,
	): string;
	generate(prompt: string, options: GenerateOptions): Promise<{ text: string; tokens: number }>;
}

export interface LoadModelOptions {
	device: 'webgpu';
	dtype: 'q4f16';
	onProgress: (file: string, loadedBytes: number, totalBytes: number) => void;
}

export interface TransformersLib {
	/** The library's `env` object; the core sets the rules of models.md 7.3 on it. */
	env: Record<string, unknown>;
	loadModel(modelId: string, options: LoadModelOptions): Promise<TextModel>;
}

export interface CacheLike {
	match(request: string): Promise<unknown>;
	keys(): Promise<{ url: string }[]>;
	delete(request: string | { url: string }): Promise<boolean>;
}

export interface WorkerDeps {
	loadLibrary(): Promise<TransformersLib>;
	gpuEnv(): GpuEnv;
	fetch(
		url: string,
		init?: { method: string },
	): Promise<{
		ok: boolean;
		headers: { get(name: string): string | null };
	}>;
	openCache(name: string): Promise<CacheLike | null>;
	/** `navigator.storage.persist`, when it exists. */
	persist(): Promise<boolean>;
	/** The origin the page was served from, for absolute cache keys. */
	origin: string;
	now(): number;
	post(message: FromWorker): void;
}

const isHit = (response: unknown) => response !== undefined && response !== null;
const withSlash = (url: string) => (url.endsWith('/') ? url : `${url}/`);

export function createWorkerCore(deps: WorkerDeps) {
	let library: Promise<TransformersLib> | null = null;
	let model: TextModel | null = null;
	let loading: Promise<void> | null = null;
	const aborts = new Map<number, AbortController>();

	const getLibrary = () => (library ??= deps.loadLibrary());
	const absolute = (path: string) => new URL(path, deps.origin).href;
	const modelUrl = (loc: ModelLocation) => `${withSlash(loc.baseUrl)}${loc.version}/`;

	/** Rules of models.md 7.3: same origin, no remote models, the Cache API. */
	function configure(lib: TransformersLib, loc: ModelLocation) {
		lib.env.allowRemoteModels = false;
		lib.env.allowLocalModels = true;
		lib.env.localModelPath = withSlash(loc.baseUrl);
		lib.env.useBrowserCache = true;
	}

	async function cacheOf(lib: TransformersLib): Promise<CacheLike | null> {
		const name = typeof lib.env.cacheKey === 'string' ? lib.env.cacheKey : DEFAULT_CACHE_NAME;
		try {
			return await deps.openCache(name);
		} catch {
			return null;
		}
	}

	async function probe(id: number, loc: ModelLocation) {
		const url = absolute(`${modelUrl(loc)}${MODEL_FILE}`);
		let cached = false;
		try {
			const cache = await deps.openCache(DEFAULT_CACHE_NAME);
			cached = cache !== null && isHit(await cache.match(url));
		} catch {
			cached = false;
		}
		let exists = false;
		let sizeBytes: number | null = null;
		try {
			const response = await deps.fetch(url, { method: 'HEAD' });
			exists = response.ok;
			const length = Number(response.headers.get('content-length'));
			sizeBytes = response.ok && Number.isFinite(length) && length > 0 ? length : null;
		} catch {
			exists = cached;
		}
		deps.post({ type: 'probe-result', id, exists: exists || cached, cached, sizeBytes });
	}

	/** Cache entries of other model versions are deleted after the new version loads (7.3). */
	async function removeOldVersions(lib: TransformersLib, loc: ModelLocation) {
		const cache = await cacheOf(lib);
		if (!cache) return;
		const root = absolute(withSlash(loc.baseUrl));
		const keep = absolute(modelUrl(loc));
		for (const request of await cache.keys())
			if (request.url.startsWith(root) && !request.url.startsWith(keep))
				await cache.delete(request);
	}

	async function warmup(current: TextModel) {
		const prompt = current.renderPrompt([{ role: 'user', content: WARMUP_PROMPT }], {});
		const started = deps.now();
		const out = await current.generate(prompt, {
			maxNewTokens: WARMUP_TOKENS,
			minNewTokens: WARMUP_TOKENS,
			onText: () => undefined,
			signal: new AbortController().signal,
		});
		const seconds = Math.max((deps.now() - started) / 1000, 0.001);
		return out.tokens / seconds;
	}

	async function load(id: number, loc: ModelLocation, minTokensPerSecond: number | null) {
		if (model) {
			deps.post({ type: 'loaded', id, tokensPerSecond: null });
			return;
		}
		const lib = await getLibrary();
		configure(lib, loc);
		const wasCached = await (async () => {
			const cache = await cacheOf(lib);
			return (
				cache !== null &&
				isHit(await cache.match(absolute(`${modelUrl(loc)}${MODEL_FILE}`)))
			);
		})();
		const files = new Map<string, { loaded: number; total: number }>();
		model = await lib.loadModel(loc.version, {
			device: 'webgpu',
			dtype: 'q4f16',
			onProgress: (file, loadedBytes, totalBytes) => {
				files.set(file, { loaded: loadedBytes, total: totalBytes });
				let loaded = 0;
				let total = 0;
				for (const f of files.values()) {
					loaded += f.loaded;
					total += f.total;
				}
				deps.post({ type: 'progress', id, loadedBytes: loaded, totalBytes: total });
			},
		});
		await removeOldVersions(lib, loc).catch(() => undefined);
		if (!wasCached) {
			try {
				await deps.persist();
			} catch {
				/* the browser may keep the cache until it needs the space */
			}
		}
		const tokensPerSecond = minTokensPerSecond === null ? null : await warmup(model);
		deps.post({ type: 'loaded', id, tokensPerSecond });
	}

	async function generate(message: Extract<ToWorker, { type: 'generate' }>) {
		if (!model) throw new Error('The model is not loaded');
		const controller = new AbortController();
		aborts.set(message.id, controller);
		try {
			const prompt = model.renderPrompt(message.messages, message.templateKwargs);
			const out = await model.generate(prompt, {
				maxNewTokens: message.maxNewTokens,
				onText: (text) => deps.post({ type: 'text', id: message.id, text }),
				signal: controller.signal,
			});
			if (controller.signal.aborted) throw new Error('aborted');
			deps.post({ type: 'done', id: message.id, text: out.text });
		} finally {
			aborts.delete(message.id);
		}
	}

	async function run(message: ToWorker) {
		switch (message.type) {
			case 'check': {
				const result = await checkGpu(deps.gpuEnv());
				deps.post({ type: 'check-result', id: message.id, ...result });
				return;
			}
			case 'probe':
				await probe(message.id, message);
				return;
			case 'load':
				// One load at a time; a second request waits for the first
				loading ??= load(message.id, message, message.minTokensPerSecond).finally(() => {
					loading = null;
				});
				await loading;
				return;
			case 'generate':
				await generate(message);
				return;
			case 'abort':
				aborts.get(message.id)?.abort();
				return;
		}
	}

	return {
		/** Handles one message; failures are reported as an `error` message, never thrown. */
		async handle(message: ToWorker): Promise<void> {
			try {
				await run(message);
			} catch (error) {
				if (message.type === 'abort') return;
				deps.post({
					type: 'error',
					id: message.id,
					message: error instanceof Error ? error.message : String(error),
				});
			}
		},
	};
}
