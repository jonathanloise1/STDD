import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export interface Args {
	strict: boolean;
	noImages: boolean;
	root: string;
	accept: string | null;
	errors: string[];
}

/** Repository root when `--root` is not given: four levels above this file (src/frontend/scripts/manual). */
export function defaultRoot(): string {
	return resolve(dirname(fileURLToPath(import.meta.url)), '../../../..');
}

export function parseArgs(argv: string[], allowed: string[]): Args {
	const a: Args = { strict: false, noImages: false, root: defaultRoot(), accept: null, errors: [] };
	for (let i = 0; i < argv.length; i++) {
		const [name, inline] = argv[i].split(/=(.*)/s, 2);
		const value = () => inline ?? argv[++i];
		if (!allowed.includes(name)) {
			a.errors.push(`unknown option ${argv[i]}`);
			continue;
		}
		if (name === '--strict') a.strict = true;
		else if (name === '--no-images') a.noImages = true;
		else if (name === '--root') {
			const v = value();
			if (!v) a.errors.push('--root needs a directory');
			else a.root = resolve(v);
		} else if (name === '--accept') {
			const v = value();
			if (!v) a.errors.push('--accept needs a story id or an area folder');
			else a.accept = v;
		}
	}
	return a;
}
