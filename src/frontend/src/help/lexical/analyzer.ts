/**
 * The analyzer of manual.md 6.4: one implementation for the build, the bench and the browser.
 * `training/assistant/lexical.py` is its Python port; both are tested against `scripts/help/fixtures/lexical.golden.json`.
 */
import { createStemmer } from './stemmers.ts';
import type { HelpLang } from './types.ts';

/** Changes with any change to the analyzer steps or to the stopword lists (manual.md 6.4). */
export const ANALYZER_VERSION = 2;

export function analyzerId(lang: HelpLang): string {
	return `snowball-${lang}`;
}

/** Parses a stopword file: one word per line, trimmed, NFC, lower case; blank lines ignored. */
export function parseStopwords(text: string): Set<string> {
	return new Set(
		text
			.split('\n')
			.map((l) => l.trim().normalize('NFC').toLowerCase())
			.filter(Boolean),
	);
}

const NON_ALNUM = /[^\p{L}\p{N}]+/u;
const COMBINING = /\p{M}/gu;

/** Remove diacritics from a stem (step 5): NFD, drop combining marks. */
export function stripDiacritics(stem: string): string {
	return stem.normalize('NFD').replace(COMBINING, '');
}

/** Steps 1 to 3: NFC, lower case, split on non letters or digits, drop one-character tokens and stopwords. */
export function tokenize(
	text: string,
	stopwords: ReadonlySet<string>,
	keepStopwords = false,
): string[] {
	const raw = text.normalize('NFC').toLowerCase().split(NON_ALNUM);
	return raw.filter((t) => [...t].length > 1 && (keepStopwords || !stopwords.has(t)));
}

export interface Analyzer {
	lang: HelpLang;
	id: string;
	version: number;
	/** The stems of a text, with repetitions, in order. */
	analyze(text: string): string[];
	/** The distinct stems of a text. */
	stems(text: string): Set<string>;
}

export function createAnalyzer(lang: HelpLang, stopwords: ReadonlySet<string>): Analyzer {
	const stem = createStemmer(lang);
	const cache = new Map<string, string>();
	const stemOf = (token: string): string => {
		let s = cache.get(token);
		if (s === undefined) {
			s = stripDiacritics(stem(token));
			cache.set(token, s);
		}
		return s;
	};
	const analyze = (text: string): string[] => tokenize(text, stopwords).map(stemOf);
	return {
		lang,
		id: analyzerId(lang),
		version: ANALYZER_VERSION,
		analyze,
		stems: (text) => new Set(analyze(text)),
	};
}
