import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isFile, readText } from './fsutil.ts';
import type { Lang } from './page.ts';

export interface Stopwords {
	words: Set<string>;
	/** `repo` when read from src/frontend/src/help/lexical/, `builtin` when the fallback list of this library was used. */
	source: 'repo' | 'builtin';
}

const HERE = dirname(fileURLToPath(import.meta.url));

function parseList(text: string): Set<string> {
	return new Set(
		text
			.split('\n')
			.map((l) => l.trim().normalize('NFC').toLowerCase())
			.filter(Boolean),
	);
}

/**
 * Stopword list of a language: `src/frontend/src/help/lexical/stopwords.{lang}.txt` under the root (the list
 * shipped with the template, manual.md 6.4), else the minimal fallback list in `scripts/lib/data/`.
 */
export function loadStopwords(root: string, lang: Lang): Stopwords {
	const repoFile = join(root, 'src/frontend/src/help/lexical', `stopwords.${lang}.txt`);
	if (isFile(repoFile)) return { words: parseList(readText(repoFile)), source: 'repo' };
	return { words: parseList(readText(join(HERE, 'data', `stopwords.${lang}.txt`))), source: 'builtin' };
}
