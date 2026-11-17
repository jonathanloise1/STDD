/** Manual pages: front matter (manual.md 2.3), heading strings (2.4) and body structure. */
import { join } from 'node:path';
import { isFile, listDirs, listFiles, readText, rel, walkFiles } from './fsutil.ts';
import { parseMarkdown } from './markdown.ts';
import type { Block, ParsedMarkdown } from './markdown.ts';

export type Lang = 'en' | 'it';
export const LANGS: Lang[] = ['en', 'it'];
export type PageKind = 'story' | 'area' | 'home';

export const FRONT_MATTER_KEYS = [
	'schema_version',
	'kind',
	'lang',
	'page_id',
	'source',
	'source_hash',
	'steps_from',
	'steps_hash',
	'routes',
	'contexts',
	'screenshots',
] as const;

/** Fields the generator rewrites on an existing page. */
export const DERIVED_KEYS = ['schema_version', 'kind', 'lang', 'page_id', 'source', 'steps_from', 'screenshots'] as const;

export const SECTION_KEYS = ['before', 'steps', 'result', 'rules', 'problems', 'related'] as const;
export type SectionKey = (typeof SECTION_KEYS)[number];

export const HEADINGS: Record<Lang, Record<SectionKey, string>> = {
	en: {
		before: 'Before you start',
		steps: 'Steps',
		result: 'What you will see',
		rules: 'Good to know',
		problems: 'If something goes wrong',
		related: 'Related pages',
	},
	it: {
		before: 'Prima di iniziare',
		steps: 'Passaggi',
		result: 'Cosa vedrai',
		rules: 'Da sapere',
		problems: 'Se qualcosa non va',
		related: 'Pagine correlate',
	},
};

export const PROBLEM_COLUMNS: Record<Lang, [string, string]> = {
	en: ['What you see', 'What to do'],
	it: ['Cosa vedi', 'Cosa fare'],
};

export function headingKey(lang: Lang, heading: string): SectionKey | null {
	for (const k of SECTION_KEYS) if (HEADINGS[lang][k] === heading) return k;
	return null;
}

export interface FrontMatter {
	/** Keys and raw values in file order. */
	entries: { key: string; value: string; line: number }[];
	/** Number of lines the block occupies, delimiters included. */
	lineCount: number;
}

export interface SplitPage {
	frontMatter: FrontMatter | null;
	error: string | null;
	body: string;
	/** 1-based line number of the first body line. */
	bodyStartLine: number;
	/** Offset in the text where the body starts. */
	bodyOffset: number;
}

/** Splits front matter from body. The first line must be `---`; the block ends at the next `---`. */
export function splitPage(text: string): SplitPage {
	const lines = text.split('\n');
	if (lines[0] !== '---') {
		return { frontMatter: null, error: 'the first line is not "---"', body: text, bodyStartLine: 1, bodyOffset: 0 };
	}
	const end = lines.findIndex((l, i) => i > 0 && l === '---');
	if (end < 0) {
		return { frontMatter: null, error: 'front matter is not closed by "---"', body: '', bodyStartLine: 1, bodyOffset: text.length };
	}
	const entries: FrontMatter['entries'] = [];
	let error: string | null = null;
	for (let i = 1; i < end; i++) {
		const m = /^([a-z_]+): (.*)$/.exec(lines[i]) ?? /^([a-z_]+):$/.exec(lines[i]);
		if (!m) {
			error ??= `line ${i + 1} is not "key: value"`;
			continue;
		}
		entries.push({ key: m[1], value: (m[2] ?? '').trim(), line: i + 1 });
	}
	const offset = lines.slice(0, end + 1).join('\n').length + 1;
	return {
		frontMatter: { entries, lineCount: end + 1 },
		error,
		body: text.slice(Math.min(offset, text.length)),
		bodyStartLine: end + 2,
		bodyOffset: Math.min(offset, text.length),
	};
}

/** Parses a flow list `[a, b]` with unquoted items; null when the value is not one. */
export function parseFlowList(value: string): string[] | null {
	const m = /^\[(.*)\]$/.exec(value.trim());
	if (!m) return null;
	if (m[1].trim() === '') return [];
	const items = m[1].split(',').map((s) => s.trim());
	if (items.some((s) => s === '' || /["'\[\]]/.test(s))) return null;
	return items;
}

export function formatFlowList(items: string[]): string {
	return `[${items.join(', ')}]`;
}

export function formatFrontMatter(fields: Record<string, string>): string {
	return `---\n${FRONT_MATTER_KEYS.map((k) => `${k}: ${fields[k]}`).join('\n')}\n---\n`;
}

export interface BodySection {
	/** Section key, or null when the heading is not one of the fixed headings of the page language. */
	key: SectionKey | null;
	heading: string;
	line: number;
	blocks: Block[];
}

export interface BodyStructure {
	hasDraftMarker: boolean;
	h1: { text: string; line: number } | null;
	h1Count: number;
	/** Blocks between the H1 and the first H2. */
	intro: Block[];
	sections: BodySection[];
	blocks: Block[];
	violations: ParsedMarkdown['violations'];
	/** Lines of the body, trailing blank lines excluded. */
	lineCount: number;
}

/** Analyses a page body into H1, introduction and H2 sections. */
export function analyzeBody(body: string, lang: Lang, firstLine = 1): BodyStructure {
	const parsed = parseMarkdown(body, firstLine);
	const out: BodyStructure = {
		hasDraftMarker: false,
		h1: null,
		h1Count: 0,
		intro: [],
		sections: [],
		blocks: parsed.blocks,
		violations: [...parsed.violations],
		lineCount: body.replace(/\n+$/, '').split('\n').length,
	};
	let cur: BodySection | null = null;
	for (const b of parsed.blocks) {
		if (b.type === 'draft') {
			out.hasDraftMarker = true;
			if (out.h1) out.violations.push({ line: b.line, message: 'the draft marker must come before the H1' });
		} else if (b.type === 'heading' && b.level === 1) {
			out.h1Count++;
			if (!out.h1) out.h1 = { text: b.text, line: b.line };
			else out.violations.push({ line: b.line, message: 'more than one H1' });
		} else if (b.type === 'heading' && b.level === 2) {
			cur = { key: headingKey(lang, b.text), heading: b.text, line: b.line, blocks: [] };
			out.sections.push(cur);
		} else if (cur) cur.blocks.push(b);
		else out.intro.push(b);
	}
	return out;
}

export interface ManualPage {
	lang: Lang;
	/** Repository-relative path. */
	path: string;
	/** Path relative to `docs/manual/{lang}/`. */
	relInLang: string;
	kind: PageKind | null;
	/** Area folder for story and area pages. */
	area: string | null;
	/** Story id for story pages. */
	storyId: string | null;
	text: string;
	frontMatter: FrontMatter | null;
	frontMatterError: string | null;
	/** Front matter values by key (first occurrence). */
	fields: Record<string, string>;
	body: string;
	bodyStartLine: number;
	structure: BodyStructure;
}

/** Classifies a page path relative to `docs/manual/{lang}/`; null when it is not a page of the layout. */
export function classifyPagePath(relInLang: string): { kind: PageKind; area: string | null; storyId: string | null } | null {
	if (relInLang === 'index.md') return { kind: 'home', area: null, storyId: null };
	const parts = relInLang.split('/');
	if (parts.length !== 2) return null;
	if (parts[1] === 'index.md') return { kind: 'area', area: parts[0], storyId: null };
	const m = /^(US-[A-Z]{2,8}-\d{2})\.md$/.exec(parts[1]);
	if (m) return { kind: 'story', area: parts[0], storyId: m[1] };
	return null;
}

export function parsePage(text: string, lang: Lang, path: string, relInLang: string): ManualPage {
	const split = splitPage(text);
	const fields: Record<string, string> = {};
	for (const e of split.frontMatter?.entries ?? []) if (!(e.key in fields)) fields[e.key] = e.value;
	const c = classifyPagePath(relInLang);
	return {
		lang,
		path,
		relInLang,
		kind: c?.kind ?? null,
		area: c?.area ?? null,
		storyId: c?.storyId ?? null,
		text,
		frontMatter: split.frontMatter,
		frontMatterError: split.error,
		fields,
		body: split.body,
		bodyStartLine: split.bodyStartLine,
		structure: analyzeBody(split.body, lang, split.bodyStartLine),
	};
}

/** Loads every page (home, area, story) of one language below `docs/manual/{lang}/`. */
export function loadPages(root: string, lang: Lang): ManualPage[] {
	const base = join(root, 'docs/manual', lang);
	const pages: ManualPage[] = [];
	const add = (relInLang: string) => {
		pages.push(parsePage(readText(join(base, relInLang)), lang, rel('docs/manual', lang, relInLang), relInLang));
	};
	if (isFile(join(base, 'index.md'))) add('index.md');
	for (const area of listDirs(base)) {
		for (const f of listFiles(join(base, area))) {
			if (classifyPagePath(`${area}/${f}`)) add(`${area}/${f}`);
		}
	}
	return pages;
}

/** Every file below `docs/manual/`, as paths relative to it. */
export function manualFiles(root: string): string[] {
	return walkFiles(join(root, 'docs/manual'));
}
