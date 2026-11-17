import { parseFields } from './story.ts';
import { isNone, sectionOf, splitSections } from './sections.ts';
import { parseBullets, parseNumbered, parseTable, stripQuotes } from './text.ts';

export interface TestDataRow {
	field: string;
	/** Value without the surrounding quotes. */
	value: string;
}

export interface ObservableTrace {
	message: string;
	logKey: string;
	severity: string;
	runbook: string;
}

export interface TestCase {
	id: string;
	title: string;
	path: string;
	area: string;
	storyId: string;
	type: string;
	priority: string;
	automatedBy: string;
	description: string;
	preconditions: string[];
	testData: TestDataRow[];
	steps: string[];
	expected: string[];
	trace: ObservableTrace | null;
}

/** Parses a test case file (development.md 8). Tolerant: missing parts come back empty. */
export function parseTestCase(text: string, path: string, area: string): TestCase {
	const f = splitSections(text);
	const m = /^([A-Z]{2,8}-[A-Z]{2,12}-\d{3}):\s*(.*)$/.exec(f.title ?? '');
	const fields = parseFields(f.preamble);
	const dataLines = sectionOf(f, 'Test Data')?.lines ?? [];
	const dataTable = isNone(dataLines) ? null : parseTable(dataLines);
	const traceLines = sectionOf(f, 'Observable trace')?.lines;
	let trace: ObservableTrace | null = null;
	if (traceLines) {
		const t = parseFields(traceLines);
		trace = {
			message: stripQuotes(t['Message'] ?? ''),
			logKey: (t['Log key'] ?? '').replace(/^`|`$/g, ''),
			severity: t['Severity'] ?? '',
			runbook: t['Runbook'] ?? '',
		};
	}
	return {
		id: m ? m[1] : '',
		title: m ? m[2].trim() : (f.title ?? ''),
		path,
		area,
		storyId: fields['User Story'] ?? '',
		type: fields['Type'] ?? '',
		priority: fields['Priority'] ?? '',
		automatedBy: fields['Automated by'] ?? '',
		description: (sectionOf(f, 'Description')?.lines ?? []).join(' ').trim(),
		preconditions: parseBullets(sectionOf(f, 'Preconditions')?.lines ?? []),
		testData: (dataTable?.rows ?? []).map((r) => ({ field: r[0] ?? '', value: stripQuotes(r[1] ?? '') })),
		steps: parseNumbered(sectionOf(f, 'Steps')?.lines ?? []),
		expected: parseBullets(sectionOf(f, 'Expected Results')?.lines ?? []),
		trace,
	};
}
