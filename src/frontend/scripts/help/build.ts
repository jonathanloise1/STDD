/** help:build (manual.md 5.1, 6): manual bundles, help packs and manifest from `docs/`. */
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, posix, resolve } from 'node:path';
import { loadPages, loadRepo, allStories, blobIdOf, contractVersion, isFile, sha256Hex, splitQuestions } from '../lib/index.ts';
import type { Block, Inline, Lang, ManualPage, Repo } from '../lib/index.ts';
import { ANALYZER_VERSION, analyzerId, createAnalyzer } from '../../src/help/lexical/analyzer.ts';
import { buildIndex } from '../../src/help/lexical/indexer.ts';
import { compileGlossary } from '../../src/help/lexical/glossary.ts';
import type { EmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import type { HelpLang, HelpPack, PackQuestion, PackSection, RetrievalParams } from '../../src/help/lexical/types.ts';
import { runChecks } from '../manual/check.ts';
import type { Finding } from '../manual/check.ts';
import { consoleIo, isEntry, parseOptions, rootOf } from './cli.ts';
import type { Io } from './cli.ts';
import { loadBench } from './benchfile.ts';
import { DEFAULT_TABLE_DIR, loadEmbedConfig, resolveTable } from './embeddings.ts';
import { GLOSSARY_PATH, loadGlossary } from './glossaryfile.ts';
import { loadTemplateStopwords, TEMPLATE_ROOT } from './paths.ts';
import { PACK_SCHEMA_VERSION, serializePack } from './pack.ts';
import { pageIdOf, pageSections, parseRoutes } from './sectioning.ts';

export const LANGUAGES: HelpLang[] = ['en', 'it'];
const ASSISTANT_CONTRACTS = ['manual-page', 'help-pack', 'assistant-protocol', 'assistant-retrieval'];

export interface BuildOptions {
	/** Fail on any manual:check finding (manual.md 6.8). */
	strict?: boolean;
	/** Skip the image checks M08 and M09 in strict mode (docs trees without screenshots). */
	noImages?: boolean;
	/** Folder with `{lang}.bin` (manual.md 6.7.4); default `models/embeddings/weights/` of the built root. */
	embeddingsDir?: string;
	/** Use a table that `models/model.lock` does not record (6.7.4); a usage error with `strict`. */
	unlocked?: boolean;
	env?: Record<string, string | undefined>;
}

export interface PublicBlock {
	type: string;
	[key: string]: unknown;
}

export interface BuiltLanguage {
	lang: HelpLang;
	pack: HelpPack;
	packJson: string;
	bundleJson: string;
	/** Image copies: repository-relative source to path below the output directory. */
	images: { from: string; to: string }[];
	/** The embedding table the pack uses (bytes copied to `{lang}/embed.bin`) and its parsed form; null for a lexical-only pack. */
	tableBytes: Buffer | null;
	table: EmbeddingTable | null;
}

export interface BuildResult {
	languages: BuiltLanguage[];
	manifestJson: string;
	findings: Finding[];
	errors: string[];
	warnings: string[];
	/** Information lines: the state of the embedding table of each language (manual.md 6.7.4). */
	notes: string[];
}

function readJsonFile(file: string): { text: string; bytes: Buffer; data: unknown } | null {
	if (!isFile(file)) return null;
	const bytes = readFileSync(file);
	return { text: bytes.toString('utf8'), bytes, data: JSON.parse(bytes.toString('utf8')) };
}

/** Assistant files: from the built root when it has them, otherwise from the template (domain trees). */
function assistantFile(root: string, name: string): ReturnType<typeof readJsonFile> {
	return readJsonFile(join(root, 'training/assistant', name)) ?? readJsonFile(join(TEMPLATE_ROOT, 'training/assistant', name));
}

interface Lock {
	model: { id: string; version: string; protocol_sha256: string; retrieval_sha256: string } | null;
	hasAssistantEntry: boolean;
}

function readLock(root: string): Lock {
	const l = readJsonFile(join(root, 'models/model.lock'));
	const entry = (l?.data as { models?: Record<string, any> } | undefined)?.models?.assistant;
	if (!entry) return { model: null, hasAssistantEntry: false };
	return {
		hasAssistantEntry: true,
		model: {
			id: 'assistant',
			version: String(entry.model_version ?? ''),
			protocol_sha256: String(entry.protocol?.sha256 ?? ''),
			retrieval_sha256: String(entry.retrieval?.sha256 ?? ''),
		},
	};
}

function projectVersion(root: string, env: Record<string, string | undefined>): string {
	if (env.HELP_PROJECT_VERSION) return env.HELP_PROJECT_VERSION;
	const pkg = readJsonFile(join(root, 'src/frontend/package.json'));
	const v = (pkg?.data as { version?: string } | undefined)?.version;
	return v ?? '0.0.0';
}

/** Pages in manual order: home, then per area (registry order) the area page and the story pages in story id order. */
function orderPages(pages: ManualPage[], repo: Repo): ManualPage[] {
	const out: ManualPage[] = [];
	const used = new Set<ManualPage>();
	const take = (p: ManualPage | undefined) => {
		if (p && !used.has(p)) {
			used.add(p);
			out.push(p);
		}
	};
	take(pages.find((p) => p.kind === 'home'));
	for (const a of repo.areas) {
		take(pages.find((p) => p.kind === 'area' && p.area === a.folder));
		for (const s of [...a.stories].sort((x, y) => (x.id < y.id ? -1 : 1))) take(pages.find((p) => p.kind === 'story' && p.storyId === s.id));
	}
	pages.filter((p) => !used.has(p)).sort((x, y) => (x.path < y.path ? -1 : 1)).forEach(take);
	return out;
}

const resolveLink = (pageDir: string, href: string): string => {
	const target = posix.normalize(posix.join(pageDir, href)).replace(/\.md$/, '');
	if (target === 'index') return '/manual';
	if (target.endsWith('/index')) return `/manual/${target.slice(0, -'/index'.length)}`;
	return `/manual/${target}`;
};

function publicBlocks(blocks: Block[], lang: Lang, page: ManualPage): PublicBlock[] {
	const dir = posix.dirname(page.relInLang) === '.' ? '' : posix.dirname(page.relInLang);
	const imgSrc = (src: string) => `/help/${lang}/images/${page.area}/${src.replace(/^images\//, '')}`;
	const inl = (list: Inline[]) => list.map((i) => (i.type === 'link' ? { ...i, href: resolveLink(dir, i.href) } : { ...i }));
	const out: PublicBlock[] = [];
	for (const b of blocks) {
		if (b.type === 'draft') continue;
		if (b.type === 'heading') out.push({ type: 'heading', level: b.level, text: b.text });
		else if (b.type === 'paragraph') out.push({ type: 'paragraph', inlines: inl(b.inlines) });
		else if (b.type === 'image') out.push({ type: 'image', alt: b.alt, src: imgSrc(b.src) });
		else if (b.type === 'table') out.push({ type: 'table', header: b.header.map(inl), rows: b.rows.map((r) => r.map(inl)) });
		else
			out.push({
				type: b.type,
				items: b.items.map((it) => ({
					inlines: inl(it.inlines),
					number: it.number,
					image: it.image ? { alt: it.image.alt, src: imgSrc(it.image.src) } : null,
				})),
			});
	}
	return out;
}

function imagesOf(blocks: Block[]): string[] {
	const out: string[] = [];
	for (const b of blocks) {
		if (b.type === 'image') out.push(b.src);
		else if (b.type === 'ordered' || b.type === 'bullets') for (const it of b.items) if (it.image) out.push(it.image.src);
	}
	return out;
}

export function buildHelp(root: string, opts: BuildOptions = {}): BuildResult {
	const env = opts.env ?? process.env;
	const errors: string[] = [];
	const warnings: string[] = [];
	const notes: string[] = [];
	const repo = loadRepo(root);
	const findings = opts.strict ? runChecks(root, { noImages: opts.noImages }) : [];
	if (findings.length) errors.push(`manual:check reports ${findings.length} finding${findings.length === 1 ? '' : 's'} (strict mode)`);

	const protocol = assistantFile(root, 'protocol.json');
	const retrieval = assistantFile(root, 'retrieval.json');
	if (!protocol || !retrieval) {
		errors.push('training/assistant/protocol.json and retrieval.json are required');
		return { languages: [], manifestJson: '', findings, errors, warnings, notes };
	}
	const lock = readLock(root);
	const compatible =
		lock.model !== null && lock.model.protocol_sha256 === sha256Hex(protocol.bytes) && lock.model.retrieval_sha256 === sha256Hex(retrieval.bytes);
	if (opts.strict && lock.hasAssistantEntry && !compatible) {
		errors.push('assistant.compatible is false while models/model.lock has an assistant entry (protocol or retrieval file changed since the lock)');
	}
	const contracts: Record<string, number> = {};
	for (const k of ASSISTANT_CONTRACTS) contracts[k] = contractVersion(root, k) ?? 1;
	const schemaVersion = contracts['help-pack'] ?? PACK_SCHEMA_VERSION;
	const retrievalParams = retrieval.data as RetrievalParams;
	const embedConfig = loadEmbedConfig(root);
	const tableDir = resolve(opts.embeddingsDir ?? join(root, DEFAULT_TABLE_DIR));
	const version = projectVersion(root, env);
	const bench = loadBench(root);
	const glossaryFile = loadGlossary(root);
	errors.push(...glossaryFile.errors);
	const stories = allStories(repo);
	const languages: BuiltLanguage[] = [];

	for (const lang of LANGUAGES) {
		const pages = orderPages(loadPages(root, lang), repo);
		if (!pages.length) {
			errors.push(`no manual pages for ${lang} under docs/manual/${lang}/`);
			continue;
		}
		const sections: PackSection[] = [];
		for (const p of pages) {
			try {
				sections.push(...pageSections(p, lang));
			} catch (e) {
				errors.push((e as Error).message);
			}
		}
		const home = pages.find((p) => p.kind === 'home');
		const name = home?.structure.h1?.text ?? repo.productName;

		// question split (6.2)
		const indexedByStory = new Map<string, string[]>();
		const questions: PackQuestion[] = [];
		const benchQuestions: { text: string; origin: string }[] = [];
		for (const s of stories) {
			const sp = splitQuestions(s.id, lang, s.questions[lang]);
			indexedByStory.set(s.id, sp.indexed);
			for (const q of sp.indexed) questions.push({ story: s.id, text: q });
			for (const q of sp.bench) benchQuestions.push({ text: q, origin: `bench question of ${s.id}` });
		}
		for (const q of bench.oos[lang]) benchQuestions.push({ text: q, origin: 'out-of-scope question of docs/manual/bench.md' });
		for (const x of bench.extra) if (x.lang === lang) benchQuestions.push({ text: x.question, origin: `extra question of docs/manual/bench.md (${x.story})` });

		const analyzerWords = loadTemplateStopwords(lang);
		const analyzer = createAnalyzer(lang, analyzerWords.words);
		const glossary = compileGlossary(glossaryFile.rows[lang], analyzer);
		if (glossary.length < glossaryFile.rows[lang].length) {
			errors.push(`${GLOSSARY_PATH} (${lang}): ${glossaryFile.rows[lang].length - glossary.length} row(s) whose term or aliases are only stopwords`);
		}
		const index = buildIndex(
			sections.map((s) => ({ title: s.title, text: s.text, questions: s.story ? (indexedByStory.get(s.story) ?? []) : [] })),
			analyzer,
			glossary,
		);

		const inputs = [
			...pages.map((p) => p.path),
			...stories.map((s) => s.path),
			...(isFile(join(root, 'docs/contracts.json')) ? ['docs/contracts.json'] : []),
			...(glossaryFile.present ? [GLOSSARY_PATH] : []),
		]
			.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0))
			.map((p) => `${p} ${blobIdOf(root, p)}`);

		const pack: HelpPack = {
			schema_version: schemaVersion,
			kind: 'help-pack',
			lang,
			project: { name, version },
			contracts,
			build: { commit: env.HELP_BUILD_COMMIT || null, inputs_sha256: sha256Hex(inputs.join('\n')) },
			assistant: {
				protocol: { sha256: sha256Hex(protocol.bytes), content: protocol.data as HelpPack['assistant']['protocol']['content'] },
				retrieval: { sha256: sha256Hex(retrieval.bytes), content: retrievalParams },
				model: lock.model,
				compatible,
			},
			analyzer: { id: analyzerId(lang), version: ANALYZER_VERSION, stopwords_sha256: analyzerWords.sha256 },
			sections,
			questions,
			index,
			embeddings: null,
		};

		// the embedding table (6.7.4): the state decides whether the pack carries the block
		const tableState = resolveTable({ root, lang, pack, dir: tableDir, unlocked: Boolean(opts.unlocked), config: embedConfig.data });
		if (tableState.message) {
			const strictFails = tableState.strictFailure && opts.strict;
			if (strictFails) errors.push(`${tableState.message} (strict mode)`);
			else if (tableState.warning) warnings.push(tableState.message);
			else notes.push(tableState.message);
		}
		pack.embeddings = tableState.block;

		errors.push(...findLeaks(pack, benchQuestions, lang));

		// bundle (5.1)
		const routes: Record<string, string[]> = {};
		const contextsMap: Record<string, string> = {};
		const images: { from: string; to: string }[] = [];
		const bundlePages = pages.map((p) => {
			const pageId = pageIdOf(p);
			const pageRoutes = p.kind === 'story' ? parseRoutes(p.fields.routes) : [];
			const pageContexts = p.kind === 'story' ? parseRoutes(p.fields.contexts) : [];
			for (const r of pageRoutes) (routes[r] ??= []).push(pageId);
			for (const c of pageContexts) if (!(c in contextsMap)) contextsMap[c] = pageId;
			for (const src of imagesOf(p.structure.blocks)) {
				const file = src.replace(/^images\//, '');
				const from = `docs/manual/${lang}/${p.area}/images/${file}`;
				if (isFile(join(root, from))) images.push({ from, to: `${lang}/images/${p.area}/${file}` });
				else warnings.push(`${p.path}: image ${src} not found`);
			}
			return {
				page_id: pageId,
				kind: p.kind,
				area: p.area,
				title: p.structure.h1?.text ?? '',
				source_id: p.kind === 'story' ? p.storyId : p.kind === 'area' ? p.area : null,
				routes: pageRoutes,
				contexts: pageContexts,
				blocks: publicBlocks(p.structure.blocks, lang, p),
			};
		});
		const bundle = { schema_version: schemaVersion, lang, project_version: version, pages: bundlePages, routes, contexts: contextsMap };
		const uniqueImages = [...new Map(images.map((i) => [i.to, i])).values()];
		languages.push({ lang, pack, packJson: serializePack(pack), bundleJson: JSON.stringify(bundle), images: uniqueImages, tableBytes: tableState.bytes && tableState.used ? tableState.bytes : null, table: tableState.used ? tableState.table : null });
	}

	let manifestJson = '';
	if (languages.length === LANGUAGES.length) {
		const entry = (file: string, data: string | Buffer) => ({ file, sha256: sha256Hex(data) });
		const manifest = {
			schema_version: schemaVersion,
			project_version: version,
			languages: LANGUAGES,
			packs: Object.fromEntries(languages.map((l) => [l.lang, entry(`${l.lang}/pack.json`, l.packJson)])),
			bundles: Object.fromEntries(languages.map((l) => [l.lang, entry(`${l.lang}/manual.json`, l.bundleJson)])),
			embeddings: Object.fromEntries(languages.map((l) => [l.lang, l.tableBytes ? entry(`${l.lang}/embed.bin`, l.tableBytes) : null])),
			assistant_compatible: languages.every((l) => l.pack.assistant.compatible),
		};
		manifestJson = JSON.stringify(manifest);
	}
	return { languages, manifestJson, findings, errors, warnings, notes };
}

/** The leak guard of manual.md 6.2: a bench question equal to an indexed question or inside any section text. */
export function findLeaks(pack: HelpPack, bench: { text: string; origin: string }[], lang: string): string[] {
	const indexed = new Map(pack.questions.map((q) => [q.text.toLowerCase(), q.story]));
	const texts = pack.sections.map((s) => ({ id: s.id, text: `${s.title}\n${s.text}`.toLowerCase() }));
	const out: string[] = [];
	for (const b of bench) {
		const q = b.text.toLowerCase();
		if (!q) continue;
		const eq = indexed.get(q);
		if (eq !== undefined) out.push(`bench leak (${lang}): the ${b.origin} "${b.text}" equals an indexed question of ${eq}`);
		const inside = texts.find((t) => t.text.includes(q));
		if (inside) out.push(`bench leak (${lang}): the ${b.origin} "${b.text}" appears in section ${inside.id}`);
	}
	return out;
}

/** Writes manifest, bundles, packs and images below `outDir`. */
export function writeHelp(root: string, result: BuildResult, outDir: string): void {
	mkdirSync(outDir, { recursive: true });
	writeFileSync(join(outDir, 'manifest.json'), result.manifestJson);
	for (const l of result.languages) {
		mkdirSync(join(outDir, l.lang), { recursive: true });
		writeFileSync(join(outDir, l.lang, 'pack.json'), l.packJson);
		writeFileSync(join(outDir, l.lang, 'manual.json'), l.bundleJson);
		if (l.tableBytes) writeFileSync(join(outDir, l.lang, 'embed.bin'), l.tableBytes);
		else rmSync(join(outDir, l.lang, 'embed.bin'), { force: true });
		for (const img of l.images) {
			const dest = join(outDir, img.to);
			mkdirSync(dirname(dest), { recursive: true });
			copyFileSync(join(root, img.from), dest);
		}
	}
}

export function main(argv: string[], io: Io): number {
	const o = parseOptions(argv, { '--strict': 'flag', '--no-images': 'flag', '--root': 'value', '--out': 'value', '--embeddings': 'value', '--unlocked': 'flag' });
	if (o.flags.has('--strict') && o.flags.has('--unlocked')) o.errors.push('--strict cannot be used with --unlocked (manual.md 6.7.4)');
	if (o.errors.length) {
		for (const e of o.errors) io.err(`help:build: ${e}`);
		io.err('usage: help:build [--strict] [--no-images] [--root {dir}] [--out {dir}] [--embeddings {dir} [--unlocked]]');
		return 2;
	}
	const root = rootOf(o);
	const out = o.values.get('--out') ?? join(root, 'src/frontend/public/help');
	const embeddingsDir = o.values.get('--embeddings');
	const result = buildHelp(root, {
		strict: o.flags.has('--strict'),
		noImages: o.flags.has('--no-images'),
		embeddingsDir: embeddingsDir ? resolve(embeddingsDir) : undefined,
		unlocked: o.flags.has('--unlocked'),
	});
	for (const f of result.findings) io.err(`${f.id} ${f.path}: ${f.message}`);
	for (const n of result.notes) io.out(`help:build: ${n}`);
	for (const w of result.warnings) io.err(`help:build: warning: ${w}`);
	if (result.errors.length) {
		for (const e of result.errors) io.err(`help:build: ${e}`);
		return 1;
	}
	writeHelp(root, result, out);
	for (const l of result.languages) io.out(`help:build: ${l.lang}: ${l.pack.sections.length} sections, ${l.pack.questions.length} indexed questions, ${Buffer.byteLength(l.packJson)} bytes`);
	io.out(`help:build: wrote ${out} (assistant compatible: ${result.languages.every((l) => l.pack.assistant.compatible)})`);
	return 0;
}

if (isEntry(import.meta.url)) process.exitCode = main(process.argv.slice(2), consoleIo);
