import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

// https://vitejs.dev/config/
export default defineConfig({
		plugins: [react()],
		server: {
			port: 3147,
			open: true,
		},
		// The assistant worker imports its library dynamically, which needs ES-module workers
		worker: { format: 'es' },
		// The library is imported only inside the assistant worker. Left to discovery, the dev server finds it when the
		// first page starts that worker, re-optimizes every dependency and reloads the page under the user (and under a
		// test). Listing it has the server bundle it at start.
		optimizeDeps: { include: ['@huggingface/transformers'] },
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
				},
			},
		},
		// Env prefix: Vite exposes VITE_* vars to client code
		envPrefix: 'VITE_',
		test: {
			environment: 'jsdom',
			globals: false,
		},
});
