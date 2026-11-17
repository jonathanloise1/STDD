import { join } from 'node:path';
import { isDir, listFiles, readText, rel, walkFiles } from './fsutil.ts';

function addSlash(p: string): string {
	return p.startsWith('/') ? p : `/${p}`;
}

/** `path` string literals of src/frontend/src/routes/*.tsx, with a leading `/`. */
export function scanRoutePaths(root: string): { found: Set<string>; scanned: boolean } {
	const found = new Set<string>();
	const files: string[] = [];
	const routesDir = join(root, 'src/frontend/src/routes');
	if (isDir(routesDir)) for (const f of listFiles(routesDir)) if (f.endsWith('.tsx')) files.push(join(routesDir, f));
	for (const f of files) {
		const text = readText(f);
		for (const m of text.matchAll(/\bpath\s*(?::|=)\s*\{?\s*(['"`])([^'"`]*)\1/g)) found.add(addSlash(m[2]));
	}
	return { found, scanned: files.length > 0 };
}

export interface ContextUse {
	key: string;
	/** Repository-relative file. */
	file: string;
}

/** Context keys used in src/frontend/src: `openHelp({ context: '...' })` and `<HelpButton context="..." />`. */
export function scanContextUses(root: string): { uses: ContextUse[]; scanned: boolean } {
	const base = join(root, 'src/frontend/src');
	const uses: ContextUse[] = [];
	if (!isDir(base)) return { uses, scanned: false };
	for (const f of walkFiles(base)) {
		if (!/\.(ts|tsx|js|jsx)$/.test(f)) continue;
		const text = readText(join(base, f));
		const file = rel('src/frontend/src', f);
		for (const m of text.matchAll(/openHelp\(\s*\{\s*context\s*:\s*(['"`])([^'"`]+)\1/g)) uses.push({ key: m[2], file });
		for (const m of text.matchAll(/<HelpButton\b[^>]*?\bcontext\s*=\s*(?:"([^"]+)"|\{\s*(['"`])([^'"`]+)\2\s*\})/g)) {
			uses.push({ key: m[1] ?? m[3], file });
		}
	}
	return { uses, scanned: true };
}
