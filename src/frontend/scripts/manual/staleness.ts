/** Staleness of a manual page against its sources (check M05, `manual:generate` report). */
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';
import { isFile } from '../lib/fsutil.ts';
import type { ManualPage } from '../lib/page.ts';
import { blobIdOf, findStory, lowestHappyPath, testCaseById } from '../lib/repo.ts';
import type { Repo } from '../lib/repo.ts';

export interface Stale {
	what: 'source' | 'steps' | 'steps_from';
	/** Repository-relative file the stored hash refers to (empty for steps_from). */
	path: string;
	stored: string;
	current: string;
	message: string;
}

const HEX = /^[0-9a-f]{40}$/;

/** Reasons a page is stale. Empty when current, or when the front matter is too broken to compare (M03). */
export function staleReasons(repo: Repo, page: ManualPage): Stale[] {
	const out: Stale[] = [];
	const f = page.fields;
	if (page.kind === 'home' || !f['source'] || f['source'] === 'none') return out;
	const current = blobIdOf(repo.root, f['source']);
	if (current && HEX.test(f['source_hash'] ?? '') && current !== f['source_hash']) {
		out.push({
			what: 'source',
			path: f['source'],
			stored: f['source_hash'],
			current,
			message: `source_hash differs from the current blob id of ${f['source']}`,
		});
	}
	if (page.kind === 'story' && page.storyId) {
		const hit = findStory(repo, page.storyId);
		const expected = hit ? (lowestHappyPath(hit.area, page.storyId)?.id ?? 'none') : null;
		if (expected !== null && f['steps_from'] !== expected) {
			out.push({
				what: 'steps_from',
				path: '',
				stored: f['steps_from'] ?? '',
				current: expected,
				message: `steps_from is ${f['steps_from'] ?? 'missing'}, the lowest-numbered Happy path test case is now ${expected}`,
			});
		}
		const tc = f['steps_from'] && f['steps_from'] !== 'none' ? testCaseById(repo, f['steps_from']) : null;
		if (tc) {
			const cur = blobIdOf(repo.root, tc.testCase.path);
			if (cur && HEX.test(f['steps_hash'] ?? '') && cur !== f['steps_hash']) {
				out.push({
					what: 'steps',
					path: tc.testCase.path,
					stored: f['steps_hash'],
					current: cur,
					message: `steps_hash differs from the current blob id of ${tc.testCase.path}`,
				});
			}
		}
	}
	return out;
}

/** `git diff {stored} {current}` for one file; a note and the full source when the stored blob is unknown. */
export function sourceDiff(root: string, stale: Stale): string {
	const abs = join(root, stale.path);
	if (!isFile(abs)) return '';
	const known = spawnSync('git', ['cat-file', '-e', `${stale.stored}^{blob}`], { cwd: root }).status === 0;
	if (!known) return `previous version not in git history\n${readFileSync(abs, 'utf8')}`;
	const old = spawnSync('git', ['cat-file', 'blob', stale.stored], { cwd: root });
	if (old.status !== 0) return `previous version not in git history\n${readFileSync(abs, 'utf8')}`;
	const dir = mkdtempSync(join(tmpdir(), 'manual-diff-'));
	try {
		const prev = join(dir, basename(stale.path));
		writeFileSync(prev, old.stdout);
		const d = spawnSync('git', ['diff', '--no-index', '--no-color', '--', prev, abs], { cwd: root, encoding: 'utf8' });
		const body = d.stdout
			.split('\n')
			.filter((l) => !/^(diff --git|index |new file|deleted file)/.test(l))
			.map((l) => (l.startsWith('--- ') ? `--- ${stale.stored} ${stale.path}` : l.startsWith('+++ ') ? `+++ current ${stale.path}` : l))
			.join('\n');
		return body.trim() === '' ? '(no textual difference)\n' : body;
	} finally {
		rmSync(dir, { recursive: true, force: true });
	}
}
