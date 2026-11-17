// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest';
import { runtimeSetting } from './runtimeConfig.ts';

afterEach(() => {
	delete window.__MYAPP_CONFIG__;
});

describe('runtimeSetting', () => {
	it('is undefined when the page has no run time settings', () => {
		expect(runtimeSetting('VITE_API_BASE_URL')).toBeUndefined();
	});

	it('is undefined for a key the settings do not name', () => {
		window.__MYAPP_CONFIG__ = { VITE_API_SCOPE: 'scope' };
		expect(runtimeSetting('VITE_API_BASE_URL')).toBeUndefined();
	});

	it('returns the value of a key that is named', () => {
		window.__MYAPP_CONFIG__ = { VITE_API_SCOPE: 'scope' };
		expect(runtimeSetting('VITE_API_SCOPE')).toBe('scope');
	});

	it('returns an empty value as empty, so that it can override a build time value', () => {
		window.__MYAPP_CONFIG__ = { VITE_API_BASE_URL: '' };
		expect(runtimeSetting('VITE_API_BASE_URL')).toBe('');
	});
});
