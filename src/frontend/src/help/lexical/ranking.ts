/**
 * BM25F ranking, reciprocal rank fusion (manual.md 6.5) and the out-of-scope decision (6.6) over a pack's index. Pure,
 * deterministic. Without an embedding scorer, or with `fusion.method` = `none`, ranking and decision are lexical only.
 */
import type { Analyzer } from './analyzer.ts';
import { createEmbeddingScorer } from './embeddings.ts';
import type { EmbeddingTable } from './embeddings.ts';
import { fuseRrf } from './fusion.ts';
import type { EmbeddingScorer, Scored } from './fusion.ts';
import { INDEX_FIELDS } from './types.ts';
import type { HelpPack, PackIndex, Posting, RetrievalParams } from './types.ts';

/** `fusion.depth` of packs that do not state one (manual.md 6.5). */
export const DEFAULT_FUSION_DEPTH = 30;

export type Hit = Scored;

export interface Decision {
	inScope: boolean;
	coverage: number;
	/** Cosine of the query and the top section (manual.md 6.6); null when fusion does not apply or the query has no stem in the table. */
	cosine: number | null;
	/** Number of distinct query stems. */
	queryTerms: number;
	topId: string | null;
}

export interface Searcher {
	/** Distinct query stems in code point order. */
	stems(query: string): string[];
	/** Top `candidates` sections by score (ties by section id), score 0 excluded. */
	search(query: string): Hit[];
	/** The out-of-scope decision; pass the hits of `search` to avoid ranking twice. */
	decide(query: string, hits?: Hit[]): Decision;
}

export interface SearcherOptions {
	index: PackIndex;
	sectionIds: readonly string[];
	/** The effective parameters (`effectiveRetrieval`). */
	retrieval: RetrievalParams;
	analyzer: Analyzer;
	/** The embedding side of a usable table; null or absent means lexical ranking only. */
	embeddings?: EmbeddingScorer | null;
}

/**
 * The parameters a pack ranks with (manual.md 6.5): the file's values, with `rrf_k`, `embedding_weight`, `min_cosine` and
 * `min_cosine_lexical` replaced by the pack's `embeddings.params` when the pack uses a table; `method` `none` there turns fusion
 * off. `depth` always stays the file's.
 */
export function effectiveRetrieval(
	pack: Pick<HelpPack, 'assistant' | 'embeddings'>,
): RetrievalParams {
	const base = pack.assistant.retrieval.content;
	const p = pack.embeddings?.params;
	return {
		...base,
		out_of_scope: {
			...base.out_of_scope,
			min_cosine_lexical: p?.min_cosine_lexical ?? base.out_of_scope.min_cosine_lexical ?? -1,
			min_cosine: p?.min_cosine ?? base.out_of_scope.min_cosine,
		},
		fusion: {
			...base.fusion,
			method: p ? p.method : base.fusion.method,
			depth: base.fusion.depth ?? DEFAULT_FUSION_DEPTH,
			rrf_k: p?.rrf_k ?? base.fusion.rrf_k,
			embedding_weight: p?.embedding_weight ?? base.fusion.embedding_weight,
		},
	};
}

const byCodePoint = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** The page of a section id (`{page_id}#{key}`). */
export const pageOfSection = (id: string): string =>
	id.slice(0, id.indexOf('#') < 0 ? id.length : id.indexOf('#'));

/**
 * `diversify` = `page` (6.5): the best section of every page, in score order, then the other sections in score
 * order. The first hit never changes.
 */
export function pagesFirst<T extends { id: string }>(ranked: readonly T[]): T[] {
	const seen = new Set<string>();
	const first: T[] = [];
	const rest: T[] = [];
	for (const h of ranked) {
		const page = pageOfSection(h.id);
		(seen.has(page) ? rest : first).push(h);
		seen.add(page);
	}
	return [...first, ...rest];
}

/**
 * The out-of-scope rule of manual.md 6.6. Without fusion: `coverage >= min_coverage`. With it: semantic acceptance
 * (`cosine >= min_cosine`), or lexical acceptance (`coverage >= min_coverage`) unless the semantic veto applies (`cosine` below
 * `min_cosine_lexical`; a null cosine never vetoes). Always needs `min_query_terms` distinct query stems.
 */
export function judgeInScope(
	fusionOn: boolean,
	coverage: number,
	cosine: number | null,
	queryTerms: number,
	oos: RetrievalParams['out_of_scope'],
): boolean {
	if (queryTerms < oos.min_query_terms) return false;
	if (!fusionOn) return coverage >= oos.min_coverage;
	if (cosine !== null && cosine >= oos.min_cosine) return true;
	return (
		coverage >= oos.min_coverage &&
		(cosine === null || cosine >= (oos.min_cosine_lexical ?? -1))
	);
}

export function createSearcher(opts: SearcherOptions): Searcher {
	const { index, sectionIds, retrieval, analyzer } = opts;
	const embeddings = opts.embeddings ?? null;
	const terms = new Map<string, Posting>(Object.entries(index.terms));
	const n = index.doc_count;
	const { k1, b, field_weights } = retrieval.bm25;
	const weights = INDEX_FIELDS.map((f) => field_weights[f]);
	const fusionOn = embeddings !== null && retrieval.fusion.method === 'rrf';
	const depth = retrieval.fusion.depth ?? DEFAULT_FUSION_DEPTH;
	const diversify = (retrieval.diversify ?? 'none') === 'page';

	const df = (t: string): number => terms.get(t)?.[0] ?? 0;
	const idf = (t: string): number => {
		const d = df(t);
		return Math.log(1 + (n - d + 0.5) / (d + 0.5));
	};
	const stems = (query: string): string[] => [...analyzer.stems(query)].sort(byCodePoint);

	/** The sections with a positive BM25F score, best first, ties by section id, before diversification. */
	function lexicalRanked(query: string): Hit[] {
		const scores = new Map<number, number>();
		for (const t of stems(query)) {
			const posting = terms.get(t);
			if (!posting) continue;
			const w = idf(t);
			const tfp = new Map<number, number>();
			for (let i = 1; i < posting.length; i++) {
				const [doc, field, tf] = posting[i] as [number, number, number];
				const avg = index.avg_length[field] || 1.0;
				tfp.set(
					doc,
					(tfp.get(doc) ?? 0) +
						(weights[field] * tf) / (1 - b + (b * index.lengths[doc][field]) / avg),
				);
			}
			for (const [doc, x] of tfp)
				scores.set(doc, (scores.get(doc) ?? 0) + (w * x * (k1 + 1)) / (x + k1));
		}
		return [...scores.entries()]
			.filter(([, s]) => s > 0)
			.map(([i, score]) => ({ index: i, id: sectionIds[i], score }))
			.sort((x, y) => y.score - x.score || byCodePoint(x.id, y.id));
	}

	const finish = (ranked: Hit[]): Hit[] =>
		(diversify ? pagesFirst(ranked) : ranked).slice(0, retrieval.candidates);

	function search(query: string): Hit[] {
		const lex = lexicalRanked(query);
		if (!fusionOn || embeddings === null) return finish(lex);
		// 6.5 Fusion: both lists cut to `depth`, fused, then diversified and cut to `candidates`.
		const emb = embeddings.rank(stems(query), depth);
		return finish(fuseRrf(lex.slice(0, depth), emb, sectionIds, retrieval.fusion));
	}

	function decide(query: string, given?: Hit[]): Decision {
		const q = stems(query);
		const hits = given ?? search(query);
		const top = hits[0];
		const unknown = Math.log(1 + (n + 0.5) / 0.5);
		const total = q.reduce((s, t) => s + (terms.has(t) ? idf(t) : unknown), 0);
		let coverage = 0;
		if (top && total > 0) {
			let found = 0;
			for (const t of q) {
				const posting = terms.get(t);
				if (
					posting &&
					posting.slice(1).some((p) => (p as [number, number, number])[0] === top.index)
				)
					found += idf(t);
			}
			coverage = found / total;
		}
		const cosine =
			fusionOn && embeddings !== null && top ? embeddings.cosine(q, top.index) : null;
		const inScope = judgeInScope(fusionOn, coverage, cosine, q.length, retrieval.out_of_scope);
		return { inScope, coverage, cosine, queryTerms: q.length, topId: top?.id ?? null };
	}

	return { stems, search, decide };
}

/**
 * A searcher over a loaded pack: the analyzer is the caller's, built with the stopwords of the pack's language. `table` is the
 * pack's embedding table after the checks of manual.md 6.7.5; without one the searcher is lexical only.
 */
export function searcherForPack(
	pack: HelpPack,
	analyzer: Analyzer,
	table?: EmbeddingTable | null,
): Searcher {
	const retrieval = effectiveRetrieval(pack);
	const sectionIds = pack.sections.map((s) => s.id);
	return createSearcher({
		index: pack.index,
		sectionIds,
		retrieval,
		analyzer,
		embeddings: table
			? createEmbeddingScorer(table, pack.index, sectionIds, retrieval.bm25.field_weights)
			: null,
	});
}
