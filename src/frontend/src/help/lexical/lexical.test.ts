// @vitest-environment node
import { createHash } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { sha256Utf8 } from '../bench/sha256.ts';
import { normalizeQuestion, splitQuestions } from '../bench/split.ts';
import {
	ANALYZER_VERSION,
	createAnalyzer,
	parseStopwords,
	stripDiacritics,
	tokenize,
} from './analyzer.ts';
import { selectContext } from './context.ts';
import { fuseRrf } from './fusion.ts';
import { compileGlossary, glossaryStems } from './glossary.ts';
import type { EmbeddingMatch, EmbeddingScorer } from './fusion.ts';
import { buildIndex } from './indexer.ts';
import { createSearcher, effectiveRetrieval, pageOfSection, pagesFirst } from './ranking.ts';
import type { RetrievalParams } from './types.ts';

const dir = new URL('.', import.meta.url).pathname;
const words = (lang: 'en' | 'it') =>
	parseStopwords(readFileSync(join(dir, `stopwords.${lang}.txt`), 'utf8'));
const en = createAnalyzer('en', words('en'));
const itA = createAnalyzer('it', words('it'));

const RETRIEVAL: RetrievalParams = {
	schema_version: 3,
	bm25: { k1: 1.2, b: 0.75, field_weights: { title: 2.0, text: 1.0, questions: 1.5 } },
	candidates: 10,
	out_of_scope: {
		min_coverage: 0.4,
		min_cosine: 0.55,
		min_cosine_lexical: -1,
		min_query_terms: 1,
	},
	fusion: { method: 'none', depth: 30, rrf_k: 60, embedding_weight: 1.0 },
};

describe('analyzer (manual.md 6.4)', () => {
	it('lower-cases, splits on non letters and digits, drops stopwords and one-character tokens, stems', () => {
		expect(en.analyze("How do I add Something NEW to the team's to-do list?")).toEqual([
			'add',
			'someth',
			'new',
			'team',
			'list',
		]);
		expect(en.analyze('A single x and I')).toEqual(['singl']);
	});
	it('splits on the apostrophe', () => {
		expect(tokenize("l'elenco", new Set())).toEqual(['elenco']);
		expect(itA.analyze("Dov'è l'elenco dei compiti?")).toEqual(['elenc', 'comp']);
		expect(tokenize("dov'è", new Set())).toEqual(['dov']);
	});
	it('keeps digits and removes diacritics from the stem', () => {
		expect(en.analyze('2024 résumé')).toEqual(['2024', 'resume']);
		expect(itA.analyze('attività città')).toEqual(['attiv', 'citt']);
		expect(stripDiacritics('è')).toBe('e');
	});
	it('stems with Snowball: english and italian', () => {
		expect(en.analyze('running runs ran generalization')).toEqual([
			'run',
			'run',
			'ran',
			'general',
		]);
		expect(itA.analyze('organizzazione organizzazioni')).toEqual(['organizz', 'organizz']);
	});
	it('normalises to NFC before splitting', () => {
		expect(en.analyze('café')).toEqual(en.analyze('café'));
	});
	it('exposes id and version', () => {
		expect(en.id).toBe('snowball-en');
		expect(itA.id).toBe('snowball-it');
		expect(en.version).toBe(ANALYZER_VERSION);
	});
	it('parses stopword files, which are sorted and lower-case', () => {
		for (const l of ['en', 'it'] as const) {
			const list = [...words(l)];
			expect(list).toEqual([...list].sort());
			expect(list.every((w) => w === w.toLowerCase())).toBe(true);
		}
	});
});

describe('sha256 and the question split (manual.md 6.2)', () => {
	it('matches node crypto, including multi-block and non-ASCII input', () => {
		for (const s of [
			'',
			'abc',
			'a'.repeat(55),
			'a'.repeat(56),
			'a'.repeat(64),
			'è'.repeat(100),
			'US-TASK-01\nen\nHow do I?',
		]) {
			expect(sha256Utf8(s)).toBe(createHash('sha256').update(s).digest('hex'));
		}
	});
	it('normalises questions', () => {
		expect(normalizeQuestion('  How   do\tI \n do it?  ')).toBe('How do I do it?');
	});
	it('puts floor(n / 2) questions, the lowest keys, in the bench set', () => {
		for (const [n, bench] of [
			[0, 0],
			[1, 0],
			[2, 1],
			[3, 1],
			[4, 2],
			[5, 2],
		] as const) {
			const qs = Array.from({ length: n }, (_, i) => `Question number ${i}?`);
			const sp = splitQuestions('US-X-01', 'en', qs);
			expect(sp.bench).toHaveLength(bench);
			expect(sp.indexed).toHaveLength(n - bench);
			expect([...sp.bench, ...sp.indexed].sort()).toEqual([...qs].sort());
		}
	});
	it('is deterministic, independent of the input order, and depends on story and language', () => {
		const qs = ['Alpha one?', 'Beta two?', 'Gamma three?', 'Delta four?'];
		const a = splitQuestions('US-X-01', 'en', qs);
		expect(splitQuestions('US-X-01', 'en', [...qs].reverse())).toEqual(a);
		const key = (q: string) => sha256Utf8(`US-X-01\nen\n${q}`);
		expect(a.bench).toEqual([...qs].sort((x, y) => (key(x) < key(y) ? -1 : 1)).slice(0, 2));
		expect(splitQuestions('US-X-02', 'en', qs)).not.toEqual(a);
	});
});

/** An independent, naive BM25F implementation straight from the formulas of 6.5 and 6.6. */
function naive(
	sections: { title: string; text: string; questions: string[] }[],
	query: string,
	analyzer = en,
) {
	const fields = sections.map((s) => [
		analyzer.analyze(s.title),
		analyzer.analyze(s.text),
		analyzer.analyze(s.questions.join('\n')),
	]);
	const N = sections.length;
	const avg = [0, 1, 2].map((f) => fields.reduce((s, d) => s + d[f].length, 0) / N);
	const w = [2.0, 1.0, 1.5];
	const Q = [...analyzer.stems(query)];
	const df = (t: string) => fields.filter((d) => d.some((f) => f.includes(t))).length;
	const idf = (t: string) => Math.log(1 + (N - df(t) + 0.5) / (df(t) + 0.5));
	const scores = fields.map((d) =>
		Q.reduce((sum, t) => {
			const tfp = [0, 1, 2].reduce(
				(a, f) =>
					a +
					(w[f] * d[f].filter((x) => x === t).length) /
						(1 - 0.75 + (0.75 * d[f].length) / (avg[f] || 1)),
				0,
			);
			return sum + (tfp ? (idf(t) * tfp * 2.2) / (tfp + 1.2) : 0);
		}, 0),
	);
	return { scores, Q, idf, df, N, fields };
}

const SECTIONS = [
	{
		id: 'a#overview',
		title: 'Create a task',
		text: 'Add a task with a title.',
		questions: ['How do I add something new?'],
	},
	{
		id: 'a#steps',
		title: 'Create a task - Steps',
		text: '1. Open the Tasks page.\n2. Click "New Task".',
		questions: [],
	},
	{
		id: 'b#overview',
		title: 'Invite a colleague',
		text: 'Send an invitation by e-mail to a colleague.',
		questions: [],
	},
	{
		id: 'b#rules',
		title: 'Invite a colleague - Good to know',
		text: 'An invitation expires after seven days.',
		questions: ['How long is an invitation valid?'],
	},
	{
		id: 'c#overview',
		title: 'Settings',
		text: 'Change the language of the application.',
		questions: [],
	},
];
const searcherOf = (params: RetrievalParams = RETRIEVAL, sections = SECTIONS) =>
	createSearcher({
		index: buildIndex(sections, en),
		sectionIds: sections.map((s) => s.id),
		retrieval: params,
		analyzer: en,
	});

describe('index (manual.md 6.3)', () => {
	const idx = buildIndex(SECTIONS, en);
	it('has the fields, counts and per-field lengths', () => {
		expect(idx.fields).toEqual(['title', 'text', 'questions']);
		expect(idx.doc_count).toBe(5);
		expect(idx.lengths[0]).toEqual([2, 3, 3]);
		expect(idx.avg_length[0]).toBeCloseTo(idx.lengths.reduce((s, r) => s + r[0], 0) / 5);
	});
	it('lists postings sorted by section then field, with document frequency first', () => {
		expect(idx.terms.task).toEqual([2, [0, 0, 1], [0, 1, 1], [1, 0, 1], [1, 1, 2]]);
		expect(Object.keys(idx.terms)).toEqual([...Object.keys(idx.terms)].sort());
	});
});

describe('glossary (manual.md 6.9)', () => {
	const g = compileGlossary(
		[
			{ term: 'task', aliases: ['job', 'to-do item', 'task'] },
			{ term: 'due date', aliases: ['deadline'] },
			{ term: 'the', aliases: ['job'] },
			{ term: 'organization', aliases: ['of the'] },
		],
		en,
	);
	it('analyzes terms and aliases, drops alias stems of the term and rows without stems', () => {
		expect(g).toEqual([
			{ term: ['task'], add: ['job', 'item'] },
			{ term: ['due', 'date'], add: ['deadlin'] },
		]);
	});
	it('adds the alias stems once per field, only where the whole term occurs in order', () => {
		expect(glossaryStems(en.analyze('Edit the task, then the task again'), g)).toEqual([
			'job',
			'item',
		]);
		expect(glossaryStems(en.analyze('Set the due date'), g)).toEqual(['deadlin']);
		expect(glossaryStems(en.analyze('A date that is due'), g)).toEqual([]);
	});
	it('expands title and text, not questions, and keeps the field lengths', () => {
		const secs = [{ title: 'Create a task', text: 'Type a due date.', questions: ['task'] }];
		const plain = buildIndex(secs, en);
		const idx = buildIndex(secs, en, g);
		expect(idx.lengths).toEqual(plain.lengths);
		expect(idx.terms.job).toEqual([1, [0, 0, 1]]);
		expect(idx.terms.deadlin).toEqual([1, [0, 1, 1]]);
		expect(idx.terms.task).toEqual(plain.terms.task);
	});
});

describe('ranking (manual.md 6.5)', () => {
	it('diversifies by page: the best section of each page first, then the others, rank 1 unchanged', () => {
		expect(pageOfSection('tasks/US-TASK-01#steps-2')).toBe('tasks/US-TASK-01');
		expect(pageOfSection('home')).toBe('home');
		const ranked = ['a#1', 'a#2', 'b#1', 'a#3', 'c#1', 'b#2'].map((id) => ({ id }));
		expect(pagesFirst(ranked).map((h) => h.id)).toEqual([
			'a#1',
			'b#1',
			'c#1',
			'a#2',
			'a#3',
			'b#2',
		]);
		const secs = [
			{ id: 'p#1', title: 'alpha', text: 'alpha beta', questions: [] },
			{ id: 'p#2', title: 'alpha', text: 'alpha', questions: [] },
			{ id: 'q#1', title: 'other', text: 'alpha', questions: [] },
		];
		expect(
			searcherOf(RETRIEVAL, secs)
				.search('alpha')
				.map((h) => h.id),
		).toEqual(['p#2', 'p#1', 'q#1']);
		expect(
			searcherOf({ ...RETRIEVAL, diversify: 'page' }, secs)
				.search('alpha')
				.map((h) => h.id),
		).toEqual(['p#2', 'q#1', 'p#1']);
		expect(
			searcherOf({ ...RETRIEVAL, diversify: 'page', candidates: 2 }, secs)
				.search('alpha')
				.map((h) => h.id),
		).toEqual(['p#2', 'q#1']);
	});
	it('equals a naive BM25F implementation', () => {
		const s = searcherOf();
		for (const q of [
			'add a new task',
			'invitation days',
			'language',
			'task page click',
			'how long is an invitation valid',
		]) {
			const ref = naive(SECTIONS, q);
			const hits = s.search(q);
			const expected = ref.scores
				.map((score, i) => ({ id: SECTIONS[i].id, score }))
				.filter((x) => x.score > 0)
				.sort((x, y) => y.score - x.score || (x.id < y.id ? -1 : 1));
			expect(hits.map((h) => h.id)).toEqual(expected.map((x) => x.id));
			hits.forEach((h, i) => expect(h.score).toBeCloseTo(expected[i].score, 10));
		}
	});
	it('returns nothing for an empty query, stopwords only, or unknown stems', () => {
		const s = searcherOf();
		expect(s.search('')).toEqual([]);
		expect(s.search('the of and')).toEqual([]);
		expect(s.search('zzzz')).toEqual([]);
	});
	it('counts each distinct query stem once', () => {
		const s = searcherOf();
		expect(s.search('task task task')).toEqual(s.search('task'));
	});
	it('breaks ties by section id in code point order and honours `candidates`', () => {
		const twin = [
			{ id: 'z#x', title: 'Same', text: 'alpha', questions: [] },
			{ id: 'a#x', title: 'Same', text: 'alpha', questions: [] },
			{ id: 'm#x', title: 'Other', text: 'beta', questions: [] },
		];
		const s = searcherOf(RETRIEVAL, twin);
		expect(s.search('alpha').map((h) => h.id)).toEqual(['a#x', 'z#x']);
		const one = searcherOf({ ...RETRIEVAL, candidates: 1 }, twin);
		expect(one.search('alpha')).toHaveLength(1);
	});
	it('weights the title above the text and the questions above the text', () => {
		const secs = [
			{ id: 't#1', title: 'budget', text: 'x', questions: [] },
			{ id: 'u#1', title: 'y', text: 'budget', questions: [] },
			{ id: 'v#1', title: 'y', text: 'z', questions: ['budget'] },
		];
		const order = searcherOf(RETRIEVAL, secs)
			.search('budget')
			.map((h) => h.id);
		expect(order).toEqual(['t#1', 'v#1', 'u#1']);
	});
	it('works with a plain object index parsed from JSON, including stems named like object members', () => {
		const secs = [
			{ id: 'p#1', title: 'constructor', text: 'toString valueOf', questions: [] },
			{ id: 'p#2', title: 'other', text: 'text', questions: [] },
		];
		const idx = JSON.parse(JSON.stringify(buildIndex(secs, en)));
		const s = createSearcher({
			index: idx,
			sectionIds: ['p#1', 'p#2'],
			retrieval: RETRIEVAL,
			analyzer: en,
		});
		expect(s.search('constructor')[0].id).toBe('p#1');
		expect(s.search('hasOwnProperty')).toEqual([]);
	});
});

describe('out-of-scope decision (manual.md 6.6)', () => {
	const s = searcherOf();
	it('follows the coverage formula', () => {
		const q = 'invitation expires';
		const ref = naive(SECTIONS, q);
		const top = s.search(q)[0];
		const found = ref.Q.filter((t) => ref.fields[top.index].some((f) => f.includes(t))).reduce(
			(a, t) => a + ref.idf(t),
			0,
		);
		const total = ref.Q.reduce((a, t) => a + ref.idf(t), 0);
		const d = s.decide(q);
		expect(d.coverage).toBeCloseTo(found / total, 10);
		expect(d.inScope).toBe(true);
		expect(d.topId).toBe(top.id);
	});
	it('counts unknown stems with idf_unknown, which lowers coverage', () => {
		const d = s.decide('invitation zzzzzz qqqqqq');
		const idfKnown = Math.log(1 + (5 - 2 + 0.5) / (2 + 0.5));
		const unknown = Math.log(1 + 5.5 / 0.5);
		expect(d.coverage).toBeCloseTo(idfKnown / (idfKnown + 2 * unknown), 10);
		expect(d.inScope).toBe(false);
	});
	it('judges a query without stems or without a result out of scope', () => {
		expect(s.decide('the of').inScope).toBe(false);
		expect(s.decide('weather forecast').inScope).toBe(false);
		expect(s.decide('').coverage).toBe(0);
	});
	it('honours min_query_terms and min_coverage', () => {
		const strict = searcherOf({
			...RETRIEVAL,
			out_of_scope: { ...RETRIEVAL.out_of_scope, min_query_terms: 3 },
		});
		expect(strict.decide('invitation').inScope).toBe(false);
		const lax = searcherOf({
			...RETRIEVAL,
			out_of_scope: { ...RETRIEVAL.out_of_scope, min_coverage: 0.01 },
		});
		expect(lax.decide('invitation zzzzzz').inScope).toBe(true);
	});
});

describe('fusion and the decision with a table (manual.md 6.5, 6.6)', () => {
	const rrf: RetrievalParams = {
		...RETRIEVAL,
		fusion: { method: 'rrf', depth: 30, rrf_k: 60, embedding_weight: 1.0 },
	};
	const ids = SECTIONS.map((x) => x.id);
	/** A scorer with a given embedding list and cosine per section (null: no query vector; undefined section: no vector). */
	const scorer = (
		list: EmbeddingMatch[],
		cos: Record<number, number | null> | null = null,
	): EmbeddingScorer => ({
		sectionVector: () => null,
		queryVector: () => null,
		rank: (_stems, depth) => list.slice(0, depth),
		cosine: (_stems, i) => (cos === null ? null : (cos[i] ?? 0)),
		cosines: () => null,
	});
	const make = (
		retrieval: RetrievalParams,
		embeddings: EmbeddingScorer | null,
		sections = SECTIONS,
	) =>
		createSearcher({
			index: buildIndex(sections, en),
			sectionIds: sections.map((x) => x.id),
			retrieval,
			analyzer: en,
			embeddings,
		});

	it('is lexical only without a scorer, with the same decision as without fusion', () => {
		const lexical = searcherOf();
		const noTable = make(rrf, null);
		for (const q of ['invitation', 'add a new task', 'weather forecast', 'invitation zzzzzz']) {
			expect(noTable.search(q)).toEqual(lexical.search(q));
			expect(noTable.decide(q)).toEqual(lexical.decide(q));
		}
		expect(noTable.decide('invitation').cosine).toBeNull();
	});
	it('ignores the scorer when fusion.method is none', () => {
		const s = make(RETRIEVAL, scorer([{ index: 4, cosine: 0.9 }], { 4: 0.9 }));
		expect(s.search('invitation')).toEqual(searcherOf().search('invitation'));
		expect(s.decide('weather forecast').cosine).toBeNull();
	});
	it('fuses two lists with reciprocal ranks, ties by section id', () => {
		const lex = [
			{ index: 2, id: ids[2], score: 3 },
			{ index: 3, id: ids[3], score: 2 },
		];
		const emb = [
			{ index: 3, cosine: 0.9 },
			{ index: 4, cosine: 0.8 },
		];
		const fused = fuseRrf(lex, emb, ids, rrf.fusion);
		expect(fused.map((f) => f.id)).toEqual([ids[3], ids[2], ids[4]]);
		expect(fused[0].score).toBe(1 / 62 + 1 / 61);
		expect(fused[1].score).toBe(1 / 61);
		expect(fused[2].score).toBe(1 / 62);
		const tie = fuseRrf(
			[{ index: 4, id: ids[4], score: 1 }],
			[{ index: 2, cosine: 0.5 }],
			ids,
			rrf.fusion,
		);
		expect(tie.map((f) => f.id)).toEqual([...[ids[2], ids[4]].sort()]);
		expect(tie[0].score).toBe(tie[1].score);
		const heavy = fuseRrf(
			[{ index: 4, id: ids[4], score: 1 }],
			[{ index: 2, cosine: 0.5 }],
			ids,
			{ rrf_k: 60, embedding_weight: 2 },
		);
		expect(heavy[0].id).toBe(ids[2]);
	});
	it('passes the embedding order through when the lexical list is empty, and the lexical order when the other is', () => {
		const emb = [
			{ index: 5, cosine: 0.9 },
			{ index: 0, cosine: 0.4 },
		];
		const s = make(rrf, scorer(emb, { 5: 0.9 }));
		expect(s.search('weather forecast').map((h) => h.id)).toEqual([ids[5], ids[0]]);
		const lexOnly = make(rrf, scorer([]));
		expect(lexOnly.search('invitation').map((h) => h.id)).toEqual(
			searcherOf()
				.search('invitation')
				.map((h) => h.id),
		);
	});
	it('cuts both lists to depth before fusing', () => {
		const emb = [
			{ index: 5, cosine: 0.9 },
			{ index: 0, cosine: 0.8 },
		];
		const deep = make(rrf, scorer(emb))
			.search('invitation')
			.map((h) => h.id);
		const shallow = make({ ...rrf, fusion: { ...rrf.fusion, depth: 1 } }, scorer(emb))
			.search('invitation')
			.map((h) => h.id);
		const lex = searcherOf()
			.search('invitation')
			.map((h) => h.id);
		expect(lex).not.toContain(ids[0]);
		expect(deep).toContain(ids[0]);
		expect(shallow).not.toContain(ids[0]);
		expect(shallow.sort()).toEqual([lex[0], ids[5]].sort());
	});
	it('diversifies after fusion and keeps `candidates` for the end', () => {
		const secs = [
			{ id: 'p#1', title: 'alpha', text: 'alpha', questions: [] },
			{ id: 'p#2', title: 'x', text: 'alpha', questions: [] },
			{ id: 'q#1', title: 'y', text: 'beta', questions: [] },
		];
		const emb = [
			{ index: 1, cosine: 0.9 },
			{ index: 2, cosine: 0.8 },
		];
		const s = make({ ...rrf, diversify: 'page', candidates: 2 }, scorer(emb), secs);
		// fused: p#1 (lex 1), p#2 (lex 2 + emb 1), q#1 (emb 2); p#2 before p#1; diversified: p#2, q#1, p#1; first 2
		expect(s.search('alpha').map((h) => h.id)).toEqual(['p#2', 'q#1']);
	});
	describe('the decision of 6.6', () => {
		const cosAt = (c: number | null) =>
			c === null ? null : { 0: c, 1: c, 2: c, 3: c, 4: c, 5: c };
		const params = (o: Partial<RetrievalParams['out_of_scope']>): RetrievalParams => ({
			...rrf,
			out_of_scope: { ...rrf.out_of_scope, ...o },
		});
		const ask = (
			q: string,
			c: number | null,
			o: Partial<RetrievalParams['out_of_scope']> = {},
		) => make(params(o), scorer([{ index: 4, cosine: 0.6 }], cosAt(c))).decide(q);
		it('accepts by cosine alone (semantic acceptance)', () => {
			const d = ask('invitation zzzzzz qqqqqq', 0.6);
			expect(d.coverage).toBeLessThan(0.4);
			expect(d.cosine).toBe(0.6);
			expect(d.inScope).toBe(true);
			expect(ask('invitation zzzzzz qqqqqq', 0.5).inScope).toBe(false);
		});
		it('accepts by coverage (lexical acceptance), also without a query vector', () => {
			expect(ask('invitation expires', 0.1).inScope).toBe(true);
			expect(ask('invitation expires', null).inScope).toBe(true);
			expect(ask('invitation expires', null).cosine).toBeNull();
		});
		it('lets the cosine veto a lexical acceptance below min_cosine_lexical', () => {
			expect(ask('invitation expires', 0.1, { min_cosine_lexical: 0.2 }).inScope).toBe(false);
			expect(ask('invitation expires', 0.3, { min_cosine_lexical: 0.2 }).inScope).toBe(true);
			expect(ask('invitation expires', 0.1, { min_cosine_lexical: -1 }).inScope).toBe(true);
			// semantic acceptance is not vetoed
			expect(ask('invitation expires', 0.8, { min_cosine_lexical: 0.9 }).inScope).toBe(true);
		});
		it('still needs min_query_terms and a section to judge', () => {
			expect(ask('invitation expires', 0.9, { min_query_terms: 5 }).inScope).toBe(false);
			expect(ask('the of', 0.9).inScope).toBe(false);
		});
		it('uses 0 for a top section without a vector', () => {
			const s = make(rrf, scorer([], {}));
			expect(s.decide('invitation expires').cosine).toBe(0);
		});
	});
	it('effectiveRetrieval lays the pack block over the file and keeps depth', () => {
		const pack = {
			assistant: { retrieval: { content: rrf } },
			embeddings: {
				params: {
					method: 'rrf',
					depth: 5,
					rrf_k: 10,
					embedding_weight: 2,
					min_cosine: 0.3,
					min_cosine_lexical: 0.1,
				},
			},
		} as never;
		const e = effectiveRetrieval(pack);
		expect(e.fusion).toEqual({ method: 'rrf', depth: 30, rrf_k: 10, embedding_weight: 2 });
		expect(e.out_of_scope.min_cosine).toBe(0.3);
		expect(e.out_of_scope.min_cosine_lexical).toBe(0.1);
		const off = effectiveRetrieval({
			assistant: { retrieval: { content: rrf } },
			embeddings: { params: { method: 'none' } },
		} as never);
		expect(off.fusion.method).toBe('none');
		const v1 = effectiveRetrieval({
			assistant: {
				retrieval: {
					content: {
						...RETRIEVAL,
						fusion: { method: 'none', rrf_k: 60, embedding_weight: 1 },
						out_of_scope: { min_coverage: 0.4, min_cosine: 0.55, min_query_terms: 1 },
					},
				},
			},
			embeddings: null,
		} as never);
		expect(v1.fusion.depth).toBe(30);
		expect(v1.out_of_scope.min_cosine_lexical).toBe(-1);
	});
});

describe('context selection (models.md 3.5)', () => {
	const mk = (n: number, len: number) =>
		Array.from({ length: n }, (_, i) => ({ id: `s${i}`, text: 'x'.repeat(len) }));
	const cfg = { max_sections: 4, max_context_chars: 4000, scan_limit: 8 };
	it('stops at max_sections and never cuts a section', () => {
		expect(selectContext(mk(10, 100), cfg).map((s) => s.id)).toEqual(['s0', 's1', 's2', 's3']);
	});
	it('skips a section that does not fit and continues', () => {
		const ranked = [
			{ id: 'a', text: 'x'.repeat(3000) },
			{ id: 'b', text: 'x'.repeat(1500) },
			{ id: 'c', text: 'x'.repeat(900) },
		];
		expect(selectContext(ranked, cfg).map((s) => s.id)).toEqual(['a', 'c']);
	});
	it('scans only scan_limit sections', () => {
		const ranked = [...mk(8, 3900), { id: 'late', text: 'x' }];
		expect(selectContext(ranked, cfg).map((s) => s.id)).not.toContain('late');
	});
});
