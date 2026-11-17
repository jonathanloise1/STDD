/** The assistant worker (models.md 7.2, 7.3): GPU check, weights and generation, off the main thread. */
import { loadTransformers } from './transformersAdapter.ts';
import { createWorkerCore } from './workerCore.ts';
import type { FromWorker, ToWorker } from './messages.ts';

interface WorkerScope {
	postMessage(message: unknown): void;
	addEventListener(type: 'message', listener: (event: { data: ToWorker }) => void): void;
	navigator: {
		gpu?: unknown;
		deviceMemory?: number;
		storage?: { persist?: () => Promise<boolean> };
	};
	location: { origin: string };
	caches?: { open(name: string): Promise<unknown> };
}

const scope = self as unknown as WorkerScope;

const core = createWorkerCore({
	loadLibrary: loadTransformers,
	gpuEnv: () => ({
		gpu: scope.navigator.gpu as never,
		deviceMemory: scope.navigator.deviceMemory,
	}),
	fetch: (url, init) => fetch(url, init),
	openCache: async (name) => (scope.caches ? ((await scope.caches.open(name)) as never) : null),
	persist: async () => (await scope.navigator.storage?.persist?.()) ?? false,
	origin: scope.location.origin,
	now: () => performance.now(),
	post: (message: FromWorker) => scope.postMessage(message),
});

scope.addEventListener('message', (event) => void core.handle(event.data));
