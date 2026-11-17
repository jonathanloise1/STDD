/**
 * The question split of manual.md 6.2: the single implementation. Bench questions never enter the help pack;
 * Python code reads bench items from `help:bench -- --export`, never recomputes them.
 */
import { sha256Utf8 } from './sha256.ts';

/** Unicode NFC, trim, collapse whitespace to one space. */
export function normalizeQuestion(q: string): string {
	return q.normalize('NFC').trim().replace(/\s+/g, ' ');
}

export interface QuestionSplit {
	bench: string[];
	indexed: string[];
}

/** Key of a question: `sha256("{story id}\n{lang}\n{normalised question}")` as lower-case hex. */
export function questionKey(storyId: string, lang: string, normalised: string): string {
	return sha256Utf8(`${storyId}\n${lang}\n${normalised}`);
}

/**
 * Bench and indexed questions of one story and language: sorted by key ascending, the first floor(n / 2) are
 * bench questions. Returned texts are normalised, in key order.
 */
export function splitQuestions(storyId: string, lang: string, questions: string[]): QuestionSplit {
	const keyed = questions.map((q) => {
		const text = normalizeQuestion(q);
		return { text, key: questionKey(storyId, lang, text) };
	});
	keyed.sort((a, b) =>
		a.key < b.key ? -1 : a.key > b.key ? 1 : a.text < b.text ? -1 : a.text > b.text ? 1 : 0,
	);
	const nb = Math.floor(keyed.length / 2);
	return {
		bench: keyed.slice(0, nb).map((k) => k.text),
		indexed: keyed.slice(nb).map((k) => k.text),
	};
}
