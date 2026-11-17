/** help:bench (manual.md 8): the retrieval bench with release gates, the export for the answer bench, the answer-gate step. */
import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { allStories, isFile, loadRepo, sha256Hex, splitQuestions } from '../lib/index.ts';
import { answerGates, answerGatesPass } from '../../src/help/bench/answers.ts';
import type { AnswerLangResult } from '../../src/help/bench/answers.ts';
import { computeLangReport, formatComparison, formatLangReport, reportPasses } from '../../src/help/bench/metrics.ts';
import type { BenchItem, ItemResult, LangReport } from '../../src/help/bench/metrics.ts';
import { selectContext } from '../../src/help/lexical/context.ts';
import { createAnalyzer } from '../../src/help/lexical/analyzer.ts';
import { createEmbeddingScorer } from '../../src/help/lexical/embeddings.ts';
import type { EmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import { compileGlossary } from '../../src/help/lexical/glossary.ts';
import { createSearcher, effectiveRetrieval, searcherForPack } from '../../src/help/lexical/ranking.ts';
import type { Searcher } from '../../src/help/lexical/ranking.ts';
import type { HelpPack, ProtocolContext } from '../../src/help/lexical/types.ts';
import { loadBench } from './benchfile.ts';
import { loadGlossary } from './glossaryfile.ts';
import { buildHelp, LANGUAGES } from './build.ts';
import type { BuildOptions } from './build.ts';
import { runCalibrate, writeCalibration } from './calibrate.ts';
import { consoleIo, isEntry, parseOptions, rootOf } from './cli.ts';
import type { Io } from './cli.ts';
import { indexWithout } from './indexed.ts';
import { loadTemplateStopwords } from './paths.ts';

const DEFAULT_CONTEXT: ProtocolContext = { max_sections: 4, max_section_chars: 1200, max_context_chars: 4000, max_question_chars: 300, scan_limit: 8 };

function itemId(source: string, lang: string, question: string): string {
	return `${source}-${lang}-${sha256Hex(`${lang}\n${question}`).slice(0, 10)}`;
}

/** The bench items of a language (8.1): story bench questions, extra questions, out-of-scope questions. */
export function benchItems(root: string, lang: 'en' | 'it'): BenchItem[] {
	const repo = loadRepo(root);
	const bench = loadBench(root);
	const items: BenchItem[] = [];
	for (const s of allStories(repo)) {
		for (const q of splitQuestions(s.id, lang, s.questions[lang]).bench) {
			items.push({ id: itemId('story', lang, q), lang, question: q, kind: 'in_scope', story: s.id, source: 'story' });
		}
	}
	for (const x of bench.extra) {
		if (x.lang === lang) items.push({ id: itemId('extra', lang, x.question), lang, question: x.question, kind: 'in_scope', story: x.story, source: 'extra' });
	}
	for (const q of bench.oos[lang]) items.push({ id: itemId('oos', lang, q), lang, question: q, kind: 'out_of_scope', story: null, source: 'oos' });
	return items;
}

/**
 * Runs retrieval and the out-of-scope decision for the items, on the pack of their language: fused when `table` is given and the
 * pack's parameters allow it (manual.md 6.5, 6.6), lexical otherwise.
 */
export function runItems(pack: HelpPack, items: BenchItem[], table: EmbeddingTable | null = null): ItemResult[] {
	const sw = loadTemplateStopwords(pack.lang);
	const analyzer = createAnalyzer(pack.lang, sw.words);
	const searcher = searcherForPack(pack, analyzer, table);
	return items.map((item) => resultOf(pack, searcher, item));
}

/** One item through retrieval and the decision, with the context of models.md 3.5. */
function resultOf(pack: HelpPack, searcher: Searcher, item: BenchItem): ItemResult {
	const ctx = (pack.assistant.protocol.content.context as ProtocolContext | undefined) ?? DEFAULT_CONTEXT;
	const hits = searcher.search(item.question);
	const decision = searcher.decide(item.question, hits);
	const selected = selectContext(
		hits.map((h) => ({ id: h.id, text: pack.sections[h.index].text })),
		ctx,
	);
	return {
		...item,
		retrieved: hits.map((h) => h.id),
		retrievedStories: hits.map((h) => pack.sections[h.index].story),
		context: selected.map((s) => s.id),
		inScopeDecision: decision.inScope,
		coverage: decision.coverage,
	};
}

/** One JSONL line of the export (8.4), keys in the order of the document. */
export function exportLine(r: ItemResult): string {
	return JSON.stringify({
		id: r.id,
		lang: r.lang,
		question: r.question,
		kind: r.kind,
		story: r.story,
		source: r.source,
		retrieved: r.retrieved,
		context: r.context,
		in_scope_decision: r.inScopeDecision,
	});
}

export interface BenchRun {
	results: Record<string, ItemResult[]>;
	reports: LangReport[];
	errors: string[];
	/** The same items without the table (`fusion.method` none), for `--compare`. */
	lexicalReports?: LangReport[];
	/** Languages whose table is used, with whether it is locked. */
	tables?: Record<string, { locked: boolean }>;
}

export type TableOptions = Pick<BuildOptions, 'embeddingsDir' | 'unlocked'>;

/** The retrieval bench (manual.md 8): fused on the packs that use a table, lexical on the others. */
export function runBench(root: string, opts: TableOptions = {}, withLexical = false): BenchRun {
	const built = buildHelp(root, opts);
	if (built.errors.length || built.languages.length < LANGUAGES.length) {
		return { results: {}, reports: [], errors: built.errors.length ? built.errors : ['the help packs could not be built'] };
	}
	const results: Record<string, ItemResult[]> = {};
	const reports: LangReport[] = [];
	const lexicalReports: LangReport[] = [];
	const tables: Record<string, { locked: boolean }> = {};
	for (const l of built.languages) {
		const items = benchItems(root, l.lang);
		results[l.lang] = runItems(l.pack, items, l.table);
		reports.push(computeLangReport(l.lang, results[l.lang]));
		if (withLexical) lexicalReports.push(computeLangReport(l.lang, l.table ? runItems(l.pack, items, null) : results[l.lang]));
		if (l.table && l.pack.embeddings) tables[l.lang] = { locked: l.pack.embeddings.locked };
	}
	return { results, reports, errors: [], lexicalReports, tables };
}

/**
 * Tuning data for the glossary and the retrieval parameters (manual.md 6.9): every indexed question is asked against
 * a pack whose index lacks that one question, as the bench asks its held-out questions. It never gates. With a table the
 * section vectors are recomputed from that index and the ranking is the fused one.
 */
export function runIndexed(root: string, opts: TableOptions = {}): BenchRun {
	const built = buildHelp(root, opts);
	if (built.errors.length || built.languages.length < LANGUAGES.length) {
		return { results: {}, reports: [], errors: built.errors.length ? built.errors : ['the help packs could not be built'] };
	}
	const glossary = loadGlossary(root);
	const results: Record<string, ItemResult[]> = {};
	const reports: LangReport[] = [];
	for (const { lang, pack, table } of built.languages) {
		const analyzer = createAnalyzer(lang, loadTemplateStopwords(lang).words);
		const entries = compileGlossary(glossary.rows[lang], analyzer);
		const retrieval = effectiveRetrieval(pack);
		const sectionIds = pack.sections.map((s) => s.id);
		results[lang] = pack.questions.map((q) => {
			const index = indexWithout(pack, q, analyzer, entries);
			const item: BenchItem = { id: itemId('indexed', lang, q.text), lang, question: q.text, kind: 'in_scope', story: q.story, source: 'indexed' };
			const embeddings = table ? createEmbeddingScorer(table, index, sectionIds, retrieval.bm25.field_weights) : null;
			const searcher = createSearcher({ index, sectionIds, retrieval, analyzer, embeddings });
			return resultOf({ ...pack, index }, searcher, item);
		});
		reports.push(computeLangReport(lang, results[lang]));
	}
	return { results, reports, errors: [] };
}

function lockHasAssistant(root: string): boolean {
	const f = join(root, 'models/model.lock');
	if (!isFile(f)) return false;
	try {
		return Boolean(JSON.parse(readFileSync(f, 'utf8'))?.models?.assistant);
	} catch {
		return false;
	}
}

function formatAnswers(r: AnswerLangResult): string[] {
	const lines = [`Answers, language ${r.lang}: ${r.n} items`];
	if (!r.gating) lines.push('  fewer than 20 items: metrics are reported, nothing gates');
	for (const g of r.gates) {
		const v = g.value === null ? 'n/a' : g.value.toFixed(3);
		lines.push(`  ${g.metric.padEnd(30)} ${v.padStart(6)}  ${g.threshold.padEnd(9)}  ${!r.gating ? 'n/a' : g.pass ? 'yes' : 'NO'}`);
	}
	return lines;
}

export function main(argv: string[], io: Io): number {
	const o = parseOptions(argv, {
		'--root': 'value',
		'--export': 'value',
		'--answers': 'value',
		'--require-answers': 'flag',
		'--indexed': 'flag',
		'--compare': 'flag',
		'--calibrate': 'flag',
		'--embeddings': 'value',
		'--unlocked': 'flag',
	});
	if (o.flags.has('--calibrate') && (o.flags.has('--indexed') || o.flags.has('--compare') || o.values.has('--export') || o.values.has('--answers')))
		o.errors.push('--calibrate runs alone (manual.md 6.7.7)');
	if (o.flags.has('--indexed') && (o.flags.has('--compare') || o.values.has('--export') || o.values.has('--answers')))
		o.errors.push('--indexed runs alone (manual.md 6.9)');
	if (o.errors.length) {
		for (const e of o.errors) io.err(`help:bench: ${e}`);
		io.err('usage: help:bench [--root {dir}] [--export {jsonl}] [--answers {eval report}] [--require-answers] [--compare] [--embeddings {dir} [--unlocked]] | help:bench --indexed [--embeddings {dir} [--unlocked]] | help:bench --calibrate [--embeddings {dir} [--unlocked]]');
		return 2;
	}
	const root = rootOf(o);
	const dir = o.values.get('--embeddings');
	const tableOpts: TableOptions = { embeddingsDir: dir ? resolve(dir) : undefined, unlocked: o.flags.has('--unlocked') };
	if (o.flags.has('--calibrate')) {
		const r = runCalibrate(root, tableOpts.embeddingsDir ?? join(root, 'models/embeddings/weights'), Boolean(tableOpts.unlocked));
		if (r.errors.length) {
			for (const e of r.errors) io.err(`help:bench: ${e}`);
			return 1;
		}
		writeCalibration(r);
		io.out('Calibration on the indexed questions only (manual.md 6.7.7); bench questions are never read.');
		for (const line of r.summary) io.out(line);
		io.out(`help:bench: wrote ${r.file}`);
		return 0;
	}
	if (o.flags.has('--indexed')) {
		const ix = runIndexed(root, tableOpts);
		if (ix.errors.length) {
			for (const e of ix.errors) io.err(`help:bench: ${e}`);
			return 1;
		}
		io.out('Indexed questions, each asked without itself (manual.md 6.9): tuning data, not the bench; no gate applies.');
		for (const r of ix.reports) for (const line of formatLangReport({ ...r, gating: false })) if (!line.startsWith('  fewer than')) io.out(line);
		return 0;
	}
	const compare = o.flags.has('--compare');
	const run = runBench(root, tableOpts, compare);
	if (run.errors.length) {
		for (const e of run.errors) io.err(`help:bench: ${e}`);
		return 1;
	}
	const unlockedTable = Object.values(run.tables ?? {}).some((t) => !t.locked);
	if (compare) {
		const fused = run.reports;
		const lexical = run.lexicalReports ?? [];
		fused.forEach((r, i) => {
			const used = run.tables?.[r.lang];
			const note = !used ? `no embedding table for ${r.lang}: lexical ranking only, fused equals lexical` : used.locked ? null : 'unlocked table: not a release result';
			for (const line of formatComparison(r, lexical[i], note)) io.out(line);
		});
		const misses = fused.flatMap((r) => r.misses.map((m) => `    ${r.lang} ${m.id} "${m.question}" expected ${m.story}; top 3: ${m.top.join(', ') || 'none'}`));
		io.out(`In-scope misses of the fused configuration (${misses.length}):`);
		for (const m of misses) io.out(m);
	} else {
		for (const r of run.reports) for (const line of formatLangReport(r)) io.out(line);
	}
	if (unlockedTable && !compare) io.out('NOTICE: unlocked table: not a release result.');
	const exportFile = o.values.get('--export');
	if (exportFile) {
		const lines = LANGUAGES.flatMap((l) => run.results[l].map(exportLine));
		writeFileSync(resolve(exportFile), lines.length ? `${lines.join('\n')}\n` : '');
		io.out(`help:bench: wrote ${lines.length} items to ${exportFile}`);
	}
	let ok = reportPasses(run.reports);
	io.out(ok ? 'help:bench: retrieval gates passed' : 'help:bench: retrieval gates FAILED');

	// Separate step: answer-quality gates, from the report of `python -m training.assistant.eval --suite project`.
	const answers = o.values.get('--answers');
	if (answers) {
		let report: unknown;
		try {
			report = JSON.parse(readFileSync(resolve(answers), 'utf8'));
		} catch (e) {
			io.err(`help:bench: cannot read the answer report ${answers}: ${(e as Error).message}`);
			return 1;
		}
		const res = answerGates(report);
		for (const r of res) for (const line of formatAnswers(r)) io.out(line);
		const pass = answerGatesPass(res);
		io.out(pass ? 'help:bench: answer gates passed' : 'help:bench: answer gates FAILED');
		ok = ok && pass;
	} else if (!lockHasAssistant(root)) {
		io.out('NOTICE: answer gates skipped: no assistant model is locked (models/model.lock has no assistant entry). Retrieval gates above are the only gates in force.');
		if (o.flags.has('--require-answers')) ok = false;
	} else {
		io.out('NOTICE: answer gates NOT evaluated: a model is locked but no report was given. Run help:bench -- --export {jsonl}, then python -m training.assistant.eval --suite project --items {jsonl}, then help:bench -- --answers {report}.');
		if (o.flags.has('--require-answers')) ok = false;
	}
	return ok ? 0 : 1;
}

if (isEntry(import.meta.url)) process.exitCode = main(process.argv.slice(2), consoleIo);
