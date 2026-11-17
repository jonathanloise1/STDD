/** Snowball stemmers of the analyzer (manual.md 6.4 step 4): the vendored generated files, no package. */
import EnglishStemmer from './snowball/english-stemmer.js';
import ItalianStemmer from './snowball/italian-stemmer.js';
import type { HelpLang } from './types.ts';

/** A fresh stemming function of the language: one lower-case token in, its Snowball stem out. */
export function createStemmer(lang: HelpLang): (token: string) => string {
	const s = lang === 'en' ? new EnglishStemmer() : new ItalianStemmer();
	return (token) => s.stemWord(token);
}
