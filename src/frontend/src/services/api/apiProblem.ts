/**
 * The single path from a failed API request to the message the user reads (operations.md 4.2, 4.3).
 *
 * The backend answers every error with a problem document that carries a `code` for keyed outcomes
 * and a `traceId`. The message comes from the locale key `apiErrors.{code with "." and "_" as "-"}`,
 * never from the English `detail`.
 */
import { isAxiosError } from 'axios';
import i18n from '../../i18n';

export const TRACE_ID_BYTES = 16;
export const SPAN_ID_BYTES = 8;

/** Random lower-case hex string of the given number of bytes, from crypto.getRandomValues. */
export const randomHex = (bytes: number): string => {
	const values = crypto.getRandomValues(new Uint8Array(bytes));
	return Array.from(values, (v) => v.toString(16).padStart(2, '0')).join('');
};

/** What the UI keeps of a failed request. */
export interface ApiProblem {
	/** Log key of a keyed outcome, for example `task.create.not_allowed`. */
	code?: string;
	/** HTTP status; absent when the service did not answer at all. */
	status?: number;
	/** Trace id shown to the user as the reference. */
	traceId: string;
	/** Field name (camelCase) to messages, for validation problems. */
	errors?: Record<string, string[]>;
}

interface ProblemBody {
	code?: unknown;
	status?: unknown;
	traceId?: unknown;
	errors?: unknown;
}

/** Turns anything thrown by an API call into an {@link ApiProblem}. */
export const toApiProblem = (error: unknown): ApiProblem => {
	if (isAxiosError(error)) {
		const body = (
			error.response?.data && typeof error.response.data === 'object'
				? error.response.data
				: {}
		) as ProblemBody;
		return {
			code: typeof body.code === 'string' ? body.code : undefined,
			status: error.response?.status,
			traceId:
				typeof body.traceId === 'string' && body.traceId
					? body.traceId
					: (error.config?.traceId ?? randomHex(TRACE_ID_BYTES)),
			errors:
				body.errors && typeof body.errors === 'object'
					? (body.errors as Record<string, string[]>)
					: undefined,
		};
	}
	return { traceId: randomHex(TRACE_ID_BYTES) };
};

/** The locale key of the message of a keyed outcome. */
export const apiErrorKey = (code: string): string => `apiErrors.${code.replace(/[._]/g, '-')}`;

/** The translated message the user reads for a problem. */
export const apiProblemMessage = (problem: ApiProblem): string => {
	if (problem.code) {
		const key = apiErrorKey(problem.code);
		if (i18n.exists(key, { ns: 'common' })) return i18n.t(key, { ns: 'common' });
	}
	if (!problem.status || problem.status >= 500)
		return i18n.t('apiErrors.unavailable', { ns: 'common' });
	if (problem.status === 429) return i18n.t('apiErrors.rateLimited', { ns: 'common' });
	return i18n.t('apiErrors.generic', { ns: 'common' });
};

/** True when the request failed because the resource does not exist (any more). */
export const isNotFound = (error: unknown): boolean =>
	isAxiosError(error) && error.response?.status === 404;
