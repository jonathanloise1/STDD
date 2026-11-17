/** Types of the help pack (manual.md 6.3) and of the retrieval parameters (6.5). Framework-free. */

export type HelpLang = 'en' | 'it';
export const HELP_LANGS: readonly HelpLang[] = ['en', 'it'];

/** Fields of the index, in the order of the pack's `index.fields`. */
export const INDEX_FIELDS = ['title', 'text', 'questions'] as const;

export interface PackSection {
	id: string;
	page_id: string;
	key: string;
	area: string | null;
	story: string | null;
	title: string;
	text: string;
	url: string;
	routes: string[];
}

export interface PackQuestion {
	story: string;
	text: string;
}

/** `[df, [section index, field index, tf], ...]`. */
export type Posting = [number, ...Array<[number, number, number]>];

export interface PackIndex {
	fields: string[];
	doc_count: number;
	avg_length: number[];
	lengths: number[][];
	terms: Record<string, Posting>;
}

/** Effective retrieval values of a pack that uses a table (manual.md 6.7.4): they replace the defaults of 6.5. */
export interface EmbeddingsParams {
	method: 'none' | 'rrf';
	depth: number;
	rrf_k: number;
	embedding_weight: number;
	min_cosine: number;
	min_cosine_lexical: number;
}

/** The `embeddings` block of a pack that uses a static embedding table (manual.md 6.7.4); null without one. */
export interface PackEmbeddings {
	file: string;
	sha256: string;
	bytes: number;
	format_version: number;
	dims: number;
	count: number;
	model: { id: string; version: string; hub_id: string; revision: string };
	locked: boolean;
	vocab_coverage: number;
	calibrated: boolean;
	params: EmbeddingsParams;
}

export interface RetrievalParams {
	schema_version: number;
	bm25: {
		k1: number;
		b: number;
		field_weights: { title: number; text: number; questions: number };
	};
	candidates: number;
	/** `page`: the best section of each page before any second section of a page (6.5). Absent means `none`. */
	diversify?: 'none' | 'page';
	/** `min_cosine_lexical` is absent from packs of `help-pack` 1: the veto is then off (-1, manual.md 6.6). */
	out_of_scope: {
		min_coverage: number;
		min_cosine: number;
		min_cosine_lexical?: number;
		min_query_terms: number;
	};
	/** `depth` is absent from packs of `help-pack` 1: 30 (manual.md 6.5). */
	fusion: { method: 'none' | 'rrf'; depth?: number; rrf_k: number; embedding_weight: number };
}

export interface ProtocolContext {
	max_sections: number;
	max_section_chars: number;
	max_context_chars: number;
	max_question_chars: number;
	scan_limit: number;
}

export interface HelpPack {
	schema_version: number;
	kind: 'help-pack';
	lang: HelpLang;
	project: { name: string; version: string };
	contracts: Record<string, number>;
	build: { commit: string | null; inputs_sha256: string };
	assistant: {
		protocol: {
			sha256: string;
			content: { context?: ProtocolContext } & Record<string, unknown>;
		};
		retrieval: { sha256: string; content: RetrievalParams };
		model: {
			id: string;
			version: string;
			protocol_sha256: string;
			retrieval_sha256: string;
		} | null;
		compatible: boolean;
	};
	analyzer: { id: string; version: number; stopwords_sha256: string };
	sections: PackSection[];
	questions: PackQuestion[];
	index: PackIndex;
	embeddings: PackEmbeddings | null;
}
