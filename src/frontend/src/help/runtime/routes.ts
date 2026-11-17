/** Which manual pages belong to a screen (manual.md 5.4). Pure: no router, no React. */
import { matchRoutes } from 'react-router-dom';
import type { BundlePage, ManualBundle } from './types.ts';

/** The route definition pattern of a location, with a leading `/`; null when no definition matches. */
export function routePatternOf(pathname: string, patterns: readonly string[]): string | null {
	const matches = matchRoutes(
		patterns.map((path) => ({ path })),
		{ pathname },
	);
	const path = matches?.[matches.length - 1]?.route.path;
	return path ?? null;
}

/** The route patterns of a list of route definitions: leading `/`, no catch-all. */
export function patternsOfRoutes(paths: ReadonlyArray<string | undefined>): string[] {
	return paths
		.filter((p): p is string => typeof p === 'string' && p !== '' && p !== '*')
		.map((p) => (p.startsWith('/') ? p : `/${p}`));
}

export interface PanelQuery {
	/** The path of the current location. */
	pathname: string;
	/** The route patterns of the application. */
	patterns: readonly string[];
	/** The context key of the component that opened the panel, if any. */
	context?: string | null;
}

/**
 * The pages "This page" lists:
 * the page of the context first (when it exists), then the pages whose `routes` contain the route pattern;
 * with no page: the area page of the first path segment, otherwise the home page.
 */
export function pagesForPanel(bundle: ManualBundle, query: PanelQuery): BundlePage[] {
	const byId = new Map(bundle.pages.map((p) => [p.page_id, p]));
	const pattern = routePatternOf(query.pathname, query.patterns);
	const ids: string[] = [];
	const add = (id: string | undefined) => {
		if (id && byId.has(id) && !ids.includes(id)) ids.push(id);
	};
	if (query.context) add(bundle.contexts?.[query.context]);
	for (const id of (pattern && bundle.routes?.[pattern]) || []) add(id);
	if (ids.length === 0) {
		const segment = query.pathname.split('/').filter(Boolean)[0];
		const area = bundle.pages.find(
			(p) => p.kind === 'area' && segment !== undefined && p.page_id === segment,
		);
		add(area?.page_id);
	}
	if (ids.length === 0) add(bundle.pages.find((p) => p.kind === 'home')?.page_id);
	return ids.map((id) => byId.get(id) as BundlePage);
}

/** The manual URL of a page. */
export function pageHref(page: Pick<BundlePage, 'kind' | 'page_id' | 'area'>): string {
	if (page.kind === 'home') return '/manual';
	return `/manual/${page.page_id}`;
}
