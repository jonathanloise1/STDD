/** Small helpers for the line-oriented Markdown of stories, test cases and area indexes. */
import { isSeparatorRow, splitRow } from '../../src/help/markdown/tablerow.ts';

export { isSeparatorRow, splitRow };

export interface Table {
	header: string[];
	rows: string[][];
}

export function splitLines(text: string): string[] {
	return text.replace(/\r\n/g, '\n').split('\n');
}

/** First pipe table of a block of lines, or null. */
export function parseTable(lines: string[]): Table | null {
	let i = lines.findIndex((l) => l.trim().startsWith('|'));
	if (i < 0) return null;
	const header = splitRow(lines[i]);
	i++;
	if (i < lines.length && isSeparatorRow(lines[i])) i++;
	const rows: string[][] = [];
	for (; i < lines.length && lines[i].trim().startsWith('|'); i++) rows.push(splitRow(lines[i]));
	return { header, rows };
}

/** Items of the numbered list in a block of lines (`1. text`); `None.` gives an empty list. */
export function parseNumbered(lines: string[]): string[] {
	const out: string[] = [];
	for (const l of lines) {
		const m = /^\d+\.\s+(.*)$/.exec(l.trim());
		if (m) out.push(m[1].trim());
	}
	return out;
}

/** Items of the bullet list in a block of lines (`- text`). */
export function parseBullets(lines: string[]): string[] {
	const out: string[] = [];
	for (const l of lines) {
		const m = /^-\s+(.*)$/.exec(l.trim());
		if (m) out.push(m[1].trim());
	}
	return out;
}

/** Removes one pair of surrounding double quotes when present. */
export function stripQuotes(s: string): string {
	const t = s.trim();
	const m = /^"(.*)"$/.exec(t);
	return m ? m[1] : t;
}

/** Every double-quoted string of a text, without the quotes. */
export function quotedStrings(text: string): string[] {
	const out: string[] = [];
	for (const m of text.matchAll(/"([^"]*)"/g)) out.push(m[1]);
	return out;
}

/** Joins items as "A, B and C". */
export function joinList(items: string[]): string {
	if (items.length <= 1) return items.join('');
	return `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`;
}

export function upperFirst(s: string): string {
	return s.length ? s[0].toUpperCase() + s.slice(1) : s;
}

export function stripTrailingPeriod(s: string): string {
	return s.replace(/\.\s*$/, '');
}

export function ordinalCompare(a: string, b: string): number {
	return a < b ? -1 : a > b ? 1 : 0;
}
