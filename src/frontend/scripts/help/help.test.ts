// @vitest-environment node
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { splitQuestions as libSplit } from '../lib/index.ts';
import { parseMarkdown as libParse } from '../lib/index.ts';
import { parseMarkdown } from '../../src/help/markdown/index.ts';
import { splitQuestions } from '../../src/help/bench/split.ts';
import { ANALYZER_VERSION, createAnalyzer, tokenize } from '../../src/help/lexical/analyzer.ts';
import type { HelpPack } from '../../src/help/lexical/types.ts';
import { runChecks } from '../manual/check.ts';
import { capture, cleanup as cleanupRepos, replaceIn, tmpRepo } from '../manual/testutil.ts';
import { parseBench } from './benchfile.ts';
import { parseGlossary } from './glossaryfile.ts';
import { main as benchMain, benchItems, runBench, runIndexed } from './bench.ts';
import { buildHelp, findLeaks, main as buildMain, writeHelp } from './build.ts';
import { buildEmbeddingsGolden, buildGolden, EMBEDDINGS_GOLDEN_PATH, GOLDEN_PATH, serializeEmbeddingsGolden, serializeGolden, TABLE_GOLDEN_PATH } from './golden.ts';
import type { EmbeddingsGolden, Golden } from './golden.ts';
import { calibrateLanguage, comparePairs, cosineGrid, guardReasons, metricsOf, runCalibrate, sliceItems } from './calibrate.ts';
import { loadEmbedConfig } from './embeddings.ts';
import { compileGlossary } from '../../src/help/lexical/glossary.ts';
import { vocabCoverage, readEmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import { clusterGaps, jaccard, main as gapsMain, validRecord } from './gaps.ts';
import type { GapRecord } from './gaps.ts';
import { loadPack, loadPackFile, serializePack } from './pack.ts';
import { loadTemplateStopwords, TEMPLATE_ROOT } from './paths.ts';
import { main as retrieveMain } from './retrieve.ts';
import { splitItems } from './sectioning.ts';

const temps: string[] = [];
const tmp = (): string => {
	const d = mkdtempSync(join(tmpdir(), 'help-test-'));
	temps.push(d);
	return d;
};
afterEach(() => {
	cleanupRepos();
	for (const d of temps.splice(0)) rmSync(d, { recursive: true, force: true });
});

const EN1 = 'docs/manual/en/tasks/US-TASK-01.md';
const sha = (b: string | Buffer) => createHash('sha256').update(b).digest('hex');
const build = (root: string, opts = {}) => {
	const r = buildHelp(root, { env: {}, ...opts });
	expect(r.errors).toEqual([]);
	return r;
};
const packOf = (root: string, lang: 'en' | 'it' = 'en'): HelpPack => JSON.parse(build(root).languages.find((l) => l.lang === lang)!.packJson);

describe('the parser and the split live in src/help and scripts/lib re-exports them', () => {
	it('re-exports the same functions', () => {
		expect(libParse).toBe(parseMarkdown);
		expect(libSplit).toBe(splitQuestions);
	});
});

describe('golden file shared with the Python port', () => {
	it('is up to date: regenerate with scripts/help/golden.ts --write', () => {
		expect(readFileSync(GOLDEN_PATH, 'utf8')).toBe(serializeGolden(buildGolden()));
	});
	it('matches the stopword lists and the analyzer for every case', () => {
		const g = JSON.parse(readFileSync(GOLDEN_PATH, 'utf8')) as Golden;
		for (const lang of ['en', 'it'] as const) {
			expect(g.stopwords_sha256[lang]).toBe(loadTemplateStopwords(lang).sha256);
		}
		expect(g.analyze.length).toBeGreaterThan(20);
		for (const a of g.analyze) {
			const sw = loadTemplateStopwords(a.lang as 'en' | 'it').words;
			expect(tokenize(a.text, sw)).toEqual(a.tokens);
			expect(createAnalyzer(a.lang as 'en' | 'it', sw).analyze(a.text)).toEqual(a.stems);
		}
	});
	it('matches the retrieval parameters of training/assistant/retrieval.json', () => {
		const g = JSON.parse(readFileSync(GOLDEN_PATH, 'utf8')) as Golden;
		expect(g.retrieval).toEqual(JSON.parse(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json'), 'utf8')));
	});
});

describe('help:build, the pack (manual.md 6)', () => {
	it('builds both languages from the fixture with the exact top-level key order', () => {
		const pack = packOf(tmpRepo());
		expect(Object.keys(pack)).toEqual(['schema_version', 'kind', 'lang', 'project', 'contracts', 'build', 'assistant', 'analyzer', 'sections', 'questions', 'index', 'embeddings']);
		expect(Object.keys(pack.assistant)).toEqual(['protocol', 'retrieval', 'model', 'compatible']);
		expect(Object.keys(pack.sections[0])).toEqual(['id', 'page_id', 'key', 'area', 'story', 'title', 'text', 'url', 'routes']);
		expect(Object.keys(pack.index)).toEqual(['fields', 'doc_count', 'avg_length', 'lengths', 'terms']);
		expect(pack).toMatchObject({ kind: 'help-pack', schema_version: 1, lang: 'en', embeddings: null });
		expect(pack.project.name).toBe('MyApp');
		expect(pack.analyzer).toEqual({ id: 'snowball-en', version: ANALYZER_VERSION, stopwords_sha256: loadTemplateStopwords('en').sha256 });
		expect(pack.contracts).toEqual({ 'manual-page': 1, 'help-pack': 1, 'assistant-protocol': 1, 'assistant-retrieval': 1 });
	});
	it('makes one section per H1 introduction and H2 section except related, with ids, titles, urls and routes', () => {
		const pack = packOf(tmpRepo());
		const ids = pack.sections.map((s) => s.id);
		expect(ids.filter((i) => i.startsWith('tasks/US-TASK-01#'))).toEqual(['tasks/US-TASK-01#overview', 'tasks/US-TASK-01#before', 'tasks/US-TASK-01#steps', 'tasks/US-TASK-01#result', 'tasks/US-TASK-01#rules', 'tasks/US-TASK-01#problems']);
		expect(ids[0]).toBe('home#overview');
		expect(ids).toContain('tasks#overview');
		expect(ids.some((i) => i.endsWith('#related'))).toBe(false);
		const steps = pack.sections.find((s) => s.id === 'tasks/US-TASK-01#steps')!;
		expect(steps).toMatchObject({ page_id: 'tasks/US-TASK-01', key: 'steps', area: 'tasks', story: 'US-TASK-01', title: 'Create a task - Steps', url: '/manual/tasks/US-TASK-01#steps', routes: ['/tasks'] });
		expect(pack.sections.find((s) => s.id === 'tasks/US-TASK-01#overview')!.title).toBe('Create a task');
		expect(pack.sections.find((s) => s.id === 'home#overview')).toMatchObject({ story: null, area: null, url: '/manual#overview', routes: [] });
		expect(pack.sections.find((s) => s.id === 'tasks#overview')).toMatchObject({ story: null, area: 'tasks', url: '/manual/tasks#overview' });
	});
	it('renders plain text: numbered items, bullets, table rows, no markup, image alt kept', () => {
		const pack = packOf(tmpRepo());
		const text = (id: string) => pack.sections.find((s) => s.id === id)!.text;
		expect(text('tasks/US-TASK-01#steps')).toBe(
			[
				'1. Open the Tasks page.',
				'2. Click "New Task".',
				'The New Task form above the list of tasks',
				'3. In the "Title" field, type a value such as "Prepare the quarterly report".',
				'4. In the "Status" field, choose "Done".',
				'5. Click "Create Task".',
			].join('\n'),
		);
		expect(text('tasks/US-TASK-01#before')).toBe('- Only Admin and Editor can do this.');
		const problems = text('tasks/US-TASK-01#problems');
		expect(problems).toContain('What you see: "Your role does not allow this change.". What to do: Ask an admin');
		expect(problems).toContain('What to do: Type a title in the Title field.');
		for (const s of pack.sections) {
			expect(s.text).not.toMatch(/\*\*|\]\(|!\[|[ \t]$/m);
			expect(s.text).toBe(s.text.normalize('NFC'));
			expect([...s.text].length).toBeLessThanOrEqual(1200);
		}
	});
	it('puts only indexed questions in the pack, never the bench questions', () => {
		const root = tmpRepo();
		const pack = packOf(root);
		const expectedIndexed = [...splitQuestions('US-TASK-01', 'en', [
			"How do I add something new to the team's to-do list?",
			"Why won't the form let me save when the name is blank?",
			"I can't find the button to add work. What am I missing?",
			'Can I set a deadline when I make a new item?',
		]).indexed];
		expect(pack.questions.filter((q) => q.story === 'US-TASK-01').map((q) => q.text)).toEqual(expectedIndexed);
		expect(pack.questions).toHaveLength(2 + 2);
		const bench = benchItems(root, 'en').filter((i) => i.source === 'story').map((i) => i.question);
		expect(bench).toHaveLength(3);
		const inPack = new Set(pack.questions.map((q) => q.text));
		for (const q of bench) expect(inPack.has(q)).toBe(false);
		expect(JSON.stringify(pack.sections)).not.toContain(bench[0]);
	});
	it('indexes questions in the questions field of every section of the story page', () => {
		const pack = packOf(tmpRepo());
		const i = pack.sections.findIndex((s) => s.id === 'tasks/US-TASK-01#steps');
		const home = pack.sections.findIndex((s) => s.id === 'home#overview');
		expect(pack.index.lengths[i][2]).toBeGreaterThan(0);
		expect(pack.index.lengths[home][2]).toBe(0);
		expect(pack.index.doc_count).toBe(pack.sections.length);
		expect(pack.index.lengths).toHaveLength(pack.sections.length);
	});
	it('writes index.terms in code point order, integer-like stems included', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, '- The first row of the list shows the new task.', '- The first row of the list shows the new task for 2024 and 7.');
		const json = build(root).languages[0].packJson;
		const terms = Object.keys(JSON.parse(json).index.terms);
		expect(terms).toContain('2024');
		const order = [...json.matchAll(/"terms":\{(.*)\},"embeddings"/gs)][0][1].match(/"([^"]+)":\[\d+,/g)!.map((m) => m.slice(1, m.indexOf('":')));
		expect(order).toEqual([...terms].sort());
		expect(order.indexOf('2024')).toBeLessThan(order.indexOf('add'));
		expect(serializePack(JSON.parse(json))).toBe(json);
	});
	it('is compact and byte-identical on two builds, including the written files', () => {
		const root = tmpRepo();
		const a = tmp();
		const b = tmp();
		writeHelp(root, build(root), a);
		writeHelp(root, build(root), b);
		for (const f of ['manifest.json', 'en/pack.json', 'it/pack.json', 'en/manual.json', 'it/manual.json', 'en/images/tasks/task-list-new-task-form-open.png']) {
			expect(readFileSync(join(a, f)).equals(readFileSync(join(b, f)))).toBe(true);
		}
		const json = readFileSync(join(a, 'en/pack.json'), 'utf8');
		expect(json).not.toMatch(/\n/);
		expect(json).not.toMatch(/": /);
	});
	it('takes the version and commit from the environment, else from package.json, never from git', () => {
		const root = tmpRepo();
		expect(packOf(root).project.version).toBe('0.0.0');
		expect(packOf(root).build.commit).toBeNull();
		mkdirSync(join(root, 'src/frontend'), { recursive: true });
		writeFileSync(join(root, 'src/frontend/package.json'), '{"version":"2.3.4"}');
		expect(packOf(root).project.version).toBe('2.3.4');
		const r = buildHelp(root, { env: { HELP_PROJECT_VERSION: '9.9.9', HELP_BUILD_COMMIT: 'abc123' } });
		const p: HelpPack = JSON.parse(r.languages[0].packJson);
		expect(p.project.version).toBe('9.9.9');
		expect(p.build.commit).toBe('abc123');
		expect(JSON.parse(r.manifestJson).project_version).toBe('9.9.9');
	});
	it('computes inputs_sha256 from the blob ids of the inputs, sorted by path', () => {
		const root = tmpRepo();
		const before = packOf(root).build.inputs_sha256;
		expect(before).toMatch(/^[0-9a-f]{64}$/);
		replaceIn(root, EN1, 'Use this page to add a task', 'Use this page to create a task');
		expect(packOf(root).build.inputs_sha256).not.toBe(before);
	});
	it('copies the protocol and retrieval files with their hashes', () => {
		const pack = packOf(tmpRepo());
		const protocolBytes = readFileSync(join(TEMPLATE_ROOT, 'training/assistant/protocol.json'));
		const retrievalBytes = readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json'));
		expect(pack.assistant.protocol.sha256).toBe(sha(protocolBytes));
		expect(pack.assistant.retrieval.sha256).toBe(sha(retrievalBytes));
		expect(pack.assistant.protocol.content).toEqual(JSON.parse(protocolBytes.toString()));
		expect(pack.assistant.retrieval.content).toEqual(JSON.parse(retrievalBytes.toString()));
		expect(pack.assistant.model).toBeNull();
		expect(pack.assistant.compatible).toBe(false);
	});
	it('is compatible when the lock carries the current hashes, incompatible (and fatal in strict mode) when it does not', () => {
		const root = tmpRepo();
		const protocol = sha(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/protocol.json')));
		const retrieval = sha(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json')));
		const lock = (p: string) => JSON.stringify({ schema_version: 1, models: { assistant: { model_version: '1.0.0', protocol: { sha256: p }, retrieval: { sha256: retrieval } } } });
		mkdirSync(join(root, 'models'), { recursive: true });
		writeFileSync(join(root, 'models/model.lock'), lock(protocol));
		const ok = packOf(root);
		expect(ok.assistant.compatible).toBe(true);
		expect(ok.assistant.model).toEqual({ id: 'assistant', version: '1.0.0', protocol_sha256: protocol, retrieval_sha256: retrieval });
		expect(JSON.parse(build(root).manifestJson).assistant_compatible).toBe(true);
		writeFileSync(join(root, 'models/model.lock'), lock('0'.repeat(64)));
		const lenient = buildHelp(root, { env: {} });
		expect(lenient.errors).toEqual([]);
		expect(JSON.parse(lenient.languages[0].packJson).assistant.compatible).toBe(false);
		expect(buildHelp(root, { env: {}, strict: true }).errors.join('\n')).toContain('assistant.compatible is false');
	});
	it('writes the manifest with the hashes of the files', () => {
		const root = tmpRepo();
		const out = tmp();
		const r = build(root);
		writeHelp(root, r, out);
		const m = JSON.parse(readFileSync(join(out, 'manifest.json'), 'utf8'));
		expect(Object.keys(m)).toEqual(['schema_version', 'project_version', 'languages', 'packs', 'bundles', 'embeddings', 'assistant_compatible']);
		expect(m.languages).toEqual(['en', 'it']);
		for (const l of ['en', 'it']) {
			expect(m.packs[l]).toEqual({ file: `${l}/pack.json`, sha256: sha(readFileSync(join(out, l, 'pack.json'))) });
			expect(m.bundles[l]).toEqual({ file: `${l}/manual.json`, sha256: sha(readFileSync(join(out, l, 'manual.json'))) });
		}
	});
	it('writes the manual bundle: pages, routes, contexts, rewritten links and image sources', () => {
		const root = tmpRepo();
		const bundle = JSON.parse(build(root).languages[0].bundleJson);
		expect(Object.keys(bundle)).toEqual(['schema_version', 'lang', 'project_version', 'pages', 'routes', 'contexts']);
		expect(bundle.pages.map((p: { page_id: string }) => p.page_id)).toEqual(['home', 'tasks', 'tasks/US-TASK-01', 'tasks/US-TASK-02']);
		expect(bundle.routes).toEqual({ '/tasks': ['tasks/US-TASK-01'], '/tasks/:id': ['tasks/US-TASK-02'] });
		expect(bundle.contexts).toEqual({ 'task-form': 'tasks/US-TASK-01' });
		const p = bundle.pages[2];
		expect(p).toMatchObject({ kind: 'story', area: 'tasks', title: 'Create a task', source_id: 'US-TASK-01', routes: ['/tasks'], contexts: ['task-form'] });
		expect(Object.keys(p)).toEqual(['page_id', 'kind', 'area', 'title', 'source_id', 'routes', 'contexts', 'blocks']);
		const json = JSON.stringify(p.blocks);
		expect(json).toContain('/help/en/images/tasks/task-list-new-task-form-open.png');
		expect(json).toContain('"href":"/manual/tasks/US-TASK-02"');
		expect(json).not.toContain('"line"');
		expect(bundle.pages[0]).toMatchObject({ kind: 'home', area: null, source_id: null });
	});
	it('copies the images that pages reference', () => {
		const root = tmpRepo();
		const out = tmp();
		writeHelp(root, build(root), out);
		expect(existsSync(join(out, 'en/images/tasks/task-list-new-task-form-open.png'))).toBe(true);
		expect(existsSync(join(out, 'it/images/tasks/task-list-new-task-form-open.png'))).toBe(true);
	});
	it('splits a long section at item boundaries with ids, titles and continuing numbers', () => {
		const root = tmpRepo();
		const bullets = Array.from({ length: 7 }, (_, i) => `- Rule number ${i + 1}: ${'detail '.repeat(40).trim()}.`).join('\n');
		replaceIn(root, EN1, '- A new task always starts with status "Todo".', `${bullets}\n- A new task always starts with status "Todo".`);
		const pack = packOf(root);
		const parts = pack.sections.filter((s) => s.id.startsWith('tasks/US-TASK-01#rules'));
		expect(parts.map((p) => p.id)).toEqual(['tasks/US-TASK-01#rules', 'tasks/US-TASK-01#rules-2']);
		expect(parts[0].title).toBe('Create a task - Good to know');
		expect(parts[1].title).toBe('Create a task - Good to know (2)');
		expect(parts[1].url).toBe('/manual/tasks/US-TASK-01#rules');
		for (const p of parts) expect([...p.text].length).toBeLessThanOrEqual(1200);
		expect(parts.map((p) => p.text).join('\n')).toContain('- Rule number 7');
		expect(parts[1].text).toContain('A new task always starts');
	});
	it('fails the build when a single item is longer than 1,200 characters', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, '- Only Admin and Editor can do this.', `- ${'word '.repeat(300)}`);
		const r = buildHelp(root, { env: {} });
		expect(r.errors.join('\n')).toMatch(/US-TASK-01\.md \(before\): a single item is longer than 1200/);
		expect(buildMain(['--root', root, '--out', tmp()], capture().io)).toBe(1);
	});
	it('splitItems keeps numbering and refuses oversize items', () => {
		const items = Array.from({ length: 5 }, (_, i) => `${i + 1}. ${'x'.repeat(250)}`);
		const parts = splitItems(items);
		expect(parts).toHaveLength(2);
		expect(parts[1].startsWith('5. ')).toBe(true);
		expect(() => splitItems(['y'.repeat(1201)])).toThrow();
		expect(splitItems(['y'.repeat(1200)])).toHaveLength(1);
	});
});

describe('the glossary (manual.md 6.9)', () => {
	const GLOSSARY = '# Help glossary\n\n## en\n\n| Term | Users also say |\n|------|----------------|\n| task | job, chore |\n\n## it\n\n| Term | Users also say |\n|------|----------------|\n| attività | lavoro |\n';
	it('parses one table per language and reports format errors', () => {
		const g = parseGlossary(GLOSSARY);
		expect(g.errors).toEqual([]);
		expect(g.rows.en).toEqual([{ term: 'task', aliases: ['job', 'chore'] }]);
		expect(g.rows.it).toEqual([{ term: 'attività', aliases: ['lavoro'] }]);
		expect(parseGlossary('## fr\n').errors[0]).toMatch(/unknown section/);
		expect(parseGlossary('## en\n\n| Word | Synonyms |\n|---|---|\n| a | b |\n').errors[0]).toMatch(/table header/);
		expect(parseGlossary('## en\n\n| Term | Users also say |\n|---|---|\n| task |  |\n').errors[0]).toMatch(/at least one alias/);
	});
	it('adds the aliases to the sections that use the term, and is an input of the pack', () => {
		const root = tmpRepo();
		const before = packOf(root);
		expect(before.index.terms.chore).toBeUndefined();
		writeFileSync(join(root, 'docs/manual/glossary.md'), GLOSSARY);
		const pack = packOf(root);
		const docs = new Set(pack.index.terms.chore.slice(1).map((p) => (p as number[])[0]));
		const withTask = new Set(pack.index.terms.task.slice(1).filter((p) => (p as number[])[1] < 2).map((p) => (p as number[])[0]));
		expect(docs).toEqual(withTask);
		expect(pack.index.lengths).toEqual(before.index.lengths);
		expect(pack.build.inputs_sha256).not.toBe(before.build.inputs_sha256);
		expect(packOf(root, 'it').index.terms.lavor).toBeDefined();
	});
	it('fails the build on a malformed glossary or a row of stopwords only', () => {
		const root = tmpRepo();
		writeFileSync(join(root, 'docs/manual/glossary.md'), '## en\n\n| Term | Users also say |\n|---|---|\n| the | of |\n');
		expect(buildHelp(root, { env: {} }).errors.join('\n')).toMatch(/only stopwords/);
	});
});

describe('help:bench --indexed (manual.md 6.9)', () => {
	it('asks every indexed question against an index without it, and never gates', () => {
		const root = tmpRepo();
		const run = runIndexed(root);
		expect(run.errors).toEqual([]);
		const pack = packOf(root);
		expect(run.results.en.map((r) => r.question)).toEqual(pack.questions.map((q) => q.text));
		expect(run.results.en.every((r) => r.source === 'indexed' && r.kind === 'in_scope')).toBe(true);
		const c = capture();
		expect(benchMain(['--root', root, '--indexed'], c.io)).toBe(0);
		expect(c.out.join('\n')).toMatch(/no gate applies/);
	});
});

describe('the leak guard (manual.md 6.2)', () => {
	it('passes on the fixture', () => {
		expect(buildHelp(tmpRepo(), { env: {} }).errors).toEqual([]);
	});
	it('fires when a bench question appears inside a section text and stops the build', () => {
		const root = tmpRepo();
		const q = benchItems(root, 'en').find((i) => i.story === 'US-TASK-01' && i.source === 'story')!.question;
		replaceIn(root, EN1, 'Use this page to add a task', `Use this page to add a task. ${q}`);
		const r = buildHelp(root, { env: {} });
		expect(r.errors.some((e) => e.includes('bench leak (en)') && e.includes(q) && e.includes('appears in section tasks/US-TASK-01#overview'))).toBe(true);
		const c = capture();
		const out = tmp();
		expect(buildMain(['--root', root, '--out', out], c.io)).toBe(1);
		expect(c.err.join('\n')).toContain('bench leak');
		expect(existsSync(join(out, 'en/pack.json'))).toBe(false);
	});
	it('fires when a bench question equals an indexed question (a question written twice in a story)', () => {
		const root = tmpRepo();
		const story = 'docs/areas/tasks/user-stories/US-TASK-02.md';
		const qs = ['How do I rename a task?', 'Can I fix a typo in a task?', 'Where do I change the name of an item?'];
		// the second-lowest key, written twice, lands once in the bench set and once in the indexed set
		const second = [...qs].sort((a, b) => (sha(`US-TASK-02\nen\n${a}`) < sha(`US-TASK-02\nen\n${b}`) ? -1 : 1))[1];
		const sp = splitQuestions('US-TASK-02', 'en', [...qs, second]);
		expect(sp.bench).toContain(second);
		expect(sp.indexed).toContain(second);
		replaceIn(root, story, '3. Where do I change the name of an item?', `3. Where do I change the name of an item?\n4. ${second}`);
		const errs = buildHelp(root, { env: {} }).errors;
		expect(errs.join('\n')).toContain(`the bench question of US-TASK-02 "${second}" equals an indexed question of US-TASK-02`);
	});
	it('also guards the questions of bench.md', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/manual/bench.md', '1. Can I pay my invoice from here?', '1. Click "New Task".');
		expect(buildHelp(root, { env: {} }).errors.join('\n')).toContain('out-of-scope question of docs/manual/bench.md');
	});
	it('findLeaks compares case-insensitively and reports equality and containment', () => {
		const pack = { questions: [{ story: 'US-A-01', text: 'How do I log in?' }], sections: [{ id: 's#1', title: 'T', text: 'You can ask: how do i reset my password?' }] } as unknown as HelpPack;
		const out = findLeaks(pack, [{ text: 'HOW DO I LOG IN?', origin: 'o1' }, { text: 'How do I reset my password?', origin: 'o2' }, { text: 'Unrelated?', origin: 'o3' }], 'en');
		expect(out).toHaveLength(2);
		expect(out[0]).toContain('equals an indexed question of US-A-01');
		expect(out[1]).toContain('appears in section s#1');
	});
});

describe('help:build --strict (manual.md 6.8)', () => {
	it('passes on the valid fixture, as manual:check does', () => {
		const root = tmpRepo();
		expect(runChecks(root)).toEqual([]);
		expect(buildHelp(root, { env: {}, strict: true }).errors).toEqual([]);
		expect(buildMain(['--root', root, '--out', tmp(), '--strict'], capture().io)).toBe(0);
	});
	it('fails on any manual:check finding, while a normal build still succeeds', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, '## What you will see', '## What you shall see');
		const strict = buildHelp(root, { env: {}, strict: true });
		expect(strict.findings.length).toBeGreaterThan(0);
		expect(strict.errors[0]).toContain('manual:check reports');
		const c = capture();
		expect(buildMain(['--root', root, '--out', tmp(), '--strict'], c.io)).toBe(1);
		expect(buildMain(['--root', root, '--out', tmp()], c.io)).toBe(0);
	});
	it('reports usage errors with exit code 2 and fails without manual pages', () => {
		expect(buildMain(['--bogus'], capture().io)).toBe(2);
		expect(buildMain(['--root', tmpRepo(false), '--out', tmp()], capture().io)).toBe(1);
	});
});

describe('help:retrieve', () => {
	const setup = () => {
		const root = tmpRepo();
		const out = tmp();
		writeHelp(root, build(root), out);
		const input = join(out, 'in.jsonl');
		writeFileSync(input, `${JSON.stringify({ id: 'q1', question: 'How do I add a task?' })}\n${JSON.stringify({ question: 'What is the weather today?' })}\n`);
		return { out, input };
	};
	it('writes one line per question with ranked sections, scores, context and the decision', () => {
		const { out, input } = setup();
		const dest = join(out, 'res.jsonl');
		const c = capture();
		expect(retrieveMain(['--pack', join(out, 'en/pack.json'), '--in', input, '--out', dest], c.io)).toBe(0);
		const rows = readFileSync(dest, 'utf8').trim().split('\n').map((l) => JSON.parse(l));
		expect(rows).toHaveLength(2);
		expect(Object.keys(rows[0])).toEqual(['id', 'question', 'retrieved', 'scores', 'context', 'in_scope_decision', 'coverage']);
		expect(rows[0].id).toBe('q1');
		expect(rows[0].retrieved[0]).toMatch(/^tasks\//);
		expect(rows[0].scores).toHaveLength(rows[0].retrieved.length);
		expect(rows[0].in_scope_decision).toBe(true);
		expect(rows[1]).toMatchObject({ id: 2, retrieved: [], context: [], in_scope_decision: false });
	});
	it('gives the same ranking as the in-memory pack, and refuses a pack of another analyzer or bad usage', () => {
		const { out, input } = setup();
		const loaded = loadPack(readFileSync(join(out, 'en/pack.json'), 'utf8'));
		expect(loaded.searcher.search('How do I add a task?')[0].id).toMatch(/^tasks\//);
		const bad = JSON.parse(readFileSync(join(out, 'en/pack.json'), 'utf8'));
		bad.analyzer.stopwords_sha256 = '0'.repeat(64);
		writeFileSync(join(out, 'bad.json'), JSON.stringify(bad));
		const c = capture();
		expect(retrieveMain(['--pack', join(out, 'bad.json'), '--in', input, '--out', join(out, 'x.jsonl')], c.io)).toBe(1);
		expect(c.err.join('\n')).toContain('another analyzer');
		const newer = JSON.parse(readFileSync(join(out, 'en/pack.json'), 'utf8'));
		newer.schema_version = 3;
		expect(() => loadPack(JSON.stringify(newer))).toThrow(/newer/);
		expect(retrieveMain(['--pack', 'x'], capture().io)).toBe(2);
	});
});

describe('help:bench (manual.md 8)', () => {
	it('parses bench.md', () => {
		const b = parseBench(readFileSync(join(TEMPLATE_ROOT, 'src/frontend/scripts/manual/fixtures/repo/docs/manual/bench.md'), 'utf8'));
		expect(b.oos.en).toHaveLength(10);
		expect(b.oos.it[0]).toBe('Posso pagare la fattura da qui?');
		expect(b.extra).toEqual([]);
		const withExtra = parseBench('# Help bench\n\n## Out of scope\n\n### en\n\nNone.\n\n### it\n\nNone.\n\n## Extra\n\n| Question | Language | Story |\n|---|---|---|\n| "Where do I see what\'s due?" | en | US-TASK-05 |\n');
		expect(withExtra.extra).toEqual([{ question: "Where do I see what's due?", lang: 'en', story: 'US-TASK-05' }]);
	});
	it('builds the items: story bench questions, extra questions, out-of-scope questions', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/manual/bench.md', '## Extra\n\nNone.', '## Extra\n\n| Question | Language | Story |\n|---|---|---|\n| "Where is the New Task button?" | en | US-TASK-01 |');
		const items = benchItems(root, 'en');
		expect(items.filter((i) => i.source === 'story')).toHaveLength(3);
		expect(items.filter((i) => i.source === 'extra')).toEqual([expect.objectContaining({ question: 'Where is the New Task button?', story: 'US-TASK-01', kind: 'in_scope' })]);
		expect(items.filter((i) => i.source === 'oos')).toHaveLength(10);
		expect(items.filter((i) => i.kind === 'out_of_scope').every((i) => i.story === null)).toBe(true);
		expect(new Set(items.map((i) => i.id)).size).toBe(items.length);
	});
	it('prints a table per language, does not gate small languages, and exits 0 with a visible notice about answer gates', () => {
		const c = capture();
		expect(benchMain(['--root', tmpRepo()], c.io)).toBe(0);
		const text = c.out.join('\n');
		expect(text).toContain('Language en');
		expect(text).toContain('Language it');
		expect(text).toContain('Recall@1');
		expect(text).toContain('nothing gates');
		expect(text).toContain('retrieval gates passed');
		expect(text).toContain('NOTICE: answer gates skipped: no assistant model is locked');
	});
	it('exits 1 when a retrieval gate fails in a language with at least 20 in-scope items', () => {
		const root = tmpRepo();
		const rows = Array.from({ length: 20 }, (_, i) => `| "Zebra giraffe ${i} savanna?" | en | US-TASK-01 |`).join('\n');
		replaceIn(root, 'docs/manual/bench.md', '## Extra\n\nNone.', `## Extra\n\n| Question | Language | Story |\n|---|---|---|\n${rows}`);
		const run = runBench(root);
		const en = run.reports.find((r) => r.lang === 'en')!;
		expect(en.gating).toBe(true);
		expect(en.gates.find((g) => g.metric === 'Recall@1')!.pass).toBe(false);
		const c = capture();
		expect(benchMain(['--root', root], c.io)).toBe(1);
		expect(c.out.join('\n')).toContain('retrieval gates FAILED');
		expect(c.out.join('\n')).toContain('NO');
	});
	it('exits 1 when the build fails on a leak', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/manual/bench.md', '1. Can I pay my invoice from here?', '1. Click "New Task".');
		const c = capture();
		expect(benchMain(['--root', root], c.io)).toBe(1);
		expect(c.err.join('\n')).toContain('bench leak');
	});
	it('exports the items in the format of 8.4', () => {
		const root = tmpRepo();
		const file = join(tmp(), 'items.jsonl');
		expect(benchMain(['--root', root, '--export', file], capture().io)).toBe(0);
		const rows = readFileSync(file, 'utf8').trim().split('\n').map((l) => JSON.parse(l));
		expect(rows).toHaveLength(3 + 10 + 2 + 10);
		for (const r of rows) {
			expect(Object.keys(r)).toEqual(['id', 'lang', 'question', 'kind', 'story', 'source', 'retrieved', 'context', 'in_scope_decision']);
			expect(['in_scope', 'out_of_scope']).toContain(r.kind);
			expect(['story', 'extra', 'oos']).toContain(r.source);
			expect(r.context.length).toBeLessThanOrEqual(4);
			for (const id of r.context) expect(r.retrieved).toContain(id);
		}
		expect(rows.filter((r) => r.source === 'oos').every((r) => r.story === null && r.kind === 'out_of_scope')).toBe(true);
		expect(rows.filter((r) => r.lang === 'it')).toHaveLength(2 + 10);
	});
	describe('answer gates step', () => {
		const report = (over: object = {}) => ({ results: { sample_app: { en: { n: 40, faithful: 0.97, useful: 0.7, wrongful_refusal: 0.1, oos_refusal: 0.95, correct_source: 0.9, ...over }, it: { n: 40, faithful: 0.97, useful: 0.7, wrongful_refusal: 0.1, oos_refusal: 0.95, correct_source: 0.9 } } } });
		const write = (r: object) => {
			const f = join(tmp(), 'eval.json');
			writeFileSync(f, JSON.stringify(r));
			return f;
		};
		it('reads a report from the Python evaluation and gates on it', () => {
			const root = tmpRepo();
			const ok = capture();
			expect(benchMain(['--root', root, '--answers', write(report())], ok.io)).toBe(0);
			expect(ok.out.join('\n')).toContain('answer gates passed');
			const bad = capture();
			expect(benchMain(['--root', root, '--answers', write(report({ faithful: 0.5 }))], bad.io)).toBe(1);
			expect(bad.out.join('\n')).toContain('answer gates FAILED');
			expect(benchMain(['--root', root, '--answers', join(tmp(), 'missing.json')], capture().io)).toBe(1);
		});
		it('skips with a visible notice when no model is locked, and says so when a model is locked without a report', () => {
			const root = tmpRepo();
			const none = capture();
			expect(benchMain(['--root', root], none.io)).toBe(0);
			expect(none.out.some((l) => l.startsWith('NOTICE: answer gates skipped'))).toBe(true);
			expect(benchMain(['--root', root, '--require-answers'], capture().io)).toBe(1);
			mkdirSync(join(root, 'models'), { recursive: true });
			writeFileSync(join(root, 'models/model.lock'), JSON.stringify({ schema_version: 1, models: { assistant: { model_version: '1.0.0' } } }));
			const locked = capture();
			expect(benchMain(['--root', root], locked.io)).toBe(0);
			expect(locked.out.some((l) => l.startsWith('NOTICE: answer gates NOT evaluated'))).toBe(true);
		});
	});
	it('reports usage errors', () => {
		expect(benchMain(['--nope'], capture().io)).toBe(2);
	});
});

describe('help:gaps (manual.md 7.4)', () => {
	const rec = (question: string, over: Partial<GapRecord> = {}): GapRecord => ({
		schema_version: 1,
		recorded_at: '2026-11-06T10:15:00Z',
		lang: 'en',
		route: '/tasks',
		question,
		outcome: 'no_match',
		tier: 'retrieval',
		pack: { project_version: '0.1.0', sha256: 'a'.repeat(64) },
		top_sections: ['tasks/US-TASK-01#overview'],
		coverage: 0.2,
		...over,
	});
	const analyzers = { en: createAnalyzer('en', loadTemplateStopwords('en').words), it: createAnalyzer('it', loadTemplateStopwords('it').words) };
	it('computes the Jaccard index of stem sets', () => {
		expect(jaccard(new Set(['a', 'b']), new Set(['b', 'c']))).toBeCloseTo(1 / 3);
		expect(jaccard(new Set(), new Set())).toBe(0);
	});
	it('clusters per route when the Jaccard index is at least 0.5', () => {
		const clusters = clusterGaps(
			[
				rec('can i get an email when someone assigns me a task'),
				rec('email when someone assigns a task to me'),
				rec('email when someone assigns me a task', { top_sections: ['tasks/US-TASK-02#overview'] }),
				rec('how do i export my data'),
				rec('email when someone assigns me a task', { route: '/team' }),
			],
			analyzers,
		);
		expect(clusters.map((c) => [c.route, c.count])).toEqual([['/tasks', 3], ['/tasks', 1], ['/team', 1]]);
		expect(clusters[0].topSection).toBe('tasks/US-TASK-01#overview');
		expect(clusters[0].outcomes).toEqual({ no_match: 3 });
		expect(clusters[0].questions.map((q) => q.text)).toHaveLength(3);
	});
	it('never clusters across languages, and keeps questions without stems alone', () => {
		const c = clusterGaps([rec('invitare un collega', { lang: 'it' }), rec('invite a colleague'), rec('the of'), rec('the of')], analyzers);
		expect(c).toHaveLength(4);
	});
	it('prints the triage report to standard output', () => {
		const file = join(tmp(), 'export.json');
		writeFileSync(file, JSON.stringify([rec('can i get an email when someone assigns me a task'), rec('email when someone assigns a task'), { bad: true }]));
		const c = capture();
		expect(gapsMain(['--in', file], c.io)).toBe(0);
		const text = c.out.join('\n');
		expect(text).toContain('Route /tasks: 2 questions in 1 cluster');
		expect(text).toContain('[2] en | no_match 2 | top section: tasks/US-TASK-01#overview');
		expect(text).toContain('1 invalid records skipped');
		expect(validRecord({ bad: true })).toBe(false);
	});
	it('reports bad input', () => {
		const file = join(tmp(), 'x.json');
		writeFileSync(file, '{}');
		expect(gapsMain(['--in', file], capture().io)).toBe(1);
		expect(gapsMain([], capture().io)).toBe(2);
		expect(gapsMain(['--in', join(tmp(), 'none.json')], capture().io)).toBe(1);
	});
});


// ---------------------------------------------------------------- static embeddings (manual.md 6.7)

interface TableGolden {
	languages: Record<'en' | 'it', { table_base64: string; table_sha256: string }>;
}
const tableGolden = JSON.parse(readFileSync(TABLE_GOLDEN_PATH, 'utf8')) as TableGolden;
const TABLES = { en: Buffer.from(tableGolden.languages.en.table_base64, 'base64'), it: Buffer.from(tableGolden.languages.it.table_base64, 'base64') };

/** A folder with `en.bin` and `it.bin` (and other files), removed afterwards. */
const tableDir = (files: Record<string, Buffer | string> = { 'en.bin': TABLES.en, 'it.bin': TABLES.it }): string => {
	const d = tmp();
	for (const [name, data] of Object.entries(files)) writeFileSync(join(d, name), data);
	return d;
};

/** The same table with `fn` applied to its meta (the header's meta size is updated). */
function rewriteMeta(bytes: Buffer, fn: (meta: any) => void): Buffer {
	const dims = bytes.readUInt16LE(12);
	const metaLen = bytes.readUInt32LE(20);
	const start = 32 + 4 * dims;
	const meta = JSON.parse(bytes.subarray(start, start + metaLen).toString('utf8'));
	fn(meta);
	const next = Buffer.from(JSON.stringify(meta));
	const out = Buffer.concat([bytes.subarray(0, start), next, bytes.subarray(start + metaLen)]);
	out.writeUInt32LE(next.length, 20);
	return out;
}

const lockEntry = (over: Record<string, unknown> = {}) => ({
	model_version: '1.0.0',
	status: 'distilled',
	base_model: { revision: readEmbeddingTable(new Uint8Array(TABLES.en)).meta.model.revision },
	exports: [{ files: [{ path: 'en.bin', sha256: sha(TABLES.en) }, { path: 'it.bin', sha256: sha(TABLES.it) }] }],
	calibration: { params: { en: { method: 'rrf', depth: 30, rrf_k: 20, embedding_weight: 1.5, min_cosine: 0.4, min_cosine_lexical: 0.1 }, it: { method: 'rrf', depth: 30, rrf_k: 40, embedding_weight: 1, min_cosine: 0.5, min_cosine_lexical: -1 } } },
	...over,
});
const writeLock = (root: string, entry: object = lockEntry(), calibrated = true) => {
	mkdirSync(join(root, 'models/embeddings/calibration'), { recursive: true });
	writeFileSync(join(root, 'models/model.lock'), JSON.stringify({ schema_version: 2, models: { embeddings: entry } }));
	writeFileSync(join(root, 'models/embeddings/calibration/1.0.0.json'), JSON.stringify({ languages: { en: { calibrated }, it: { calibrated } } }));
};
/** A repository whose embed.json does not warn about the vocabulary coverage of the golden table, which is not this manual's. */
const noCoverageFloor = (root: string) => {
	const cfg = JSON.parse(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/config/embed.json'), 'utf8'));
	cfg.build.min_vocab_coverage = 0;
	mkdirSync(join(root, 'training/assistant/config'), { recursive: true });
	writeFileSync(join(root, 'training/assistant/config/embed.json'), JSON.stringify(cfg));
};
const withTable = (root: string, dir: string, opts: object = {}) => buildHelp(root, { env: {}, embeddingsDir: dir, ...opts });

describe('embedding golden files shared with the Python builder and port (manual.md 6.7.9)', () => {
	it('embeddings.golden.json is up to date: regenerate with scripts/help/golden.ts --write', () => {
		expect(readFileSync(EMBEDDINGS_GOLDEN_PATH, 'utf8')).toBe(serializeEmbeddingsGolden(buildEmbeddingsGolden()));
	});
	it('belongs to the table golden file, holds at least 12 queries and covers every rule it must', () => {
		const g = JSON.parse(readFileSync(EMBEDDINGS_GOLDEN_PATH, 'utf8')) as EmbeddingsGolden;
		expect(g.languages.en.cases.length).toBeGreaterThanOrEqual(12);
		for (const lang of ['en', 'it'] as const) expect(g.languages[lang].table_sha256).toBe(tableGolden.languages[lang].table_sha256);
		expect(g.retrieval).toEqual(JSON.parse(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json'), 'utf8')));
		const tags = new Set(Object.values(g.languages).flatMap((l) => l.cases.flatMap((c) => c.tags)));
		for (const t of ['no_table_stem', 'no_lexical_hit', 'fused_tie', 'depth_cut', 'diversify_after_fusion', 'semantic_accept', 'lexical_accept', 'veto', 'veto_off', 'section_without_vector'])
			expect(tags.has(t)).toBe(true);
	});
	it('a veto case is accepted when the veto is off, and the -1 rule equals "coverage or cosine"', () => {
		const g = JSON.parse(readFileSync(EMBEDDINGS_GOLDEN_PATH, 'utf8')) as EmbeddingsGolden;
		const cases = g.languages.en.cases.filter((c) => c.query === 'invitation expires');
		const veto = cases.find((c) => c.tags.includes('veto'))!;
		const off = cases.find((c) => c.tags.includes('veto_off'))!;
		expect(veto.decision.in_scope).toBe(false);
		expect(off.decision.in_scope).toBe(true);
		expect(off.retrieval.out_of_scope.min_cosine_lexical).toBe(-1);
		expect(off.decision.coverage).toBeGreaterThanOrEqual(off.retrieval.out_of_scope.min_coverage);
	});
});

describe('help:build and the embedding table (manual.md 6.7.4)', () => {
	it('no table: a lexical-only pack, null block, null manifest entries, an info line, strict passes', () => {
		const root = tmpRepo();
		const r = withTable(root, tableDir({}), { strict: true });
		expect(r.errors).toEqual([]);
		expect(r.notes.join('\n')).toMatch(/en: no en.bin.*lexical ranking only/);
		for (const l of r.languages) expect(l).toMatchObject({ table: null, tableBytes: null, pack: { embeddings: null } });
		expect(JSON.parse(r.manifestJson).embeddings).toEqual({ en: null, it: null });
		expect(r.warnings).toEqual([]);
	});
	it('off: fusion.method none uses no table even when one exists', () => {
		const root = tmpRepo();
		mkdirSync(join(root, 'training/assistant'), { recursive: true });
		const file = JSON.parse(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json'), 'utf8'));
		file.fusion.method = 'none';
		writeFileSync(join(root, 'training/assistant/retrieval.json'), JSON.stringify(file));
		const r = withTable(root, tableDir());
		expect(r.errors).toEqual([]);
		expect(r.notes.join('\n')).toMatch(/fusion.method is none/);
		expect(r.languages.every((l) => l.pack.embeddings === null && l.tableBytes === null)).toBe(true);
	});
	it('invalid: a table that fails a check of 6.7.5 is not used, with a warning; strict fails', () => {
		const root = tmpRepo();
		const dir = tableDir({ 'en.bin': TABLES.en.subarray(0, TABLES.en.length - 5), 'it.bin': TABLES.it });
		const r = withTable(root, dir);
		expect(r.errors).toEqual([]);
		expect(r.warnings.join('\n')).toMatch(/en: the table is not valid \(the file has/);
		expect(r.languages.find((l) => l.lang === 'en')!.pack.embeddings).toBeNull();
		const s = withTable(root, dir, { strict: true });
		expect(s.errors.join('\n')).toMatch(/not valid/);
		const wrongLang = withTable(root, tableDir({ 'en.bin': TABLES.it }));
		expect(wrongLang.warnings.join('\n')).toMatch(/table is for it, the pack for en/);
	});
	it('analyzer mismatch: a table built with another analyzer is not used; strict fails', () => {
		const root = tmpRepo();
		const dir = tableDir({ 'en.bin': rewriteMeta(TABLES.en, (m) => (m.analyzer.version = 99)), 'it.bin': TABLES.it });
		const r = withTable(root, dir);
		expect(r.warnings.join('\n')).toMatch(/en: the table was built with another analyzer: rebuild it/);
		expect(r.languages.find((l) => l.lang === 'en')!.pack.embeddings).toBeNull();
		expect(withTable(root, dir, { strict: true }).errors.join('\n')).toMatch(/another analyzer/);
	});
	describe('unlocked', () => {
		it('without a lock entry the table is not used; --unlocked uses it with locked false, the defaults and calibrated false', () => {
			const root = tmpRepo();
			noCoverageFloor(root);
			const dir = tableDir();
			const r = withTable(root, dir);
			expect(r.warnings.join('\n')).toMatch(/en: the table is not locked \(models\/model.lock has no embeddings entry\)/);
			expect(r.languages.every((l) => l.pack.embeddings === null)).toBe(true);
			expect(withTable(root, dir, { strict: true }).errors.join('\n')).toMatch(/not locked/);
			const u = withTable(root, dir, { unlocked: true });
			expect(u.errors).toEqual([]);
			const block = u.languages.find((l) => l.lang === 'en')!.pack.embeddings!;
			expect(block).toMatchObject({ file: 'embed.bin', sha256: sha(TABLES.en), bytes: TABLES.en.length, format_version: 1, dims: 16, locked: false, calibrated: false });
			expect(block.params).toEqual({ method: 'rrf', depth: 30, rrf_k: 60, embedding_weight: 1, min_cosine: 0.55, min_cosine_lexical: -1 });
			expect(Object.keys(block)).toEqual(['file', 'sha256', 'bytes', 'format_version', 'dims', 'count', 'model', 'locked', 'vocab_coverage', 'calibrated', 'params']);
			expect(Object.keys(block.model)).toEqual(['id', 'version', 'hub_id', 'revision']);
			expect(Object.keys(block.params)).toEqual(['method', 'depth', 'rrf_k', 'embedding_weight', 'min_cosine', 'min_cosine_lexical']);
		});
		it('names what differs from the lock: the SHA-256, the version or the revision', () => {
			const root = tmpRepo();
			noCoverageFloor(root);
			const dir = tableDir();
			writeLock(root, lockEntry({ exports: [{ files: [{ path: 'en.bin', sha256: 'f'.repeat(64) }, { path: 'it.bin', sha256: sha(TABLES.it) }] }] }));
			expect(withTable(root, dir).warnings.join('\n')).toMatch(/en: the table is not locked \(the SHA-256 of en.bin differs from the lock\)/);
			writeLock(root, lockEntry({ model_version: '2.0.0' }));
			expect(withTable(root, dir).warnings.join('\n')).toMatch(/meta.model.version 1.0.0 differs from the lock's 2.0.0/);
			writeLock(root, lockEntry({ base_model: { revision: 'b'.repeat(40) } }));
			expect(withTable(root, dir).warnings.join('\n')).toMatch(/meta.model.revision differs/);
		});
		it('takes the parameters of calibration.json for the table it belongs to, and ignores one for another table', () => {
			const root = tmpRepo();
			noCoverageFloor(root);
			const params = { method: 'rrf', depth: 99, rrf_k: 10, embedding_weight: 2, min_cosine: 0.3, min_cosine_lexical: 0.05 };
			const cal = (table: string) => JSON.stringify({ languages: { en: { table_sha256: table, calibrated: true, params } } });
			const r = withTable(root, tableDir({ 'en.bin': TABLES.en, 'it.bin': TABLES.it, 'calibration.json': cal(sha(TABLES.en)) }), { unlocked: true });
			const block = r.languages.find((l) => l.lang === 'en')!.pack.embeddings!;
			expect(block).toMatchObject({ locked: false, calibrated: true, params: { ...params, depth: 30 } });
			const stale = withTable(root, tableDir({ 'en.bin': TABLES.en, 'it.bin': TABLES.it, 'calibration.json': cal('0'.repeat(64)) }), { unlocked: true });
			expect(stale.languages.find((l) => l.lang === 'en')!.pack.embeddings).toMatchObject({ calibrated: false, params: { rrf_k: 60 } });
			expect(stale.notes.join('\n')).toMatch(/another table: ignored/);
			const off = withTable(root, tableDir({ 'en.bin': TABLES.en, 'calibration.json': JSON.stringify({ languages: { en: { table_sha256: sha(TABLES.en), calibrated: true, params: { ...params, method: 'none' } } } }) }), { unlocked: true });
			expect(off.languages.find((l) => l.lang === 'en')!.pack.embeddings).toBeNull();
			expect(off.notes.join('\n')).toMatch(/params.method is none/);
		});
		it('--strict with --unlocked is a usage error', () => {
			const c = capture();
			expect(buildMain(['--root', tmpRepo(), '--out', tmp(), '--strict', '--unlocked', '--embeddings', tableDir()], c.io)).toBe(2);
			expect(c.err.join('\n')).toMatch(/--strict cannot be used with --unlocked/);
		});
	});
	describe('locked', () => {
		it('uses the table with the lock parameters, writes embed.bin and the manifest entry, and builds byte for byte again', () => {
			const root = tmpRepo();
			noCoverageFloor(root);
			writeLock(root);
			const dir = tableDir();
			const a = withTable(root, dir, { strict: true });
			expect(a.errors).toEqual([]);
			const en = a.languages.find((l) => l.lang === 'en')!;
			const it = a.languages.find((l) => l.lang === 'it')!;
			expect(en.pack.embeddings).toMatchObject({ locked: true, calibrated: true, params: { method: 'rrf', depth: 30, rrf_k: 20, embedding_weight: 1.5, min_cosine: 0.4, min_cosine_lexical: 0.1 } });
			expect(it.pack.embeddings!.params).toEqual({ method: 'rrf', depth: 30, rrf_k: 40, embedding_weight: 1, min_cosine: 0.5, min_cosine_lexical: -1 });
			const index = en.pack.index;
			const table = readEmbeddingTable(new Uint8Array(TABLES.en));
			expect(en.pack.embeddings!.vocab_coverage).toBe(vocabCoverage(table, index));
			let inTable = 0;
			let total = 0;
			for (const [stem, posting] of Object.entries(index.terms)) for (let i = 1; i < posting.length; i++) {
				const tf = (posting[i] as number[])[2];
				total += tf;
				if (table.index.has(stem)) inTable += tf;
			}
			expect(en.pack.embeddings!.vocab_coverage).toBe(Math.round((inTable / total) * 1e4) / 1e4);
			const m = JSON.parse(a.manifestJson);
			expect(m.embeddings.en).toEqual({ file: 'en/embed.bin', sha256: sha(TABLES.en) });
			expect(Object.keys(m)).toEqual(['schema_version', 'project_version', 'languages', 'packs', 'bundles', 'embeddings', 'assistant_compatible']);
			const o1 = tmp();
			const o2 = tmp();
			writeHelp(root, a, o1);
			writeHelp(root, withTable(root, dir, { strict: true }), o2);
			expect(readFileSync(join(o1, 'en/embed.bin')).equals(TABLES.en)).toBe(true);
			for (const f of ['manifest.json', 'en/pack.json', 'it/pack.json', 'en/embed.bin', 'it/embed.bin']) expect(readFileSync(join(o1, f)).equals(readFileSync(join(o2, f)))).toBe(true);
			expect(Object.keys(JSON.parse(readFileSync(join(o1, 'en/pack.json'), 'utf8'))).at(-1)).toBe('embeddings');
		});
		it('reads calibrated from the recorded report, and a stale embed.bin is removed when the pack has no table', () => {
			const root = tmpRepo();
			noCoverageFloor(root);
			writeLock(root, lockEntry(), false);
			const out = tmp();
			writeHelp(root, withTable(root, tableDir()), out);
			expect(existsSync(join(out, 'en/embed.bin'))).toBe(true);
			expect(withTable(root, tableDir()).languages[0].pack.embeddings!.calibrated).toBe(false);
			writeHelp(root, withTable(root, tableDir({})), out);
			expect(existsSync(join(out, 'en/embed.bin'))).toBe(false);
		});
		it('warns when the manual gained words the table lacks, and strict fails', () => {
			const root = tmpRepo();
			writeLock(root);
			const dir = tableDir();
			const r = withTable(root, dir);
			expect(r.warnings.join('\n')).toMatch(/vocab coverage .* is below 0.9/);
			expect(r.languages[0].pack.embeddings).not.toBeNull();                    // still used meanwhile
			expect(withTable(root, dir, { strict: true }).errors.join('\n')).toMatch(/vocab coverage/);
		});
	});
});

describe('help:retrieve and the table', () => {
	const setup = () => {
		const root = tmpRepo();
		noCoverageFloor(root);
		writeLock(root);
		const out = tmp();
		writeHelp(root, withTable(root, tableDir()), out);
		const input = join(out, 'in.jsonl');
		writeFileSync(input, `${JSON.stringify({ id: 'q1', question: 'How do I add a task?' })}\n`);
		return { out, input };
	};
	it('loads the table next to the pack and ranks fused; a damaged table leaves it lexical with the reason', () => {
		const { out, input } = setup();
		const loaded = loadPackFile(join(out, 'en/pack.json'));
		expect(loaded.table).not.toBeNull();
		expect(loaded.tableError).toBeNull();
		const lexical = loadPack(readFileSync(join(out, 'en/pack.json'), 'utf8'));
		expect(loaded.searcher.decide('How do I add a task?').cosine).not.toBeNull();
		expect(lexical.searcher.decide('How do I add a task?').cosine).toBeNull();
		const c = capture();
		expect(retrieveMain(['--pack', join(out, 'en/pack.json'), '--in', input, '--out', join(out, 'r.jsonl')], c.io)).toBe(0);
		const bytes = readFileSync(join(out, 'en/embed.bin'));
		bytes[bytes.length - 1] ^= 1;
		writeFileSync(join(out, 'en/embed.bin'), bytes);
		const damaged = loadPackFile(join(out, 'en/pack.json'));
		expect(damaged.table).toBeNull();
		expect(damaged.tableError).toMatch(/SHA-256/);
		expect(damaged.searcher.search('How do I add a task?')[0].id).toMatch(/^tasks\//);
	});
});

describe('help:bench with a table (manual.md 6.7.8, 8.4)', () => {
	const run = (argv: string[]) => {
		const c = capture();
		const code = benchMain(argv, c.io);
		return { code, text: c.out.join('\n'), err: c.err.join('\n') };
	};
	it('--compare prints both configurations, gates on the fused one and labels an unlocked table', () => {
		const root = tmpRepo();
		noCoverageFloor(root);
		const c = capture();
		const code = benchMain(['--root', root, '--compare', '--embeddings', tableDir(), '--unlocked'], c.io);
		const text = c.out.join('\n');
		expect(code).toBe(0);
		expect(text).toMatch(/Language en: fused \(gates\) against lexical/);
		expect(text).toMatch(/Metric\s+Fused\s+Lexical\s+Diff/);
		expect(text).toContain('unlocked table: not a release result');
		expect(text).toContain('retrieval gates passed');
		expect(text).toMatch(/Recall@3\s+\d\.\d{3}\s+\d\.\d{3}\s+[+-]\d\.\d{3}/);
	});
	it('--compare without a table says so and shows equal columns', () => {
		const c = capture();
		expect(benchMain(['--root', tmpRepo(), '--compare', '--embeddings', tableDir({})], c.io)).toBe(0);
		const text = c.out.join('\n');
		expect(text).toContain('no embedding table for en: lexical ranking only, fused equals lexical');
		expect(text).toMatch(/Recall@1\s+(\d\.\d{3})\s+\1\s+\+0\.000/);
	});
	it('the fused configuration is what gates: a table changes the exit code through the same gates', () => {
		const root = tmpRepo();
		noCoverageFloor(root);
		const withT = runBench(root, { embeddingsDir: tableDir(), unlocked: true }, true);
		expect(withT.errors).toEqual([]);
		expect(withT.tables).toEqual({ en: { locked: false }, it: { locked: false } });
		expect(withT.lexicalReports).toHaveLength(2);
		const without = runBench(root, { embeddingsDir: tableDir({}) });
		expect(without.tables).toEqual({});
		expect(without.reports.map((r) => r.lang)).toEqual(['en', 'it']);
	});
	it('--indexed uses the table and the leave-one-out section vectors', () => {
		const root = tmpRepo();
		noCoverageFloor(root);
		const withT = runIndexed(root, { embeddingsDir: tableDir(), unlocked: true });
		expect(withT.errors).toEqual([]);
		expect(withT.results.en.map((r) => r.question)).toEqual(packOf(root).questions.map((q) => q.text));
		const c = capture();
		expect(benchMain(['--root', root, '--indexed', '--embeddings', tableDir(), '--unlocked'], c.io)).toBe(0);
		expect(c.out.join('\n')).toMatch(/no gate applies/);
	});
	it('reports usage errors for options that must run alone', () => {
		expect(run(['--calibrate', '--compare']).code).toBe(2);
		expect(run(['--indexed', '--compare']).code).toBe(2);
		expect(run(['--calibrate', '--export', 'x']).code).toBe(2);
	});
	it('--calibrate writes calibration.json into the folder and prints a summary; too few questions means not calibrated', () => {
		const root = tmpRepo();
		noCoverageFloor(root);
		const dir = tableDir();
		const c = capture();
		expect(benchMain(['--root', root, '--calibrate', '--embeddings', dir, '--unlocked'], c.io)).toBe(0);
		const text = c.out.join('\n');
		expect(text).toMatch(/NOT calibrated \(defaults\)/);
		expect(text).toMatch(/fewer than 30 indexed questions/);
		const file = JSON.parse(readFileSync(join(dir, 'calibration.json'), 'utf8'));
		expect(Object.keys(file)).toEqual(['schema_version', 'kind', 'config_sha256', 'retrieval_sha256', 'languages']);
		expect(file).toMatchObject({ schema_version: 1, kind: 'help-embeddings-calibration', config_sha256: loadEmbedConfig(root).sha256 });
		const en = file.languages.en;
		expect(Object.keys(en)).toEqual(['table_sha256', 'pack_inputs_sha256', 'calibrated', 'items', 'params', 'target_missed', 'metrics', 'notes']);
		expect(en).toMatchObject({ table_sha256: sha(TABLES.en), calibrated: false, target_missed: false });
		expect(en.items.tuning + en.items.held_out).toBe(packOf(root).questions.length);
		expect(en.params).toEqual({ method: 'rrf', depth: 30, rrf_k: 60, embedding_weight: 1, min_cosine: 0.55, min_cosine_lexical: -1 });
		expect(Object.keys(en.metrics)).toEqual(['lexical', 'fused']);
		expect(Object.keys(en.metrics.fused)).toEqual(['tuning', 'held_out']);
		expect(Object.keys(en.metrics.fused.tuning)).toEqual(['recall_at_1', 'recall_at_3', 'mrr_at_10', 'false_no_match']);
		expect(benchMain(['--root', root, '--calibrate', '--embeddings', tableDir({})], capture().io)).toBe(1);
		expect(benchMain(['--root', root, '--calibrate', '--embeddings', dir], capture().io)).toBe(1);      // a table the lock does not record needs --unlocked
		expect(runCalibrate(root, dir, false).errors).toEqual(['no language has a usable embedding table']);
	});
});

describe('calibration (manual.md 6.7.7)', () => {
	it('slices by the key of 6.7.7, deterministically, with ceil(share x n) held out', () => {
		const items = Array.from({ length: 10 }, (_, i) => ({ q: { story: `US-X-0${i % 3}`, text: `  Question   number ${i}? ` } }));
		const a = sliceItems('en', items, 0.3);
		const b = sliceItems('en', [...items].reverse(), 0.3);
		expect(a.heldOut).toHaveLength(3);
		expect(a.tuning).toHaveLength(7);
		expect(b.heldOut.map((x) => x.q.text).sort()).toEqual(a.heldOut.map((x) => x.q.text).sort());
		const key = (i: { q: { story: string; text: string } }) => sha(`calibration\nen\n${i.q.story}\n${i.q.text.normalize('NFC').trim().replace(/\s+/g, ' ')}`);
		const sorted = [...items].sort((x, y) => (key(x) < key(y) ? -1 : 1));
		expect(a.heldOut).toEqual(sorted.slice(0, 3));
		expect(sliceItems('it', items, 0.3).heldOut).not.toEqual(a.heldOut);
		expect(sliceItems('en', items, 0).heldOut).toEqual([]);
		expect(sliceItems('en', items.slice(0, 7), 0.3).heldOut).toHaveLength(3);
	});
	it('orders the fusion grid by Recall@3, MRR@10, Recall@1, the smaller weight, the larger rrf_k', () => {
		const p = (over: object) => ({ k: 60, w: 1, r3: 0.5, mrr: 0.4, r1: 0.3, ...over });
		const best = (list: ReturnType<typeof p>[]) => [...list].sort(comparePairs)[0];
		expect(best([p({}), p({ r3: 0.6 })]).r3).toBe(0.6);
		expect(best([p({ mrr: 0.5 }), p({ r1: 0.9 })]).mrr).toBe(0.5);
		expect(best([p({ r1: 0.4 }), p({})]).r1).toBe(0.4);
		expect(best([p({ w: 2 }), p({ w: 0.5 })]).w).toBe(0.5);
		expect(best([p({ k: 10 }), p({ k: 60 }), p({ k: 40 })]).k).toBe(60);
	});
	it('has a grid of 100 values from 0 to 0.99', () => {
		const g = cosineGrid(0.01);
		expect(g).toHaveLength(100);
		expect([g[0], g[1], g[55], g[99]]).toEqual([0, 0.01, 0.55, 0.99]);
	});
	it('guards on the held-out slice: Recall@3 and false no-match must not get worse', () => {
		const m = (r3: number, fnm: number) => ({ recall_at_1: 0, recall_at_3: r3, mrr_at_10: 0, false_no_match: fnm });
		expect(guardReasons(m(0.5, 0.1), m(0.6, 0.1))).toEqual([]);
		expect(guardReasons(m(0.5, 0.1), m(0.4, 0.1))[0]).toMatch(/Recall@3/);
		expect(guardReasons(m(0.5, 0.1), m(0.5, 0.2))[0]).toMatch(/false no-match/);
		expect(guardReasons(m(0.5, 0.1), m(0.4, 0.2))).toHaveLength(2);
	});
	it('computes metrics of a slice and null for an empty one', () => {
		const e = (rank: number, inScope: boolean) => ({ rank, inScope, coverage: 0, cosine: null, queryTerms: 1 });
		expect(metricsOf([e(1, true), e(3, true), e(0, false), e(5, true)])).toEqual({ recall_at_1: 0.25, recall_at_3: 0.5, mrr_at_10: 0.383333, false_no_match: 0.25 });
		expect(metricsOf([])).toEqual({ recall_at_1: null, recall_at_3: null, mrr_at_10: null, false_no_match: null });
	});

	/** A pack of the fixture manual with 40 indexed questions, for the thresholds: only `pack.questions` are asked. */
	const synthetic = () => {
		const root = tmpRepo();
		noCoverageFloor(root);
		const built = withTable(root, tableDir(), { unlocked: true }).languages.find((l) => l.lang === 'en')!;
		const stories = [...new Set(built.pack.sections.map((s) => s.story).filter((x): x is string => !!x))];
		const words = ['task', 'create', 'title', 'team', 'invite', 'organization', 'status', 'edit', 'colleague', 'job'];
		const questions = Array.from({ length: 40 }, (_, i) => ({ story: stories[i % stories.length], text: `${words[i % 10]} ${words[(i * 3 + 1) % 10]} question ${i}` }));
		const pack = { ...built.pack, questions };
		const analyzer = createAnalyzer('en', loadTemplateStopwords('en').words);
		const cfg = loadEmbedConfig(root).data.calibration;
		const run = (over: Partial<typeof cfg>) => calibrateLanguage({ lang: 'en', pack, table: built.table!, tableSha256: sha(TABLES.en), glossary: compileGlossary([], analyzer), config: { ...cfg, ...over } });
		return { run, cfg };
	};
	it('picks values of the grids, tunes on 70 percent and holds out 30 percent of the questions', () => {
		const { run, cfg } = synthetic();
		const r = run({});
		expect(r.items).toEqual({ tuning: 28, held_out: 12 });
		expect(r.calibrated).toBe(true);
		expect(cfg.rrf_k).toContain(r.params.rrf_k);
		expect(cfg.embedding_weight).toContain(r.params.embedding_weight);
		expect(r.params.min_cosine).toBeGreaterThanOrEqual(0);
		expect(r.params.min_cosine).toBeLessThanOrEqual(0.99);
		expect(r.params.depth).toBe(30);
		expect(['rrf', 'none']).toContain(r.params.method);
		expect(run({})).toEqual(r);
	});
	it('takes the largest min_cosine that meets the target, and reports a missed target', () => {
		const { run } = synthetic();
		expect(run({ target_false_no_match: 1 }).params.min_cosine).toBe(0.99);
		const missed = run({ target_false_no_match: -1 });
		expect(missed.target_missed).toBe(true);
		expect(missed.notes.join(' ')).toMatch(/target missed/);
		expect(run({ target_false_no_match: 1 }).target_missed).toBe(false);
	});
	it('takes the largest veto value within the loss budget, and -1 (veto off) when none qualifies', () => {
		const { run } = synthetic();
		expect(run({ max_veto_loss: 1 }).params.min_cosine_lexical).toBe(0.99);
		const none = run({ max_veto_loss: -1 });
		expect(none.params.min_cosine_lexical).toBe(-1);
		expect(none.notes.join(' ')).toMatch(/veto off/);
	});
	it('uses the single pair of a one-pair grid', () => {
		const { run } = synthetic();
		const r = run({ rrf_k: [20], embedding_weight: [0.75] });
		expect([r.params.rrf_k, r.params.embedding_weight]).toEqual([20, 0.75]);
	});
	it('does not tune with fewer than min_items questions', () => {
		const { run } = synthetic();
		const r = run({ min_items: 41 });
		expect(r.calibrated).toBe(false);
		expect(r.params).toEqual({ method: 'rrf', depth: 30, rrf_k: 60, embedding_weight: 1, min_cosine: 0.55, min_cosine_lexical: -1 });
		expect(r.notes[0]).toMatch(/fewer than 41 indexed questions \(40\)/);
	});
});
