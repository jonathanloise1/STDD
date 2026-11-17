import { join } from 'node:path';
import { isDir, listFiles, readText } from './fsutil.ts';
import type { Lang } from './page.ts';

export type Flat = Record<string, string>;

export interface Locales {
	en: Flat;
	it: Flat;
	/** False when `src/frontend/public/locales/` has no files for either language. */
	available: boolean;
}

function flatten(prefix: string, v: unknown, out: Flat): void {
	if (typeof v === 'string') out[prefix] = v;
	else if (v && typeof v === 'object') {
		for (const [k, x] of Object.entries(v as Record<string, unknown>)) flatten(prefix ? `${prefix}.${k}` : k, x, out);
	}
}

/** Flattens every locale file of a language to `namespace.key -> value`. */
export function loadLocaleFlat(root: string, lang: Lang): Flat {
	const dir = join(root, 'src/frontend/public/locales', lang);
	const out: Flat = {};
	if (!isDir(dir)) return out;
	for (const f of listFiles(dir)) {
		if (!f.endsWith('.json')) continue;
		let data: unknown;
		try {
			data = JSON.parse(readText(join(dir, f)));
		} catch {
			continue;
		}
		flatten(f.replace(/\.json$/, ''), data, out);
	}
	return out;
}

export function loadLocales(root: string): Locales {
	const en = loadLocaleFlat(root, 'en');
	const it = loadLocaleFlat(root, 'it');
	return { en, it, available: Object.keys(en).length + Object.keys(it).length > 0 };
}

/**
 * UI text lookup (manual.md 3.1): the keys whose English value equals the string exactly; the Italian text is
 * their `it` value when every one of them gives the same value. Values containing `{{` are skipped.
 */
export function italianUiText(loc: Locales, english: string): string | null {
	let found: string | null = null;
	for (const [key, value] of Object.entries(loc.en)) {
		if (value !== english || value.includes('{{')) continue;
		const it = loc.it[key];
		if (it === undefined || it.includes('{{')) return null;
		if (found === null) found = it;
		else if (found !== it) return null;
	}
	return found;
}

/** Every value of the locale files of a language. */
export function localeValues(loc: Locales, lang: Lang): Set<string> {
	return new Set(Object.values(loc[lang]));
}
