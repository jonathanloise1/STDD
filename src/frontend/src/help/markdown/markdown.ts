/**
 * The closed Markdown subset of manual pages (manual.md 2.5). Parses text into a block tree and reports every
 * construct outside the subset as a violation. Pure and deterministic.
 */
import { splitRow, isSeparatorRow } from './tablerow.ts';

export const DRAFT_MARKER = '<!-- draft: written by manual:generate, remove after review -->';

export type Inline =
	| { type: 'text'; text: string }
	| { type: 'bold'; text: string }
	| { type: 'link'; text: string; href: string };

export interface ImageRef {
	alt: string;
	src: string;
	line: number;
}

export interface ListItem {
	inlines: Inline[];
	/** Number written in an ordered list; null in a bullet list. */
	number: number | null;
	line: number;
	image: ImageRef | null;
}

export type Block =
	| { type: 'heading'; level: number; text: string; line: number }
	| { type: 'paragraph'; inlines: Inline[]; line: number }
	| { type: 'ordered'; items: ListItem[]; line: number }
	| { type: 'bullets'; items: ListItem[]; line: number }
	| ({ type: 'image' } & ImageRef)
	| { type: 'table'; header: Inline[][]; rows: Inline[][][]; line: number }
	| { type: 'draft'; line: number };

export interface Violation {
	line: number;
	message: string;
}

export interface ParsedMarkdown {
	blocks: Block[];
	violations: Violation[];
}

const IMAGE_LINE = /^!\[([^\]]*)\]\(([^)]*)\)\s*$/;
const IMAGE_SRC = /^images\/[^/]+\.png$/;

export function inlineText(inlines: Inline[]): string {
	return inlines.map((i) => i.text).join('');
}

function checkPlain(text: string, line: number, v: Violation[]): void {
	if (/`/.test(text)) v.push({ line, message: 'code spans are not allowed' });
	if (/<[A-Za-z/!]/.test(text)) v.push({ line, message: 'HTML is not allowed' });
	if (/\*/.test(text)) v.push({ line, message: 'only **bold** emphasis is allowed' });
	if (/(^|[\s(])_[^_\s][^_]*_($|[\s).,;:!?])/.test(text))
		v.push({ line, message: 'italics are not allowed' });
	if (/!\[/.test(text)) v.push({ line, message: 'an image must be on its own line' });
	if (/(https?:\/\/|www\.|mailto:)/i.test(text))
		v.push({ line, message: 'external URLs are not allowed' });
}

/** Parses inline content: text, `**bold**` and `[text](page.md)` links. */
export function parseInlines(src: string, line: number, v: Violation[]): Inline[] {
	const out: Inline[] = [];
	const re = /\*\*([^*]+)\*\*|(?<!!)\[([^\]]*)\]\(([^)]*)\)/g;
	let last = 0;
	const text = (t: string) => {
		if (!t) return;
		checkPlain(t, line, v);
		out.push({ type: 'text', text: t });
	};
	for (const m of src.matchAll(re)) {
		text(src.slice(last, m.index));
		if (m[1] !== undefined) {
			checkPlain(m[1], line, v);
			out.push({ type: 'bold', text: m[1] });
		} else {
			const href = m[3];
			if (!m[2]) v.push({ line, message: 'link text is empty' });
			if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('//')) {
				v.push({ line, message: `external link "${href}" is not allowed` });
			} else if (href.includes('#')) {
				v.push({ line, message: `link "${href}" must not have an anchor` });
			} else if (!href.endsWith('.md')) {
				v.push({ line, message: `link "${href}" must point to a page (.md)` });
			}
			checkPlain(m[2], line, v);
			out.push({ type: 'link', text: m[2], href });
		}
		last = (m.index ?? 0) + m[0].length;
	}
	text(src.slice(last));
	return out;
}

function checkImage(alt: string, src: string, line: number, v: Violation[]): void {
	if (!alt.trim()) v.push({ line, message: 'image alt text is required' });
	else if (alt.length > 120)
		v.push({ line, message: `image alt text is ${alt.length} characters (limit 120)` });
	if (!IMAGE_SRC.test(src))
		v.push({ line, message: `image path "${src}" must be images/{file}.png` });
}

const startsBlock = (l: string): boolean =>
	/^(#{1,6})\s/.test(l) ||
	l.startsWith('|') ||
	/^\d+\.\s/.test(l) ||
	/^[-*+]\s/.test(l) ||
	IMAGE_LINE.test(l) ||
	/^(```|~~~)/.test(l) ||
	l.startsWith('>') ||
	l.startsWith('<!--');

/** Parses a page body (without front matter). `firstLine` is the 1-based number of its first line. */
export function parseMarkdown(text: string, firstLine = 1): ParsedMarkdown {
	const lines = text.replace(/\r\n/g, '\n').split('\n');
	const blocks: Block[] = [];
	const v: Violation[] = [];
	const n = lines.length;
	const at = (i: number) => firstLine + i;
	let i = 0;
	while (i < n) {
		const line = lines[i];
		if (line.trim() === '') {
			i++;
			continue;
		}
		if (line === DRAFT_MARKER) {
			blocks.push({ type: 'draft', line: at(i) });
			i++;
			continue;
		}
		if (line.startsWith('<!--') || /^<[A-Za-z/]/.test(line)) {
			v.push({ line: at(i), message: 'HTML is not allowed' });
			i++;
			continue;
		}
		if (/^(```|~~~)/.test(line)) {
			v.push({ line: at(i), message: 'code blocks are not allowed' });
			const fence = line.slice(0, 3);
			i++;
			while (i < n && !lines[i].startsWith(fence)) i++;
			i++;
			continue;
		}
		const h = /^(#{1,6})\s+(.*?)\s*$/.exec(line);
		if (h) {
			if (h[1].length > 2)
				v.push({ line: at(i), message: `heading level ${h[1].length} is not allowed` });
			else blocks.push({ type: 'heading', level: h[1].length, text: h[2], line: at(i) });
			i++;
			continue;
		}
		if (/^(-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
			v.push({ line: at(i), message: 'horizontal rules are not allowed' });
			i++;
			continue;
		}
		if (line.startsWith('>')) {
			v.push({ line: at(i), message: 'block quotes are not allowed' });
			i++;
			continue;
		}
		if (line.startsWith('|')) {
			const start = i;
			const rows: string[] = [];
			while (i < n && lines[i].startsWith('|')) rows.push(lines[i++]);
			const header = splitRow(rows[0]);
			let body = rows.slice(1);
			if (body.length && isSeparatorRow(body[0])) body = body.slice(1);
			else
				v.push({
					line: at(start),
					message: 'table needs a |---| separator row after the header',
				});
			const cell = (c: string, k: number) => parseInlines(c, at(start + k), v);
			const hdr = header.map((c) => cell(c, 0));
			const bodyRows = body.map((r, k) => {
				const cells = splitRow(r);
				if (cells.length !== header.length) {
					v.push({
						line: at(start + k + 2),
						message: `table row has ${cells.length} cells, header has ${header.length}`,
					});
				}
				return cells.map((c) => cell(c, k + 2));
			});
			blocks.push({ type: 'table', header: hdr, rows: bodyRows, line: at(start) });
			continue;
		}
		const img = IMAGE_LINE.exec(line);
		if (img) {
			checkImage(img[1], img[2], at(i), v);
			blocks.push({ type: 'image', alt: img[1], src: img[2], line: at(i) });
			i++;
			continue;
		}
		const ordered = /^(\d+)\.\s+(.*)$/.exec(line);
		const bullet = /^([-*+])\s+(.*)$/.exec(line);
		if (ordered || bullet) {
			const isOrdered = Boolean(ordered);
			if (bullet && bullet[1] !== '-')
				v.push({ line: at(i), message: `bullet "${bullet[1]}" is not allowed, use "-"` });
			const items: ListItem[] = [];
			const startLine = at(i);
			const itemRe = isOrdered ? /^(\d+)\.\s+(.*)$/ : /^[-*+]\s+(.*)$/;
			let j = i;
			while (j < n) {
				const l = lines[j];
				if (l.trim() === '') {
					let k = j;
					while (k < n && lines[k].trim() === '') k++;
					if (k < n && (itemRe.test(lines[k]) || /^ {3}!\[/.test(lines[k]))) {
						j = k;
						continue;
					}
					break;
				}
				const m = itemRe.exec(l);
				if (m) {
					const num = isOrdered ? Number(m[1]) : null;
					const content = isOrdered ? m[2] : m[1];
					if (isOrdered && num !== items.length + 1) {
						v.push({
							line: at(j),
							message: `list number ${num} should be ${items.length + 1}`,
						});
					}
					items.push({
						inlines: parseInlines(content, at(j), v),
						number: num,
						line: at(j),
						image: null,
					});
					j++;
					continue;
				}
				const im = /^ {3}!\[([^\]]*)\]\(([^)]*)\)\s*$/.exec(l);
				if (im && items.length) {
					const last = items[items.length - 1];
					checkImage(im[1], im[2], at(j), v);
					if (last.image)
						v.push({ line: at(j), message: 'at most one image per list item' });
					else last.image = { alt: im[1], src: im[2], line: at(j) };
					j++;
					continue;
				}
				if (/^\s+\S/.test(l)) {
					v.push({
						line: at(j),
						message: 'nested lists and list continuation lines are not allowed',
					});
					j++;
					continue;
				}
				break;
			}
			blocks.push({ type: isOrdered ? 'ordered' : 'bullets', items, line: startLine });
			i = j;
			continue;
		}
		if (/^\s+\S/.test(line)) {
			v.push({ line: at(i), message: 'indented content is not allowed' });
			i++;
			continue;
		}
		const start = i;
		const para: string[] = [];
		while (i < n && lines[i].trim() !== '' && (i === start || !startsBlock(lines[i]))) {
			if (/^\s/.test(lines[i]) && i !== start)
				v.push({ line: at(i), message: 'indented content is not allowed' });
			para.push(lines[i].trim());
			i++;
		}
		blocks.push({
			type: 'paragraph',
			inlines: parseInlines(para.join('\n'), at(start), v),
			line: at(start),
		});
	}
	return { blocks, violations: v };
}

/** Image references of a block list, in order of first appearance, without repeats. */
export function imageRefs(blocks: Block[]): string[] {
	const out: string[] = [];
	const add = (s: string) => {
		if (!out.includes(s)) out.push(s);
	};
	for (const b of blocks) {
		if (b.type === 'image') add(b.src);
		else if (b.type === 'ordered' || b.type === 'bullets')
			for (const it of b.items) if (it.image) add(it.image.src);
	}
	return out;
}

/** Links of a block list: every link inline with its line. */
export function linksOf(blocks: Block[]): { href: string; text: string; line: number }[] {
	const out: { href: string; text: string; line: number }[] = [];
	const scan = (inl: Inline[], line: number) => {
		for (const x of inl) if (x.type === 'link') out.push({ href: x.href, text: x.text, line });
	};
	for (const b of blocks) {
		if (b.type === 'paragraph') scan(b.inlines, b.line);
		else if (b.type === 'ordered' || b.type === 'bullets')
			for (const it of b.items) scan(it.inlines, it.line);
		else if (b.type === 'table') {
			for (const c of b.header) scan(c, b.line);
			for (const r of b.rows) for (const c of r) scan(c, b.line);
		}
	}
	return out;
}
