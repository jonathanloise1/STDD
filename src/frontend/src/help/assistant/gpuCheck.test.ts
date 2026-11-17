import { describe, expect, it } from 'vitest';
import { checkGpu, checkWarmup, readCheck, writeCheck } from './gpuCheck.ts';
import type { GpuAdapterLike, GpuEnv, KeyValueStore } from './gpuCheck.ts';

export const goodAdapter = (over: Partial<GpuAdapterLike> = {}): GpuAdapterLike => ({
	info: {
		vendor: 'nvidia',
		architecture: 'ampere',
		device: '',
		description: 'RTX',
		isFallbackAdapter: false,
	},
	features: new Set(['shader-f16']),
	limits: { maxBufferSize: 4294967296, maxStorageBufferBindingSize: 2147483648 },
	...over,
});

const env = (adapter: GpuAdapterLike | null, extra: Partial<GpuEnv> = {}): GpuEnv => ({
	gpu: { requestAdapter: async () => adapter },
	...extra,
});

describe('GPU check, steps 1 to 7 (models.md 7.2)', () => {
	it('passes a good adapter', async () => {
		expect(await checkGpu(env(goodAdapter(), { deviceMemory: 8 }))).toEqual({ ok: true });
	});

	it('step 1: no navigator.gpu', async () => {
		expect(await checkGpu({})).toEqual({ ok: false, reason: 'noWebgpu' });
	});

	it('step 2: null adapter or a request that throws', async () => {
		expect(await checkGpu(env(null))).toEqual({ ok: false, reason: 'noAdapter' });
		const throwing: GpuEnv = {
			gpu: {
				requestAdapter: async () => {
					throw new Error('x');
				},
			},
		};
		expect(await checkGpu(throwing)).toEqual({ ok: false, reason: 'noAdapter' });
	});

	it('asks for the high-performance adapter', async () => {
		let asked: unknown;
		await checkGpu({
			gpu: {
				requestAdapter: async (o) => {
					asked = o;
					return goodAdapter();
				},
			},
		});
		expect(asked).toEqual({ powerPreference: 'high-performance' });
	});

	it('step 3: fallback adapter, in info and in the older place', async () => {
		const info = goodAdapter({ info: { isFallbackAdapter: true } });
		expect(await checkGpu(env(info))).toEqual({ ok: false, reason: 'softwareAdapter' });
		const old = goodAdapter({ info: undefined, isFallbackAdapter: true });
		expect(await checkGpu(env(old))).toEqual({ ok: false, reason: 'softwareAdapter' });
	});

	it('step 3: reads the older requestAdapterInfo', async () => {
		const old = goodAdapter({
			info: undefined,
			requestAdapterInfo: async () => ({ vendor: 'google', device: 'SwiftShader' }),
		});
		expect(await checkGpu(env(old))).toEqual({ ok: false, reason: 'softwareAdapter' });
	});

	for (const field of ['vendor', 'architecture', 'device', 'description'] as const)
		for (const name of [
			'SwiftShader',
			'llvmpipe (LLVM 15)',
			'lavapipe',
			'softpipe',
			'Microsoft Basic Render Driver',
			'WARP',
			'Software Rasterizer',
		])
			it(`step 4: rejects "${name}" in ${field}`, async () => {
				const adapter = goodAdapter({ info: { [field]: name, isFallbackAdapter: false } });
				expect(await checkGpu(env(adapter))).toEqual({
					ok: false,
					reason: 'softwareAdapter',
				});
			});

	it('step 5: no shader-f16', async () => {
		expect(await checkGpu(env(goodAdapter({ features: new Set() })))).toEqual({
			ok: false,
			reason: 'noShaderF16',
		});
	});

	it('step 6: buffer limits, both of them', async () => {
		const small = (maxBufferSize: number, maxStorageBufferBindingSize: number) =>
			goodAdapter({ limits: { maxBufferSize, maxStorageBufferBindingSize } });
		expect(await checkGpu(env(small(268435455, 2147483648)))).toEqual({
			ok: false,
			reason: 'lowLimits',
		});
		expect(await checkGpu(env(small(4294967296, 134217727)))).toEqual({
			ok: false,
			reason: 'lowLimits',
		});
		expect(await checkGpu(env(small(268435456, 134217728)))).toEqual({ ok: true });
	});

	it('step 7: device memory is checked only when present', async () => {
		expect(await checkGpu(env(goodAdapter(), { deviceMemory: 2 }))).toEqual({
			ok: false,
			reason: 'lowMemory',
		});
		expect(await checkGpu(env(goodAdapter(), { deviceMemory: 4 }))).toEqual({ ok: true });
		expect(await checkGpu(env(goodAdapter()))).toEqual({ ok: true });
	});
});

describe('step 8 and the stored result', () => {
	it('compares the rate with the minimum', () => {
		expect(checkWarmup(8, 8)).toEqual({ ok: true });
		expect(checkWarmup(7.9, 8)).toEqual({ ok: false, reason: 'tooSlow' });
	});

	const memory = () => {
		const data = new Map<string, string>();
		const store: KeyValueStore = {
			getItem: (k) => data.get(k) ?? null,
			setItem: (k, v) => void data.set(k, v),
		};
		return { data, store: () => store };
	};

	it('keeps ok and fail:{reason} per model version', () => {
		const m = memory();
		writeCheck(m.store, '1.0.0', { ok: true });
		writeCheck(m.store, '2.0.0', { ok: false, reason: 'tooSlow' });
		expect(m.data.get('help.assistant.webgpu.1.0.0')).toBe('ok');
		expect(m.data.get('help.assistant.webgpu.2.0.0')).toBe('fail:tooSlow');
		expect(readCheck(m.store, '1.0.0')).toEqual({ ok: true });
		expect(readCheck(m.store, '2.0.0')).toEqual({ ok: false, reason: 'tooSlow' });
		expect(readCheck(m.store, '3.0.0')).toBeNull();
	});

	it('survives storage that throws', () => {
		const broken = () => {
			throw new Error('denied');
		};
		expect(readCheck(broken, '1')).toBeNull();
		expect(() => writeCheck(broken, '1', { ok: true })).not.toThrow();
	});
});
