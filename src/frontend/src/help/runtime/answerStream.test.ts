import { AxiosError } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { HelpAnswerError, createAnswerStream, failureOfStatus, parseLine } from './answerStream.ts';
import { createServerEngine } from './serverEngine.ts';
import { runAssistant } from './engine.ts';
import { SECTIONS, testPack } from './fixtures/pack.ts';
import type { AnswerEngine } from './engine.ts';

const mock = vi.hoisted(() => ({
	calls: [] as unknown[][],
	impl: (async () => undefined) as (...args: any[]) => Promise<unknown>,
}));
vi.mock('../../services/api/apiClient', () => ({
	default: {
		post: (...args: unknown[]) => {
			mock.calls.push(args);
			return mock.impl(...args);
		},
		get: () => Promise.reject(new Error('unused')),
	},
}));
const { default: helpService } = await import('../../services/api/helpService');

const line = (o: object) => `${JSON.stringify(o)}\n`;
const request = { lang: 'en' as const, question: 'q', sectionIds: ['a'], packSha256: 'abc' };

describe('answer stream (models.md 7.4)', () => {
	it('parses delta, done and error lines', () => {
		expect(parseLine('{"type":"delta","text":"Hi"}')).toEqual({ type: 'delta', text: 'Hi' });
		expect(parseLine('{"type":"done","outcome":"answered","cited":[1,2]}')).toEqual({
			type: 'done',
			outcome: 'answered',
			cited: [1, 2],
			body: null,
		});
		expect(parseLine('{"type":"error","reason":"timeout"}')).toEqual({
			type: 'error',
			reason: 'timeout',
		});
		expect(() => parseLine('not json')).toThrow(HelpAnswerError);
		expect(() => parseLine('{"type":"done","outcome":"maybe"}')).toThrow(HelpAnswerError);
		expect(() => parseLine('{"type":"other"}')).toThrow(HelpAnswerError);
	});

	it('reads lines as they complete and rebuilds the raw output from the deltas', () => {
		const seen: string[] = [];
		const stream = createAnswerStream('Refusal.', (t) => seen.push(t));
		const all =
			line({ type: 'delta', text: 'Click ' }) +
			line({ type: 'delta', text: '"New".\nSource: [1]' }) +
			line({ type: 'done', outcome: 'answered', cited: [1], model: 'm', device: 'cpu' });
		stream.feed(all.slice(0, 20)); // a cut line is not read
		expect(seen).toEqual([]);
		stream.feed(all.slice(0, all.indexOf('\n') + 1 + 8));
		expect(seen).toEqual(['Click ']);
		expect(stream.finish(all)).toBe('Click "New".\nSource: [1]');
		expect(seen.at(-1)).toBe('Click "New".\nSource: [1]');
	});

	it('reads a last line without a line feed', () => {
		const stream = createAnswerStream('R');
		expect(
			stream.finish(
				`${line({
					type: 'delta',
					text: 'A\nSource: [1]',
				})}{"type":"done","outcome":"answered","cited":[1]}`,
			),
		).toBe('A\nSource: [1]');
	});

	it('uses the refusal sentence or the body when there are no deltas', () => {
		expect(
			createAnswerStream('Nope.').finish(
				line({ type: 'done', outcome: 'refused', cited: [] }),
			),
		).toBe('Nope.');
		expect(
			createAnswerStream('Nope.').finish(
				line({ type: 'done', outcome: 'answered', cited: [1, 3], body: 'Body' }),
			),
		).toBe('Body\nSource: [1], [3]');
	});

	it('fails on an error event and on a stream that never says done', () => {
		const err = (reason: string) =>
			createAnswerStream('R').finish(
				line({ type: 'delta', text: 'x' }) + line({ type: 'error', reason }),
			);
		expect(() => err('timeout')).toThrowError(expect.objectContaining({ failure: 'timeout' }));
		expect(() => err('unavailable')).toThrowError(
			expect.objectContaining({ failure: 'unavailable' }),
		);
		expect(() => createAnswerStream('R').finish(line({ type: 'delta', text: 'x' }))).toThrow(
			HelpAnswerError,
		);
	});

	it('maps the statuses before the stream', () => {
		expect(failureOfStatus(400)).toBe('invalid_request');
		expect(failureOfStatus(409)).toBe('pack_changed');
		expect(failureOfStatus(503)).toBe('unavailable');
		expect(failureOfStatus(504)).toBe('timeout');
	});
});

describe('helpService.answer', () => {
	beforeEach(() => {
		mock.calls = [];
	});

	it('posts snake_case, streams and resolves with the raw output', async () => {
		const body =
			line({ type: 'delta', text: 'Do it.\nSource: [1]' }) +
			line({ type: 'done', outcome: 'answered', cited: [1] });
		mock.impl = async (_url, _data, config) => {
			config.onDownloadProgress({ event: { target: { responseText: body } } });
			return { data: body };
		};
		const partials: string[] = [];
		const out = await helpService.answer(request, { onText: (t) => partials.push(t) });
		expect(out).toBe('Do it.\nSource: [1]');
		expect(partials).toEqual(['Do it.\nSource: [1]']);
		expect(mock.calls).toHaveLength(1);
		expect(mock.calls[0][0]).toBe('/api/help/answer');
		expect(mock.calls[0][1]).toEqual({
			lang: 'en',
			question: 'q',
			section_ids: ['a'],
			pack_sha256: 'abc',
		});
		expect(mock.calls[0][2]).toMatchObject({ 'axios-retry': { retries: 0 } });
	});

	for (const [status, failure] of [
		[400, 'invalid_request'],
		[409, 'pack_changed'],
		[503, 'unavailable'],
		[504, 'timeout'],
	] as const)
		it(`rejects with ${failure} on ${status}`, async () => {
			const error = new AxiosError('x', 'ERR_BAD_RESPONSE', undefined, undefined, {
				status,
			} as never);
			mock.impl = () => Promise.reject(error);
			await expect(helpService.answer(request, {})).rejects.toMatchObject({
				failure,
				status,
			});
		});

	it('rejects with the reason of an error event', async () => {
		mock.impl = async () => ({ data: line({ type: 'error', reason: 'timeout' }) });
		await expect(helpService.answer(request, {})).rejects.toMatchObject({ failure: 'timeout' });
	});

	it('lets a network error through', async () => {
		mock.impl = () => Promise.reject(new Error('network'));
		await expect(helpService.answer(request, {})).rejects.toThrow('network');
	});
});

describe('server failures move the ladder', () => {
	const pack = testPack();
	for (const failure of ['invalid_request', 'pack_changed', 'unavailable', 'timeout'] as const)
		it(`hands the same question to the next tier after ${failure}`, async () => {
			const server = createServerEngine(1, {
				answer: async () => {
					throw new HelpAnswerError(failure);
				},
			});
			const next: AnswerEngine = {
				tier: 'browser',
				timeoutMs: 1000,
				availability: async () => ({ state: 'ready' }),
				generate: async () => 'Click.\nSource: [1]',
			};
			const result = await runAssistant({
				engines: [server, next],
				lang: 'en',
				question: 'q',
				ranked: SECTIONS.slice(2, 6),
				pack,
				packSha256: 'abc',
			});
			expect(result.outcome).toMatchObject({ kind: 'answered', tier: 'browser' });
		});

	it('ends in retrieval with failed=true when the server is the only tier', async () => {
		const server = createServerEngine(1, {
			answer: async () => {
				throw new HelpAnswerError('timeout', 504);
			},
		});
		const result = await runAssistant({
			engines: [server],
			lang: 'en',
			question: 'q',
			ranked: SECTIONS.slice(2, 6),
			pack,
			packSha256: 'abc',
		});
		expect(result.outcome).toEqual({ kind: 'retrieval', failed: true });
	});
});
