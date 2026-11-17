/** Pipe-table row helpers of the Markdown subset (manual.md 2.5). Pure, framework-free. */

/** Splits a pipe-table row into trimmed cells; `\|` is a literal pipe. */
export function splitRow(line: string): string[] {
	let s = line.trim();
	if (s.startsWith('|')) s = s.slice(1);
	if (s.endsWith('|') && !s.endsWith('\\|')) s = s.slice(0, -1);
	const cells: string[] = [];
	let cur = '';
	for (let i = 0; i < s.length; i++) {
		const c = s[i];
		if (c === '\\' && s[i + 1] === '|') {
			cur += '|';
			i++;
		} else if (c === '|') {
			cells.push(cur.trim());
			cur = '';
		} else cur += c;
	}
	cells.push(cur.trim());
	return cells;
}

export function isSeparatorRow(line: string): boolean {
	return /^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$/.test(line.trim()) && line.includes('-');
}
