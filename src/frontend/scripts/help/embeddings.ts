/**
 * The embedding table at build time (manual.md 6.7.4): finds `{lang}.bin`, checks it, decides the state of the table for a pack
 * and builds the pack's `embeddings` block. Used by `help:build`, `help:bench`, `help:retrieve` and the golden file.
 */
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { isFile, sha256Hex } from '../lib/index.ts';
import {
	EmbeddingTableError,
	readEmbeddingTable,
	sameJson,
	vocabCoverage,
} from '../../src/help/lexical/embeddings.ts';
import type { EmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import type {
	EmbeddingsParams,
	HelpLang,
	HelpPack,
	PackEmbeddings,
	RetrievalParams,
} from '../../src/help/lexical/types.ts';
import { TEMPLATE_ROOT } from './paths.ts';

export const EMBED_CONFIG = 'training/assistant/config/embed.json';
export const DEFAULT_TABLE_DIR = 'models/embeddings/weights';
export const CALIBRATION_FILE = 'calibration.json';

export interface EmbedConfig {
	schema_version: number;
	model: Record<string, unknown>;
	table: { dims: number } & Record<string, unknown>;
	sources: Record<string, unknown>;
	build: { min_vocab_coverage: number };
	calibration: {
		held_out_share: number;
		min_items: number;
		rrf_k: number[];
		embedding_weight: number[];
		cosine_step: number;
		max_veto_loss: number;
		target_false_no_match: number;
	};
}

export interface LoadedFile<T> {
	data: T;
	bytes: Buffer;
	sha256: string;
}

const readJson = <T>(file: string): LoadedFile<T> | null => {
	if (!isFile(file)) return null;
	const bytes = readFileSync(file);
	return { data: JSON.parse(bytes.toString('utf8')) as T, bytes, sha256: sha256Hex(bytes) };
};

/** `training/assistant/config/embed.json` of the built root, else of the template (domain trees). */
export function loadEmbedConfig(root: string): LoadedFile<EmbedConfig> {
	const f = readJson<EmbedConfig>(join(root, EMBED_CONFIG)) ?? readJson<EmbedConfig>(join(TEMPLATE_ROOT, EMBED_CONFIG));
	if (!f) throw new Error(`${EMBED_CONFIG} is missing`);
	return f;
}

interface LockEmbeddings {
	model_version: string;
	base_model: { revision: string };
	exports: { files: { path: string; sha256: string }[] }[];
	calibration: { params: Record<string, EmbeddingsParams> };
}

/** The `embeddings` entry of `models/model.lock`, or null. */
export function readLockEmbeddings(root: string): LockEmbeddings | null {
	try {
		const l = readJson<{ models?: Record<string, unknown> }>(join(root, 'models/model.lock'));
		return (l?.data?.models?.embeddings as LockEmbeddings | undefined) ?? null;
	} catch {
		return null;
	}
}

interface CalibrationFile {
	languages?: Record<string, { table_sha256?: string; calibrated?: boolean; params?: EmbeddingsParams }>;
}

export type TableStateKind = 'off' | 'none' | 'invalid' | 'analyzer' | 'unlocked' | 'locked';

export interface TableState {
	kind: TableStateKind;
	/** True when the table is used (the pack has the block). */
	used: boolean;
	/** The message for the console: an info line or, with `warning`, a warning. */
	message: string | null;
	warning: boolean;
	/** A finding that `--strict` turns into an error. */
	strictFailure: boolean;
	table: EmbeddingTable | null;
	bytes: Buffer | null;
	block: PackEmbeddings | null;
}

export interface ResolveOptions {
	root: string;
	lang: HelpLang;
	pack: Pick<HelpPack, 'analyzer' | 'index' | 'assistant'>;
	/** Folder with `{lang}.bin` (and `calibration.json` for an unlocked table); default `models/embeddings/weights/`. */
	dir: string;
	unlocked: boolean;
	config: EmbedConfig;
}

const state = (kind: TableStateKind, message: string | null, extra: Partial<TableState> = {}): TableState => ({
	kind,
	used: false,
	message,
	warning: false,
	strictFailure: false,
	table: null,
	bytes: null,
	block: null,
	...extra,
});

const MODEL_KEYS = ['id', 'version', 'hub_id', 'revision'] as const;

/** The default parameters of `retrieval.json` as the `params` of a block that is not calibrated (6.7.4). */
export function defaultParams(retrieval: RetrievalParams): EmbeddingsParams {
	return {
		method: retrieval.fusion.method,
		depth: retrieval.fusion.depth ?? 30,
		rrf_k: retrieval.fusion.rrf_k,
		embedding_weight: retrieval.fusion.embedding_weight,
		min_cosine: retrieval.out_of_scope.min_cosine,
		min_cosine_lexical: retrieval.out_of_scope.min_cosine_lexical ?? -1,
	};
}

/** Keys of `params` in the order of 6.4.4, `depth` always from `retrieval.json`. */
function orderedParams(p: Partial<EmbeddingsParams>, defaults: EmbeddingsParams): EmbeddingsParams {
	return {
		method: p.method ?? defaults.method,
		depth: defaults.depth,
		rrf_k: p.rrf_k ?? defaults.rrf_k,
		embedding_weight: p.embedding_weight ?? defaults.embedding_weight,
		min_cosine: p.min_cosine ?? defaults.min_cosine,
		min_cosine_lexical: p.min_cosine_lexical ?? defaults.min_cosine_lexical,
	};
}

/** The state of the table of one language (manual.md 6.7.4): the first matching row decides. */
export function resolveTable(o: ResolveOptions): TableState {
	const { lang, pack, dir, config } = o;
	const retrieval = pack.assistant.retrieval.content;
	const defaults = defaultParams(retrieval);
	if (retrieval.fusion.method === 'none')
		return state('off', `${lang}: fusion.method is none: lexical ranking, no table used`);
	const file = join(dir, `${lang}.bin`);
	if (!isFile(file)) return state('none', `${lang}: no ${lang}.bin in ${dir}: lexical ranking only`);

	const bytes = readFileSync(file);
	let table: EmbeddingTable;
	try {
		table = readEmbeddingTable(bytes);
		if (table.lang !== lang) throw new EmbeddingTableError(`the table is for ${table.lang}, the pack for ${lang}`);
		const m = table.meta.model as unknown;
		if (!m || typeof m !== 'object' || MODEL_KEYS.some((k) => typeof (m as Record<string, unknown>)[k] !== 'string'))
			throw new EmbeddingTableError('meta.model is malformed');
		if (!table.meta.analyzer || typeof table.meta.analyzer !== 'object')
			throw new EmbeddingTableError('meta.analyzer is malformed');
	} catch (e) {
		const why = e instanceof EmbeddingTableError ? e.message : String(e);
		return state('invalid', `${lang}: the table is not valid (${why}): lexical ranking only`, { warning: true, strictFailure: true });
	}
	if (!sameJson(table.meta.analyzer, pack.analyzer))
		return state('analyzer', `${lang}: the table was built with another analyzer: rebuild it`, { warning: true, strictFailure: true });

	const sha = sha256Hex(bytes);
	const lock = readLockEmbeddings(o.root);
	const lockedFile = lock?.exports.flatMap((x) => x.files).find((f) => f.path === `${lang}.bin`);
	let difference: string | null = null;
	if (!lock) difference = 'models/model.lock has no embeddings entry';
	else if (!lockedFile || lockedFile.sha256 !== sha) difference = `the SHA-256 of ${lang}.bin differs from the lock`;
	else if (table.meta.model.version !== lock.model_version) difference = `meta.model.version ${table.meta.model.version} differs from the lock's ${lock.model_version}`;
	else if (table.meta.model.revision !== lock.base_model.revision) difference = 'meta.model.revision differs from the lock';
	if (difference && !o.unlocked)
		return state('unlocked', `${lang}: the table is not locked (${difference}): lexical ranking only; use --unlocked for a table not yet recorded`, { warning: true, strictFailure: true });

	const locked = difference === null;
	let params: EmbeddingsParams = defaults;
	let calibrated = false;
	const notes: string[] = [];
	if (locked) {
		const p = lock!.calibration?.params?.[lang];
		if (p) {
			params = orderedParams(p, defaults);
			const report = readJson<CalibrationFile>(join(o.root, 'models/embeddings/calibration', `${lock!.model_version}.json`));
			calibrated = report?.data.languages?.[lang]?.calibrated === true;
		}
	} else {
		const cal = readJson<CalibrationFile>(join(dir, CALIBRATION_FILE));
		const entry = cal?.data.languages?.[lang];
		if (entry?.params) {
			if (entry.table_sha256 === sha) {
				params = orderedParams(entry.params, defaults);
				calibrated = entry.calibrated === true;
			} else notes.push(`${CALIBRATION_FILE} is for another table: ignored, run help:bench -- --calibrate again`);
		}
	}
	if (params.method === 'none')
		return state('off', `${lang}: the calibration judged the table not to help (params.method is none): lexical ranking, no table used`);

	const block: PackEmbeddings = {
		file: 'embed.bin',
		sha256: sha,
		bytes: bytes.length,
		format_version: table.meta.format_version,
		dims: table.dims,
		count: table.count,
		model: {
			id: table.meta.model.id,
			version: table.meta.model.version,
			hub_id: table.meta.model.hub_id,
			revision: table.meta.model.revision,
		},
		locked,
		vocab_coverage: vocabCoverage(table, pack.index),
		calibrated,
		params,
	};
	const low = block.vocab_coverage < config.build.min_vocab_coverage;
	const message =
		[
			`${lang}: ${locked ? 'locked' : 'UNLOCKED'} table used (${table.count} stems, vocab coverage ${block.vocab_coverage}, ${calibrated ? 'calibrated' : 'not calibrated'})`,
			...notes,
		].join('; ') +
		(low
			? `; vocab coverage ${block.vocab_coverage} is below ${config.build.min_vocab_coverage}: the manual gained words the table lacks, rebuild it`
			: '');
	return state(locked ? 'locked' : 'unlocked', message, { used: true, table, bytes, block, warning: low, strictFailure: low });
}

/** Loads the table of a written pack: next to the pack file, by the block's `file`. Hash and structure checks of 6.7.5. */
export function loadTableForPack(packFile: string, pack: Pick<HelpPack, 'lang' | 'analyzer' | 'embeddings'>): { table: EmbeddingTable | null; error: string | null } {
	const block = pack.embeddings;
	if (!block) return { table: null, error: null };
	try {
		const bytes = readFileSync(resolve(packFile, '..', block.file));
		if (sha256Hex(bytes) !== block.sha256) throw new EmbeddingTableError('the SHA-256 of the table differs from the pack block');
		const table = readEmbeddingTable(bytes);
		if (table.lang !== pack.lang) throw new EmbeddingTableError(`the table is for ${table.lang}, the pack for ${pack.lang}`);
		if (table.dims !== block.dims || table.count !== block.count) throw new EmbeddingTableError('dims or count differ from the pack block');
		if (!sameJson(table.meta.model, block.model)) throw new EmbeddingTableError('meta.model differs from the pack block');
		if (!sameJson(table.meta.analyzer, pack.analyzer)) throw new EmbeddingTableError('meta.analyzer differs from the pack analyzer');
		return { table, error: null };
	} catch (e) {
		return { table: null, error: e instanceof Error ? e.message : String(e) };
	}
}
