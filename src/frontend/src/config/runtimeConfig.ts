/**
 * Settings given when the application starts instead of when it is built.
 *
 * Vite bakes every VITE_* value into the bundle at build time, so one build could only serve one environment.
 * In the Docker image, `/config.js` is written at container start from the container's environment
 * (`docker/40-runtime-config.sh`) and sets `window.__MYAPP_CONFIG__`. A key present there wins over the build-time
 * value, even when its value is empty; a key that is absent falls back to the build-time value, so `npm run dev`
 * and the E2E run work as before. Only the settings that decide how a visitor signs in and where the API is are read
 * this way.
 */
declare global {
	interface Window {
		__MYAPP_CONFIG__?: Record<string, string>;
	}
}

/** The value given at run time for a VITE_* key, or undefined when none was given. */
export function runtimeSetting(key: string): string | undefined {
	if (typeof window === 'undefined') return undefined;
	const config = window.__MYAPP_CONFIG__;
	if (!config || !Object.prototype.hasOwnProperty.call(config, key)) return undefined;
	return config[key];
}
