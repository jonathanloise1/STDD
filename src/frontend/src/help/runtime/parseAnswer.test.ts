import { describe, expect, it } from 'vitest';
import { parseAnswer, streamingText } from './parseAnswer.ts';

const limits = {
	sources: 2,
	maxBodyChars: 600,
	maxBodyLines: 7,
	refusal: "I can't find this in the manual.",
};

describe('answer parsing (models.md 3.4)', () => {
	it('accepts a body with a source line', () => {
		expect(parseAnswer('Click "New Task".\nSource: [1]', limits)).toEqual({
			kind: 'answered',
			body: 'Click "New Task".',
			cited: [1],
		});
		expect(parseAnswer('One.\nSource: [1], [2]\n', limits)).toMatchObject({ cited: [1, 2] });
	});

	it('recognises the refusal sentence only when it is alone', () => {
		expect(parseAnswer("I can't find this in the manual.", limits)).toEqual({
			kind: 'refused',
		});
		expect(parseAnswer("I can't find this in the manual.\nSource: [1]", limits).kind).toBe(
			'invalid',
		);
	});

	it('treats an answer without a source as invalid', () => {
		expect(parseAnswer('Click New Task.', limits).kind).toBe('invalid');
	});

	it('rejects a source outside the context, repeated or not ascending', () => {
		expect(parseAnswer('Body.\nSource: [3]', limits).kind).toBe('invalid');
		expect(parseAnswer('Body.\nSource: [2], [1]', limits).kind).toBe('invalid');
		expect(parseAnswer('Body.\nSource: [1], [1]', limits).kind).toBe('invalid');
	});

	it('rejects an empty body and one that is too long', () => {
		expect(parseAnswer('Source: [1]', limits).kind).toBe('invalid');
		expect(parseAnswer(`${'x'.repeat(601)}\nSource: [1]`, limits).kind).toBe('invalid');
		expect(parseAnswer(`${'a\n'.repeat(8)}Source: [1]`, limits).kind).toBe('invalid');
	});
});

describe('streamed text', () => {
	it('never shows the source line or a start of the refusal', () => {
		expect(streamingText('Click New Task.\nSource: [1]', limits.refusal)).toBe(
			'Click New Task.',
		);
		expect(streamingText('Click New Task.\nSou', limits.refusal)).toBe('Click New Task.');
		expect(streamingText("I can't fi", limits.refusal)).toBe('');
		expect(streamingText("I can't find this in the manual.", limits.refusal)).toBe('');
		expect(streamingText('Click New', limits.refusal)).toBe('Click New');
	});
});
