/** Pack file I/O: serialisation with the exact key order of manual.md 6.3 and loading with the analyzer check. */
import { readFileSync } from 'node:fs';
import { ANALYZER_VERSION, createAnalyzer } from '../../src/help/lexical/analyzer.ts';
import type { Analyzer } from '../../src/help/lexical/analyzer.ts';
import type { EmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import { searcherForPack } from '../../src/help/lexical/ranking.ts';
import type { Searcher } from '../../src/help/lexical/ranking.ts';
import type { HelpLang, HelpPack } from '../../src/help/lexical/types.ts';
import { loadTableForPack } from './embeddings.ts';
import { loadTemplateStopwords } from './paths.ts';

/** Highest `help-pack` schema version this code reads. */
export const PACK_SCHEMA_VERSION = 2;

const byCodePoint = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/**
 * Compact JSON, keys in the order of the object, except `index.terms` whose keys are written in code point order
 * (a plain object would put integer-like stems first).
 */
export function serializePack(pack: HelpPack): string {
	const marker = '__TERMS__';
	const json = JSON.stringify({ ...pack, index: { ...pack.index, terms: marker } });
	const terms = Object.keys(pack.index.terms)
		.sort(byCodePoint)
		.map((t) => `${JSON.stringify(t)}:${JSON.stringify(pack.index.terms[t])}`)
		.join(',');
	return json.replace(`"${marker}"`, () => `{${terms}}`);
}

export interface LoadedPack {
	pack: HelpPack;
	analyzer: Analyzer;
	searcher: Searcher;
	/** The embedding table the searcher uses; null for a pack without one or whose table failed its checks. */
	table: EmbeddingTable | null;
	/** Why the table is not used although the pack names one (manual.md 6.7.5); null otherwise. */
	tableError: string | null;
}

/**
 * Parses a pack and builds its analyzer; throws when the pack was built with another analyzer or a newer format. `table` is the
 * already checked table of the pack (`loadPackFile` reads it next to the file); without one the searcher is lexical only.
 */
export function loadPack(text: string, table: EmbeddingTable | null = null, tableError: string | null = null): LoadedPack {
	const pack = JSON.parse(text) as HelpPack;
	if (pack.kind !== 'help-pack') throw new Error('not a help pack');
	if (pack.schema_version > PACK_SCHEMA_VERSION) throw new Error(`pack schema_version ${pack.schema_version} is newer than ${PACK_SCHEMA_VERSION}`);
	const lang = pack.lang as HelpLang;
	const sw = loadTemplateStopwords(lang);
	if (pack.analyzer.version !== ANALYZER_VERSION || pack.analyzer.stopwords_sha256 !== sw.sha256) {
		throw new Error(`pack ${lang} was built with another analyzer or stopword list: rebuild it with help:build`);
	}
	const analyzer = createAnalyzer(lang, sw.words);
	return { pack, analyzer, searcher: searcherForPack(pack, analyzer, table), table, tableError };
}

/** A pack file with the table that sits next to it (the block's `file`); a table that fails a check of 6.7.5 leaves it lexical only. */
export function loadPackFile(file: string): LoadedPack {
	const text = readFileSync(file, 'utf8');
	const pack = JSON.parse(text) as HelpPack;
	const { table, error } = loadTableForPack(file, pack);
	return loadPack(text, table, error);
}
