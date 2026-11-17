/** Messages between the page and the assistant worker (models.md 7.2, 7.3). Plain data: structured-clone safe. */
import type { ChatMessage } from './protocol.ts';

/** The reasons of a failed device check or load; the locale key is `help:deviceReason.{reason}`. */
export type DeviceReason =
	| 'noWebgpu'
	| 'noAdapter'
	| 'softwareAdapter'
	| 'noShaderF16'
	| 'lowLimits'
	| 'lowMemory'
	| 'tooSlow'
	| 'notFound'
	| 'loadFailed';

export interface ModelLocation {
	/** `ModelBaseUrl` (models.md 7.7), with a trailing slash. */
	baseUrl: string;
	/** `assistant.model.version` of the pack. */
	version: string;
}

export type ToWorker =
	/** Steps 1 to 7 of models.md 7.2. */
	| { type: 'check'; id: number }
	/** Is the model in the browser cache, and how large is the download. */
	| ({ type: 'probe'; id: number } & ModelLocation)
	/** Loads the weights; `minTokensPerSecond` is set when step 8 still has to run. */
	| ({ type: 'load'; id: number; minTokensPerSecond: number | null } & ModelLocation)
	| {
			type: 'generate';
			id: number;
			messages: ChatMessage[];
			maxNewTokens: number;
			templateKwargs: Record<string, unknown>;
	  }
	| { type: 'abort'; id: number };

export type FromWorker =
	| { type: 'check-result'; id: number; ok: true }
	| { type: 'check-result'; id: number; ok: false; reason: DeviceReason }
	| {
			type: 'probe-result';
			id: number;
			exists: boolean;
			cached: boolean;
			sizeBytes: number | null;
	  }
	| { type: 'progress'; id: number; loadedBytes: number; totalBytes: number }
	/** `tokensPerSecond` is set when step 8 ran. */
	| { type: 'loaded'; id: number; tokensPerSecond: number | null }
	| { type: 'text'; id: number; text: string }
	| { type: 'done'; id: number; text: string }
	| { type: 'error'; id: number; message: string };

/** What the page needs from a worker: the real `Worker` and the in-memory test channel both fit. */
export interface WorkerPort {
	post(message: ToWorker): void;
	onMessage(listener: (message: FromWorker) => void): void;
	/** The worker died or could not start. */
	onError(listener: (message: string) => void): void;
	terminate(): void;
}
