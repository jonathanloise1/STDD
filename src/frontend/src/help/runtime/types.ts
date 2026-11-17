/** Types of the manual bundle (manual.md 5.1) and of the help runtime. Framework-free. */
import type { EmbeddingTable, HelpLang } from '../index.ts';

/** The contract version of the pack, bundle and manifest this runtime understands (manual.md 6.8). */
export const SUPPORTED_SCHEMA_VERSION = 2;

export type BundleInline =
	| { type: 'text'; text: string }
	| { type: 'bold'; text: string }
	| { type: 'link'; text: string; href: string };

export interface BundleImage {
	alt: string;
	src: string;
}

export interface BundleListItem {
	inlines: BundleInline[];
	number: number | null;
	image: BundleImage | null;
}

export type BundleBlock =
	| { type: 'heading'; level: number; text: string }
	| { type: 'paragraph'; inlines: BundleInline[] }
	| { type: 'ordered'; items: BundleListItem[] }
	| { type: 'bullets'; items: BundleListItem[] }
	| ({ type: 'image' } & BundleImage)
	| { type: 'table'; header: BundleInline[][]; rows: BundleInline[][][] };

export type PageKind = 'story' | 'area' | 'home';

export interface BundlePage {
	page_id: string;
	kind: PageKind;
	area: string | null;
	title: string;
	source_id: string | null;
	routes: string[];
	contexts: string[];
	blocks: BundleBlock[];
}

export interface ManualBundle {
	schema_version: number;
	lang: HelpLang;
	project_version: string;
	pages: BundlePage[];
	/** Route pattern to page ids, in manual order. */
	routes: Record<string, string[]>;
	/** Context key to the page that declares it first. */
	contexts: Record<string, string>;
}

export interface Manifest {
	schema_version: number;
	project_version: string;
	languages: string[];
	packs: Record<string, { file: string; sha256: string }>;
	bundles: Record<string, { file: string; sha256: string }>;
	/** The table file of each language, or null when the pack uses none (manual.md 6.8); absent in manifests of `help-pack` 1. */
	embeddings?: Record<string, { file: string; sha256: string } | null>;
	assistant_compatible: boolean;
}

/** A pack with the sha256 of its file, as the manifest lists it. */
export interface LoadedPack<P> {
	pack: P;
	sha256: string;
	/** The checked embedding table of the pack (manual.md 6.7.5); null or absent: retrieval is lexical only. */
	embeddings?: EmbeddingTable | null;
	/** Why the pack names a table that is not used; for the panel's status line. */
	embeddingsReason?: string | null;
}

/** The tiers that can generate an answer on top of retrieval (manual.md 7.1). */
export type GenerationTier = 'browser' | 'server';
export type AssistantTier = GenerationTier | 'retrieval';
export type TierOrder = 'browser-first' | 'server-first' | 'retrieval-only';

/** Outcomes of a question the documentation did not answer (manual.md 7.4). */
export type GapOutcome = 'no_match' | 'refused' | 'invalid' | 'not_helpful' | 'timeout';

export interface GapRecord {
	schema_version: 1;
	recorded_at: string;
	lang: HelpLang;
	route: string;
	question: string;
	outcome: GapOutcome;
	tier: AssistantTier;
	pack: { project_version: string; sha256: string };
	top_sections: string[];
	coverage: number;
}

/** What `GET /api/help/status` tells the panel (models.md 7.4, 7.7), normalised. */
export interface HelpStatus {
	order: TierOrder;
	server: { available: boolean; timeoutSeconds: number };
	browser: {
		enabled: boolean;
		askBeforeDownloadMb: number;
		timeoutSeconds: number;
		/** Base URL of the ONNX files (models.md 7.7); the factory falls back to `DEFAULT_MODEL_BASE_URL`. */
		modelBaseUrl?: string;
		/** Warm-up rate of models.md 7.2 step 8; the factory falls back to `DEFAULT_MIN_TOKENS_PER_SECOND`. */
		minTokensPerSecond?: number;
	};
}

export const DEFAULT_MODEL_BASE_URL = '/models/assistant/';
export const DEFAULT_MIN_TOKENS_PER_SECOND = 8;

/** The status used when the call fails or says nothing: retrieval only (models.md 7.7). */
export const RETRIEVAL_ONLY_STATUS: HelpStatus = {
	order: 'retrieval-only',
	server: { available: false, timeoutSeconds: 20 },
	browser: { enabled: false, askBeforeDownloadMb: 100, timeoutSeconds: 20 },
};
