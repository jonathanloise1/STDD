import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { normalizeQuestion, parseOutput, renderMessages, selectSections } from './protocol.ts';
import type { AssistantProtocol } from './protocol.ts';

const read = (name: string) =>
	JSON.parse(readFileSync(resolve(process.cwd(), '../../training/assistant', name), 'utf8'));
const protocol = read('protocol.json') as AssistantProtocol;
const golden = read('protocol.golden.json');

describe('assistant protocol against the golden cases (models.md 3.7)', () => {
	it('has at least 12 render cases', () =>
		expect(golden.render.length).toBeGreaterThanOrEqual(12));

	for (const c of golden.render)
		it(`renders ${c.id}`, () => {
			const out = renderMessages(protocol, c.lang, c.app_name, c.sections, c.question);
			expect(out.messages[0].role).toBe('system');
			expect(out.messages[0].content).toBe(c.expected.system);
			expect(out.messages[1].role).toBe('user');
			expect(out.messages[1].content).toBe(c.expected.user);
			expect(out.question).toBe(c.expected.question);
		});

	for (const c of golden.parse)
		it(`parses ${c.id}`, () => {
			const out = parseOutput(protocol, c.lang, c.output, c.k);
			expect(out.kind).toBe(c.expected.outcome);
			expect(out.kind === 'answered' ? out.cited : []).toEqual(c.expected.cited);
		});

	for (const c of golden.select)
		it(`selects ${c.id}`, () => {
			const ids = selectSections(
				protocol,
				c.ranked as { id: string; text: string }[],
				c.max_sections,
			).map((s) => s.id);
			expect(ids).toEqual(c.expected.selected_ids);
		});

	it('cuts a question by code points, never inside a surrogate pair', () => {
		expect(normalizeQuestion('a\u{1F600}b', 2)).toBe('a\u{1F600}');
	});
});
