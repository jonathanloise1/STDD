/**
 * The static embedding table of manual.md 6.7: the reader of the `{lang}.bin` file (6.7.2, checks of 6.7.5) and the vectors
 * of 6.7.6. Pure and framework-free; it runs in the browser, in the scripts and in the bench. The hash check needs a digest
 * function and is the caller's (`crypto.subtle` in the browser, `node:crypto` in the scripts).
 *
 * `training/assistant/embtable.py` writes the file and `training/assistant/lexical.py` is the Python port of the vectors; both are
 * tested against `scripts/help/fixtures/embeddings.table.golden.json` and `embeddings.golden.json`.
 */
import { INDEX_FIELDS } from './types.ts';
import type { EmbeddingMatch, EmbeddingScorer } from './fusion.ts';
import type { HelpLang, PackEmbeddings, PackIndex } from './types.ts';

export const TABLE_FORMAT_VERSION = 1;
const HEADER_BYTES = 32;
const MAGIC = [0x48, 0x45, 0x4c, 0x50, 0x45, 0x4d, 0x42, 0x00]; // "HELPEMB\0"

/** The table cannot be used; the message is the reason of 6.7.5 and is kept for the panel's status line. */
export class EmbeddingTableError extends Error {
	constructor(message: string) {
		super(message);
		this.name = 'EmbeddingTableError';
	}
}

export interface TableMeta {
	kind: string;
	format_version: number;
	lang: string;
	model: { id: string; version: string; hub_id: string; revision: string };
	analyzer: { id: string; version: number; stopwords_sha256: string };
	config_sha256: string;
	inputs_sha256: string;
	sources: { project: number; wordlist: number; tokenizer: number };
}

export interface EmbeddingTable {
	lang: string;
	dims: number;
	count: number;
	/** The float32 scales read as float64 (exact). */
	scales: Float64Array;
	meta: TableMeta;
	/** Stems, sorted by code point, unique. */
	vocab: string[];
	/** Row of a stem. */
	index: Map<string, number>;
	weights: Uint8Array;
	/** Row-major, `count x dims`. */
	vectors: Int8Array;
	bytes: number;
}

const isObject = (v: unknown): v is Record<string, unknown> =>
	typeof v === 'object' && v !== null && !Array.isArray(v);

/** Structural equality of JSON values, independent of key order. */
export function sameJson(a: unknown, b: unknown): boolean {
	if (a === b) return true;
	if (Array.isArray(a) && Array.isArray(b))
		return a.length === b.length && a.every((x, i) => sameJson(x, b[i]));
	if (isObject(a) && isObject(b)) {
		const ka = Object.keys(a).sort();
		const kb = Object.keys(b).sort();
		return ka.length === kb.length && ka.every((k, i) => k === kb[i] && sameJson(a[k], b[k]));
	}
	return false;
}

/**
 * Parses a table: size, magic, `format_version`, the exact size, the scales, `meta`, the vocabulary and the weights
 * (6.7.5 checks 2 and 4, and 3 as far as `meta` parsing goes). Throws `EmbeddingTableError`.
 */
export function readEmbeddingTable(bytes: Uint8Array): EmbeddingTable {
	if (bytes.length < HEADER_BYTES)
		throw new EmbeddingTableError('the file is shorter than its 32-byte header');
	const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
	if (MAGIC.some((m, i) => bytes[i] !== m))
		throw new EmbeddingTableError('bad magic: not a help embedding table');
	const version = view.getUint16(8, true);
	if (version < 1 || version > TABLE_FORMAT_VERSION)
		throw new EmbeddingTableError(
			`format_version ${version} is not supported (this reader knows ${TABLE_FORMAT_VERSION})`,
		);
	if (bytes[10] > 0x7f || bytes[11] > 0x7f)
		throw new EmbeddingTableError('the language is not ASCII');
	const lang = String.fromCharCode(bytes[10], bytes[11]);
	const dims = view.getUint16(12, true);
	const count = view.getUint32(16, true);
	const metaBytes = view.getUint32(20, true);
	const vocabBytes = view.getUint32(24, true);
	if (dims < 1 || view.getUint16(14, true) !== 0 || view.getUint32(28, true) !== 0)
		throw new EmbeddingTableError('dims is zero or a reserved field is not zero');
	const size = HEADER_BYTES + 4 * dims + metaBytes + vocabBytes + count + count * dims;
	if (bytes.length !== size)
		throw new EmbeddingTableError(
			`the file has ${bytes.length} bytes, its header says ${size}`,
		);

	let off = HEADER_BYTES;
	const scales = new Float64Array(dims);
	for (let j = 0; j < dims; j++) {
		scales[j] = view.getFloat32(off + 4 * j, true);
		if (!(scales[j] > 0))
			throw new EmbeddingTableError('a quantisation scale is not greater than 0');
	}
	off += 4 * dims;
	const decoder = new TextDecoder('utf-8', { fatal: true });
	let meta: TableMeta;
	try {
		const parsed: unknown = JSON.parse(decoder.decode(bytes.subarray(off, off + metaBytes)));
		if (!isObject(parsed)) throw new Error('not an object');
		meta = parsed as unknown as TableMeta;
	} catch {
		throw new EmbeddingTableError('meta is not UTF-8 JSON');
	}
	off += metaBytes;
	let text: string;
	try {
		text = decoder.decode(bytes.subarray(off, off + vocabBytes));
	} catch {
		throw new EmbeddingTableError('the vocabulary is not UTF-8');
	}
	off += vocabBytes;
	let vocab: string[];
	if (count === 0) {
		if (text) throw new EmbeddingTableError('the vocabulary has bytes but count is 0');
		vocab = [];
	} else {
		if (!text.endsWith('\n'))
			throw new EmbeddingTableError('the vocabulary does not end with a line feed');
		vocab = text.slice(0, -1).split('\n');
	}
	if (vocab.length !== count)
		throw new EmbeddingTableError(
			`the vocabulary has ${vocab.length} stems, count says ${count}`,
		);
	for (let i = 1; i < vocab.length; i++)
		if (!(vocab[i - 1] < vocab[i]))
			throw new EmbeddingTableError('the vocabulary is not sorted by code point and unique');
	if (vocab.length && vocab[0] === '')
		throw new EmbeddingTableError('the vocabulary has an empty stem');
	const weights = bytes.subarray(off, off + count);
	off += count;
	for (let i = 0; i < count; i++)
		if (weights[i] < 1) throw new EmbeddingTableError('a weight is 0');
	const vectors = new Int8Array(bytes.buffer, bytes.byteOffset + off, count * dims);
	return {
		lang,
		dims,
		count,
		scales,
		meta,
		vocab,
		index: new Map(vocab.map((s, i) => [s, i])),
		weights,
		vectors,
		bytes: bytes.length,
	};
}

/**
 * Checks 2 and 3 of 6.7.5 against the pack: the language, `dims` and `count` of the block, `meta.model` equal to the block's
 * model and `meta.analyzer` equal to the pack's analyzer. Throws `EmbeddingTableError`.
 */
export function checkTableForPack(
	table: EmbeddingTable,
	pack: { lang: HelpLang | string; analyzer: unknown },
	block: Pick<PackEmbeddings, 'dims' | 'count' | 'model'>,
): void {
	if (table.lang !== pack.lang)
		throw new EmbeddingTableError(`the table is for ${table.lang}, the pack for ${pack.lang}`);
	if (table.dims !== block.dims || table.count !== block.count)
		throw new EmbeddingTableError('dims or count differ from the pack block');
	if (!sameJson(table.meta.model, block.model))
		throw new EmbeddingTableError('meta.model differs from the pack block');
	if (!sameJson(table.meta.analyzer, pack.analyzer))
		throw new EmbeddingTableError(
			'the table was built with another analyzer: rebuild it (manual.md 6.7.4)',
		);
}

/** The stems of the table that the pack's index has, as `sum of tf over their postings / sum of tf over all postings`, 4 decimals. */
export function vocabCoverage(table: EmbeddingTable, index: PackIndex): number {
	let inTable = 0;
	let total = 0;
	for (const [stem, posting] of Object.entries(index.terms)) {
		const has = table.index.has(stem);
		for (let i = 1; i < posting.length; i++) {
			const tf = (posting[i] as [number, number, number])[2];
			total += tf;
			if (has) inTable += tf;
		}
	}
	return total === 0 ? 0 : Math.round((inTable / total) * 1e4) / 1e4;
}

function normalise(x: Float64Array): Float64Array | null {
	let s = 0;
	for (let j = 0; j < x.length; j++) s += x[j] * x[j];
	const n = Math.sqrt(s);
	if (n === 0) return null;
	const out = new Float64Array(x.length);
	for (let j = 0; j < x.length; j++) out[j] = x[j] / n;
	return out;
}

const byCodePoint = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/**
 * Section vectors, computed once from the pack's index (glossary gains included), and query vectors (6.7.6):
 *
 *     s_d = normalise(sum over stems t in the table of (sum over fields f of w_f * tf(t,d,f)) * u_t * v_t)
 *     q   = normalise(sum over the distinct query stems t in the table of u_t * v_t)
 *
 * `u_t` is the stem's weight divided by 255, `v_t` its decoded vector; all arithmetic is float64.
 */
export function createEmbeddingScorer(
	table: EmbeddingTable,
	index: PackIndex,
	sectionIds: readonly string[],
	fieldWeights: { title: number; text: number; questions: number },
): EmbeddingScorer {
	const { dims, vectors, scales, weights } = table;
	const w = INDEX_FIELDS.map((f) => fieldWeights[f]);
	/** `u_t * v_t` of a row, element by element: `(q * scale) * u`. */
	const row = (r: number, out: Float64Array, factor: number): void => {
		const u = weights[r] / 255;
		for (let j = 0; j < dims; j++) out[j] += factor * (vectors[r * dims + j] * scales[j] * u);
	};

	// stem rows weight per section: doc -> (row -> sum over fields of w_f * tf)
	const perDoc: Map<number, number>[] = Array.from({ length: index.doc_count }, () => new Map());
	for (const [stem, posting] of Object.entries(index.terms)) {
		const r = table.index.get(stem);
		if (r === undefined) continue;
		for (let i = 1; i < posting.length; i++) {
			const [doc, field, tf] = posting[i] as [number, number, number];
			const m = perDoc[doc];
			m.set(r, (m.get(r) ?? 0) + w[field] * tf);
		}
	}
	const sectionVectors: (Float64Array | null)[] = perDoc.map((m) => {
		if (m.size === 0) return null;
		const acc = new Float64Array(dims);
		for (const r of [...m.keys()].sort((a, b) => a - b)) row(r, acc, m.get(r) as number);
		return normalise(acc);
	});

	let lastKey: string | null = null;
	let lastVector: Float64Array | null = null;
	function queryVector(stems: readonly string[]): Float64Array | null {
		const key = stems.join('\u0000');
		if (key === lastKey) return lastVector;
		const rows = [
			...new Set(
				stems.map((s) => table.index.get(s)).filter((r): r is number => r !== undefined),
			),
		].sort((a, b) => a - b);
		let q: Float64Array | null = null;
		if (rows.length) {
			const acc = new Float64Array(dims);
			for (const r of rows) row(r, acc, 1);
			q = normalise(acc);
		}
		lastKey = key;
		lastVector = q;
		return q;
	}
	const dot = (a: Float64Array, b: Float64Array): number => {
		let s = 0;
		for (let j = 0; j < dims; j++) s += a[j] * b[j];
		return s;
	};

	return {
		sectionVector: (i) => sectionVectors[i] ?? null,
		queryVector,
		rank(stems, depth): EmbeddingMatch[] {
			const q = queryVector(stems);
			if (!q) return [];
			const hits: EmbeddingMatch[] = [];
			sectionVectors.forEach((s, i) => {
				if (!s) return;
				const c = dot(q, s);
				if (c > 0) hits.push({ index: i, cosine: c });
			});
			hits.sort(
				(a, b) =>
					b.cosine - a.cosine || byCodePoint(sectionIds[a.index], sectionIds[b.index]),
			);
			return hits.slice(0, depth);
		},
		cosine(stems, sectionIndex): number | null {
			const q = queryVector(stems);
			if (!q) return null;
			const s = sectionVectors[sectionIndex];
			return s ? dot(q, s) : 0;
		},
		cosines(stems): (number | null)[] | null {
			const q = queryVector(stems);
			if (!q) return null;
			return sectionVectors.map((s) => (s ? dot(q, s) : null));
		},
	};
}
