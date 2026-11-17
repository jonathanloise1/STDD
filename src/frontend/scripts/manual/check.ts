/** manual:check (manual.md 3.3, 3.4): checks M01 to M16 over docs/manual/. */
import { createHash } from 'node:crypto';
import { closeSync, openSync, readFileSync, readSync } from 'node:fs';
import { join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { contractVersion } from '../lib/contracts.ts';
import { isFile, readText } from '../lib/fsutil.ts';
import { loadLocales, italianUiText, localeValues } from '../lib/locales.ts';
import type { Locales } from '../lib/locales.ts';
import { DRAFT_MARKER, imageRefs, inlineText, linksOf } from '../lib/markdown.ts';
import type { Block } from '../lib/markdown.ts';
import {
	FRONT_MATTER_KEYS,
	HEADINGS,
	LANGS,
	PROBLEM_COLUMNS,
	SECTION_KEYS,
	loadPages,
	manualFiles,
	parseFlowList,
} from '../lib/page.ts';
import type { BodySection, Lang, ManualPage } from '../lib/page.ts';
import { splitQuestions, normalizeQuestion } from '../lib/questions.ts';
import { allStories, findStory, loadRepo, testCaseById } from '../lib/repo.ts';
import type { Area, Repo } from '../lib/repo.ts';
import { scanContextUses, scanRoutePaths } from '../lib/sourcescan.ts';
import { loadStopwords } from '../lib/stopwords.ts';
import { parseTable, splitLines, stripQuotes, quotedStrings } from '../lib/text.ts';
import { parseArgs } from './args.ts';
import { staleReasons } from './staleness.ts';

export type CheckId =
	| 'M01' | 'M02' | 'M03' | 'M04' | 'M05' | 'M06' | 'M07' | 'M08'
	| 'M09' | 'M10' | 'M11' | 'M12' | 'M13' | 'M14' | 'M15' | 'M16';

export interface Finding {
	id: CheckId;
	/** Repository-relative path. */
	path: string;
	message: string;
	severity: 'fail' | 'warn';
}

export interface CheckOptions {
	/** Skip M08 and M09 (docs trees without screenshots). */
	noImages?: boolean;
}

/** Checks whose findings are warnings in non-strict mode (manual.md 3.3). */
export const WARNING_IDS: CheckId[] = ['M05', 'M06', 'M14'];

const HEX40 = /^[0-9a-f]{40}$/;
const ROUTE = /^\/$|^(\/([A-Za-z0-9_-]+|:[A-Za-z0-9_]+))+$/;
const CONTEXT = /^[a-z]+(-[a-z]+)*$/;
const IMAGE_NAME = /^[a-z0-9]+(-[a-z0-9]+)*\.png$/;
const PNG_SIGNATURE = '89504e470d0a1a0a';

interface Ctx {
	root: string;
	repo: Repo;
	loc: Locales;
	pages: Record<Lang, ManualPage[]>;
	files: string[];
	opts: CheckOptions;
	add: (id: CheckId, path: string, message: string) => void;
}

const pagePath = (lang: Lang, relInLang: string) => `docs/manual/${lang}/${relInLang}`;

function sourceOf(ctx: Ctx, page: ManualPage) {
	if (page.kind === 'story' && page.storyId) return findStory(ctx.repo, page.storyId);
	return null;
}

function areaOf(ctx: Ctx, page: ManualPage): Area | null {
	return ctx.repo.areas.find((a) => a.folder === page.area) ?? null;
}

// ---------------------------------------------------------------- M01
function m01(ctx: Ctx): void {
	for (const f of ctx.files) {
		const p = `docs/manual/${f}`;
		const parts = f.split('/');
		let ok = false;
		if (parts.length === 1) ok = f === 'captures.json' || f === 'bench.md' || f === 'glossary.md';
		else if (!LANGS.includes(parts[0] as Lang)) ok = false;
		else if (parts.length === 2) ok = parts[1] === 'index.md';
		else if (parts.length === 3) ok = parts[2] === 'index.md' || /^US-[A-Z]{2,8}-\d{2}\.md$/.test(parts[2]);
		else if (parts.length === 4) ok = parts[2] === 'images' && IMAGE_NAME.test(parts[3]);
		if (!ok) ctx.add('M01', p, 'file is not part of the manual layout (manual.md 2.1)');
	}
}

// ---------------------------------------------------------------- M02
function m02(ctx: Ctx): void {
	for (const lang of LANGS) {
		const have = new Set(ctx.pages[lang].map((p) => p.relInLang));
		const expected = new Set<string>(['index.md']);
		for (const a of ctx.repo.areas) {
			expected.add(`${a.folder}/index.md`);
			for (const s of a.stories) expected.add(`${a.folder}/${s.id}.md`);
		}
		for (const e of expected) if (!have.has(e)) ctx.add('M02', pagePath(lang, e), 'page is missing');
		for (const p of ctx.pages[lang]) {
			if (!expected.has(p.relInLang)) ctx.add('M02', p.path, 'page has no source: the story or area it describes does not exist');
		}
	}
}

// ---------------------------------------------------------------- M03
function expectedFields(ctx: Ctx, page: ManualPage): Record<string, string> | null {
	if (page.kind === 'home') return { kind: 'home', page_id: 'home', source: 'none' };
	if (page.kind === 'area' && page.area) {
		return { kind: 'area', page_id: page.area, source: `docs/areas/${page.area}/userstories.md` };
	}
	if (page.kind === 'story' && page.area && page.storyId) {
		return {
			kind: 'story',
			page_id: `${page.area}/${page.storyId}`,
			source: `docs/areas/${page.area}/user-stories/${page.storyId}.md`,
		};
	}
	void ctx;
	return null;
}

function m03(ctx: Ctx): void {
	const version = String(contractVersion(ctx.root, 'manual-page') ?? 1);
	for (const lang of LANGS) {
		for (const page of ctx.pages[lang]) {
			const bad = (msg: string) => ctx.add('M03', page.path, msg);
			if (!page.frontMatter) {
				bad(`front matter: ${page.frontMatterError}`);
				continue;
			}
			if (page.frontMatterError) bad(`front matter: ${page.frontMatterError}`);
			const keys = page.frontMatter.entries.map((e) => e.key);
			const dup = keys.filter((k, i) => keys.indexOf(k) !== i);
			for (const k of new Set(dup)) bad(`field ${k} appears more than once`);
			for (const k of FRONT_MATTER_KEYS) if (!keys.includes(k)) bad(`field ${k} is missing`);
			for (const k of keys) if (!(FRONT_MATTER_KEYS as readonly string[]).includes(k)) bad(`unknown field ${k}`);
			const known = keys.filter((k) => (FRONT_MATTER_KEYS as readonly string[]).includes(k));
			if (
				!dup.length &&
				known.length === FRONT_MATTER_KEYS.length &&
				known.some((k, i) => k !== FRONT_MATTER_KEYS[i])
			) {
				bad(`fields are not in the order ${FRONT_MATTER_KEYS.join(', ')}`);
			}
			const f = page.fields;
			if ('schema_version' in f && f['schema_version'] !== version) {
				bad(`schema_version is ${f['schema_version']}, the manual-page contract is at ${version}`);
			}
			const exp = expectedFields(ctx, page);
			if ('lang' in f && f['lang'] !== lang) bad(`lang is ${f['lang']}, the folder is ${lang}`);
			if (exp) {
				for (const k of ['kind', 'page_id', 'source']) {
					if (k in f && f[k] !== exp[k]) bad(`${k} is "${f[k]}", expected "${exp[k]}"`);
				}
			}
			if ('source_hash' in f) {
				const h = f['source_hash'];
				if (page.kind === 'home' ? h !== 'none' : !HEX40.test(h)) {
					bad(`source_hash "${h}" must be ${page.kind === 'home' ? '"none"' : 'a 40-character lower-case hex blob id'}`);
				}
			}
			if ('steps_from' in f) {
				const sf = f['steps_from'];
				if (page.kind !== 'story') {
					if (sf !== 'none') bad('steps_from must be "none" on this kind of page');
				} else if (sf !== 'none') {
					const tc = testCaseById(ctx.repo, sf);
					if (!tc) bad(`steps_from "${sf}" is not a test case`);
					else if (tc.testCase.storyId !== page.storyId) bad(`steps_from "${sf}" belongs to ${tc.testCase.storyId}, not to ${page.storyId}`);
				}
			}
			if ('steps_hash' in f) {
				const none = f['steps_from'] === 'none' || page.kind !== 'story';
				if (none ? f['steps_hash'] !== 'none' : !HEX40.test(f['steps_hash'])) {
					bad(`steps_hash "${f['steps_hash']}" must be ${none ? '"none"' : 'a 40-character lower-case hex blob id'}`);
				}
			}
			if ('routes' in f) {
				const r = parseFlowList(f['routes']);
				if (!r) bad('routes is not a flow list such as [/tasks]');
				else if (page.kind !== 'story' && r.length) bad('routes must be [] on this kind of page');
				else for (const x of r) if (!ROUTE.test(x)) bad(`route "${x}" is not a route pattern with a leading / and :param segments`);
			}
			if ('contexts' in f) {
				const c = parseFlowList(f['contexts']);
				if (!c) bad('contexts is not a flow list such as [task-form]');
				else for (const x of c) if (!CONTEXT.test(x)) bad(`context "${x}" does not match ^[a-z]+(-[a-z]+)*$`);
			}
			if ('screenshots' in f) {
				const s = parseFlowList(f['screenshots']);
				const refs = imageRefs(page.structure.blocks);
				if (!s) bad('screenshots is not a flow list');
				else if (s.join('\n') !== refs.join('\n')) {
					bad(`screenshots [${s.join(', ')}] does not equal the image references of the body [${refs.join(', ')}]`);
				}
			}
		}
	}
}

// ---------------------------------------------------------------- M04
function listCount(blocks: Block[]): number {
	return blocks.filter((b) => b.type === 'ordered').reduce((n, b) => n + (b.type === 'ordered' ? b.items.length : 0), 0);
}

function checkSection(ctx: Ctx, page: ManualPage, s: BodySection, bad: (line: number, m: string) => void): void {
	const types = s.blocks.filter((b) => b.type !== 'image').map((b) => b.type);
	if (!s.blocks.length) {
		bad(s.line, `section "${s.heading}" is empty; omit optional sections`);
		return;
	}
	const only = (allowed: string[], what: string) => {
		for (const b of s.blocks) if (b.type !== 'image' && !allowed.includes(b.type)) bad(b.type === 'draft' ? s.line : (b as { line: number }).line, `"${s.heading}" must contain only ${what}`);
	};
	switch (s.key) {
		case 'before':
			only(['paragraph', 'bullets'], 'paragraphs and bullets');
			break;
		case 'steps': {
			only(['ordered'], 'a numbered list');
			if (types.filter((t) => t === 'ordered').length > 1) bad(s.line, '"Steps" must be one numbered list');
			const n = listCount(s.blocks);
			if (n > 12) bad(s.line, `"${s.heading}" has ${n} items (limit 12)`);
			break;
		}
		case 'result':
		case 'rules':
			only(['bullets'], 'a bullet list');
			break;
		case 'problems': {
			only(['table'], 'one table');
			const t = s.blocks.find((b) => b.type === 'table');
			if (t && t.type === 'table') {
				const cols = PROBLEM_COLUMNS[page.lang];
				const got = t.header.map(inlineText);
				if (got.length !== 2 || got[0] !== cols[0] || got[1] !== cols[1]) {
					bad(t.line, `table columns must be "${cols[0]}" / "${cols[1]}"`);
				}
				if (!t.rows.length) bad(t.line, 'table has no rows');
			}
			break;
		}
		case 'related':
			only(['bullets'], 'a bullet list');
			for (const b of s.blocks) {
				if (b.type !== 'bullets') continue;
				for (const it of b.items) {
					if (it.inlines.length !== 1 || it.inlines[0].type !== 'link') bad(it.line, 'each related page item must be exactly one link');
				}
			}
			break;
		default:
			break;
	}
}

function m04(ctx: Ctx): void {
	for (const lang of LANGS) {
		for (const page of ctx.pages[lang]) {
			const st = page.structure;
			const bad = (line: number, m: string) => ctx.add('M04', page.path, `line ${line}: ${m}`);
			for (const v of st.violations) bad(v.line, v.message);
			if (st.h1Count === 0) ctx.add('M04', page.path, 'there is no H1');
			else {
				const first = st.blocks.find((b) => b.type !== 'draft');
				if (first && !(first.type === 'heading' && first.level === 1)) bad((first as { line: number }).line, 'the H1 must be the first thing after the draft marker');
			}
			if (st.h1) {
				if (lang === 'it' && st.h1.text.length > 70) bad(st.h1.line, `the title is ${st.h1.text.length} characters (limit 70)`);
				if (lang === 'en') {
					const hit = sourceOf(ctx, page);
					const want = hit ? hit.story.title : page.kind === 'area' ? areaOf(ctx, page)?.title : undefined;
					if (want !== undefined && st.h1.text !== want) bad(st.h1.line, `the title is "${st.h1.text}", expected "${want}"`);
				}
			}
			if (!st.intro.length) ctx.add('M04', page.path, 'there is no introduction under the H1');
			for (const b of st.intro) {
				if (b.type !== 'paragraph') bad((b as { line: number }).line, 'only paragraphs may stand between the H1 and the first H2');
			}
			if (st.lineCount > 150) ctx.add('M04', page.path, `the body has ${st.lineCount} lines (limit 150)`);
			if (page.kind !== 'story') {
				for (const s of st.sections) bad(s.line, `"${s.heading}": this kind of page has only the H1 and the introduction`);
				continue;
			}
			let last = -1;
			const seen = new Set<string>();
			for (const s of st.sections) {
				if (!s.key) {
					bad(s.line, `heading "${s.heading}" is not one of the ${lang} headings: ${SECTION_KEYS.map((k) => HEADINGS[lang][k]).join(', ')}`);
					continue;
				}
				if (seen.has(s.key)) bad(s.line, `heading "${s.heading}" appears more than once`);
				seen.add(s.key);
				const idx = SECTION_KEYS.indexOf(s.key);
				if (idx < last) bad(s.line, `heading "${s.heading}" is out of order`);
				last = Math.max(last, idx);
				checkSection(ctx, page, s, bad);
			}
			if (!seen.has('steps')) ctx.add('M04', page.path, `required section "${HEADINGS[lang].steps}" is missing`);
			const hit = sourceOf(ctx, page);
			if (hit && (hit.story.errorStates.length > 0 || hit.story.userInput === true) && !seen.has('problems')) {
				ctx.add('M04', page.path, `required section "${HEADINGS[lang].problems}" is missing (the story has Error States or user input)`);
			}
		}
	}
}

// ---------------------------------------------------------------- M05, M06
function m05m06(ctx: Ctx): void {
	for (const lang of LANGS) {
		for (const page of ctx.pages[lang]) {
			for (const r of staleReasons(ctx.repo, page)) ctx.add('M05', page.path, `stale: ${r.message}`);
			if (page.structure.hasDraftMarker) ctx.add('M06', page.path, 'draft marker present: rewrite the page, then remove the marker');
		}
	}
}

// ---------------------------------------------------------------- M07
function m07(ctx: Ctx): void {
	for (const lang of LANGS) {
		const have = new Set(ctx.pages[lang].map((p) => p.relInLang));
		for (const page of ctx.pages[lang]) {
			for (const l of linksOf(page.structure.blocks)) {
				if (!l.href.endsWith('.md') || l.href.includes('#') || /^[a-z][a-z0-9+.-]*:/i.test(l.href)) continue;
				const target = posix.normalize(posix.join(posix.dirname(page.relInLang), l.href));
				if (target.startsWith('..') || !have.has(target)) {
					ctx.add('M07', page.path, `line ${l.line}: link "${l.href}" does not resolve to a ${lang} page`);
				}
			}
		}
	}
}

// ---------------------------------------------------------------- M08, M09
interface Captures {
	entries: Map<string, { sha256: string }>;
	error: string | null;
}

function loadCaptures(ctx: Ctx): Captures {
	const abs = join(ctx.root, 'docs/manual/captures.json');
	const out: Captures = { entries: new Map(), error: null };
	if (!isFile(abs)) {
		out.error = 'docs/manual/captures.json does not exist';
		return out;
	}
	try {
		const data = JSON.parse(readText(abs));
		if (!Array.isArray(data?.images)) out.error = 'captures.json has no "images" array';
		else for (const e of data.images) if (e && typeof e.path === 'string') out.entries.set(e.path, { sha256: String(e.sha256 ?? '') });
	} catch (e) {
		out.error = `captures.json is not valid JSON: ${(e as Error).message}`;
	}
	return out;
}

function pngSignature(abs: string): string {
	const fd = openSync(abs, 'r');
	try {
		const buf = Buffer.alloc(8);
		readSync(fd, buf, 0, 8, 0);
		return buf.toString('hex');
	} finally {
		closeSync(fd);
	}
}

function m08m09(ctx: Ctx): void {
	const cap = loadCaptures(ctx);
	let reportedCaptureError = false;
	const referenced = new Set<string>();
	for (const lang of LANGS) {
		for (const page of ctx.pages[lang]) {
			const dir = posix.dirname(`docs/manual/${lang}/${page.relInLang}`);
			for (const src of imageRefs(page.structure.blocks)) {
				if (!/^images\/[^/]+\.png$/.test(src)) continue; // M04 reports the form
				const imgRel = `${dir}/${src}`;
				referenced.add(imgRel);
				const abs = join(ctx.root, imgRel);
				if (!isFile(abs)) {
					ctx.add('M08', page.path, `image ${src} does not exist at ${imgRel}`);
					continue;
				}
				if (pngSignature(abs) !== PNG_SIGNATURE) ctx.add('M08', page.path, `image ${src} is not a PNG file`);
				if (cap.error) {
					if (!reportedCaptureError) ctx.add('M08', 'docs/manual/captures.json', cap.error);
					reportedCaptureError = true;
					continue;
				}
				const e = cap.entries.get(imgRel);
				if (!e) ctx.add('M08', page.path, `image ${src} has no entry in captures.json`);
				else if (e.sha256 !== createHash('sha256').update(readFileSync(abs)).digest('hex')) {
					ctx.add('M08', page.path, `image ${src}: the sha256 in captures.json differs from the file`);
				}
			}
		}
	}
	for (const f of ctx.files) {
		const m = /^(en|it)\/[^/]+\/images\/[^/]+$/.exec(f);
		if (m && !referenced.has(`docs/manual/${f}`)) ctx.add('M09', `docs/manual/${f}`, 'image is not referenced by any page');
	}
	for (const p of cap.entries.keys()) {
		if (!isFile(join(ctx.root, p))) ctx.add('M09', 'docs/manual/captures.json', `entry ${p} has no file`);
	}
}

// ---------------------------------------------------------------- M10
function m10(ctx: Ctx): void {
	const { found, scanned } = scanRoutePaths(ctx.root);
	for (const lang of LANGS) {
		for (const page of ctx.pages[lang]) {
			if (page.kind !== 'story' || !('routes' in page.fields)) continue;
			const routes = parseFlowList(page.fields['routes']);
			if (!routes) continue; // M03
			if (!routes.length) ctx.add('M10', page.path, 'a story page needs at least one route');
			if (scanned) {
				for (const r of routes) {
					if (!found.has(r.startsWith('/') ? r : `/${r}`)) {
						ctx.add('M10', page.path, `route ${r} is not a path in src/frontend/src/routes/*.tsx`);
					}
				}
			}
		}
	}
}

// ---------------------------------------------------------------- M11
function stepImages(page: ManualPage): string[] {
	const out: string[] = [];
	for (const s of page.structure.sections) {
		for (const b of s.blocks) {
			if (b.type === 'image') out.push(`${s.key ?? s.heading}/-:${posix.basename(b.src)}`);
			else if (b.type === 'ordered' || b.type === 'bullets') {
				b.items.forEach((it, i) => {
					if (it.image) out.push(`${s.key ?? s.heading}/${i + 1}:${posix.basename(it.image.src)}`);
				});
			}
		}
	}
	return out;
}

function problemRows(page: ManualPage): number {
	const s = page.structure.sections.find((x) => x.key === 'problems');
	const t = s?.blocks.find((b) => b.type === 'table');
	return t && t.type === 'table' ? t.rows.length : 0;
}

function stepCount(page: ManualPage): number {
	const s = page.structure.sections.find((x) => x.key === 'steps');
	return s ? listCount(s.blocks) : 0;
}

function m11(ctx: Ctx): void {
	const it = new Map(ctx.pages.it.map((p) => [p.relInLang, p]));
	for (const en of ctx.pages.en) {
		const p = it.get(en.relInLang);
		if (!p) continue; // M02 reports the missing page
		const bad = (m: string) => ctx.add('M11', p.path, `${m} (en: ${en.path})`);
		for (const k of ['kind', 'page_id', 'source', 'source_hash', 'steps_from', 'steps_hash', 'routes', 'contexts']) {
			if ((en.fields[k] ?? '') !== (p.fields[k] ?? '')) bad(`${k} differs: en "${en.fields[k]}", it "${p.fields[k]}"`);
		}
		const keys = (x: ManualPage) => x.structure.sections.map((s) => s.key ?? `?${s.heading}`).join(', ');
		if (keys(en) !== keys(p)) bad(`H2 sections differ: en [${keys(en)}], it [${keys(p)}]`);
		if (stepCount(en) !== stepCount(p)) bad(`steps count differs: en ${stepCount(en)}, it ${stepCount(p)}`);
		if (problemRows(en) !== problemRows(p)) bad(`problems rows differ: en ${problemRows(en)}, it ${problemRows(p)}`);
		const a = stepImages(en).join(', ');
		const b = stepImages(p).join(', ');
		if (a !== b) bad(`images differ: en [${a}], it [${b}]`);
	}
	const imgs = (lang: Lang) => new Set(ctx.files.filter((f) => f.startsWith(`${lang}/`) && /\/images\//.test(f)).map((f) => f.slice(lang.length + 1)));
	const en = imgs('en');
	const it2 = imgs('it');
	for (const f of en) if (!it2.has(f)) ctx.add('M11', `docs/manual/en/${f}`, `image has no counterpart at docs/manual/it/${f}`);
	for (const f of it2) if (!en.has(f)) ctx.add('M11', `docs/manual/it/${f}`, `image has no counterpart at docs/manual/en/${f}`);
}

// ---------------------------------------------------------------- M12
export function languageShare(body: string, other: Set<string>, own: Set<string>): { share: number; tokens: number } {
	const text = body
		.split('\n')
		.filter((l) => l !== DRAFT_MARKER)
		.join('\n')
		.replace(/"[^"]*"/g, ' ')
		.replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
		.replace(/\[([^\]]*)\]\([^)]*\)/g, '$1');
	const tokens = text.normalize('NFC').toLowerCase().split(/[^\p{L}\p{N}]+/u).filter(Boolean);
	if (!tokens.length) return { share: 0, tokens: 0 };
	const hits = tokens.filter((t) => other.has(t) && !own.has(t)).length;
	return { share: hits / tokens.length, tokens: tokens.length };
}

function m12(ctx: Ctx): void {
	const stop = { en: loadStopwords(ctx.root, 'en').words, it: loadStopwords(ctx.root, 'it').words };
	for (const lang of LANGS) {
		const other: Lang = lang === 'en' ? 'it' : 'en';
		for (const page of ctx.pages[lang]) {
			const { share } = languageShare(page.body, stop[other], stop[lang]);
			if (share > 0.1) {
				ctx.add('M12', page.path, `${(share).toFixed(2)} of the words outside quotes are ${other === 'it' ? 'Italian' : 'English'} stopwords (limit 0.10): the page is not written in ${lang === 'en' ? 'English' : 'Italian'}`);
			}
		}
	}
}

// ---------------------------------------------------------------- M13
function problemFirstColumn(page: ManualPage): string[] {
	const s = page.structure.sections.find((x) => x.key === 'problems');
	const t = s?.blocks.find((b) => b.type === 'table');
	return t && t.type === 'table' ? t.rows.map((r) => inlineText(r[0] ?? [])) : [];
}

function m13(ctx: Ctx): void {
	for (const lang of LANGS) {
		for (const page of ctx.pages[lang]) {
			const hit = sourceOf(ctx, page);
			if (!hit) continue;
			const col = problemFirstColumn(page);
			for (const e of hit.story.errorStates) {
				if (lang === 'en') {
					if (!col.some((c) => c.includes(`"${e.message}"`))) {
						ctx.add('M13', page.path, `Error States message "${e.message}" is not in the "What you see" column`);
					}
				} else {
					const t = italianUiText(ctx.loc, e.message);
					if (t !== null && !page.body.includes(`"${t}"`)) {
						ctx.add('M13', page.path, `the Italian UI text "${t}" of the Error States message "${e.message}" is missing`);
					}
				}
			}
		}
	}
}

// ---------------------------------------------------------------- M14
function rawSection(page: ManualPage, s: BodySection): string[] {
	const lines = splitLines(page.body);
	const secs = page.structure.sections;
	const next = secs[secs.indexOf(s) + 1];
	const from = s.line - page.bodyStartLine + 1;
	const to = next ? next.line - page.bodyStartLine : lines.length;
	return lines.slice(from, to);
}

function m14(ctx: Ctx): void {
	if (!ctx.loc.available) return;
	for (const lang of LANGS) {
		const values = localeValues(ctx.loc, lang);
		for (const page of ctx.pages[lang]) {
			const allowed = new Set(values);
			const sf = page.fields['steps_from'];
			const tc = sf && sf !== 'none' ? testCaseById(ctx.repo, sf) : null;
			for (const d of tc?.testCase.testData ?? []) allowed.add(d.value);
			const seen = new Set<string>();
			for (const s of page.structure.sections) {
				if (s.key !== 'steps' && s.key !== 'problems') continue;
				const text = rawSection(page, s).filter((l) => !/^\s*!\[/.test(l)).join('\n');
				for (const q of quotedStrings(text)) {
					if (!q || allowed.has(q) || seen.has(q)) continue;
					seen.add(q);
					ctx.add('M14', page.path, `"${q}" is not a value of the ${lang} locale files or a Test Data value of ${sf ?? 'steps_from'}`);
				}
			}
		}
	}
}

// ---------------------------------------------------------------- M15
function m15(ctx: Ctx): void {
	const { uses, scanned } = scanContextUses(ctx.root);
	if (!scanned) return;
	const first = new Map<string, string>();
	for (const u of uses) if (!first.has(u.key)) first.set(u.key, u.file);
	for (const [key, file] of first) {
		for (const lang of LANGS) {
			const declaring = ctx.pages[lang].filter(
				(p) => p.kind === 'story' && (parseFlowList(p.fields['contexts'] ?? '') ?? []).includes(key),
			);
			if (declaring.length !== 1) {
				ctx.add('M15', file, `context "${key}" is declared by ${declaring.length} ${lang} story pages (expected exactly 1)${declaring.length ? `: ${declaring.map((p) => p.path).join(', ')}` : ''}`);
			}
		}
	}
}

// ---------------------------------------------------------------- M16
const BENCH_PATH = 'docs/manual/bench.md';

function m16(ctx: Ctx): void {
	const abs = join(ctx.root, BENCH_PATH);
	const bad = (m: string) => ctx.add('M16', BENCH_PATH, m);
	if (!isFile(abs)) {
		bad('file is missing (manual.md 8.2)');
		return;
	}
	const lines = splitLines(readText(abs));
	const heads = lines.map((l, i) => ({ l, i })).filter((x) => /^#{1,6} /.test(x.l));
	const want = ['# Help bench', '## Out of scope', '### en', '### it', '## Extra'];
	if (heads.map((h) => h.l).join('\n') !== want.join('\n')) {
		bad(`headings must be exactly: ${want.join(' / ')}; found: ${heads.map((h) => h.l).join(' / ') || 'none'}`);
		return;
	}
	const block = (k: number) => lines.slice(heads[k].i + 1, k + 1 < heads.length ? heads[k + 1].i : lines.length).filter((l) => l.trim() !== '');
	const stories = allStories(ctx.repo);
	const indexed: Record<Lang, Set<string>> = { en: new Set(), it: new Set() };
	const benchCount: Record<Lang, number> = { en: 0, it: 0 };
	for (const s of stories) {
		for (const lang of LANGS) {
			const sp = splitQuestions(s.id, lang, s.questions[lang]);
			sp.indexed.forEach((q) => indexed[lang].add(q));
			benchCount[lang] += sp.bench.length;
		}
	}
	const extra = block(4);
	const extraRows: { q: string; lang: string; story: string }[] = [];
	if (extra.length === 1 && extra[0].trim() === 'None.') {
		/* empty */
	} else {
		const t = parseTable(extra);
		if (!t || t.header.join('|') !== 'Question|Language|Story') bad('Extra must be a table with the columns Question, Language, Story, or None.');
		else {
			for (const r of t.rows) {
				const q = r[0] ?? '';
				const lang = r[1] ?? '';
				const story = r[2] ?? '';
				if (!/^".+"$/.test(q)) bad(`Extra: the question ${q || '(empty)'} must be in double quotes`);
				if (lang !== 'en' && lang !== 'it') bad(`Extra: Language "${lang}" must be en or it`);
				if (!stories.some((s) => s.id === story)) bad(`Extra: Story "${story}" does not name a story file`);
				const text = normalizeQuestion(stripQuotes(q));
				if ((lang === 'en' || lang === 'it') && indexed[lang].has(text)) bad(`Extra: the question ${q} equals an indexed question of a story`);
				if (lang === 'en' || lang === 'it') extraRows.push({ q: text, lang, story });
			}
		}
	}
	for (const [k, lang] of [[2, 'en'], [3, 'it']] as const) {
		const b = block(k);
		let n = 0;
		if (b.length === 1 && b[0].trim() === 'None.') n = 0;
		else {
			b.forEach((l, i) => {
				const m = /^(\d+)\. (\S.*)$/.exec(l);
				if (!m) bad(`Out of scope / ${lang}: line "${l}" is not a numbered item`);
				else {
					n++;
					if (Number(m[1]) !== i + 1) bad(`Out of scope / ${lang}: item number ${m[1]} should be ${i + 1}`);
				}
			});
		}
		const inScope = benchCount[lang] + extraRows.filter((r) => r.lang === lang).length;
		const need = Math.max(10, Math.ceil(0.2 * inScope));
		if (n < need) bad(`Out of scope / ${lang}: ${n} questions, at least ${need} needed (10, and 20% of ${inScope} in-scope bench questions)`);
	}
}

// ---------------------------------------------------------------- runner
export function runChecks(root: string, opts: CheckOptions = {}): Finding[] {
	const repo = loadRepo(root);
	const findings: Finding[] = [];
	const ctx: Ctx = {
		root,
		repo,
		loc: loadLocales(root),
		pages: { en: loadPages(root, 'en'), it: loadPages(root, 'it') },
		files: manualFiles(root),
		opts,
		add: (id, path, message) =>
			findings.push({ id, path, message, severity: WARNING_IDS.includes(id) ? 'warn' : 'fail' }),
	};
	m01(ctx);
	m02(ctx);
	m03(ctx);
	m04(ctx);
	m05m06(ctx);
	m07(ctx);
	if (!opts.noImages) m08m09(ctx);
	m10(ctx);
	m11(ctx);
	m12(ctx);
	m13(ctx);
	m14(ctx);
	m15(ctx);
	m16(ctx);
	return findings.sort(
		(a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : a.path < b.path ? -1 : a.path > b.path ? 1 : a.message < b.message ? -1 : a.message > b.message ? 1 : 0),
	);
}

export interface Io {
	out: (line: string) => void;
	err: (line: string) => void;
}

export function main(argv: string[], io: Io): number {
	const args = parseArgs(argv, ['--strict', '--root', '--no-images']);
	if (args.errors.length) {
		for (const e of args.errors) io.err(`manual:check: ${e}`);
		io.err('usage: manual:check [--strict] [--root {dir}] [--no-images]');
		return 2;
	}
	const findings = runChecks(args.root, { noImages: args.noImages });
	for (const f of findings) io.out(`${f.id} ${f.path}: ${f.message}`);
	const fails = findings.filter((f) => f.severity === 'fail').length;
	const warns = findings.length - fails;
	io.out(
		`manual:check: ${fails} failure${fails === 1 ? '' : 's'}, ${warns} warning${warns === 1 ? '' : 's'}${args.strict ? ' (strict: warnings fail)' : ''}`,
	);
	return fails > 0 || (args.strict && warns > 0) ? 1 : 0;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
	process.exitCode = main(process.argv.slice(2), { out: (l) => console.log(l), err: (l) => console.error(l) });
}
