/** The period of the unanswered-questions download and the file it produces. Pure helpers. */

/** The bounds an empty date field stands for. */
export const EARLIEST_DAY = '1970-01-01';
export const LATEST_DAY = '2999-12-31';

export interface Period {
	from: string;
	to: string;
}

/** Dates are `yyyy-MM-dd`; an empty field means no bound on that side. */
export function resolvePeriod(from: string, to: string): Period {
	return { from: from || EARLIEST_DAY, to: to || LATEST_DAY };
}

/** True when the period ends before it starts; ISO dates compare as text. */
export function periodError(period: Period): boolean {
	return period.to < period.from;
}

export function gapFileName(period: Period): string {
	return `unanswered-questions_${period.from}_${period.to}.json`;
}

/** Hands a JSON array to the browser as a file download. */
export function downloadJson(fileName: string, data: unknown): void {
	const url = URL.createObjectURL(
		new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }),
	);
	const link = document.createElement('a');
	link.href = url;
	link.download = fileName;
	document.body.appendChild(link);
	link.click();
	link.remove();
	URL.revokeObjectURL(url);
}
