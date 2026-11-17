import { describe, expect, it } from 'vitest';
import { pageHref, pagesForPanel, patternsOfRoutes, routePatternOf } from './routes.ts';
import { BUNDLE } from './fixtures/pack.ts';

const patterns = patternsOfRoutes([
	'dashboard',
	'tasks',
	'settings/team',
	'manual/:area/:story',
	'*',
	undefined,
]);

describe('route to manual page mapping (manual.md 5.4)', () => {
	it('builds patterns with a leading slash and without the catch-all', () => {
		expect(patterns).toEqual([
			'/dashboard',
			'/tasks',
			'/settings/team',
			'/manual/:area/:story',
		]);
	});

	it('matches the route definition, not the address', () => {
		expect(routePatternOf('/tasks', patterns)).toBe('/tasks');
		expect(routePatternOf('/manual/tasks/US-TASK-01', patterns)).toBe('/manual/:area/:story');
		expect(routePatternOf('/billing', patterns)).toBeNull();
	});

	it('lists the pages whose routes contain the pattern, in manual order', () => {
		const pages = pagesForPanel(BUNDLE, { pathname: '/tasks', patterns });
		expect(pages.map((p) => p.title)).toEqual(['Create a task', 'Delete a task']);
	});

	it('lists the page of a context first, then the other pages of the route, without repeats', () => {
		const pages = pagesForPanel(BUNDLE, { pathname: '/tasks', patterns, context: 'task-form' });
		expect(pages.map((p) => p.page_id)).toEqual(['tasks/US-TASK-01', 'tasks/US-TASK-02']);
		const reversed = {
			...BUNDLE,
			contexts: { 'task-form': 'tasks/US-TASK-02' },
		};
		expect(
			pagesForPanel(reversed, { pathname: '/tasks', patterns, context: 'task-form' }).map(
				(p) => p.page_id,
			),
		).toEqual(['tasks/US-TASK-02', 'tasks/US-TASK-01']);
	});

	it('ignores an unknown context key', () => {
		const pages = pagesForPanel(BUNDLE, { pathname: '/tasks', patterns, context: 'nope' });
		expect(pages).toHaveLength(2);
	});

	it('falls back to the area page of the first path segment', () => {
		const withArea = [...patterns, '/tasks/archive'];
		const pages = pagesForPanel(BUNDLE, { pathname: '/tasks/archive', patterns: withArea });
		expect(pages.map((p) => p.page_id)).toEqual(['tasks']);
	});

	it('falls back to the home page when there is no page and no area page', () => {
		const pages = pagesForPanel(BUNDLE, { pathname: '/billing', patterns });
		expect(pages.map((p) => p.page_id)).toEqual(['home']);
	});

	it('gives the manual address of a page', () => {
		expect(pageHref(BUNDLE.pages[0])).toBe('/manual');
		expect(pageHref(BUNDLE.pages[1])).toBe('/manual/tasks');
		expect(pageHref(BUNDLE.pages[2])).toBe('/manual/tasks/US-TASK-01');
	});
});
