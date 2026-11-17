// @vitest-environment node
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { HelpPack } from '../index.ts';
import { readEmbeddingTable } from '../index.ts';
import { createHelpLoader, loadEmbeddingTable } from './loader.ts';
import type { Digest, Fetcher } from './loader.ts';
import { createPackSearcher, retrieve } from './searcher.ts';
import { testPack } from './fixtures/pack.ts';

const golden = JSON.parse(
	readFileSync(
		new URL('../../../scripts/help/fixtures/embeddings.table.golden.json', import.meta.url),
		'utf8',
	),
) as { languages: { en: { table_base64: string } } };
const TABLE = new Uint8Array(Buffer.from(golden.languages.en.table_base64, 'base64'));
const sha = (b: Uint8Array) => createHash('sha256').update(b).digest('hex');
const nodeDigest: Digest = async (b) => sha(b);

/** The fixture pack with the block of 6.7.4 for the golden table, the retrieval of schema 3 and an analyzer that matches the table. */
function packWithTable(overrides: Partial<NonNullable<HelpPack['embeddings']>> = {}): HelpPack {
	const t = readEmbeddingTable(TABLE);
	const pack = testPack();
	pack.schema_version = 2;
	pack.analyzer = t.meta.analyzer;
	pack.assistant.retrieval.content = {
		schema_version: 3,
		bm25: { k1: 1.2, b: 0.75, field_weights: { title: 2, text: 1, questions: 1.5 } },
		candidates: 10,
		diversify: 'page',
		out_of_scope: {
			min_coverage: 0.4,
			min_cosine: 0.55,
			min_cosine_lexical: -1,
			min_query_terms: 1,
		},
		fusion: { method: 'rrf', depth: 30, rrf_k: 60, embedding_weight: 1 },
	};
	pack.embeddings = {
		file: 'embed.bin',
		sha256: sha(TABLE),
		bytes: TABLE.length,
		format_version: 1,
		dims: t.dims,
		count: t.count,
		model: t.meta.model,
		locked: true,
		vocab_coverage: 0.95,
		calibrated: true,
		params: {
			method: 'rrf',
			depth: 30,
			rrf_k: 60,
			embedding_weight: 1,
			min_cosine: 0.55,
			min_cosine_lexical: -1,
		},
		...overrides,
	};
	return pack;
}

interface Files {
	manifest?: Record<string, unknown>;
	pack: HelpPack;
	table?: Uint8Array | 'missing' | 'error';
	tableManifestSha?: string;
}

function server(f: Files): { fetcher: Fetcher; calls: string[] } {
	const calls: string[] = [];
	const json = (v: unknown) => new Response(JSON.stringify(v));
	const manifest = f.manifest ?? {
		schema_version: 2,
		packs: { en: { file: 'en/pack.json', sha256: 'p' } },
		bundles: {},
		embeddings: {
			en: f.pack.embeddings
				? { file: 'en/embed.bin', sha256: f.tableManifestSha ?? sha(TABLE) }
				: null,
		},
		assistant_compatible: false,
	};
	const fetcher: Fetcher = async (url) => {
		calls.push(url);
		if (url === '/help/manifest.json') return json(manifest);
		if (url === '/help/en/pack.json') return json(f.pack);
		if (url === '/help/en/embed.bin') {
			if (f.table === 'missing') return new Response('', { status: 404 });
			if (f.table === 'error') throw new Error('network');
			return new Response((f.table ?? TABLE) as unknown as BodyInit);
		}
		return new Response('', { status: 404 });
	};
	return { fetcher, calls };
}

afterEach(() => vi.restoreAllMocks());

describe('the help loader and the embedding table (manual.md 6.7.5)', () => {
	it('loads and checks the table, and the searcher fuses with it', async () => {
		const s = server({ pack: packWithTable() });
		const loaded = await createHelpLoader(s.fetcher, nodeDigest).pack('en');
		expect(loaded.embeddingsReason).toBeNull();
		expect(loaded.embeddings).not.toBeNull();
		expect(loaded.embeddings!.count).toBe(readEmbeddingTable(TABLE).count);
		expect(s.calls).toEqual([
			'/help/manifest.json',
			'/help/en/pack.json',
			'/help/en/embed.bin',
		]);
		const ps = createPackSearcher(loaded.pack, loaded.embeddings);
		const r = retrieve(ps, 'rename a job');
		expect(r.decision.cosine).not.toBeNull();
	});
	it('resolves only after the table has loaded: the first query already sees it', async () => {
		let release!: () => void;
		const gate = new Promise<void>((r) => (release = r));
		const s = server({ pack: packWithTable() });
		const slow: Fetcher = async (url) => {
			if (url.endsWith('embed.bin')) await gate;
			return s.fetcher(url);
		};
		const loader = createHelpLoader(slow, nodeDigest);
		let done = false;
		const p = loader.pack('en').then((l) => ((done = true), l));
		await new Promise((r) => setTimeout(r, 20));
		expect(done).toBe(false);
		release();
		expect((await p).embeddings).not.toBeNull();
	});
	it('uses no table for a pack without a block, and fetches none', async () => {
		const pack = testPack();
		pack.schema_version = 2;
		const s = server({ pack });
		const loaded = await createHelpLoader(s.fetcher, nodeDigest).pack('en');
		expect(loaded.embeddings).toBeNull();
		expect(loaded.embeddingsReason).toBeNull();
		expect(s.calls).not.toContain('/help/en/embed.bin');
	});
	it('reads a pack of help-pack 1 and a manifest without the embeddings map unchanged', async () => {
		const pack = testPack();
		const manifest = {
			schema_version: 1,
			packs: { en: { file: 'en/pack.json', sha256: 'p' } },
			bundles: {},
			assistant_compatible: false,
		};
		const loaded = await createHelpLoader(server({ pack, manifest }).fetcher, nodeDigest).pack(
			'en',
		);
		expect(loaded.embeddings).toBeNull();
		expect(loaded.pack.embeddings).toBeNull();
	});
	it('refuses a pack newer than it knows', async () => {
		const pack = packWithTable();
		pack.schema_version = 3;
		await expect(
			createHelpLoader(server({ pack }).fetcher, nodeDigest).pack('en'),
		).rejects.toThrow(/unsupported schema/);
	});

	const failures: [string, () => Files, RegExp][] = [
		[
			'the table cannot be requested',
			() => ({ pack: packWithTable(), table: 'error' }),
			/could not be requested/,
		],
		['the table answers 404', () => ({ pack: packWithTable(), table: 'missing' }), /404/],
		[
			'its SHA-256 differs from the pack',
			() => ({ pack: packWithTable({ sha256: 'f'.repeat(64) }) }),
			/SHA-256/,
		],
		[
			'its SHA-256 differs from the manifest',
			() => ({ pack: packWithTable(), tableManifestSha: 'f'.repeat(64) }),
			/SHA-256/,
		],
		[
			'the manifest does not list it',
			() => ({
				pack: packWithTable(),
				manifest: {
					schema_version: 2,
					packs: { en: { file: 'en/pack.json', sha256: 'p' } },
					bundles: {},
					embeddings: { en: null },
				},
			}),
			/manifest/,
		],
		[
			'the bytes are not a table (hash matches the block)',
			() => {
				const junk = new Uint8Array(50);
				return {
					pack: packWithTable({ sha256: sha(junk) }),
					table: junk,
					tableManifestSha: sha(junk),
				};
			},
			/magic/,
		],
		[
			'the block names other dims',
			() => ({ pack: packWithTable({ dims: 64 }) }),
			/dims or count/,
		],
		[
			'the block names another model',
			() => ({
				pack: packWithTable({
					model: { id: 'embeddings', version: '9.9.9', hub_id: 'x', revision: 'y' },
				}),
			}),
			/meta.model/,
		],
		[
			'the pack has another analyzer',
			() => {
				const p = packWithTable();
				p.analyzer = { ...p.analyzer, version: 99 };
				return { pack: p };
			},
			/another analyzer/,
		],
		[
			'the pack is for another language',
			() => {
				const p = packWithTable();
				(p as { lang: string }).lang = 'it';
				return { pack: p };
			},
			/for en/,
		],
	];
	for (const [name, make, reason] of failures)
		it(`falls back to lexical ranking, with its reason, when ${name}`, async () => {
			const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
			const f = make();
			const loader = createHelpLoader(server(f).fetcher, nodeDigest);
			const loaded = await loader.pack('en');
			expect(loaded.embeddings).toBeNull();
			expect(loaded.embeddingsReason).toMatch(reason);
			expect(warn).toHaveBeenCalledTimes(1);
			// help stays available and lexical: the searcher answers as without a table
			const ps = createPackSearcher(loaded.pack, loaded.embeddings);
			expect(retrieve(ps, 'create a task').decision.cosine).toBeNull();
		});
	it('does not use the table when crypto.subtle is unavailable', async () => {
		const f = server({ pack: packWithTable() });
		const r = await loadEmbeddingTable(
			packWithTable(),
			{ file: 'en/embed.bin', sha256: sha(TABLE) },
			f.fetcher,
			async () => null,
		);
		expect(r.table).toBeNull();
		expect(r.reason).toMatch(/crypto.subtle/);
	});
});
