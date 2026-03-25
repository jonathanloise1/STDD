import { defineConfig, loadEnv } from 'vite';
import type { UserConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

// ─── Theme Presets ───────────────────────────────────────────────────────────
// Map VITE_THEME env var values to SCSS theme files.
// Each theme file defines color variables that override !default values
// in settings/_index.scss. Add new themes here and in src/styles/themes/.
const THEME_MAP: Record<string, string> = {
	'myapp-professional': 'themes/myapp-professional',
	'facit-default': 'themes/facit-default',
};
const DEFAULT_THEME = 'myapp-professional';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
	// Load .env files so we can read VITE_THEME at config time
	const env = loadEnv(mode, __dirname, 'VITE_');
	const themeName = env.VITE_THEME || DEFAULT_THEME;
	const themePath = THEME_MAP[themeName];

	if (!themePath) {
		throw new Error(
			`[vite] Unknown theme "${themeName}". Available themes: ${Object.keys(THEME_MAP).join(', ')}`,
		);
	}

	// Resolve absolute path for the theme file (so SCSS @import resolves correctly)
	const themeAbsPath = path
		.resolve(__dirname, 'src', 'styles', `${themePath}`)
		.replace(/\\/g, '/'); // Sass requires forward slashes

	console.log(`\n  🎨 Theme: ${themeName} → ${themePath}\n`);

	return {
		plugins: [react()],
		server: {
			port: 3147,
			open: true,
		},
		build: {
			outDir: 'build',
			sourcemap: true,
		},
		css: {
			preprocessorOptions: {
				scss: {
					// Allow SCSS to resolve imports from node_modules (e.g. bootstrap)
					loadPaths: [path.resolve(__dirname, 'node_modules')],
					// Silence deprecation warnings from dependencies (bootstrap, etc.)
					silenceDeprecations: [
						'legacy-js-api',
						'import',
						'global-builtin',
						'color-functions',
						'mixed-decls',
					],
					// Inject the selected theme's variables BEFORE every SCSS file.
					// Theme variables are defined without !default, so they take
					// precedence over the !default declarations in settings/_index.scss.
					additionalData: `@import '${themeAbsPath}';\n`,
				},
			},
		},
		// Env prefix: Vite exposes VITE_* vars to client code
		envPrefix: 'VITE_',
		test: {
			environment: 'jsdom',
			globals: false,
		},
	};
});
