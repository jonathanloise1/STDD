/** Locations of the template's own files, independent of the `--root` being built. */
import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseStopwords } from '../../src/help/lexical/analyzer.ts';
import type { HelpLang } from '../../src/help/lexical/types.ts';
import { isFile, sha256Hex } from '../lib/index.ts';

const HERE = dirname(fileURLToPath(import.meta.url));

/** The repository that contains these scripts (src/frontend/scripts/help is four levels below). */
export const TEMPLATE_ROOT = resolve(HERE, '../../../..');

export const LEXICAL_DIR = resolve(HERE, '../../src/help/lexical');

export function stopwordsFile(lang: HelpLang): string {
	return join(LEXICAL_DIR, `stopwords.${lang}.txt`);
}

export interface LoadedStopwords {
	words: Set<string>;
	/** SHA-256 of the file bytes. */
	sha256: string;
}

/** Stopword list shipped with the template (manual.md 6.4) and the SHA-256 of its bytes. */
export function loadTemplateStopwords(lang: HelpLang): LoadedStopwords {
	const file = stopwordsFile(lang);
	if (!isFile(file)) throw new Error(`stopword list missing: ${file}`);
	const bytes = readFileSync(file);
	return { words: parseStopwords(bytes.toString('utf8')), sha256: sha256Hex(bytes) };
}
