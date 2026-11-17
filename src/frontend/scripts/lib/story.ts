import { isNone, sectionOf, splitSections } from './sections.ts';
import { parseBullets, parseNumbered, parseTable, splitLines, stripQuotes } from './text.ts';

export interface AccessRow {
	role: string;
	allowed: string;
}

export interface ErrorState {
	situation: string;
	/** The exact English message, without the surrounding quotes. */
	message: string;
	action: string;
}

export interface Story {
	id: string;
	title: string;
	/** Repository-relative path of the story file. */
	path: string;
	area: string;
	priority: string;
	feature: string;
	/** `true` for `Yes`, `false` for `No`, null when absent or invalid. */
	userInput: boolean | null;
	asA: string;
	iWant: string;
	soThat: string;
	criteria: string[];
	access: AccessRow[];
	rules: string[];
	errorStates: ErrorState[];
	nonGoals: string[];
	questions: { en: string[]; it: string[] };
	lineCount: number;
}

export function parseFields(lines: string[]): Record<string, string> {
	const t = parseTable(lines);
	const out: Record<string, string> = {};
	if (t) for (const r of t.rows) if (r.length >= 2) out[r[0]] = r[1];
	return out;
}

function listOrNone(lines: string[] | undefined, parse: (l: string[]) => string[]): string[] {
	if (!lines || isNone(lines)) return [];
	return parse(lines);
}

/** Parses a user story file (development.md 6). Tolerant: missing parts come back empty. */
export function parseStory(text: string, path: string, area: string): Story {
	const f = splitSections(text);
	const m = /^(US-[A-Z]{2,8}-\d{2}):\s*(.*)$/.exec(f.title ?? '');
	const fields = parseFields(f.preamble);
	const storyLines = sectionOf(f, 'Story')?.lines ?? [];
	const grab = (re: RegExp) => {
		for (const l of storyLines) {
			const x = re.exec(l.trim());
			if (x) return x[1].trim();
		}
		return '';
	};
	const accessTable = parseTable(sectionOf(f, 'Access Control')?.lines ?? []);
	const errLines = sectionOf(f, 'Error States')?.lines;
	const errTable = errLines && !isNone(errLines) ? parseTable(errLines) : null;
	const q = sectionOf(f, 'Questions users ask')?.lines ?? [];
	const questions = { en: [] as string[], it: [] as string[] };
	let cur: 'en' | 'it' | null = null;
	for (const l of q) {
		const h = /^### (en|it)\s*$/.exec(l);
		if (h) cur = h[1] as 'en' | 'it';
		else if (cur) {
			const x = /^\d+\.\s+(.*)$/.exec(l.trim());
			if (x) questions[cur].push(x[1].trim());
		}
	}
	const ui = fields['User input'];
	return {
		id: m ? m[1] : '',
		title: m ? m[2].trim() : (f.title ?? ''),
		path,
		area,
		priority: fields['Priority'] ?? '',
		feature: fields['Feature'] ?? '',
		userInput: ui === 'Yes' ? true : ui === 'No' ? false : null,
		asA: grab(/^\*\*As an?\*\*\s*(.*)$/),
		iWant: grab(/^\*\*I want\*\*\s*(.*)$/),
		soThat: grab(/^\*\*So that\*\*\s*(.*)$/),
		criteria: parseNumbered(sectionOf(f, 'Acceptance Criteria')?.lines ?? []),
		access: (accessTable?.rows ?? []).map((r) => ({ role: r[0] ?? '', allowed: r[1] ?? '' })),
		rules: listOrNone(sectionOf(f, 'Business Rules')?.lines, parseNumbered),
		errorStates: (errTable?.rows ?? []).map((r) => ({
			situation: r[0] ?? '',
			message: stripQuotes(r[1] ?? ''),
			action: r[2] ?? '',
		})),
		nonGoals: listOrNone(sectionOf(f, 'Non-Goals')?.lines, parseBullets),
		questions,
		lineCount: splitLines(text.replace(/\n$/, '')).length,
	};
}
