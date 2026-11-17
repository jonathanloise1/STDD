/**
 * Reciprocal rank fusion of the lexical list and the embedding list (manual.md 6.5), and the interface of the embedding side
 * (6.7.6). The vectors themselves are in `embeddings.ts`; ranking, decision and fusion never depend on how a scorer is made.
 */

export interface EmbeddingMatch {
	/** Section index. */
	index: number;
	cosine: number;
}

/** Query and section vectors of a pack that uses a table (manual.md 6.7.6). `stems` are the distinct query stems. */
export interface EmbeddingScorer {
	/** The vector of a section; null when none of its stems is in the table. */
	sectionVector(sectionIndex: number): Float64Array | null;
	/** The query vector; null when no query stem is in the table. */
	queryVector(stems: readonly string[]): Float64Array | null;
	/** The embedding list (6.5 step 2): cosine greater than 0, best first, ties by section id, cut to `depth`. */
	rank(stems: readonly string[], depth: number): EmbeddingMatch[];
	/** Cosine to one section: null without a query vector, 0 when the section has no vector (6.6). */
	cosine(stems: readonly string[], sectionIndex: number): number | null;
	/** Cosine to every section (null for a section without a vector); null without a query vector. */
	cosines(stems: readonly string[]): (number | null)[] | null;
}

export interface Scored {
	index: number;
	id: string;
	score: number;
}

/**
 * 6.5 step 3: the fused score of a section is `1/(rrf_k + rank_lex) + embedding_weight/(rrf_k + rank_emb)`, ranks from 1; a
 * section absent from a list contributes 0 for that list. Ordered by fused score, ties by section id in code point order.
 * Both lists arrive already cut to `fusion.depth`.
 */
export function fuseRrf(
	lexical: readonly Scored[],
	embedded: readonly EmbeddingMatch[],
	ids: readonly string[],
	fusion: { rrf_k: number; embedding_weight: number },
): Scored[] {
	const score = new Map<number, number>();
	lexical.forEach((h, i) =>
		score.set(h.index, (score.get(h.index) ?? 0) + 1 / (fusion.rrf_k + i + 1)),
	);
	embedded.forEach((h, i) =>
		score.set(
			h.index,
			(score.get(h.index) ?? 0) + fusion.embedding_weight / (fusion.rrf_k + i + 1),
		),
	);
	return [...score.entries()]
		.map(([index, s]) => ({ index, id: ids[index], score: s }))
		.sort((a, b) => b.score - a.score || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}
