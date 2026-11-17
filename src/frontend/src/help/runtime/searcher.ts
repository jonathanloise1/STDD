/** Retrieval over a loaded pack: the analyzer of the language, results for search and cards for the assistant. */
import { createAnalyzer, parseStopwords, searcherForPack } from '../index.ts';
import type {
	Analyzer,
	Decision,
	EmbeddingTable,
	HelpLang,
	HelpPack,
	PackSection,
	Searcher,
} from '../index.ts';
import stopwordsEn from '../lexical/stopwords.en.txt?raw';
import stopwordsIt from '../lexical/stopwords.it.txt?raw';

/** Search results show at most this many sections (manual.md 5.3). */
export const MAX_SEARCH_RESULTS = 10;
/** The assistant shows at most this many cards (manual.md 7.2). */
export const MAX_CARDS = 3;
/** Characters of a section shown in a result or a card (manual.md 5.3, 7.2). */
export const SNIPPET_CHARS = 160;

const STOPWORDS: Record<HelpLang, string> = { en: stopwordsEn, it: stopwordsIt };

export interface PackSearcher {
	pack: HelpPack;
	analyzer: Analyzer;
	searcher: Searcher;
	/** Sections by id. */
	sections: Map<string, PackSection>;
	/** Page title by page id, from the overview section of each page. */
	pageTitles: Map<string, string>;
}

const searchers = new WeakMap<HelpPack, { table: EmbeddingTable | null; ps: PackSearcher }>();

/**
 * The searcher of a pack, with its embedding table when it has a checked one (manual.md 6.7.5) and lexical only otherwise. The
 * section vectors are computed once per pack and table, not per question.
 */
export function createPackSearcher(
	pack: HelpPack,
	table: EmbeddingTable | null = null,
): PackSearcher {
	const cached = searchers.get(pack);
	if (cached && cached.table === table) return cached.ps;
	const analyzer = createAnalyzer(pack.lang, parseStopwords(STOPWORDS[pack.lang]));
	const sections = new Map(pack.sections.map((s) => [s.id, s]));
	const pageTitles = new Map<string, string>();
	for (const s of pack.sections)
		if (s.key === 'overview' && !pageTitles.has(s.page_id)) pageTitles.set(s.page_id, s.title);
	const ps = {
		pack,
		analyzer,
		searcher: searcherForPack(pack, analyzer, table),
		sections,
		pageTitles,
	};
	searchers.set(pack, { table, ps });
	return ps;
}

/** The first `limit` characters of a text on one line. */
export function snippetOf(text: string, limit = SNIPPET_CHARS): string {
	const flat = text.replace(/\s*\n\s*/g, ' ').trim();
	const chars = [...flat];
	return chars.length <= limit ? flat : chars.slice(0, limit).join('').trimEnd();
}

export interface Segment {
	text: string;
	bold: boolean;
}

const WORD = /[\p{L}\p{N}]+/gu;

/** Splits a text so that words whose stem matches a query stem are marked bold (manual.md 5.3). */
export function highlight(
	text: string,
	queryStems: ReadonlySet<string>,
	analyzer: Analyzer,
): Segment[] {
	const out: Segment[] = [];
	let last = 0;
	const push = (t: string, bold: boolean) => {
		if (!t) return;
		const prev = out[out.length - 1];
		if (prev && prev.bold === bold) prev.text += t;
		else out.push({ text: t, bold });
	};
	for (const m of text.matchAll(WORD)) {
		const start = m.index ?? 0;
		push(text.slice(last, start), false);
		const stems = analyzer.analyze(m[0]);
		push(
			m[0],
			stems.some((s) => queryStems.has(s)),
		);
		last = start + m[0].length;
	}
	push(text.slice(last), false);
	return out;
}

/** The heading of a section: the part of its title after the page title; the page title for an overview. */
export function sectionHeading(section: PackSection, pageTitle: string | undefined): string | null {
	if (section.key === 'overview') return null;
	const prefix = `${pageTitle ?? ''} - `;
	return pageTitle && section.title.startsWith(prefix)
		? section.title.slice(prefix.length)
		: section.title;
}

export interface SectionView {
	id: string;
	pageId: string;
	pageTitle: string;
	/** Null for the introduction of a page. */
	heading: string | null;
	snippet: string;
	url: string;
}

export function viewOf(ps: PackSearcher, section: PackSection): SectionView {
	const pageTitle = ps.pageTitles.get(section.page_id) ?? section.title;
	return {
		id: section.id,
		pageId: section.page_id,
		pageTitle,
		heading: sectionHeading(section, pageTitle),
		snippet: snippetOf(section.text),
		url: section.url,
	};
}

export interface SearchGroup {
	pageId: string;
	pageTitle: string;
	url: string;
	results: SectionView[];
}

/**
 * Search results (manual.md 5.3): up to 10 sections, grouped by page in the rank order of each page's best
 * section, without the out-of-scope decision.
 */
export function searchManual(ps: PackSearcher, query: string): SearchGroup[] {
	const hits = ps.searcher.search(query).slice(0, MAX_SEARCH_RESULTS);
	const groups = new Map<string, SearchGroup>();
	for (const hit of hits) {
		const section = ps.sections.get(hit.id);
		if (!section) continue;
		const view = viewOf(ps, section);
		let g = groups.get(view.pageId);
		if (!g) {
			g = {
				pageId: view.pageId,
				pageTitle: view.pageTitle,
				url: view.url.replace(/#.*$/, ''),
				results: [],
			};
			groups.set(view.pageId, g);
		}
		g.results.push(view);
	}
	return [...groups.values()];
}

export interface Retrieval {
	/** The best sections in rank order (up to the candidates of the pack). */
	ranked: PackSection[];
	decision: Decision;
}

/** Retrieval for the assistant: the ranked sections and the out-of-scope decision (manual.md 6.6). */
export function retrieve(ps: PackSearcher, question: string): Retrieval {
	const hits = ps.searcher.search(question);
	const decision = ps.searcher.decide(question, hits);
	const ranked = hits.map((h) => ps.sections.get(h.id)).filter((s): s is PackSection => !!s);
	return { ranked, decision };
}
