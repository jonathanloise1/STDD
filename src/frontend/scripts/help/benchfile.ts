/** Reads `docs/manual/bench.md` (manual.md 8.2). The format itself is checked by manual:check M16. */
import { join } from 'node:path';
import { isFile, parseTable, readText, splitLines, stripQuotes } from '../lib/index.ts';
import { normalizeQuestion } from '../../src/help/bench/split.ts';
import type { HelpLang } from '../../src/help/lexical/types.ts';

export interface ExtraQuestion {
	question: string;
	lang: HelpLang;
	story: string;
}

export interface BenchFile {
	present: boolean;
	oos: Record<HelpLang, string[]>;
	extra: ExtraQuestion[];
}

export function parseBench(text: string): BenchFile {
	const lines = splitLines(text);
	const out: BenchFile = { present: true, oos: { en: [], it: [] }, extra: [] };
	let section = '';
	let lang: HelpLang | null = null;
	const tableLines: string[] = [];
	for (const l of lines) {
		if (/^## /.test(l)) {
			section = l.slice(3).trim();
			lang = null;
		} else if (/^### /.test(l)) {
			const v = l.slice(4).trim();
			lang = v === 'en' || v === 'it' ? v : null;
		} else if (section === 'Out of scope' && lang) {
			const m = /^\d+\.\s+(\S.*)$/.exec(l);
			if (m) out.oos[lang].push(normalizeQuestion(m[1]));
		} else if (section === 'Extra' && l.trim().startsWith('|')) tableLines.push(l);
	}
	const t = parseTable(tableLines);
	for (const r of t?.rows ?? []) {
		const l = r[1];
		if (l === 'en' || l === 'it') out.extra.push({ question: normalizeQuestion(stripQuotes(r[0] ?? '')), lang: l, story: r[2] ?? '' });
	}
	return out;
}

export function loadBench(root: string): BenchFile {
	const file = join(root, 'docs/manual/bench.md');
	if (!isFile(file)) return { present: false, oos: { en: [], it: [] }, extra: [] };
	return parseBench(readText(file));
}
