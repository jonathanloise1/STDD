/** Sections of a manual page (manual.md 6.1): plain text, split at item boundaries into parts of at most 1,200 characters. */
import { HEADINGS } from '../lib/index.ts';
import type { Block, Inline, Lang, ManualPage } from '../lib/index.ts';

export const MAX_SECTION_CHARS = 1200;

export interface SectionDraft {
	id: string;
	page_id: string;
	key: string;
	area: string | null;
	story: string | null;
	title: string;
	text: string;
	url: string;
	routes: string[];
}

const codePoints = (s: string): number => [...s].length;

function plain(inlines: Inline[]): string {
	return inlines
		.map((i) => i.text)
		.join('')
		.replace(/\s*\n\s*/g, ' ')
		.trim();
}

/** The text lines of blocks: paragraphs, list items (`{n}. ` or `- `), table rows, image alt texts (6.1). */
export function blockItems(blocks: Block[]): string[] {
	const out: string[] = [];
	for (const b of blocks) {
		if (b.type === 'paragraph') out.push(plain(b.inlines));
		else if (b.type === 'ordered' || b.type === 'bullets') {
			for (const it of b.items) {
				out.push(`${it.number !== null ? `${it.number}. ` : '- '}${plain(it.inlines)}`);
				if (it.image?.alt) out.push(it.image.alt);
			}
		} else if (b.type === 'table') {
			const header = b.header.map(plain);
			for (const row of b.rows) out.push(row.map((c, i) => `${header[i] ?? ''}: ${plain(c)}.`).join(' '));
		} else if (b.type === 'image' && b.alt) out.push(b.alt);
	}
	return out.map((s) => s.normalize('NFC').replace(/[ \t]+$/g, ''));
}

/** Parts of at most `limit` characters; throws when one item is longer. */
export function splitItems(items: string[], limit = MAX_SECTION_CHARS): string[] {
	const parts: string[] = [];
	let cur = '';
	for (const item of items) {
		if (codePoints(item) > limit) throw new Error(`a single item is longer than ${limit} characters: "${item.slice(0, 60)}..."`);
		const next = cur === '' ? item : `${cur}\n${item}`;
		if (codePoints(next) > limit) {
			parts.push(cur);
			cur = item;
		} else cur = next;
	}
	if (cur !== '') parts.push(cur);
	return parts;
}

export function pageUrl(page: ManualPage): string {
	if (page.kind === 'home') return '/manual';
	if (page.kind === 'area') return `/manual/${page.area}`;
	return `/manual/${page.area}/${page.storyId}`;
}

export function pageIdOf(page: ManualPage): string {
	if (page.fields.page_id) return page.fields.page_id;
	if (page.kind === 'home') return 'home';
	if (page.kind === 'area') return page.area ?? '';
	return `${page.area}/${page.storyId}`;
}

export function parseRoutes(value: string | undefined): string[] {
	const v = (value ?? '').trim();
	if (!v.startsWith('[') || !v.endsWith(']')) return [];
	return v
		.slice(1, -1)
		.split(',')
		.map((s) => s.trim())
		.filter(Boolean);
}

/** Sections of one page, in page order: `overview`, then every H2 except `related`. Throws on an oversize item. */
export function pageSections(page: ManualPage, lang: Lang): SectionDraft[] {
	const st = page.structure;
	if (!st.h1) throw new Error(`${page.path}: the page has no H1`);
	const title = st.h1.text;
	const pageId = pageIdOf(page);
	const routes = page.kind === 'story' ? parseRoutes(page.fields.routes) : [];
	const out: SectionDraft[] = [];
	const add = (key: string, sectionTitle: string, blocks: Block[]) => {
		let parts: string[];
		try {
			parts = splitItems(blockItems(blocks));
		} catch (e) {
			throw new Error(`${page.path} (${key}): ${(e as Error).message}`);
		}
		parts.forEach((text, i) => {
			const n = i + 1;
			out.push({
				id: `${pageId}#${key}${n > 1 ? `-${n}` : ''}`,
				page_id: pageId,
				key,
				area: page.area,
				story: page.storyId,
				title: n > 1 ? `${sectionTitle} (${n})` : sectionTitle,
				text,
				url: `${pageUrl(page)}#${key}`,
				routes,
			});
		});
	};
	add('overview', title, st.intro);
	for (const s of st.sections) {
		if (s.key === null || s.key === 'related') continue;
		add(s.key, `${title} - ${HEADINGS[lang][s.key]}`, s.blocks);
	}
	return out;
}
