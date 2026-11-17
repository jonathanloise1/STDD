/**
 * The browser tier (manual.md 7.1, models.md 7.1 to 7.3): the assistant model in a Web Worker on WebGPU.
 * One controller per model version holds the state that outlives a question (check result, download, loaded model);
 * the engine of a question is a thin view on it. All side effects (worker, storage) are injected.
 */
import type { AnswerEngine, EngineAvailability, EngineFactory } from '../runtime/engine.ts';
import { DEFAULT_MIN_TOKENS_PER_SECOND, DEFAULT_MODEL_BASE_URL } from '../runtime/types.ts';
import type { HelpPack } from '../index.ts';
import { checkWarmup, downloadKey, readCheck, writeCheck } from './gpuCheck.ts';
import type { KeyValueStore } from './gpuCheck.ts';
import type { DeviceReason, FromWorker, ModelLocation, ToWorker, WorkerPort } from './messages.ts';
import { renderMessages } from './protocol.ts';
import type { AssistantProtocol, ChatMessage } from './protocol.ts';

const MB = 1024 * 1024;

export interface BrowserEngineDeps {
	createWorker(): WorkerPort;
	store(): KeyValueStore;
}

export interface BrowserSettings extends ModelLocation {
	askBeforeDownloadMb: number;
	minTokensPerSecond: number;
	timeoutSeconds: number;
}

type Phase = 'idle' | 'loading' | 'loaded' | 'failed';

interface Pending {
	resolve(message: FromWorker): void;
	reject(error: Error): void;
	onEvent?(message: FromWorker): void;
}

export interface BrowserController {
	availability(): Promise<EngineAvailability>;
	acceptDownload(): void;
	declineDownload(): void;
	subscribe(listener: (availability: EngineAvailability) => void): () => void;
	generate(
		messages: ChatMessage[],
		options: {
			maxNewTokens: number;
			templateKwargs: Record<string, unknown>;
			signal: AbortSignal;
			onText(text: string): void;
		},
	): Promise<string>;
}

export function createBrowserController(
	settings: BrowserSettings,
	deps: BrowserEngineDeps,
): BrowserController {
	let port: WorkerPort | null = null;
	let nextId = 0;
	const pending = new Map<number, Pending>();
	const listeners = new Set<(availability: EngineAvailability) => void>();

	let phase: Phase = 'idle';
	let failure = 'loadFailed';
	let consented = false;
	let progress = { doneBytes: 0, totalBytes: 0 };
	let sizeMb = 0;
	let probeRun: Promise<{ exists: boolean; cached: boolean; sizeBytes: number | null }> | null =
		null;

	const failAll = (message: string) => {
		for (const p of pending.values()) p.reject(new Error(message));
		pending.clear();
		port = null;
		if (phase === 'loading') fail('loadFailed');
	};

	function worker(): WorkerPort {
		if (port) return port;
		const created = deps.createWorker();
		created.onMessage((message) => {
			const entry = pending.get(message.id);
			if (!entry) return;
			if (message.type === 'error') {
				pending.delete(message.id);
				entry.reject(new Error(message.message));
			} else if (message.type === 'progress' || message.type === 'text')
				entry.onEvent?.(message);
			else {
				pending.delete(message.id);
				entry.resolve(message);
			}
		});
		created.onError(failAll);
		port = created;
		return created;
	}

	function call(
		build: (id: number) => ToWorker,
		onEvent?: (message: FromWorker) => void,
	): Promise<FromWorker> {
		return new Promise((resolve, reject) => {
			nextId += 1;
			const id = nextId;
			pending.set(id, { resolve, reject, onEvent });
			try {
				worker().post(build(id));
			} catch (error) {
				pending.delete(id);
				reject(error instanceof Error ? error : new Error(String(error)));
			}
		});
	}

	const declined = () => {
		try {
			return deps.store().getItem(downloadKey(settings.version)) === 'declined';
		} catch {
			return false;
		}
	};

	function snapshot(): EngineAvailability {
		if (phase === 'loaded') return { state: 'ready' };
		if (phase === 'failed') return { state: 'unavailable', reason: failure };
		const totalMb = progress.totalBytes > 0 ? Math.ceil(progress.totalBytes / MB) : sizeMb;
		return {
			state: 'preparing',
			doneMb: Math.min(Math.floor(progress.doneBytes / MB), totalMb),
			totalMb,
		};
	}
	const notify = () => listeners.forEach((l) => l(snapshot()));

	function fail(reason: string) {
		phase = 'failed';
		failure = reason;
		notify();
	}

	function startLoad(minTokensPerSecond: number | null) {
		if (phase !== 'idle') return;
		phase = 'loading';
		call(
			(id) => ({
				type: 'load',
				id,
				baseUrl: settings.baseUrl,
				version: settings.version,
				minTokensPerSecond,
			}),
			(message) => {
				if (message.type !== 'progress') return;
				progress = { doneBytes: message.loadedBytes, totalBytes: message.totalBytes };
				notify();
			},
		).then(
			(message) => {
				if (message.type === 'loaded' && message.tokensPerSecond !== null) {
					const result = checkWarmup(
						message.tokensPerSecond,
						settings.minTokensPerSecond,
					);
					writeCheck(deps.store, settings.version, result);
					if (!result.ok) return fail(result.reason);
				}
				phase = 'loaded';
				notify();
			},
			() => fail('loadFailed'),
		);
	}

	let checkRun: Promise<{ ok: true } | { ok: false; reason: DeviceReason }> | null = null;

	/** Steps 1 to 7, once per controller; a failure is kept in storage for this model version. */
	const runCheck = () =>
		(checkRun ??= call((id) => ({ type: 'check', id })).then(
			(message): { ok: true } | { ok: false; reason: DeviceReason } => {
				const result: { ok: true } | { ok: false; reason: DeviceReason } =
					message.type === 'check-result' && message.ok
						? { ok: true }
						: {
								ok: false,
								reason:
									message.type === 'check-result' && !message.ok
										? message.reason
										: 'noWebgpu',
							};
				if (!result.ok) writeCheck(deps.store, settings.version, result);
				return result;
			},
			(): { ok: false; reason: DeviceReason } => ({ ok: false, reason: 'noWebgpu' }),
		));

	async function deviceResult(): Promise<{ ok: true } | { ok: false; reason: string }> {
		return readCheck(deps.store, settings.version) ?? runCheck();
	}

	const probe = () =>
		(probeRun ??= call((id) => ({
			type: 'probe',
			id,
			baseUrl: settings.baseUrl,
			version: settings.version,
		})).then((message) =>
			message.type === 'probe-result'
				? { exists: message.exists, cached: message.cached, sizeBytes: message.sizeBytes }
				: { exists: false, cached: false, sizeBytes: null },
		));

	async function availability(): Promise<EngineAvailability> {
		if (phase !== 'idle') return snapshot();
		if (declined()) return { state: 'unavailable' };
		const device = await deviceResult();
		if (!device.ok) return { state: 'unavailable', reason: device.reason };
		if (phase !== 'idle') return snapshot();
		let found;
		try {
			found = await probe();
		} catch {
			return { state: 'unavailable', reason: 'loadFailed' };
		}
		if (!found.exists) return { state: 'unavailable', reason: 'notFound' };
		sizeMb = found.sizeBytes ? Math.ceil(found.sizeBytes / MB) : 0;
		// Step 8 runs once per device and model version: when the stored result is `ok` it is skipped
		const warmup = readCheck(deps.store, settings.version)?.ok
			? null
			: settings.minTokensPerSecond;
		if (!found.cached && sizeMb > settings.askBeforeDownloadMb && !consented)
			return { state: 'consent', sizeMb };
		startLoad(warmup);
		return snapshot();
	}

	return {
		availability,
		acceptDownload() {
			consented = true;
			const warmup = readCheck(deps.store, settings.version)?.ok
				? null
				: settings.minTokensPerSecond;
			startLoad(warmup);
			notify();
		},
		declineDownload() {
			try {
				deps.store().setItem(downloadKey(settings.version), 'declined');
			} catch {
				/* storage unavailable: asked again next time */
			}
			notify();
		},
		subscribe(listener) {
			listeners.add(listener);
			return () => void listeners.delete(listener);
		},
		async generate(messages, options) {
			if (phase !== 'loaded') throw new Error('The assistant is not ready');
			return new Promise<string>((resolve, reject) => {
				nextId += 1;
				const id = nextId;
				const onAbort = () => {
					pending.delete(id);
					try {
						worker().post({ type: 'abort', id });
					} catch {
						/* the worker is gone */
					}
					reject(new Error('aborted'));
				};
				if (options.signal.aborted) return onAbort();
				options.signal.addEventListener('abort', onAbort, { once: true });
				pending.set(id, {
					resolve: (message) => {
						options.signal.removeEventListener('abort', onAbort);
						if (message.type === 'done') resolve(message.text);
						else reject(new Error('unexpected reply'));
					},
					reject: (error) => {
						options.signal.removeEventListener('abort', onAbort);
						reject(error);
					},
					onEvent: (message) => {
						if (message.type === 'text') options.onText(message.text);
					},
				});
				try {
					worker().post({
						type: 'generate',
						id,
						messages,
						maxNewTokens: options.maxNewTokens,
						templateKwargs: options.templateKwargs,
					});
				} catch (error) {
					pending.delete(id);
					options.signal.removeEventListener('abort', onAbort);
					reject(error instanceof Error ? error : new Error(String(error)));
				}
			});
		},
	};
}

/** The engine of one question: renders the prompt of the pack's protocol and streams the controller's answer. */
export function createBrowserEngine(
	controller: BrowserController,
	pack: HelpPack,
	timeoutSeconds: number,
): AnswerEngine {
	const protocol = pack.assistant.protocol.content as unknown as AssistantProtocol;
	return {
		tier: 'browser',
		timeoutMs: timeoutSeconds * 1000,
		availability: () => controller.availability(),
		acceptDownload: () => controller.acceptDownload(),
		declineDownload: () => controller.declineDownload(),
		subscribe: (listener) => controller.subscribe(listener),
		async generate(request) {
			const { messages } = renderMessages(
				protocol,
				request.lang,
				pack.project.name,
				request.sections,
				request.question,
			);
			return controller.generate(messages, {
				maxNewTokens: protocol.decoding.max_new_tokens,
				// Reasoning is off in training, browser and server (models.md 3.7)
				templateKwargs:
					protocol.decoding.reasoning === 'off' ? { enable_thinking: false } : {},
				signal: request.signal,
				onText: request.onText,
			});
		},
	};
}

/** The real worker of the page. */
export function createPageWorker(): WorkerPort {
	const worker = new Worker(new URL('./assistant.worker.ts', import.meta.url), {
		type: 'module',
	});
	return {
		post: (message) => worker.postMessage(message),
		onMessage: (listener) =>
			worker.addEventListener('message', (e) => listener(e.data as FromWorker)),
		onError: (listener) => {
			worker.addEventListener('error', (e) => listener(e.message || 'worker error'));
			worker.addEventListener('messageerror', () => listener('worker message error'));
		},
		terminate: () => worker.terminate(),
	};
}

const pageStore = (): KeyValueStore => window.localStorage;

/**
 * The factory of the registry. The tier is not eligible when the browser setting is off, when the pack has no model
 * or its protocol is not the one the model was trained with, or when there is no Worker (models.md 7.5).
 */
export function createBrowserEngineFactory(deps: BrowserEngineDeps | null = null): EngineFactory {
	const controllers = new Map<string, BrowserController>();
	return ({ status, pack }) => {
		const model = pack.assistant.model;
		if (!status.browser.enabled || !pack.assistant.compatible || !model) return null;
		if (model.protocol_sha256 !== pack.assistant.protocol.sha256) return null;
		const actual: BrowserEngineDeps | null =
			deps ??
			(typeof Worker === 'undefined'
				? null
				: { createWorker: createPageWorker, store: pageStore });
		if (!actual) return null;
		const baseUrl = status.browser.modelBaseUrl ?? DEFAULT_MODEL_BASE_URL;
		const key = `${baseUrl}|${model.version}`;
		let controller = controllers.get(key);
		if (!controller) {
			controller = createBrowserController(
				{
					baseUrl,
					version: model.version,
					askBeforeDownloadMb: status.browser.askBeforeDownloadMb,
					minTokensPerSecond:
						status.browser.minTokensPerSecond ?? DEFAULT_MIN_TOKENS_PER_SECOND,
					timeoutSeconds: status.browser.timeoutSeconds,
				},
				actual,
			);
			controllers.set(key, controller);
		}
		return createBrowserEngine(controller, pack, status.browser.timeoutSeconds);
	};
}

export const browserEngineFactory: EngineFactory = createBrowserEngineFactory();
