/**
 * Loads the manifest, the manual bundle and the help pack from `/help/` (manual.md 5.1).
 * Any failure is one error: the surfaces say "unavailable" and never need to know why.
 * Without a manual the files do not exist (a dev server answers with its index page, which does not parse).
 */
import { checkTableForPack, EmbeddingTableError, readEmbeddingTable } from '../index.ts';
import type { EmbeddingTable, HelpLang, HelpPack } from '../index.ts';
import { SUPPORTED_SCHEMA_VERSION } from './types.ts';
import type { LoadedPack, Manifest, ManualBundle } from './types.ts';

export class HelpUnavailableError extends Error {
	constructor(message: string) {
		super(message);
		this.name = 'HelpUnavailableError';
	}
}

export type Fetcher = (url: string) => Promise<Response>;

const defaultFetch: Fetcher = (url) => fetch(url, { cache: 'no-cache' });

/** Lower-case hex SHA-256 of bytes; null when the platform has no `crypto.subtle` (the table is then not used, manual.md 6.7.5). */
export type Digest = (bytes: Uint8Array) => Promise<string | null>;

export const browserDigest: Digest = async (bytes) => {
	const subtle = globalThis.crypto?.subtle;
	if (!subtle) return null;
	const buf = await subtle.digest('SHA-256', bytes as unknown as ArrayBuffer);
	return [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, '0')).join('');
};

async function getJson<T>(fetcher: Fetcher, url: string): Promise<T> {
	let response: Response;
	try {
		response = await fetcher(url);
	} catch {
		throw new HelpUnavailableError(`${url} could not be requested`);
	}
	if (!response.ok) throw new HelpUnavailableError(`${url} answered ${response.status}`);
	try {
		return (await response.json()) as T;
	} catch {
		throw new HelpUnavailableError(`${url} is not JSON`);
	}
}

/** The runtime refuses a document with a higher `schema_version` than it knows (manual.md 6.8). */
export function checkSchema(version: unknown, what: string): void {
	if (typeof version !== 'number' || version > SUPPORTED_SCHEMA_VERSION)
		throw new HelpUnavailableError(`${what} has an unsupported schema version`);
}

export interface HelpLoader {
	manifest(): Promise<Manifest>;
	bundle(lang: HelpLang): Promise<ManualBundle>;
	pack(lang: HelpLang): Promise<LoadedPack<HelpPack>>;
}

/**
 * The embedding table of a pack (manual.md 6.7.5), checked in order: the SHA-256 against the pack block and the manifest, then the
 * structure, then `meta` against the block and the pack. Any failure gives `{ table: null, reason }`: help never becomes
 * unavailable because of a table.
 */
export async function loadEmbeddingTable(
	pack: HelpPack,
	manifestEntry: { file: string; sha256: string } | null | undefined,
	fetcher: Fetcher,
	digest: Digest,
): Promise<{ table: EmbeddingTable | null; reason: string | null }> {
	const block = pack.embeddings;
	if (!block) return { table: null, reason: null };
	try {
		if (!manifestEntry) throw new EmbeddingTableError('the manifest does not list the table');
		let response: Response;
		try {
			response = await fetcher(`/help/${manifestEntry.file}`);
		} catch {
			throw new EmbeddingTableError('the table could not be requested');
		}
		if (!response.ok) throw new EmbeddingTableError(`the table answered ${response.status}`);
		const bytes = new Uint8Array(await response.arrayBuffer());
		const sha = await digest(bytes);
		if (sha === null)
			throw new EmbeddingTableError('crypto.subtle is not available to check the table');
		if (sha !== block.sha256 || sha !== manifestEntry.sha256)
			throw new EmbeddingTableError(
				'the SHA-256 of the table differs from the pack or the manifest',
			);
		const table = readEmbeddingTable(bytes);
		checkTableForPack(table, pack, block);
		return { table, reason: null };
	} catch (e) {
		return { table: null, reason: e instanceof Error ? e.message : String(e) };
	}
}

/**
 * A loader that keeps what it loaded; a failed load is forgotten, so the next attempt asks again. `pack(lang)` resolves once the
 * pack's table is loaded or has failed, so the results of a question never change while the user reads them.
 */
export function createHelpLoader(
	fetcher: Fetcher = defaultFetch,
	digest: Digest = browserDigest,
): HelpLoader {
	const cache = new Map<string, Promise<unknown>>();
	const once = <T>(key: string, load: () => Promise<T>): Promise<T> => {
		let p = cache.get(key) as Promise<T> | undefined;
		if (!p) {
			p = load();
			cache.set(key, p);
			p.catch(() => cache.delete(key));
		}
		return p;
	};

	const manifest = () =>
		once('manifest', async () => {
			const m = await getJson<Manifest>(fetcher, '/help/manifest.json');
			checkSchema(m?.schema_version, 'the manifest');
			return m;
		});

	return {
		manifest,
		bundle: (lang) =>
			once(`bundle:${lang}`, async () => {
				const m = await manifest();
				const entry = m.bundles?.[lang];
				if (!entry) throw new HelpUnavailableError(`no manual in ${lang}`);
				const bundle = await getJson<ManualBundle>(fetcher, `/help/${entry.file}`);
				checkSchema(bundle?.schema_version, 'the manual');
				if (!Array.isArray(bundle.pages))
					throw new HelpUnavailableError('the manual has no pages');
				return bundle;
			}),
		pack: (lang) =>
			once(`pack:${lang}`, async () => {
				const m = await manifest();
				const entry = m.packs?.[lang];
				if (!entry) throw new HelpUnavailableError(`no help pack in ${lang}`);
				const pack = await getJson<HelpPack>(fetcher, `/help/${entry.file}`);
				checkSchema(pack?.schema_version, 'the help pack');
				if (pack.kind !== 'help-pack' || !pack.index || !Array.isArray(pack.sections))
					throw new HelpUnavailableError('the help pack is malformed');
				const { table, reason } = await loadEmbeddingTable(
					pack,
					m.embeddings?.[lang],
					fetcher,
					digest,
				);
				if (reason)
					console.warn(
						`help: the embedding table of ${lang} is not used (${reason}); retrieval is lexical only`,
					);
				return { pack, sha256: entry.sha256, embeddings: table, embeddingsReason: reason };
			}),
	};
}

/** The loader of the application. */
export const helpLoader: HelpLoader = createHelpLoader();
