/** Reads `docs/manual/glossary.md` (manual.md 6.9): per language, a table of terms and the words users say for them. */
import { join } from 'node:path';
import { isFile, parseTable, readText, splitLines } from '../lib/index.ts';
import type { GlossaryRow } from '../../src/help/lexical/glossary.ts';
import type { HelpLang } from '../../src/help/lexical/types.ts';

export const GLOSSARY_PATH = 'docs/manual/glossary.md';

export interface GlossaryFile {
	present: boolean;
	rows: Record<HelpLang, GlossaryRow[]>;
	/** Format problems, with the line they were found on. */
	errors: string[];
}

export function parseGlossary(text: string): GlossaryFile {
	const out: GlossaryFile = { present: true, rows: { en: [], it: [] }, errors: [] };
	const tables: Record<HelpLang, string[]> = { en: [], it: [] };
	let lang: HelpLang | null = null;
	for (const l of splitLines(text)) {
		if (/^## /.test(l)) {
			const v = l.slice(3).trim();
			lang = v === 'en' || v === 'it' ? v : null;
			if (!lang) out.errors.push(`${GLOSSARY_PATH}: unknown section "${v}" (expected "## en" or "## it")`);
		} else if (lang && l.trim().startsWith('|')) tables[lang].push(l);
	}
	for (const lang of ['en', 'it'] as const) {
		const t = parseTable(tables[lang]);
		if (!t) continue;
		if (t.header[0] !== 'Term' || t.header[1] !== 'Users also say') {
			out.errors.push(`${GLOSSARY_PATH} (${lang}): the table header must be "| Term | Users also say |"`);
			continue;
		}
		for (const r of t.rows) {
			const term = (r[0] ?? '').trim();
			const aliases = (r[1] ?? '')
				.split(',')
				.map((a) => a.trim())
				.filter(Boolean);
			if (!term || !aliases.length) out.errors.push(`${GLOSSARY_PATH} (${lang}): a row needs a term and at least one alias`);
			else out.rows[lang].push({ term: term.normalize('NFC'), aliases: aliases.map((a) => a.normalize('NFC')) });
		}
	}
	return out;
}

export function loadGlossary(root: string): GlossaryFile {
	const file = join(root, GLOSSARY_PATH);
	if (!isFile(file)) return { present: false, rows: { en: [], it: [] }, errors: [] };
	return parseGlossary(readText(file));
}
