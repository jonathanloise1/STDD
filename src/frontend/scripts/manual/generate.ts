/**
 * manual:generate (manual.md 3.1, 3.2): creates missing pages as drafts, refreshes derived front matter,
 * reports stale pages with the source diff, and with --accept marks pages as reviewed.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isFile } from '../lib/fsutil.ts';
import { contractVersion } from '../lib/contracts.ts';
import { loadLocales } from '../lib/locales.ts';
import { imageRefs } from '../lib/markdown.ts';
import { formatFlowList, LANGS, loadPages, splitPage } from '../lib/page.ts';
import type { Lang, ManualPage } from '../lib/page.ts';
import { blobIdOf, findStory, loadRepo, lowestHappyPath } from '../lib/repo.ts';
import type { Repo } from '../lib/repo.ts';
import { parseArgs } from './args.ts';
import { runChecks } from './check.ts';
import { draftAreaPage, draftHomePage, draftStoryPage } from './draft.ts';
import { sourceDiff, staleReasons } from './staleness.ts';

export interface Io {
	out: (line: string) => void;
	err: (line: string) => void;
}

/** Replaces the value of existing front matter lines in place. Returns the keys that have no line. */
export function setFrontMatterValues(text: string, updates: Record<string, string>): { text: string; missing: string[] } {
	const split = splitPage(text);
	if (!split.frontMatter) return { text, missing: Object.keys(updates) };
	const lines = text.split('\n');
	const missing: string[] = [];
	for (const [key, value] of Object.entries(updates)) {
		const entry = split.frontMatter.entries.find((e) => e.key === key);
		if (!entry) missing.push(key);
		else lines[entry.line - 1] = `${key}: ${value}`;
	}
	return { text: lines.join('\n'), missing };
}

/** Values of the derived fields (manual.md 2.3 "Generator"); null when the page cannot be derived. */
export function derivedFields(repo: Repo, page: ManualPage): Record<string, string> | null {
	const base = {
		schema_version: String(contractVersion(repo.root, 'manual-page') ?? 1),
		kind: page.kind as string,
		lang: page.lang,
		screenshots: formatFlowList(imageRefs(page.structure.blocks)),
	};
	if (page.kind === 'home') return { ...base, page_id: 'home', source: 'none', steps_from: 'none' };
	const area = repo.areas.find((a) => a.folder === page.area);
	if (!area) return null;
	if (page.kind === 'area') {
		if (!area.index) return null;
		return { ...base, page_id: area.folder, source: area.indexPath, steps_from: 'none' };
	}
	const story = area.stories.find((s) => s.id === page.storyId);
	if (!story) return null;
	return {
		...base,
		page_id: `${area.folder}/${story.id}`,
		source: story.path,
		steps_from: lowestHappyPath(area, story.id)?.id ?? 'none',
	};
}

function write(root: string, relPath: string, text: string): void {
	const abs = join(root, relPath);
	mkdirSync(dirname(abs), { recursive: true });
	writeFileSync(abs, text);
}

export function generate(root: string, io: Io): number {
	const repo = loadRepo(root);
	const loc = loadLocales(root);
	const counts = { created: 0, updated: 0, stale: 0, orphan: 0 };

	for (const lang of LANGS) {
		const existing = new Map(loadPages(root, lang).map((p) => [p.relInLang, p]));
		const create = (relInLang: string, text: string) => {
			const path = `docs/manual/${lang}/${relInLang}`;
			write(root, path, text);
			io.out(`created ${path}`);
			counts.created++;
		};
		if (!existing.has('index.md')) create('index.md', draftHomePage(repo, lang));
		for (const area of repo.areas) {
			if (!area.index) {
				io.err(`skipped area ${area.folder}: ${area.indexPath} does not exist`);
				continue;
			}
			if (!existing.has(`${area.folder}/index.md`)) create(`${area.folder}/index.md`, draftAreaPage(repo, area, lang));
			for (const story of area.stories) {
				const key = `${area.folder}/${story.id}.md`;
				if (!existing.has(key)) create(key, draftStoryPage(repo, area, story, lang, loc));
			}
			for (const story of area.stories) {
				if (!lowestHappyPath(area, story.id)) {
					io.err(`warning: ${story.id} has no Happy path test case; its page has no Steps section (steps_from is none)`);
				}
			}
		}
		// Existing pages: refresh derived front matter, then report stale and orphan pages.
		for (const page of existing.values()) {
			const derived = derivedFields(repo, page);
			if (!derived) {
				io.out(`no source ${page.path}: its source no longer exists; deleting the page is the author's decision`);
				counts.orphan++;
				continue;
			}
			const res = setFrontMatterValues(page.text, derived);
			if (res.missing.length) {
				io.err(`${page.path}: front matter lacks ${res.missing.join(', ')}; fix by hand (M03)`);
			}
			if (res.text !== page.text) {
				write(root, page.path, res.text);
				io.out(`updated ${page.path}`);
				counts.updated++;
			}
		}
	}
	// Stale report runs on the pages as they are after the refresh.
	for (const lang of LANGS) {
		for (const page of loadPages(root, lang)) {
			const reasons = staleReasons(repo, page);
			if (!reasons.length) continue;
			counts.stale++;
			io.out(`stale ${page.path}`);
			for (const r of reasons) {
				io.out(`  ${r.message}`);
				if (r.what !== 'steps_from') {
					for (const l of sourceDiff(root, r).replace(/\n$/, '').split('\n')) io.out(`  ${l}`);
				}
			}
		}
	}
	io.out(
		`manual:generate: ${counts.created} created, ${counts.updated} updated, ${counts.stale} stale, ${counts.orphan} without source`,
	);
	return 0;
}

/** `--accept`: sets source_hash and steps_hash of the pages of one story or area (manual.md 3.2). */
export function accept(root: string, target: string, io: Io, noImages = false): number {
	const repo = loadRepo(root);
	const storyHit = /^US-/.test(target) ? findStory(repo, target) : null;
	const area = storyHit ? storyHit.area : repo.areas.find((a) => a.folder === target);
	if (!area) {
		io.err(`--accept: "${target}" is neither a story id nor an area folder`);
		return 1;
	}
	const wanted: { lang: Lang; rel: string }[] = [];
	for (const lang of LANGS) {
		if (storyHit) wanted.push({ lang, rel: `${area.folder}/${storyHit.story.id}.md` });
		else {
			wanted.push({ lang, rel: `${area.folder}/index.md` });
			for (const s of area.stories) wanted.push({ lang, rel: `${area.folder}/${s.id}.md` });
		}
	}
	const pages: ManualPage[] = [];
	const problems: string[] = [];
	for (const w of wanted) {
		const p = loadPages(root, w.lang).find((x) => x.relInLang === w.rel);
		if (!p) problems.push(`docs/manual/${w.lang}/${w.rel} does not exist`);
		else pages.push(p);
	}
	for (const p of pages) if (p.structure.hasDraftMarker) problems.push(`${p.path} still has the draft marker`);
	const paths = new Set(pages.map((p) => p.path));
	for (const f of runChecks(root, { noImages })) {
		if (f.id !== 'M05' && f.id !== 'M06' && paths.has(f.path)) problems.push(`${f.id} ${f.path}: ${f.message}`);
	}
	if (problems.length) {
		io.err(`--accept refused, nothing changed:`);
		for (const m of problems) io.err(`  ${m}`);
		return 1;
	}
	for (const page of pages) {
		const derived = derivedFields(repo, page) as Record<string, string>;
		const stepsFrom = derived['steps_from'];
		const stepsPath = stepsFrom === 'none' ? null : area.testCases.find((t) => t.id === stepsFrom)?.path;
		const updates = {
			...derived,
			source_hash: page.kind === 'home' ? 'none' : (blobIdOf(root, derived['source']) ?? 'none'),
			steps_hash: stepsPath ? (blobIdOf(root, stepsPath) ?? 'none') : 'none',
		};
		const res = setFrontMatterValues(page.text, updates);
		if (res.text !== page.text) write(root, page.path, res.text);
		io.out(`accepted ${page.path}`);
	}
	return 0;
}

export function main(argv: string[], io: Io): number {
	const args = parseArgs(argv, ['--accept', '--root', '--no-images']);
	if (args.errors.length) {
		for (const e of args.errors) io.err(`manual:generate: ${e}`);
		io.err('usage: manual:generate [--root {dir}] [--accept {story id or area folder} [--no-images]]');
		return 2;
	}
	if (!isFile(join(args.root, 'docs/contracts.json')) && !isFile(join(args.root, 'docs/guidelines/development.md'))) {
		io.err(`manual:generate: ${args.root} does not look like a repository root (no docs/contracts.json)`);
		return 2;
	}
	return args.accept ? accept(args.root, args.accept, io, args.noImages) : generate(args.root, io);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
	process.exitCode = main(process.argv.slice(2), { out: (l) => console.log(l), err: (l) => console.error(l) });
}
