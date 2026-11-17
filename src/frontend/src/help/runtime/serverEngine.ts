/** The server tier (manual.md 7.1, models.md 7.4): the answer of `POST /api/help/answer`. */
import type { HelpLang } from '../index.ts';
import helpService from '../../services/api/helpService';
import { refusalOf } from './engine.ts';
import type { AnswerEngine, EngineFactory } from './engine.ts';

export interface ServerEngineDeps {
	answer: typeof helpService.answer;
}

/** Margin on top of the server's own limit, so the server's "too slow" reply arrives before the client gives up. */
const CLIENT_MARGIN_MS = 2000;

export function createServerEngine(
	timeoutSeconds: number,
	deps: ServerEngineDeps = helpService,
	refusalFor: (lang: HelpLang) => string = () => '',
): AnswerEngine {
	return {
		tier: 'server',
		timeoutMs: timeoutSeconds * 1000 + CLIENT_MARGIN_MS,
		availability: async () => ({ state: 'ready' }),
		generate: (request) =>
			deps.answer(
				{
					lang: request.lang,
					question: request.question,
					sectionIds: request.sections.map((s) => s.id),
					packSha256: request.packSha256,
				},
				{
					signal: request.signal,
					onText: request.onText,
					refusal: refusalFor(request.lang),
				},
			),
	};
}

/** Eligible when the backend reports the model host available (the pack's compatibility is checked by the registry). */
export const serverEngineFactory: EngineFactory = ({ status, pack }) =>
	status.server.available
		? createServerEngine(status.server.timeoutSeconds, helpService, (lang) =>
				refusalOf(pack, lang),
			)
		: null;
