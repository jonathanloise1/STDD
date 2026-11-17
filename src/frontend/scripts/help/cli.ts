/** Small option parser and I/O contract shared by the help commands. */
import { resolve } from 'node:path';
import { defaultRoot } from '../manual/args.ts';

export interface Io {
	out: (line: string) => void;
	err: (line: string) => void;
}

export const consoleIo: Io = { out: (l) => console.log(l), err: (l) => console.error(l) };

export interface Options {
	flags: Set<string>;
	values: Map<string, string>;
	errors: string[];
}

/** `spec` maps an option name to `flag` or `value`. Accepts `--name value` and `--name=value`. */
export function parseOptions(argv: string[], spec: Record<string, 'flag' | 'value'>): Options {
	const o: Options = { flags: new Set(), values: new Map(), errors: [] };
	for (let i = 0; i < argv.length; i++) {
		const [name, inline] = argv[i].split(/=(.*)/s, 2);
		const kind = spec[name];
		if (!kind) {
			o.errors.push(`unknown option ${argv[i]}`);
		} else if (kind === 'flag') {
			o.flags.add(name);
		} else {
			const v = inline ?? argv[++i];
			if (v === undefined || v === '') o.errors.push(`${name} needs a value`);
			else o.values.set(name, v);
		}
	}
	return o;
}

export function rootOf(o: Options): string {
	const v = o.values.get('--root');
	return v ? resolve(v) : defaultRoot();
}

/** True when this module is the program entry point. */
export function isEntry(metaUrl: string): boolean {
	return Boolean(process.argv[1]) && new URL(metaUrl).pathname === resolve(process.argv[1]);
}
