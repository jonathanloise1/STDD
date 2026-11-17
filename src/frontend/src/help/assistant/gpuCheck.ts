/**
 * The real GPU check of models.md 7.2. Steps 1 to 7 run in the worker before anything is downloaded; step 8 runs
 * after the model has loaded. The result of the whole check is kept in `localStorage` per model version.
 */
import type { DeviceReason } from './messages.ts';

export interface GpuAdapterInfo {
	vendor?: string;
	architecture?: string;
	device?: string;
	description?: string;
	isFallbackAdapter?: boolean;
}

/** The part of a `GPUAdapter` the check reads, including the older shape (`isFallbackAdapter`, `requestAdapterInfo`). */
export interface GpuAdapterLike {
	info?: GpuAdapterInfo;
	isFallbackAdapter?: boolean;
	requestAdapterInfo?: () => Promise<GpuAdapterInfo>;
	features: { has(feature: string): boolean };
	limits: { maxBufferSize: number; maxStorageBufferBindingSize: number };
}

export interface GpuEnv {
	gpu?: {
		requestAdapter(options?: { powerPreference?: string }): Promise<GpuAdapterLike | null>;
	};
	deviceMemory?: number;
}

export type GpuCheckResult = { ok: true } | { ok: false; reason: DeviceReason };

export const SOFTWARE_ADAPTER =
	/swiftshader|llvmpipe|lavapipe|softpipe|warp|basic render|software/i;
export const MIN_BUFFER_SIZE = 268435456;
export const MIN_STORAGE_BINDING_SIZE = 134217728;
export const MIN_DEVICE_MEMORY_GB = 4;
export const WARMUP_TOKENS = 16;
/** The fixed prompt of step 8. */
export const WARMUP_PROMPT = 'Count from one to twenty in words: one, two, three,';

/** Steps 1 to 7. The q4f16 variant is the only one the browser tier loads, so step 5 always applies. */
export async function checkGpu(env: GpuEnv): Promise<GpuCheckResult> {
	if (!env.gpu) return { ok: false, reason: 'noWebgpu' };
	let adapter: GpuAdapterLike | null;
	try {
		adapter = await env.gpu.requestAdapter({ powerPreference: 'high-performance' });
	} catch {
		return { ok: false, reason: 'noAdapter' };
	}
	if (!adapter) return { ok: false, reason: 'noAdapter' };

	let info = adapter.info;
	if (!info && adapter.requestAdapterInfo) {
		try {
			info = await adapter.requestAdapterInfo();
		} catch {
			info = undefined;
		}
	}
	if ((info?.isFallbackAdapter ?? adapter.isFallbackAdapter) === true)
		return { ok: false, reason: 'softwareAdapter' };
	const names = [info?.vendor, info?.architecture, info?.device, info?.description];
	if (names.some((n) => typeof n === 'string' && SOFTWARE_ADAPTER.test(n)))
		return { ok: false, reason: 'softwareAdapter' };

	if (!adapter.features.has('shader-f16')) return { ok: false, reason: 'noShaderF16' };
	if (
		!(adapter.limits.maxBufferSize >= MIN_BUFFER_SIZE) ||
		!(adapter.limits.maxStorageBufferBindingSize >= MIN_STORAGE_BINDING_SIZE)
	)
		return { ok: false, reason: 'lowLimits' };
	if (env.deviceMemory !== undefined && env.deviceMemory < MIN_DEVICE_MEMORY_GB)
		return { ok: false, reason: 'lowMemory' };
	return { ok: true };
}

/** Step 8: the measured rate against `MinTokensPerSecond`. */
export function checkWarmup(tokensPerSecond: number, minTokensPerSecond: number): GpuCheckResult {
	return tokensPerSecond >= minTokensPerSecond ? { ok: true } : { ok: false, reason: 'tooSlow' };
}

/** What `localStorage` keeps: `ok`, or `fail:{reason}` (7.2). */
export type StoredCheck = { ok: true } | { ok: false; reason: string };

export const checkKey = (modelVersion: string) => `help.assistant.webgpu.${modelVersion}`;
export const downloadKey = (modelVersion: string) => `help.assistant.download.${modelVersion}`;

export interface KeyValueStore {
	getItem(key: string): string | null;
	setItem(key: string, value: string): void;
}

/** Storage can be missing or throw; then there is no stored result and the check runs again. */
export function readCheck(store: () => KeyValueStore, modelVersion: string): StoredCheck | null {
	try {
		const value = store().getItem(checkKey(modelVersion));
		if (value === 'ok') return { ok: true };
		if (value?.startsWith('fail:')) return { ok: false, reason: value.slice(5) };
	} catch {
		/* storage unavailable */
	}
	return null;
}

export function writeCheck(
	store: () => KeyValueStore,
	modelVersion: string,
	result: GpuCheckResult,
): void {
	try {
		store().setItem(checkKey(modelVersion), result.ok ? 'ok' : `fail:${result.reason}`);
	} catch {
		/* storage unavailable: the next page load checks again */
	}
}
