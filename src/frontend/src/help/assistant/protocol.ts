/**
 * The assistant protocol in TypeScript (models.md 3): renders the two messages, parses the model output and selects
 * the context. The definition is the pack's copy of `training/assistant/protocol.json`
 * (`pack.assistant.protocol.content`, with its SHA-256), so the prompt cannot differ from training.
 * Tested byte for byte against `training/assistant/protocol.golden.json`.
 */
import { selectContext } from '../lexical/context.ts';
import type { ProtocolContext } from '../lexical/types.ts';
import { parseAnswer } from '../runtime/parseAnswer.ts';
import type { ParsedAnswer } from '../runtime/parseAnswer.ts';

export interface AssistantProtocol {
	schema_version: number;
	system: string;
	user: string;
	section: string;
	section_separator: string;
	languages: Record<string, { language_name: string; refusal: string }>;
	context: ProtocolContext;
	answer: { source_prefix: string; max_body_chars: number; max_body_lines: number };
	decoding: { temperature: number; max_new_tokens: number; reasoning: string };
}

export interface ProtocolSection {
	title: string;
	text: string;
}

export interface ChatMessage {
	role: 'system' | 'user';
	content: string;
}

export interface RenderedPrompt {
	messages: ChatMessage[];
	/** The normalised question that was substituted. */
	question: string;
}

/** The JavaScript `\s` set is exactly the whitespace of 3.7 (U+0009 to U+000D, U+0020, U+00A0, U+1680, U+2000 to U+200A, U+2028, U+2029, U+202F, U+205F, U+3000, U+FEFF). */
const WHITESPACE_RUN = /\s+/g;

/** NFC, then CRLF to LF. */
export const normalizeText = (value: string): string =>
	value.normalize('NFC').replace(/\r\n/g, '\n');

/** The question as the model sees it: normalised, trimmed, whitespace collapsed, cut at a number of code points. */
export function normalizeQuestion(question: string, maxChars: number): string {
	const collapsed = normalizeText(question).replace(WHITESPACE_RUN, ' ').trim();
	return [...collapsed].slice(0, maxChars).join('');
}

/** One left-to-right pass: only the listed placeholders are replaced and substituted values are not scanned again. */
function substitute(template: string, values: Record<string, string>): string {
	return template.replace(/\{([a-z_]+)\}/g, (whole, name: string) =>
		Object.prototype.hasOwnProperty.call(values, name) ? values[name] : whole,
	);
}

/** 3.2 and 3.7: the system and user messages. */
export function renderMessages(
	protocol: AssistantProtocol,
	lang: string,
	appName: string,
	sections: readonly ProtocolSection[],
	question: string,
): RenderedPrompt {
	const language = protocol.languages[lang];
	if (!language) throw new Error(`The protocol has no language "${lang}"`);
	const normalised = normalizeQuestion(question, protocol.context.max_question_chars);
	const rendered = sections
		.map((s, i) =>
			substitute(protocol.section, {
				n: String(i + 1),
				title: normalizeText(s.title),
				text: normalizeText(s.text),
			}),
		)
		.join(protocol.section_separator);
	const system = substitute(protocol.system, {
		app_name: normalizeText(appName),
		refusal: language.refusal,
		language_name: language.language_name,
	});
	const user = substitute(protocol.user, { sections: rendered, question: normalised });
	return {
		messages: [
			{ role: 'system', content: system },
			{ role: 'user', content: user },
		],
		question: normalised,
	};
}

/** 3.4: `k` is the number of sections in the context. */
export function parseOutput(
	protocol: AssistantProtocol,
	lang: string,
	output: string,
	k: number,
): ParsedAnswer {
	return parseAnswer(output, {
		sources: k,
		maxBodyChars: protocol.answer.max_body_chars,
		maxBodyLines: protocol.answer.max_body_lines,
		refusal: protocol.languages[lang]?.refusal ?? '',
	});
}

/** 3.5: `maxSections` can only lower the protocol value. */
export function selectSections<T extends { text: string }>(
	protocol: AssistantProtocol,
	ranked: readonly T[],
	maxSections: number | null = null,
): T[] {
	const limit = Math.min(
		protocol.context.max_sections,
		maxSections ?? protocol.context.max_sections,
	);
	return selectContext(ranked, { ...protocol.context, max_sections: limit });
}
