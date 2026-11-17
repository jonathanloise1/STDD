// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { answerGates, answerGatesPass } from './answers.ts';
import {
	computeLangReport,
	formatLangReport,
	hitRank,
	MIN_GATED_ITEMS,
	reportPasses,
} from './metrics.ts';
import type { ItemResult } from './metrics.ts';

function item(n: number, over: Partial<ItemResult>): ItemResult {
	return {
		id: `i${n}`,
		lang: 'en',
		question: `Question ${n}?`,
		kind: 'in_scope',
		story: 'US-A-01',
		source: 'story',
		retrieved: [],
		retrievedStories: [],
		context: [],
		inScopeDecision: true,
		coverage: 1,
		...over,
	};
}
const hitAt = (rank: number): Partial<ItemResult> => ({
	retrieved: Array.from({ length: Math.max(rank, 1) }, (_, i) => `s${i}`),
	retrievedStories: Array.from({ length: Math.max(rank, 1) }, (_, i) =>
		i + 1 === rank ? 'US-A-01' : 'US-B-01',
	),
});
const gate = (r: ReturnType<typeof computeLangReport>, name: string) =>
	r.gates.find((g) => g.metric === name)!;

describe('retrieval metrics (manual.md 8.3)', () => {
	it('computes the rank of the first hit within the top 10', () => {
		expect(hitRank(item(1, hitAt(1)))).toBe(1);
		expect(hitRank(item(1, hitAt(3)))).toBe(3);
		expect(hitRank(item(1, hitAt(0)))).toBe(0);
		expect(
			hitRank(item(1, { retrievedStories: [...Array(10).fill('US-B-01'), 'US-A-01'] })),
		).toBe(0);
	});
	it('computes Recall@1, Recall@3, MRR@10, false no-match and out-of-scope rejection', () => {
		const ins = [
			...Array.from({ length: 12 }, (_, i) => item(i, hitAt(1))),
			...Array.from({ length: 4 }, (_, i) => item(20 + i, hitAt(2))),
			...Array.from({ length: 2 }, (_, i) => item(30 + i, hitAt(5))),
			...Array.from({ length: 2 }, (_, i) =>
				item(40 + i, { ...hitAt(0), inScopeDecision: false }),
			),
		];
		const oos = Array.from({ length: 10 }, (_, i) =>
			item(50 + i, {
				kind: 'out_of_scope',
				story: null,
				source: 'oos',
				inScopeDecision: i < 2,
			}),
		);
		const r = computeLangReport('en', [...ins, ...oos]);
		expect(r.inScope).toBe(20);
		expect(gate(r, 'Recall@1').value).toBeCloseTo(12 / 20);
		expect(gate(r, 'Recall@3').value).toBeCloseTo(16 / 20);
		expect(gate(r, 'MRR@10').value).toBeCloseTo((12 + 4 / 2 + 2 / 5) / 20);
		expect(gate(r, 'False no-match').value).toBeCloseTo(2 / 20);
		expect(gate(r, 'Out-of-scope rejection').value).toBeCloseTo(8 / 10);
		expect(r.gating).toBe(true);
		expect(r.gates.every((g) => g.pass)).toBe(true);
		expect(r.misses.map((m) => m.id)).toEqual(['i30', 'i31', 'i40', 'i41']);
	});
	it('fails each gate at its threshold', () => {
		const make = (opts: { r1: number; r3: number; fnm: number; oosRej: number }) => {
			const ins = Array.from({ length: 20 }, (_, i) =>
				item(i, {
					...hitAt(i < opts.r1 ? 1 : i < opts.r3 ? 3 : 0),
					inScopeDecision: i >= opts.fnm,
				}),
			);
			const oos = Array.from({ length: 10 }, (_, i) =>
				item(100 + i, {
					kind: 'out_of_scope',
					story: null,
					source: 'oos',
					inScopeDecision: i >= opts.oosRej,
				}),
			);
			return computeLangReport('en', [...ins, ...oos]);
		};
		const good = { r1: 12, r3: 16, fnm: 2, oosRej: 7 };
		expect(reportPasses([make(good)])).toBe(true);
		expect(gate(make({ ...good, r1: 11 }), 'Recall@1').pass).toBe(false);
		expect(gate(make({ ...good, r3: 15 }), 'Recall@3').pass).toBe(false);
		expect(gate(make({ ...good, fnm: 3 }), 'False no-match').pass).toBe(false);
		expect(gate(make({ ...good, oosRej: 6 }), 'Out-of-scope rejection').pass).toBe(false);
		expect(reportPasses([make({ ...good, r1: 11 })])).toBe(false);
	});
	it('reports but does not gate a language with fewer than 20 in-scope items', () => {
		const r = computeLangReport('it', [
			...Array.from({ length: MIN_GATED_ITEMS - 1 }, (_, i) => item(i, hitAt(0))),
			item(99, { kind: 'out_of_scope', story: null, inScopeDecision: true }),
		]);
		expect(r.gating).toBe(false);
		expect(r.gates.every((g) => g.pass)).toBe(true);
		expect(formatLangReport(r).join('\n')).toContain('nothing gates');
	});
	it('prints a table and the misses with their top 3 sections', () => {
		const lines = formatLangReport(
			computeLangReport('en', [
				item(1, { ...hitAt(0), retrieved: ['a#x', 'b#y', 'c#z', 'd#w'] }),
			]),
		);
		const text = lines.join('\n');
		expect(text).toContain('Recall@1');
		expect(text).toContain('>= 0.60');
		expect(text).toContain('top 3: a#x, b#y, c#z');
	});
});

describe('answer gates (manual.md 8.3, separate step)', () => {
	const metrics = {
		n: 40,
		faithful: 0.97,
		useful: 0.7,
		wrongful_refusal: 0.1,
		oos_refusal: 0.95,
		correct_source: 0.9,
	};
	const report = (en: object, it: object = metrics) => ({ results: { sample_app: { en, it } } });
	it('passes when every metric meets its threshold', () => {
		expect(answerGatesPass(answerGates(report(metrics)))).toBe(true);
	});
	it('fails per metric and per language', () => {
		for (const [key, bad] of [
			['faithful', 0.9],
			['useful', 0.6],
			['wrongful_refusal', 0.2],
			['oos_refusal', 0.8],
			['correct_source', 0.8],
		] as const) {
			const res = answerGates(report({ ...metrics, [key]: bad }));
			expect(answerGatesPass(res)).toBe(false);
			expect(res[0].gates.filter((g) => !g.pass)).toHaveLength(1);
			expect(res[1].gates.every((g) => g.pass)).toBe(true);
		}
	});
	it('fails when a language or metric is missing, and does not gate below 20 items', () => {
		expect(answerGatesPass(answerGates({ results: {} }))).toBe(false);
		expect(answerGatesPass(answerGates(report({ n: 40, faithful: 1 })))).toBe(false);
		const small = answerGates(report({ ...metrics, n: 10, faithful: 0.1 }));
		expect(small[0].gating).toBe(false);
		expect(small[0].gates.every((g) => g.pass)).toBe(true);
	});
});
