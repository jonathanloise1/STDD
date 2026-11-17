import { describe, expect, it } from 'vitest';
import { createPackSearcher, highlight, retrieve, searchManual, snippetOf } from './searcher.ts';
import { testPack } from './fixtures/pack.ts';

const ps = createPackSearcher(testPack());

describe('search (manual.md 5.3)', () => {
	it('finds the singular for a plural and bolds the matching words', () => {
		const groups = searchManual(ps, 'tasks');
		expect(groups.length).toBeGreaterThan(0);
		const titles = groups.map((g) => g.pageTitle);
		expect(titles).toContain('Create a task');
		const stems = new Set(ps.searcher.stems('tasks'));
		const segments = highlight('Create a task now', stems, ps.analyzer);
		expect(segments.filter((s) => s.bold).map((s) => s.text)).toEqual(['task']);
	});

	it('groups results by page and shows at most ten sections', () => {
		const groups = searchManual(ps, 'task');
		const total = groups.reduce((n, g) => n + g.results.length, 0);
		expect(total).toBeLessThanOrEqual(10);
		const ids = groups.map((g) => g.pageId);
		expect(new Set(ids).size).toBe(ids.length);
	});

	it('returns nothing for words the manual does not contain', () => {
		expect(searchManual(ps, 'zzqxj')).toEqual([]);
	});

	it('cuts the snippet at 160 characters and splits a section heading from its page title', () => {
		expect(snippetOf('a'.repeat(300))).toHaveLength(160);
		const group = searchManual(ps, 'steps title')[0];
		const withHeading = group?.results.find((r) => r.heading !== null);
		expect(withHeading?.heading).toBe('Steps');
	});

	it('marks a question about something the manual does not do as out of scope', () => {
		expect(retrieve(ps, 'Can I pay my invoice from here?').decision.inScope).toBe(false);
		expect(retrieve(ps, 'How do I create a task').decision.inScope).toBe(true);
	});
});
