import apiClient from './apiClient';

/**
 * Reports a situation the user meets in the browser only (operations.md 3.6); the backend logs it.
 * Fire and forget: a failure to report never disturbs the page.
 */
export const reportClientEvent = (key: string, route: string): void => {
	apiClient.post('/api/client-events', { key, route }).catch(() => undefined);
};
