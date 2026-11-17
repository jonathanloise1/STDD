// @vitest-environment node
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { buildIndex } from './indexer.ts';
import { createAnalyzer, parseStopwords } from './analyzer.ts';
import { compileGlossary } from './glossary.ts';
import {
	checkTableForPack,
	createEmbeddingScorer,
	EmbeddingTableError,
	readEmbeddingTable,
	sameJson,
	vocabCoverage,
} from './embeddings.ts';

const fixture = new URL(
	'../../../scripts/help/fixtures/embeddings.table.golden.json',
	import.meta.url,
);
const golden = JSON.parse(readFileSync(fixture, 'utf8')) as {
	languages: Record<
		string,
		{
			inputs: {
				pack: {
					sections: { id: string; title: string; text: string; story: string | null }[];
					questions: { story: string; text: string }[];
				};
				glossary: { term: string; aliases: string[] }[];
			};
			table_base64: string;
			decoded: {
				dims: number;
				count: number;
				vocab: string[];
				weights: number[];
				scales: number[];
				vectors: number[][];
			};
		}
	>;
};
const bytesOf = (lang: 'en' | 'it') =>
	new Uint8Array(Buffer.from(golden.languages[lang].table_base64, 'base64'));
const enWords = parseStopwords(
	readFileSync(new URL('./stopwords.en.txt', import.meta.url), 'utf8'),
);
const en = createAnalyzer('en', enWords);

/** A table written by the layout of manual.md 6.7.2, independently of the Python writer, for the checks to break. */
function craft(
	o: {
		lang?: string;
		vocab?: string[];
		weights?: number[];
		scales?: number[];
		version?: number;
		magic?: number[];
		meta?: unknown;
		dims?: number;
		trailing?: number;
		reserved?: number;
	} = {},
): Uint8Array {
	const vocab = o.vocab ?? ['alpha', 'beta', 'gamma'];
	const dims = o.dims ?? 4;
	const scales = o.scales ?? Array.from({ length: dims }, () => 0.5);
	const meta = new TextEncoder().encode(JSON.stringify(o.meta ?? { kind: 'help-embeddings' }));
	const vocabBytes = new TextEncoder().encode(vocab.map((s) => `${s}\n`).join(''));
	const weights = o.weights ?? vocab.map(() => 7);
	const size =
		32 + 4 * dims + meta.length + vocabBytes.length + vocab.length + vocab.length * dims;
	const out = new Uint8Array(size + (o.trailing ?? 0));
	const v = new DataView(out.buffer);
	out.set(o.magic ?? [0x48, 0x45, 0x4c, 0x50, 0x45, 0x4d, 0x42, 0x00]);
	v.setUint16(8, o.version ?? 1, true);
	out[10] = (o.lang ?? 'en').charCodeAt(0);
	out[11] = (o.lang ?? 'en').charCodeAt(1);
	v.setUint16(12, dims, true);
	v.setUint32(16, vocab.length, true);
	v.setUint32(20, meta.length, true);
	v.setUint32(24, vocabBytes.length, true);
	v.setUint32(28, o.reserved ?? 0, true);
	let off = 32;
	scales.forEach((s, j) => v.setFloat32(off + 4 * j, s, true));
	off += 4 * dims;
	out.set(meta, off);
	off += meta.length;
	out.set(vocabBytes, off);
	off += vocabBytes.length;
	out.set(weights, off);
	off += vocab.length;
	for (let i = 0; i < vocab.length * dims; i++) out[off + i] = (i * 37) % 255;
	return out;
}

describe('readEmbeddingTable (manual.md 6.7.2, 6.7.5)', () => {
	it('reads a table: header, scales, meta, vocabulary, weights, vectors', () => {
		const t = readEmbeddingTable(craft());
		expect(t).toMatchObject({ lang: 'en', dims: 4, count: 3, bytes: craft().length });
		expect(t.vocab).toEqual(['alpha', 'beta', 'gamma']);
		expect([...t.scales]).toEqual([0.5, 0.5, 0.5, 0.5]);
		expect([...t.weights]).toEqual([7, 7, 7]);
		expect(t.vectors).toHaveLength(12);
		expect(t.index.get('beta')).toBe(1);
		expect(t.meta).toEqual({ kind: 'help-embeddings' });
	});
	it('reads the golden table written by the Python builder and decodes it exactly', () => {
		for (const lang of ['en', 'it'] as const) {
			const g = golden.languages[lang].decoded;
			const t = readEmbeddingTable(bytesOf(lang));
			expect(t.vocab).toEqual(g.vocab);
			expect([...t.weights]).toEqual(g.weights);
			expect([...t.scales]).toEqual(g.scales);
			expect(t.dims).toBe(g.dims);
			for (let i = 0; i < t.count; i++)
				for (let j = 0; j < t.dims; j++)
					expect(t.vectors[i * t.dims + j] * t.scales[j]).toBe(g.vectors[i][j]);
		}
	});
	it('reads a table from a view that does not start at the buffer start', () => {
		const raw = craft();
		const padded = new Uint8Array(raw.length + 3);
		padded.set(raw, 3);
		const t = readEmbeddingTable(padded.subarray(3));
		expect(t.vocab).toEqual(['alpha', 'beta', 'gamma']);
		expect(t.vectors[1]).toBe(raw[raw.length - 12 + 1]);
	});
	const reasons: [string, () => Uint8Array, RegExp][] = [
		['a file shorter than the header', () => craft().subarray(0, 20), /shorter/],
		['bad magic', () => craft({ magic: [1, 2, 3, 4, 5, 6, 7, 8] }), /magic/],
		['a newer format version', () => craft({ version: 2 }), /format_version/],
		['format version 0', () => craft({ version: 0 }), /format_version/],
		['a size that does not match the header', () => craft({ trailing: 1 }), /bytes/],
		['a reserved field that is not zero', () => craft({ reserved: 1 }), /reserved/],
		['a scale of 0', () => craft({ scales: [0.5, 0, 0.5, 0.5] }), /scale/],
		['a negative scale', () => craft({ scales: [0.5, -1, 0.5, 0.5] }), /scale/],
		['an unsorted vocabulary', () => craft({ vocab: ['beta', 'alpha', 'gamma'] }), /sorted/],
		['a duplicate stem', () => craft({ vocab: ['alpha', 'alpha', 'gamma'] }), /unique/],
		['a weight of 0', () => craft({ weights: [7, 0, 7] }), /weight/],
		['a meta that is not JSON', () => craft({ meta: 'x' }), /meta/],
	];
	for (const [name, make, message] of reasons)
		it(`refuses ${name} with its reason`, () => {
			expect(() => readEmbeddingTable(make())).toThrow(message);
			expect(() => readEmbeddingTable(make())).toThrow(EmbeddingTableError);
		});
	it('sorts the vocabulary by code point, which is the order of JavaScript strings for the BMP', () => {
		const t = readEmbeddingTable(craft({ vocab: ['Zeta', 'alpha', 'zeta', 'été'] }));
		expect(t.vocab).toEqual(['Zeta', 'alpha', 'zeta', 'été']);
		expect(() => readEmbeddingTable(craft({ vocab: ['alpha', 'zeta', 'Zeta'] }))).toThrow(
			/sorted/,
		);
	});
});

describe('checkTableForPack (manual.md 6.7.5 steps 2 and 3)', () => {
	const model = { id: 'embeddings', version: '1.0.0', hub_id: 'o/m', revision: 'a'.repeat(40) };
	const analyzer = { id: 'snowball-en', version: 2, stopwords_sha256: 'b'.repeat(64) };
	const table = readEmbeddingTable(craft({ meta: { kind: 'help-embeddings', model, analyzer } }));
	const block = { dims: 4, count: 3, model };
	it('accepts a table that matches the pack, whatever the key order of meta', () => {
		expect(() =>
			checkTableForPack(
				table,
				{
					lang: 'en',
					analyzer: {
						stopwords_sha256: analyzer.stopwords_sha256,
						version: 2,
						id: 'snowball-en',
					},
				},
				block,
			),
		).not.toThrow();
	});
	it('refuses another language, dims, count, model or analyzer', () => {
		expect(() => checkTableForPack(table, { lang: 'it', analyzer }, block)).toThrow(/for en/);
		expect(() =>
			checkTableForPack(table, { lang: 'en', analyzer }, { ...block, dims: 8 }),
		).toThrow(/dims or count/);
		expect(() =>
			checkTableForPack(table, { lang: 'en', analyzer }, { ...block, count: 4 }),
		).toThrow(/dims or count/);
		expect(() =>
			checkTableForPack(
				table,
				{ lang: 'en', analyzer },
				{ ...block, model: { ...model, version: '2.0.0' } },
			),
		).toThrow(/meta.model/);
		expect(() =>
			checkTableForPack(table, { lang: 'en', analyzer: { ...analyzer, version: 3 } }, block),
		).toThrow(/another analyzer/);
	});
	it('compares JSON structurally', () => {
		expect(sameJson({ a: 1, b: [1, { c: 2 }] }, { b: [1, { c: 2 }], a: 1 })).toBe(true);
		expect(sameJson({ a: 1 }, { a: 1, b: 2 })).toBe(false);
		expect(sameJson([1, 2], [2, 1])).toBe(false);
	});
});

describe('vectors (manual.md 6.7.6)', () => {
	const g = golden.languages.en;
	const table = readEmbeddingTable(bytesOf('en'));
	const sections = g.inputs.pack.sections.map((x) => ({
		id: x.id,
		title: x.title,
		text: x.text,
		questions: x.story
			? g.inputs.pack.questions.filter((q) => q.story === x.story).map((q) => q.text)
			: [],
	}));
	const index = buildIndex(sections, en, compileGlossary(g.inputs.glossary, en));
	const weights = { title: 2, text: 1, questions: 1.5 };
	const scorer = createEmbeddingScorer(
		table,
		index,
		sections.map((s) => s.id),
		weights,
	);

	/** The formula of 6.7.6 written out from the decoded vectors of the golden file. */
	const decoded = (stem: string): number[] | null => {
		const r = g.decoded.vocab.indexOf(stem);
		return r < 0 ? null : g.decoded.vectors[r].map((x) => x * (g.decoded.weights[r] / 255));
	};
	const unit = (x: number[]) => {
		const n = Math.sqrt(x.reduce((s, v) => s + v * v, 0));
		return n === 0 ? null : x.map((v) => v / n);
	};
	it('computes the query vector as the normalised sum of u*v over the distinct stems in the table', () => {
		const q = scorer.queryVector(['job', 'task', 'zzzz', 'task'].map((s) => en.analyze(s)[0]));
		const rows = ['job', 'task'].map((s) => decoded(en.analyze(s)[0]) as number[]);
		const want = unit(rows[0].map((_, j) => rows.reduce((s, r) => s + r[j], 0)));
		expect(q).not.toBeNull();
		q!.forEach((v, j) => expect(v).toBeCloseTo(want![j], 12));
		expect(Math.hypot(...q!)).toBeCloseTo(1, 12);
	});
	it('has no query vector when no stem is in the table', () => {
		expect(scorer.queryVector(['zzzz'])).toBeNull();
		expect(scorer.queryVector([])).toBeNull();
		expect(scorer.rank(['zzzz'], 30)).toEqual([]);
		expect(scorer.cosine(['zzzz'], 0)).toBeNull();
		expect(scorer.cosines(['zzzz'])).toBeNull();
	});
	it('computes section vectors from the field-weighted term frequencies, normalised', () => {
		const i = sections.findIndex((s) => s.id === 'team/US-TEAM-01#rules');
		const v = scorer.sectionVector(i)!;
		expect(Math.hypot(...v)).toBeCloseTo(1, 12);
		// the direction agrees with a recomputation from the postings of the index (glossary gains included)
		const acc = new Array(table.dims).fill(0);
		for (const [stem, posting] of Object.entries(index.terms)) {
			const r = g.decoded.vocab.indexOf(stem);
			if (r < 0) continue;
			let w = 0;
			for (let k = 1; k < posting.length; k++) {
				const [doc, field, tf] = posting[k] as [number, number, number];
				if (doc === i) w += [2, 1, 1.5][field] * tf;
			}
			if (w) decoded(stem)!.forEach((x, j) => (acc[j] += w * x));
		}
		const want = unit(acc)!;
		v.forEach((x, j) => expect(x).toBeCloseTo(want[j], 12));
	});
	it('gives no vector to a section without a stem in the table, and 0 as its cosine', () => {
		const blank = sections.findIndex((s) => s.id === 'home#blank');
		const rare = sections.findIndex((s) => s.id === 'home#rare');
		expect(scorer.sectionVector(blank)).toBeNull();
		expect(scorer.sectionVector(rare)).toBeNull();
		expect(scorer.cosine(['task'], blank)).toBe(0);
		expect(scorer.cosines(['task'])![blank]).toBeNull();
	});
	it('ranks by cosine above 0, ties by section id, and cuts to depth', () => {
		const stems = ['task'].map((s) => en.analyze(s)[0]);
		const all = scorer.rank(stems, 100);
		expect(all.length).toBeGreaterThan(2);
		expect(all.every((m) => m.cosine > 0)).toBe(true);
		for (let k = 1; k < all.length; k++)
			expect(all[k - 1].cosine).toBeGreaterThanOrEqual(all[k].cosine);
		expect(scorer.rank(stems, 2)).toEqual(all.slice(0, 2));
		const ids = sections.map((s) => s.id);
		expect(ids[all[0].index].startsWith('tasks/')).toBe(true);
	});
	it('computes the vocabulary coverage of an index', () => {
		const cov = vocabCoverage(table, index);
		expect(cov).toBeGreaterThan(0.5);
		expect(cov).toBeLessThanOrEqual(1);
		expect(vocabCoverage({ ...table, index: new Map() }, index)).toBe(0);
		const all = Object.fromEntries(Object.keys(index.terms).map((k) => [k, 0]));
		expect(
			vocabCoverage({ ...table, index: new Map(Object.keys(all).map((k) => [k, 0])) }, index),
		).toBe(1);
	});
});
