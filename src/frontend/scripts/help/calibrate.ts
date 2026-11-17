/**
 * help:bench --calibrate (manual.md 6.7.7): the fusion parameters and the two cosine thresholds of a table, per language, from the
 * project's indexed questions only. It runs here, in TypeScript, because the numbers must come from the ranking code the browser
 * runs. Bench, out-of-scope and Extra questions are never read.
 */
import { join } from 'node:path';
import { writeFileSync } from 'node:fs';
import { sha256Hex } from '../lib/index.ts';
import { createAnalyzer } from '../../src/help/lexical/analyzer.ts';
import { createEmbeddingScorer } from '../../src/help/lexical/embeddings.ts';
import type { EmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import { compileGlossary } from '../../src/help/lexical/glossary.ts';
import type { GlossaryEntry } from '../../src/help/lexical/glossary.ts';
import { createSearcher, DEFAULT_FUSION_DEPTH, judgeInScope } from '../../src/help/lexical/ranking.ts';
import type { EmbeddingsParams, HelpLang, HelpPack, PackQuestion, RetrievalParams } from '../../src/help/lexical/types.ts';
import { normalizeQuestion } from '../../src/help/bench/split.ts';
import { buildHelp, LANGUAGES } from './build.ts';
import { CALIBRATION_FILE, defaultParams, loadEmbedConfig } from './embeddings.ts';
import type { EmbedConfig } from './embeddings.ts';
import { loadGlossary } from './glossaryfile.ts';
import { indexWithout } from './indexed.ts';
import { loadTemplateStopwords } from './paths.ts';

export interface Metrics {
	recall_at_1: number | null;
	recall_at_3: number | null;
	mrr_at_10: number | null;
	false_no_match: number | null;
}

export interface LangCalibration {
	table_sha256: string;
	pack_inputs_sha256: string;
	calibrated: boolean;
	items: { tuning: number; held_out: number };
	params: EmbeddingsParams;
	target_missed: boolean;
	metrics: {
		lexical: { tuning: Metrics; held_out: Metrics };
		fused: { tuning: Metrics; held_out: Metrics };
	};
	notes: string[];
}

interface Evaluated {
	/** Rank (from 1) of the first section of the expected story within the top 10; 0 when none. */
	rank: number;
	inScope: boolean;
	coverage: number;
	cosine: number | null;
	queryTerms: number;
}

interface Item {
	q: PackQuestion;
	key: string;
	evaluate(retrieval: RetrievalParams, withTable: boolean): Evaluated;
}

const round6 = (x: number): number => Math.round(x * 1e6) / 1e6;

export interface PairScore {
	k: number;
	w: number;
	r3: number;
	mrr: number;
	r1: number;
}

/**
 * Order of the fusion grid (6.7.7 step 3): negative when `a` is the better pair: higher Recall@3, then MRR@10, then Recall@1,
 * then the smaller `embedding_weight`, then the larger `rrf_k`.
 */
export function comparePairs(a: PairScore, b: PairScore): number {
	return b.r3 - a.r3 || b.mrr - a.mrr || b.r1 - a.r1 || a.w - b.w || b.k - a.k;
}

/** The guard of 6.7.7 step 6: why the table must not be used, from the held-out metrics of the two configurations; empty when it may. */
export function guardReasons(lexical: Metrics, fused: Metrics): string[] {
	const why: string[] = [];
	if ((fused.recall_at_3 ?? 0) < (lexical.recall_at_3 ?? 0)) why.push(`held-out Recall@3 is ${fused.recall_at_3} fused against ${lexical.recall_at_3} lexical`);
	if ((fused.false_no_match ?? 0) > (lexical.false_no_match ?? 0)) why.push(`held-out false no-match is ${fused.false_no_match} fused against ${lexical.false_no_match} lexical`);
	return why;
}

/** Grid of a threshold: 0.00, 0.01, ... up to 0.99 (`cosine_step`), free of float noise. */
export function cosineGrid(step: number): number[] {
	const out: number[] = [];
	for (let k = 0; k * step < 1 - 1e-9; k++) out.push(Math.round(k * step * 1e6) / 1e6);
	return out;
}

export function metricsOf(list: readonly Evaluated[]): Metrics {
	const n = list.length;
	if (n === 0) return { recall_at_1: null, recall_at_3: null, mrr_at_10: null, false_no_match: null };
	return {
		recall_at_1: round6(list.filter((e) => e.rank === 1).length / n),
		recall_at_3: round6(list.filter((e) => e.rank >= 1 && e.rank <= 3).length / n),
		mrr_at_10: round6(list.reduce((s, e) => s + (e.rank ? 1 / e.rank : 0), 0) / n),
		false_no_match: round6(list.filter((e) => !e.inScope).length / n),
	};
}

/** The slices of 6.7.7 step 2: items sorted by `sha256("calibration\n{lang}\n{story}\n{normalised question}")`; the first share is held out. */
export function sliceItems<T extends { q: PackQuestion }>(lang: string, items: readonly T[], share: number): { tuning: T[]; heldOut: T[] } {
	const keyed = items.map((it) => ({ it, key: sha256Hex(`calibration\n${lang}\n${it.q.story}\n${normalizeQuestion(it.q.text)}`) }));
	keyed.sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));
	const held = Math.ceil(share * keyed.length);
	return { heldOut: keyed.slice(0, held).map((k) => k.it), tuning: keyed.slice(held).map((k) => k.it) };
}

export interface CalibrateInput {
	lang: HelpLang;
	pack: HelpPack;
	table: EmbeddingTable;
	tableSha256: string;
	glossary: readonly GlossaryEntry[];
	config: EmbedConfig['calibration'];
}

export function calibrateLanguage(input: CalibrateInput): LangCalibration {
	const { lang, pack, table, config } = input;
	const base = pack.assistant.retrieval.content;
	const defaults = defaultParams(base);
	const analyzer = createAnalyzer(lang, loadTemplateStopwords(lang).words);
	const sectionIds = pack.sections.map((s) => s.id);
	const storyOf = pack.sections.map((s) => s.story);

	const variant = (p: Omit<EmbeddingsParams, 'method' | 'depth'>): RetrievalParams => ({
		...base,
		out_of_scope: { ...base.out_of_scope, min_cosine: p.min_cosine, min_cosine_lexical: p.min_cosine_lexical },
		fusion: { method: 'rrf', depth: base.fusion.depth ?? DEFAULT_FUSION_DEPTH, rrf_k: p.rrf_k, embedding_weight: p.embedding_weight },
	});

	const items: Item[] = pack.questions.map((q) => {
		const index = indexWithout(pack, q, analyzer, input.glossary);
		const scorer = createEmbeddingScorer(table, index, sectionIds, base.bm25.field_weights);
		return {
			q,
			key: sha256Hex(`calibration\n${lang}\n${q.story}\n${normalizeQuestion(q.text)}`),
			evaluate(retrieval, withTable) {
				const s = createSearcher({ index, sectionIds, retrieval, analyzer, embeddings: withTable ? scorer : null });
				const hits = s.search(q.text);
				const d = s.decide(q.text, hits);
				const i = hits.slice(0, 10).findIndex((h) => storyOf[h.index] !== null && storyOf[h.index] === q.story);
				return { rank: i < 0 ? 0 : i + 1, inScope: d.inScope, coverage: d.coverage, cosine: d.cosine, queryTerms: d.queryTerms };
			},
		};
	});

	const { tuning, heldOut } = sliceItems(lang, items, config.held_out_share);
	const lexicalVariant = { ...base, fusion: { ...base.fusion, method: 'none' as const } };
	const notes: string[] = [];
	let params: EmbeddingsParams = defaults;
	let calibrated = false;
	let targetMissed = false;

	if (items.length < config.min_items) {
		notes.push(`fewer than ${config.min_items} indexed questions (${items.length}): nothing is tuned, the parameters are the defaults of retrieval.json`);
	} else {
		// 3. fusion grid on the tuning slice: highest Recall@3, then MRR@10, Recall@1, the smaller weight, the larger rrf_k
		let best: PairScore | null = null;
		for (const k of config.rrf_k)
			for (const w of config.embedding_weight) {
				const ev = tuning.map((it) => it.evaluate(variant({ ...defaults, rrf_k: k, embedding_weight: w }), true));
				const m = metricsOf(ev);
				const cand = { k, w, r3: m.recall_at_3 ?? 0, mrr: m.mrr_at_10 ?? 0, r1: m.recall_at_1 ?? 0 };
				if (!best || comparePairs(cand, best) < 0) best = cand;
			}
		const pair = best!;
		const chosen = variant({ ...defaults, rrf_k: pair.k, embedding_weight: pair.w });
		const evals = tuning.map((it) => it.evaluate(chosen, true));
		const grid = cosineGrid(config.cosine_step);
		const oos = base.out_of_scope;

		// 4. veto threshold: the largest grid value that rejects at most `max_veto_loss` of the tuning items the lexical rule accepts
		let veto = -1;
		for (const v of grid) {
			const lost = evals.filter((e) => e.coverage >= oos.min_coverage && e.cosine !== null && e.cosine < v).length;
			if (evals.length && lost / evals.length <= config.max_veto_loss) veto = v;
		}
		if (veto === -1) notes.push('no value of the veto grid keeps the loss within max_veto_loss: min_cosine_lexical is -1 (veto off)');

		// 5. semantic threshold: the largest grid value whose false no-match on the tuning slice meets the target
		const fnmAt = (v: number): number =>
			evals.filter((e) => !judgeInScope(true, e.coverage, e.cosine, e.queryTerms, { ...oos, min_cosine: v, min_cosine_lexical: veto })).length / (evals.length || 1);
		const rows = grid.map((v) => ({ v, f: fnmAt(v) }));
		const meets = rows.filter((r) => r.f <= config.target_false_no_match);
		let minCosine: number;
		if (meets.length) minCosine = Math.max(...meets.map((r) => r.v));
		else {
			const lowest = Math.min(...rows.map((r) => r.f));
			minCosine = Math.max(...rows.filter((r) => r.f === lowest).map((r) => r.v));
			targetMissed = true;
			notes.push(`no min_cosine reaches a false no-match of ${config.target_false_no_match} on the tuning slice: the lowest is ${round6(lowest)} (target missed)`);
		}
		params = { method: 'rrf', depth: defaults.depth, rrf_k: pair.k, embedding_weight: pair.w, min_cosine: minCosine, min_cosine_lexical: veto };
		calibrated = true;
	}

	const final = variant(params);
	const metrics = {
		lexical: { tuning: metricsOf(tuning.map((it) => it.evaluate(lexicalVariant, false))), held_out: metricsOf(heldOut.map((it) => it.evaluate(lexicalVariant, false))) },
		fused: { tuning: metricsOf(tuning.map((it) => it.evaluate(final, true))), held_out: metricsOf(heldOut.map((it) => it.evaluate(final, true))) },
	};

	// 6. guard on the held-out slice: the table must not make recall or false no-match worse
	if (calibrated && heldOut.length) {
		const why = guardReasons(metrics.lexical.held_out, metrics.fused.held_out);
		if (why.length) {
			params = { ...params, method: 'none' };
			notes.push(`guard: ${why.join('; ')}: params.method is none, the table is not used`);
		}
	}

	return {
		table_sha256: input.tableSha256,
		pack_inputs_sha256: pack.build.inputs_sha256,
		calibrated,
		items: { tuning: tuning.length, held_out: heldOut.length },
		params,
		target_missed: targetMissed,
		metrics,
		notes,
	};
}

export interface CalibrateResult {
	file: string;
	json: string;
	summary: string[];
	errors: string[];
}

/** Builds the packs with the table of `dir`, calibrates every language that has a usable one and writes `{dir}/calibration.json`. */
export function runCalibrate(root: string, dir: string, unlocked: boolean): CalibrateResult {
	const file = join(dir, CALIBRATION_FILE);
	const built = buildHelp(root, { embeddingsDir: dir, unlocked });
	if (built.errors.length || built.languages.length < LANGUAGES.length) {
		return { file, json: '', summary: [], errors: built.errors.length ? built.errors : ['the help packs could not be built'] };
	}
	const config = loadEmbedConfig(root);
	const glossary = loadGlossary(root);
	const languages: Record<string, LangCalibration> = {};
	const summary: string[] = [];
	for (const l of built.languages) {
		if (!l.table || !l.tableBytes) {
			summary.push(`${l.lang}: no usable table in ${dir}${unlocked ? '' : ' (a table that the lock does not record needs --unlocked)'}: skipped`);
			continue;
		}
		const analyzer = createAnalyzer(l.lang, loadTemplateStopwords(l.lang).words);
		const r = calibrateLanguage({
			lang: l.lang,
			pack: l.pack,
			table: l.table,
			tableSha256: l.pack.embeddings!.sha256,
			glossary: compileGlossary(glossary.rows[l.lang], analyzer),
			config: config.data.calibration,
		});
		languages[l.lang] = r;
		const f = r.metrics.fused;
		const x = r.metrics.lexical;
		summary.push(
			`${l.lang}: ${r.items.tuning} tuning and ${r.items.held_out} held-out indexed questions; ${r.calibrated ? 'calibrated' : 'NOT calibrated (defaults)'}; params ${JSON.stringify(r.params)}`,
			`  held-out Recall@3 fused ${f.held_out.recall_at_3} / lexical ${x.held_out.recall_at_3}; false no-match fused ${f.held_out.false_no_match} / lexical ${x.held_out.false_no_match}`,
			...r.notes.map((n) => `  note: ${n}`),
		);
	}
	if (!Object.keys(languages).length) return { file, json: '', summary, errors: ['no language has a usable embedding table'] };
	const body = {
		schema_version: 1,
		kind: 'help-embeddings-calibration',
		config_sha256: config.sha256,
		retrieval_sha256: built.languages[0].pack.assistant.retrieval.sha256,
		languages,
	};
	return { file, json: `${JSON.stringify(body, null, 2)}\n`, summary, errors: [] };
}

export function writeCalibration(r: CalibrateResult): void {
	writeFileSync(r.file, r.json);
}
