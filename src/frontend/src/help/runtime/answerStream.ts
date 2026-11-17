/**
 * The answer stream of `POST /api/help/answer` (models.md 7.4): one JSON object per line, `delta` events with the
 * text as it is written, then one `done` (outcome and cited positions) or one `error` (reason `timeout` or
 * `unavailable`). Pure: the service feeds it the text received so far.
 */

/** Why the server tier gave no answer; each of them moves the ladder to the next tier (models.md 7.1). */
export type AnswerFailure =
	| 'invalid_request' // 400
	| 'pack_changed' // 409
	| 'busy' // 503
	| 'unavailable' // 503 or an `unavailable` error event
	| 'timeout'; // 504 or a `timeout` error event

export class HelpAnswerError extends Error {
	constructor(
		readonly failure: AnswerFailure,
		readonly status: number | null = null,
	) {
		super(`help answer failed: ${failure}`);
		this.name = 'HelpAnswerError';
	}
}

/** The failure for the status of a response that did not start the stream. */
export function failureOfStatus(status: number | undefined): AnswerFailure {
	if (status === 400) return 'invalid_request';
	if (status === 409) return 'pack_changed';
	if (status === 504) return 'timeout';
	return 'unavailable';
}

export type AnswerEvent =
	| { type: 'delta'; text: string }
	| {
			type: 'done';
			outcome: 'answered' | 'refused' | 'invalid';
			cited: number[];
			body: string | null;
	  }
	| { type: 'error'; reason: 'timeout' | 'unavailable' };

/** Reads one line; anything that is not a known event is a broken stream. */
export function parseLine(line: string): AnswerEvent {
	let raw: unknown;
	try {
		raw = JSON.parse(line);
	} catch {
		throw new HelpAnswerError('unavailable');
	}
	const bag = (raw ?? {}) as Record<string, unknown>;
	if (bag.type === 'delta' && typeof bag.text === 'string')
		return { type: 'delta', text: bag.text };
	if (bag.type === 'done') {
		const outcome = bag.outcome;
		if (outcome !== 'answered' && outcome !== 'refused' && outcome !== 'invalid')
			throw new HelpAnswerError('unavailable');
		return {
			type: 'done',
			outcome,
			cited: Array.isArray(bag.cited)
				? bag.cited.filter((n): n is number => typeof n === 'number')
				: [],
			body: typeof bag.body === 'string' ? bag.body : null,
		};
	}
	if (bag.type === 'error')
		return { type: 'error', reason: bag.reason === 'timeout' ? 'timeout' : 'unavailable' };
	throw new HelpAnswerError('unavailable');
}

/**
 * Accumulates the events. `feed` takes the whole text received so far (the XHR `responseText`), so a line is read
 * once it is complete; `finish` reads a last line without a line feed and returns the raw model output.
 */
export function createAnswerStream(refusal: string, onText?: (partial: string) => void) {
	let offset = 0;
	let text = '';
	let done: Extract<AnswerEvent, { type: 'done' }> | null = null;

	const handle = (line: string) => {
		if (line.trim() === '') return;
		const event = parseLine(line);
		if (event.type === 'error') throw new HelpAnswerError(event.reason);
		if (event.type === 'delta') {
			text += event.text;
			onText?.(text);
		} else done = event;
	};

	const consume = (received: string, complete: boolean) => {
		let end = received.lastIndexOf('\n') + 1;
		if (complete) end = received.length;
		const chunk = received.slice(offset, end);
		offset = end;
		const lines = chunk.split('\n');
		// A trailing piece without a line feed is only read when the response is complete
		for (const line of lines) handle(line);
	};

	return {
		feed: (received: string) => consume(received, false),
		finish(received: string): string {
			consume(received, true);
			if (!done) throw new HelpAnswerError('unavailable');
			return rawOf(done, text, refusal);
		},
	};
}

/** The raw model output the client parses again (models.md 3.4): the deltas, or the parts of `done` without them. */
function rawOf(
	done: Extract<AnswerEvent, { type: 'done' }>,
	text: string,
	refusal: string,
): string {
	if (text !== '') return text;
	if (done.outcome === 'refused') return refusal;
	if (done.outcome === 'answered' && done.body !== null)
		return `${done.body}\nSource: ${done.cited.map((n) => `[${n}]`).join(', ')}`;
	return '';
}
