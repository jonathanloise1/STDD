/** Normalisation of the answer of `GET /api/help/status` (models.md 7.4, 7.7). */
import {
	DEFAULT_MIN_TOKENS_PER_SECOND,
	DEFAULT_MODEL_BASE_URL,
	RETRIEVAL_ONLY_STATUS,
} from './types.ts';
import type { HelpStatus, TierOrder } from './types.ts';

type Bag = Record<string, unknown>;

const isBag = (v: unknown): v is Bag => typeof v === 'object' && v !== null && !Array.isArray(v);

/** The first present value among the snake_case and camelCase spellings of a key. */
function pick(bag: unknown, ...keys: string[]): unknown {
	if (!isBag(bag)) return undefined;
	for (const k of keys) if (k in bag) return bag[k];
	return undefined;
}

const bool = (v: unknown, fallback: boolean): boolean => (typeof v === 'boolean' ? v : fallback);
const num = (v: unknown, fallback: number): number =>
	typeof v === 'number' && Number.isFinite(v) && v >= 0 ? v : fallback;

const ORDERS: TierOrder[] = ['browser-first', 'server-first', 'retrieval-only'];

/**
 * Reads the status leniently: a missing or unknown value falls back to retrieval only, so a status the panel does
 * not understand never switches a generation tier on (models.md 7.7).
 */
export function normalizeStatus(raw: unknown): HelpStatus {
	const base = RETRIEVAL_ONLY_STATUS;
	const assistant = pick(raw, 'assistant') ?? raw;
	const order = pick(assistant, 'order');
	const server = pick(assistant, 'server');
	const browser = pick(assistant, 'browser');
	return {
		order: ORDERS.includes(order as TierOrder) ? (order as TierOrder) : base.order,
		server: {
			available: bool(pick(server, 'available', 'enabled'), false),
			timeoutSeconds: num(
				pick(server, 'timeout_seconds', 'timeoutSeconds'),
				base.server.timeoutSeconds,
			),
		},
		browser: {
			enabled: bool(pick(browser, 'enabled'), false),
			askBeforeDownloadMb: num(
				pick(browser, 'ask_before_download_mb', 'askBeforeDownloadMb'),
				base.browser.askBeforeDownloadMb,
			),
			timeoutSeconds: num(
				pick(browser, 'timeout_seconds', 'timeoutSeconds'),
				base.browser.timeoutSeconds,
			),
			modelBaseUrl:
				typeof pick(browser, 'model_base_url', 'modelBaseUrl') === 'string' &&
				pick(browser, 'model_base_url', 'modelBaseUrl') !== ''
					? (pick(browser, 'model_base_url', 'modelBaseUrl') as string)
					: DEFAULT_MODEL_BASE_URL,
			minTokensPerSecond: num(
				pick(browser, 'min_tokens_per_second', 'minTokensPerSecond'),
				DEFAULT_MIN_TOKENS_PER_SECOND,
			),
		},
	};
}
