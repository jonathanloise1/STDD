import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';
import contents from '../routes/contentRoutes';
import { patternsOfRoutes, routePatternOf } from '../help/runtime/routes';

/** The route definition patterns of the application, taken from its router (manual.md 5.4). */
export const ROUTE_PATTERNS: readonly string[] = patternsOfRoutes(contents.map((r) => r.path));

/** The current path and the route pattern it matches (`/tasks`); the pattern is null when none matches. */
export const useHelpRoute = (): {
	pathname: string;
	pattern: string | null;
	patterns: readonly string[];
} => {
	const { pathname } = useLocation();
	return useMemo(
		() => ({
			pathname,
			pattern: routePatternOf(pathname, ROUTE_PATTERNS),
			patterns: ROUTE_PATTERNS,
		}),
		[pathname],
	);
};
