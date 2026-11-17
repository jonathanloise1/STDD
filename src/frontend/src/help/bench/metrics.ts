/** Retrieval metrics and gates of the project bench (manual.md 8.3). Pure functions over per-item results. */

export interface BenchItem {
	id: string;
	lang: string;
	question: string;
	kind: 'in_scope' | 'out_of_scope';
	/** Expected story of an in-scope question; null for out of scope. */
	story: string | null;
	/** `indexed`: an indexed question asked leave-one-out (`help:bench -- --indexed`, manual.md 6.9); never gates. */
	source: 'story' | 'extra' | 'oos' | 'indexed';
}

export interface ItemResult extends BenchItem {
	/** Section ids returned by retrieval, best first. */
	retrieved: string[];
	/** Story of each returned section (null for home and area sections). */
	retrievedStories: (string | null)[];
	/** Section ids selected by models.md 3.5. */
	context: string[];
	/** Result of the out-of-scope decision of 6.6: true when judged in scope. */
	inScopeDecision: boolean;
	coverage: number;
}

export interface Gate {
	metric: string;
	/** Null when the population is empty. */
	value: number | null;
	/** `>= 0.60`, `<= 0.10` or `reported`. */
	threshold: string;
	/** Always true for reported metrics and for languages below the minimum. */
	pass: boolean;
	gated: boolean;
}

export interface LangReport {
	lang: string;
	inScope: number;
	outOfScope: number;
	/** False when the language has fewer in-scope items than `MIN_GATED_ITEMS`. */
	gating: boolean;
	gates: Gate[];
	/** In-scope items without a hit in the top 3, with their top 3 sections. */
	misses: { id: string; question: string; story: string | null; top: string[] }[];
}

/** A language with fewer in-scope bench items reports its metrics but does not gate (8.3). */
export const MIN_GATED_ITEMS = 20;

export const RETRIEVAL_THRESHOLDS = {
	recall1: { op: '>=', value: 0.6 },
	recall3: { op: '>=', value: 0.8 },
	falseNoMatch: { op: '<=', value: 0.1 },
	oosRejection: { op: '>=', value: 0.7 },
} as const;

const ratio = (num: number, den: number): number | null => (den === 0 ? null : num / den);

/** Rank (from 1) of the first returned section of the expected story within the top 10, or 0. */
export function hitRank(r: ItemResult): number {
	const i = r.retrievedStories.slice(0, 10).findIndex((s) => s !== null && s === r.story);
	return i < 0 ? 0 : i + 1;
}

export function evaluateGate(
	metric: string,
	value: number | null,
	op: '>=' | '<=' | null,
	limit: number | null,
	gating: boolean,
): Gate {
	if (op === null || limit === null)
		return { metric, value, threshold: 'reported', pass: true, gated: false };
	const threshold = `${op} ${limit.toFixed(2)}`;
	const ok = value === null || (op === '>=' ? value >= limit : value <= limit);
	return { metric, value, threshold, pass: !gating || ok, gated: gating && value !== null };
}

/** Metrics of one language (8.3). */
export function computeLangReport(lang: string, results: ItemResult[]): LangReport {
	const inScope = results.filter((r) => r.kind === 'in_scope');
	const oos = results.filter((r) => r.kind === 'out_of_scope');
	const gating = inScope.length >= MIN_GATED_ITEMS;
	const ranks = inScope.map(hitRank);
	const recall = (k: number) =>
		ratio(ranks.filter((x) => x >= 1 && x <= k).length, inScope.length);
	const mrr = ratio(
		ranks.reduce((s, x) => s + (x ? 1 / x : 0), 0),
		inScope.length,
	);
	const t = RETRIEVAL_THRESHOLDS;
	const gates = [
		evaluateGate('Recall@1', recall(1), t.recall1.op, t.recall1.value, gating),
		evaluateGate('Recall@3', recall(3), t.recall3.op, t.recall3.value, gating),
		evaluateGate('MRR@10', mrr, null, null, gating),
		evaluateGate(
			'False no-match',
			ratio(inScope.filter((r) => !r.inScopeDecision).length, inScope.length),
			t.falseNoMatch.op,
			t.falseNoMatch.value,
			gating,
		),
		evaluateGate(
			'Out-of-scope rejection',
			ratio(oos.filter((r) => !r.inScopeDecision).length, oos.length),
			t.oosRejection.op,
			t.oosRejection.value,
			gating,
		),
	];
	const misses = inScope
		.map((r, i) => ({ r, rank: ranks[i] }))
		.filter((x) => x.rank === 0 || x.rank > 3)
		.map(({ r }) => ({
			id: r.id,
			question: r.question,
			story: r.story,
			top: r.retrieved.slice(0, 3),
		}));
	return { lang, inScope: inScope.length, outOfScope: oos.length, gating, gates, misses };
}

export function reportPasses(reports: LangReport[]): boolean {
	return reports.every((r) => r.gates.every((g) => g.pass));
}

const fmt = (v: number | null): string => (v === null ? 'n/a' : v.toFixed(3));

/** Plain-text table of one language report: metric, value, threshold, pass; then the misses. */
export function formatLangReport(r: LangReport): string[] {
	const lines = [
		`Language ${r.lang}: ${r.inScope} in-scope and ${r.outOfScope} out-of-scope items`,
	];
	if (!r.gating)
		lines.push(
			`  fewer than ${MIN_GATED_ITEMS} in-scope bench items: metrics are reported, nothing gates`,
		);
	const w = Math.max(...r.gates.map((g) => g.metric.length));
	lines.push(`  ${'Metric'.padEnd(w)}  ${'Value'.padStart(6)}  ${'Threshold'.padEnd(9)}  Pass`);
	for (const g of r.gates) {
		const pass = g.threshold === 'reported' ? '-' : !r.gating ? 'n/a' : g.pass ? 'yes' : 'NO';
		lines.push(
			`  ${g.metric.padEnd(w)}  ${fmt(g.value).padStart(6)}  ${g.threshold.padEnd(9)}  ${pass}`,
		);
	}
	if (r.misses.length) {
		lines.push(`  In-scope misses (${r.misses.length}):`);
		for (const m of r.misses)
			lines.push(
				`    ${m.id} "${m.question}" expected ${m.story}; top 3: ${m.top.join(', ') || 'none'}`,
			);
	}
	return lines;
}

/**
 * The fused report beside the lexical one (manual.md 6.7.8): per metric the two values and their difference. The fused report is
 * the one that gates; `lexical` is the same items with `fusion.method` = `none`.
 */
export function formatComparison(
	fused: LangReport,
	lexical: LangReport,
	note: string | null,
): string[] {
	const lines = [
		`Language ${fused.lang}: fused (gates) against lexical, ${fused.inScope} in-scope and ${fused.outOfScope} out-of-scope items`,
	];
	if (note) lines.push(`  ${note}`);
	const w = Math.max(...fused.gates.map((g) => g.metric.length));
	lines.push(
		`  ${'Metric'.padEnd(w)}  ${'Fused'.padStart(6)}  ${'Lexical'.padStart(7)}  ${'Diff'.padStart(7)}  Threshold  Pass`,
	);
	fused.gates.forEach((g, i) => {
		const l = lexical.gates[i];
		const diff =
			g.value === null || l.value === null
				? 'n/a'
				: `${g.value - l.value >= 0 ? '+' : ''}${(g.value - l.value).toFixed(3)}`;
		const pass =
			g.threshold === 'reported' ? '-' : !fused.gating ? 'n/a' : g.pass ? 'yes' : 'NO';
		lines.push(
			`  ${g.metric.padEnd(w)}  ${fmt(g.value).padStart(6)}  ${fmt(l.value).padStart(7)}  ${diff.padStart(7)}  ${g.threshold.padEnd(9)}  ${pass}`,
		);
	});
	return lines;
}
