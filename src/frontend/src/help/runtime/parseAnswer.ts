/** Parsing of a model output (models.md 3.3, 3.4) and the text the panel may show while it streams. */

export interface AnswerLimits {
	/** Number of sections in the context. */
	sources: number;
	maxBodyChars: number;
	maxBodyLines: number;
	refusal: string;
}

export type ParsedAnswer =
	| { kind: 'answered'; body: string; cited: number[] }
	| { kind: 'refused' }
	| { kind: 'invalid' };

const SOURCE_LINE = /^Source: (\[[1-9]\](?:, \[[1-9]\])*)$/;

/** 3.4: refusal, answer with a valid source line, or invalid (the panel treats invalid like a refusal). */
export function parseAnswer(raw: string, limits: AnswerLimits): ParsedAnswer {
	const output = raw.replace(/\r\n/g, '\n').trimEnd();
	if (output === limits.refusal) return { kind: 'refused' };
	// The refusal sentence followed by anything else is not a refusal and not an answer (3.4 step 4)
	if (limits.refusal !== '' && output.startsWith(limits.refusal)) return { kind: 'invalid' };
	const lines = output.split('\n');
	const last = lines.pop() ?? '';
	const match = SOURCE_LINE.exec(last);
	if (!match) return { kind: 'invalid' };
	const cited = [...match[1].matchAll(/\d/g)].map((m) => Number(m[0]));
	const ascending = cited.every((n, i) => i === 0 || n > cited[i - 1]);
	if (!ascending || cited.some((n) => n > limits.sources)) return { kind: 'invalid' };
	const body = lines.join('\n');
	if (
		lines.length < 1 ||
		lines.length > limits.maxBodyLines ||
		[...body].length > limits.maxBodyChars ||
		lines.some((l) => l.startsWith('Source:')) ||
		body.trim() === ''
	)
		return { kind: 'invalid' };
	return { kind: 'answered', body, cited };
}

/**
 * What of a partial output may be shown while the answer is written: never the source line (it becomes chips),
 * never a start of the refusal sentence (it is never shown).
 */
export function streamingText(partial: string, refusal: string): string {
	const text = partial.replace(/\r\n/g, '\n');
	if (refusal.startsWith(text.trimEnd())) return '';
	const lines = text.split('\n');
	const last = lines[lines.length - 1];
	if (last !== '' && ('Source: '.startsWith(last) || last.startsWith('Source:'))) lines.pop();
	return lines.join('\n').trimEnd();
}
