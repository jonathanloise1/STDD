import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, sep } from 'node:path';

/** Posix-style join of repository-relative path segments. */
export function rel(...parts: string[]): string {
	return parts.join('/').replace(/\/+/g, '/');
}

export function isFile(abs: string): boolean {
	return existsSync(abs) && statSync(abs).isFile();
}

export function isDir(abs: string): boolean {
	return existsSync(abs) && statSync(abs).isDirectory();
}

/** Sub-directory names, sorted by code point. */
export function listDirs(abs: string): string[] {
	if (!isDir(abs)) return [];
	return readdirSync(abs, { withFileTypes: true })
		.filter((e) => e.isDirectory())
		.map((e) => e.name)
		.sort(compareCodePoint);
}

/** File names directly inside a directory, sorted by code point. */
export function listFiles(abs: string): string[] {
	if (!isDir(abs)) return [];
	return readdirSync(abs, { withFileTypes: true })
		.filter((e) => e.isFile())
		.map((e) => e.name)
		.sort(compareCodePoint);
}

/** Every file below a directory, as posix paths relative to it, sorted. Empty when the directory is missing. */
export function walkFiles(abs: string): string[] {
	const out: string[] = [];
	const visit = (dir: string, prefix: string) => {
		for (const e of readdirSync(dir, { withFileTypes: true })) {
			const p = prefix ? `${prefix}/${e.name}` : e.name;
			if (e.isDirectory()) visit(join(dir, e.name), p);
			else out.push(p);
		}
	};
	if (isDir(abs)) visit(abs, '');
	return out.sort(compareCodePoint);
}

export function readText(abs: string): string {
	return readFileSync(abs, 'utf8');
}

export function compareCodePoint(a: string, b: string): number {
	return a < b ? -1 : a > b ? 1 : 0;
}

export function toPosix(p: string): string {
	return sep === '/' ? p : p.split(sep).join('/');
}
