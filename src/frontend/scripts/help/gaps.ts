/** help:gaps (manual.md 7.4): triage report of documentation gaps, clustered per route. */
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createAnalyzer } from '../../src/help/lexical/analyzer.ts';
import type { Analyzer } from '../../src/help/lexical/analyzer.ts';
import type { HelpLang } from '../../src/help/lexical/types.ts';
import { consoleIo, isEntry, parseOptions } from './cli.ts';
import type { Io } from './cli.ts';
import { loadTemplateStopwords } from './paths.ts';

export const GAP_OUTCOMES = ['no_match', 'refused', 'invalid', 'not_helpful', 'timeout'] as const;
/** Two questions are in one cluster when the Jaccard index of their stem sets is at least this (7.4). */
export const CLUSTER_JACCARD = 0.5;

export interface GapRecord {
	schema_version: number;
	recorded_at: string;
	lang: HelpLang;
	route: string;
	question: string;
	outcome: (typeof GAP_OUTCOMES)[number];
	tier: string;
	pack: { project_version: string; sha256: string };
	top_sections: string[];
	coverage: number;
}

export interface GapCluster {
	route: string;
	count: number;
	questions: { text: string; count: number }[];
	outcomes: Record<string, number>;
	topSection: string | null;
	langs: string[];
}

export function jaccard(a: ReadonlySet<string>, b: ReadonlySet<string>): number {
	if (a.size === 0 || b.size === 0) return 0;
	let inter = 0;
	for (const x of a) if (b.has(x)) inter++;
	return inter / (a.size + b.size - inter);
}

export function validRecord(r: unknown): r is GapRecord {
	const x = r as Partial<GapRecord> | null;
	return (
		!!x &&
		x.schema_version === 1 &&
		(x.lang === 'en' || x.lang === 'it') &&
		typeof x.route === 'string' &&
		typeof x.question === 'string' &&
		GAP_OUTCOMES.includes(x.outcome as never) &&
		Array.isArray(x.top_sections)
	);
}

/** Clusters records per route (connected components of the Jaccard relation); biggest cluster first. */
export function clusterGaps(records: GapRecord[], analyzers: Record<HelpLang, Analyzer>): GapCluster[] {
	const byRoute = new Map<string, GapRecord[]>();
	for (const r of records) byRoute.set(r.route, [...(byRoute.get(r.route) ?? []), r]);
	const clusters: GapCluster[] = [];
	for (const [route, recs] of byRoute) {
		const sorted = [...recs].sort((a, b) => (a.question < b.question ? -1 : a.question > b.question ? 1 : a.lang < b.lang ? -1 : a.lang > b.lang ? 1 : 0));
		const stems = sorted.map((r) => analyzers[r.lang].stems(r.question));
		const parent = sorted.map((_, i) => i);
		const find = (i: number): number => (parent[i] === i ? i : (parent[i] = find(parent[i])));
		for (let i = 0; i < sorted.length; i++) {
			for (let j = i + 1; j < sorted.length; j++) {
				if (sorted[i].lang === sorted[j].lang && jaccard(stems[i], stems[j]) >= CLUSTER_JACCARD) parent[find(j)] = find(i);
			}
		}
		const groups = new Map<number, GapRecord[]>();
		sorted.forEach((r, i) => groups.set(find(i), [...(groups.get(find(i)) ?? []), r]));
		for (const g of groups.values()) {
			const qCount = new Map<string, number>();
			const outcomes: Record<string, number> = {};
			const tops = new Map<string, number>();
			for (const r of g) {
				qCount.set(r.question, (qCount.get(r.question) ?? 0) + 1);
				outcomes[r.outcome] = (outcomes[r.outcome] ?? 0) + 1;
				const top = r.top_sections[0];
				if (top) tops.set(top, (tops.get(top) ?? 0) + 1);
			}
			const topSection = [...tops.entries()].sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1))[0]?.[0] ?? null;
			clusters.push({
				route,
				count: g.length,
				questions: [...qCount.entries()].map(([text, count]) => ({ text, count })).sort((a, b) => b.count - a.count || (a.text < b.text ? -1 : 1)),
				outcomes,
				topSection,
				langs: [...new Set(g.map((r) => r.lang))].sort(),
			});
		}
	}
	return clusters.sort((a, b) => (a.route < b.route ? -1 : a.route > b.route ? 1 : b.count - a.count || (a.questions[0].text < b.questions[0].text ? -1 : 1)));
}

export function formatGaps(clusters: GapCluster[]): string[] {
	const lines: string[] = [];
	const routes = [...new Set(clusters.map((c) => c.route))];
	for (const route of routes) {
		const cs = clusters.filter((c) => c.route === route);
		lines.push(`Route ${route || '(none)'}: ${cs.reduce((s, c) => s + c.count, 0)} questions in ${cs.length} cluster${cs.length === 1 ? '' : 's'}`);
		for (const c of cs) {
			const outcomes = Object.entries(c.outcomes)
				.sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1))
				.map(([k, v]) => `${k} ${v}`)
				.join(', ');
			lines.push(`  [${c.count}] ${c.langs.join('/')} | ${outcomes} | top section: ${c.topSection ?? 'none'}`);
			for (const q of c.questions.slice(0, 5)) lines.push(`      "${q.text}"${q.count > 1 ? ` x${q.count}` : ''}`);
			if (c.questions.length > 5) lines.push(`      ... ${c.questions.length - 5} more`);
		}
	}
	return lines;
}

export function main(argv: string[], io: Io): number {
	const o = parseOptions(argv, { '--in': 'value' });
	const file = o.values.get('--in');
	if (o.errors.length || !file) {
		for (const e of o.errors) io.err(`help:gaps: ${e}`);
		io.err('usage: help:gaps --in {export.json}');
		return 2;
	}
	let data: unknown;
	try {
		data = JSON.parse(readFileSync(resolve(file), 'utf8'));
	} catch (e) {
		io.err(`help:gaps: cannot read ${file}: ${(e as Error).message}`);
		return 1;
	}
	if (!Array.isArray(data)) {
		io.err('help:gaps: the export must be a JSON array of gap records');
		return 1;
	}
	const records = data.filter(validRecord);
	const skipped = data.length - records.length;
	const analyzers = {
		en: createAnalyzer('en', loadTemplateStopwords('en').words),
		it: createAnalyzer('it', loadTemplateStopwords('it').words),
	};
	const clusters = clusterGaps(records, analyzers);
	for (const line of formatGaps(clusters)) io.out(line);
	io.out(`help:gaps: ${records.length} records, ${clusters.length} clusters${skipped ? `, ${skipped} invalid records skipped` : ''}`);
	return 0;
}

if (isEntry(import.meta.url)) process.exitCode = main(process.argv.slice(2), consoleIo);
