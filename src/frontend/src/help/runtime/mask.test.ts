import { describe, expect, it } from 'vitest';
import { maskQuestion } from './mask.ts';
import { normalizeStatus } from './status.ts';
import { periodError, resolvePeriod } from './gapExport.ts';

describe('masking (manual.md 7.4)', () => {
	it('replaces e-mail addresses, web addresses and numbers of four or more digits', () => {
		expect(
			maskQuestion(
				'can i pay invoice 12345678 from here, write to anna.rossi@example.com or see https://example.com/pay',
			),
		).toBe('can i pay invoice [number] from here, write to [email] or see [url]');
	});

	it('masks digit runs with separators and keeps short numbers', () => {
		expect(maskQuestion('card 1234 5678-9012.3456 and 123 items')).toBe(
			'card [number] and 123 items',
		);
	});

	it('keeps at most 300 characters after masking', () => {
		expect(maskQuestion('a'.repeat(400))).toHaveLength(300);
	});
});

describe('status of the help tiers', () => {
	it('reads snake_case and camelCase values', () => {
		expect(
			normalizeStatus({
				assistant: {
					order: 'server-first',
					server: { available: true, timeout_seconds: 5 },
				},
			}).server,
		).toEqual({ available: true, timeoutSeconds: 5 });
		expect(
			normalizeStatus({
				order: 'browser-first',
				browser: { enabled: true, askBeforeDownloadMb: 50 },
			}).browser,
		).toMatchObject({
			enabled: true,
			askBeforeDownloadMb: 50,
		});
	});

	it('falls back to retrieval only on anything it does not understand', () => {
		for (const raw of [null, 'x', {}, { order: 'turbo' }]) {
			const status = normalizeStatus(raw);
			expect(status.order).toBe('retrieval-only');
			expect(status.server.available).toBe(false);
		}
	});
});

describe('period of the unanswered questions', () => {
	it('refuses a period that ends before it starts', () => {
		expect(periodError(resolvePeriod('2030-02-01', '2030-01-01'))).toBe(true);
		expect(periodError(resolvePeriod('2030-01-01', '2030-01-01'))).toBe(false);
	});

	it('reads an empty field as no bound', () => {
		expect(periodError(resolvePeriod('', ''))).toBe(false);
		expect(resolvePeriod('', '2030-01-01').from).toBe('1970-01-01');
	});
});
