/** Helpers for the manual tests: temporary copies of the fixture repository, always removed afterwards. */
import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
export const FIXTURE_REPO = resolve(HERE, 'fixtures/repo');
export const FIXTURE_GOLDEN = resolve(HERE, 'fixtures/golden');

const made: string[] = [];

/** Copies the fixture repository (with its valid manual) to a temporary directory. */
export function tmpRepo(withManual = true): string {
	const dir = mkdtempSync(join(tmpdir(), 'manual-test-'));
	made.push(dir);
	cpSync(FIXTURE_REPO, dir, { recursive: true });
	if (!withManual) rmSync(join(dir, 'docs/manual'), { recursive: true, force: true });
	return dir;
}

export function cleanup(): void {
	for (const d of made.splice(0)) rmSync(d, { recursive: true, force: true });
}

export function edit(root: string, relPath: string, fn: (text: string) => string): void {
	const abs = join(root, relPath);
	writeFileSync(abs, fn(readFileSync(abs, 'utf8')));
}

export function replaceIn(root: string, relPath: string, from: string, to: string): void {
	edit(root, relPath, (t) => {
		if (!t.includes(from)) throw new Error(`"${from}" not found in ${relPath}`);
		return t.replace(from, to);
	});
}

export function capture(): { out: string[]; err: string[]; io: { out: (l: string) => void; err: (l: string) => void } } {
	const out: string[] = [];
	const err: string[] = [];
	return { out, err, io: { out: (l) => out.push(l), err: (l) => err.push(l) } };
}
