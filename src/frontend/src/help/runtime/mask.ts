/** Masking of a question before it leaves the browser as a gap record (manual.md 7.4). */

export const MAX_QUESTION_CHARS = 300;

const EMAIL = /[^\s@<>()[\],;:"']+@[^\s@<>()[\],;:"']+\.[^\s@<>()[\],;:"']+/g;
const URL_PATTERN = /(?:https?:\/\/|www\.)[^\s]*[^\s.,;:!?)\]"']/gi;
const DIGIT_RUN = /\d(?:[ .-]*\d){3,}/g;

/** E-mail addresses become `[email]`, URLs `[url]`, runs of four or more digits `[number]`; at most 300 characters. */
export function maskQuestion(question: string): string {
	const masked = question
		.replace(EMAIL, '[email]')
		.replace(URL_PATTERN, '[url]')
		.replace(DIGIT_RUN, '[number]')
		.replace(/\s+/g, ' ')
		.trim();
	return [...masked].slice(0, MAX_QUESTION_CHARS).join('');
}
