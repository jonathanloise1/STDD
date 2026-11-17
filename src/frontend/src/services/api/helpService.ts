import apiClient from './apiClient';
import {
	HelpAnswerError,
	createAnswerStream,
	failureOfStatus,
} from '../../help/runtime/answerStream';
import { normalizeStatus } from '../../help/runtime/status';
import type { GapRecord, HelpStatus } from '../../help/runtime/types';
import type { HelpLang } from '../../help';
import axios from 'axios';

export interface HelpAnswerRequest {
	lang: HelpLang;
	question: string;
	sectionIds: string[];
	packSha256: string;
}

const helpService = {
	/** Tier settings and server availability for the panel; a failure is the caller's to turn into retrieval only. */
	getStatus: async (): Promise<HelpStatus> => {
		const response = await apiClient.get('/api/help/status');
		return normalizeStatus(response.data);
	},

	/**
	 * Asks the server tier (NDJSON stream, models.md 7.4); `onText` receives the whole output so far while it
	 * streams. Resolves with the raw model output. A failure before the stream (400, 409, 503, 504) or an `error`
	 * event rejects with a `HelpAnswerError`, and the ladder moves to the next tier.
	 */
	// US-HELP-05
	answer: async (
		request: HelpAnswerRequest,
		options: { signal?: AbortSignal; onText?: (partial: string) => void; refusal?: string },
	): Promise<string> => {
		const stream = createAnswerStream(options.refusal ?? '', options.onText);
		const textOf = (target: unknown) =>
			(target as { responseText?: string } | undefined)?.responseText;
		try {
			const response = await apiClient.post(
				'/api/help/answer',
				{
					lang: request.lang,
					question: request.question,
					section_ids: request.sectionIds,
					pack_sha256: request.packSha256,
				},
				{
					responseType: 'text',
					signal: options.signal,
					// The tier is abandoned at once: a retry would only delay the next tier
					'axios-retry': { retries: 0 },
					// The whole text is parsed at the end, not as the browser converts it
					transformResponse: (data) => data,
					onDownloadProgress: (progress) => {
						const received = textOf(progress.event?.target);
						if (typeof received === 'string') stream.feed(received);
					},
				},
			);
			return stream.finish(typeof response.data === 'string' ? response.data : '');
		} catch (error) {
			if (error instanceof HelpAnswerError) throw error;
			if (axios.isAxiosError(error) && error.response)
				throw new HelpAnswerError(
					failureOfStatus(error.response.status),
					error.response.status,
				);
			throw error;
		}
	},

	/** Records a question the documentation did not answer (manual.md 7.4). */
	// US-HELP-04, US-HELP-06
	recordGap: async (record: GapRecord): Promise<void> => {
		await apiClient.post('/api/help/gaps', record);
	},

	/** The gap records of a period (Admin); dates are `yyyy-MM-dd`. */
	// US-HELP-07
	downloadGaps: async (from: string, to: string): Promise<GapRecord[]> => {
		const response = await apiClient.get('/api/help/gaps', { params: { from, to } });
		return Array.isArray(response.data) ? response.data : [];
	},
};

export default helpService;
