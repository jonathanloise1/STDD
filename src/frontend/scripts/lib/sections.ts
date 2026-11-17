import { splitLines } from './text.ts';

export interface Section {
	heading: string;
	/** 1-based line number of the heading. */
	line: number;
	/** Lines below the heading, up to the next H2, with blank edges removed. */
	lines: string[];
}

export interface MdFile {
	title: string | null;
	titleLine: number;
	/** Lines between the H1 and the first H2. */
	preamble: string[];
	sections: Section[];
	lineCount: number;
}

function trimBlank(lines: string[]): string[] {
	let a = 0;
	let b = lines.length;
	while (a < b && lines[a].trim() === '') a++;
	while (b > a && lines[b - 1].trim() === '') b--;
	return lines.slice(a, b);
}

/** Splits a story, test case or index file into its H1 and its H2 sections. */
export function splitSections(text: string): MdFile {
	const lines = splitLines(text);
	const file: MdFile = { title: null, titleLine: 0, preamble: [], sections: [], lineCount: lines.length };
	type Cur = { heading: string; line: number; lines: string[] };
	let cur = null as Cur | null;
	const pre: string[] = [];
	lines.forEach((l, i) => {
		const h1 = /^# (.*)$/.exec(l);
		const h2 = /^## (.*)$/.exec(l);
		if (h1 && file.title === null && !cur) {
			file.title = h1[1].trim();
			file.titleLine = i + 1;
		} else if (h2) {
			if (cur) file.sections.push({ heading: cur.heading, line: cur.line, lines: trimBlank(cur.lines) });
			else file.preamble = trimBlank(pre);
			cur = { heading: h2[1].trim(), line: i + 1, lines: [] };
		} else if (cur) cur.lines.push(l);
		else pre.push(l);
	});
	if (cur) file.sections.push({ heading: cur.heading, line: cur.line, lines: trimBlank(cur.lines) });
	else file.preamble = trimBlank(pre);
	return file;
}

export function sectionOf(file: MdFile, heading: string): Section | undefined {
	return file.sections.find((s) => s.heading === heading);
}

export function isNone(lines: string[]): boolean {
	return lines.length === 1 && lines[0].trim() === 'None.';
}
