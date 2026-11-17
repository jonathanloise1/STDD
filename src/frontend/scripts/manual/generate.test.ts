// @vitest-environment node
import { execFileSync } from 'node:child_process';
import { readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { loadLocales } from '../lib/locales.ts';
import { walkFiles } from '../lib/fsutil.ts';
import { gitBlobId } from '../lib/hash.ts';
import { runChecks } from './check.ts';
import { nonGoalSentence, secondPerson, transformStep, validationRows } from './draft.ts';
import { accept, generate, main } from './generate.ts';
import { capture, cleanup, edit, FIXTURE_GOLDEN, replaceIn, tmpRepo } from './testutil.ts';

afterEach(cleanup);

const read = (root: string, p: string) => readFileSync(join(root, p), 'utf8');

describe('transformStep', () => {
	const loc = loadLocales(join(import.meta.dirname, 'fixtures/repo'));
	const cases: [string, string | null, string | null][] = [
		['Open the Tasks page.', 'Open the Tasks page.', 'Apri la pagina Attività.'],
		['Open the "New Task" page.', 'Open the "New Task" page.', 'Apri la pagina "Nuova attività".'],
		['Click the "New Task" button.', 'Click the "New Task" button.', 'Fai clic su "Nuova attività".'],
		['Click the "Team" link in the menu.', 'Click the "Team" link in the menu.', 'Fai clic su "Team" nel menu.'],
		['Click the "Save" button in the dialog.', 'Click the "Save" button in the dialog.', 'Fai clic su "Save" nella finestra di dialogo.'],
		['Click the "Delete" button on the row of "Task A".', 'Click the "Delete" button on the row of "Task A".', 'Fai clic su "Delete" sulla riga di "Task A".'],
		['Clear the "Title" field.', 'Clear the "Title" field.', 'Svuota il campo "Titolo".'],
		['Type "x" in the "Title" field.', 'In the "Title" field, type a value such as "x".', 'Nel campo "Titolo" scrivi un valore come "x".'],
		['Select "Done" in the "Status" field.', 'In the "Status" field, choose "Done".', 'Nel campo "Stato" scegli "Completata".'],
		['As Admin: Click the "Save" button.', null, null],
		['Press Enter.', 'Press Enter.', 'Press Enter.'],
		['Click the "Cancel" button.', 'Click the "Cancel" button.', 'Fai clic su "Cancel".'],
	];
	for (const [step, en, itText] of cases) {
		it(`transforms: ${step}`, () => {
			expect(transformStep(step, 'en', loc)).toBe(en);
			expect(transformStep(step, 'it', loc)).toBe(itText);
		});
	}
});

describe('draft sentences', () => {
	const loc = loadLocales(join(import.meta.dirname, 'fixtures/repo'));

	it('turns Non-Goals into natural sentences', () => {
		expect(nonGoalSentence('Choosing who the task is assigned to.')).toBe('This feature does not include choosing who the task is assigned to.');
		expect(nonGoalSentence('Answers without a source.')).toBe('This feature does not include answers without a source.');
		expect(nonGoalSentence('Paging: the list shows the 50 newest tasks.')).toBe('This feature does not include paging: the list shows the 50 newest tasks.');
		expect(nonGoalSentence('VAT checks.')).toBe('This feature does not include VAT checks.');
	});

	it('speaks to the reader instead of in the first person', () => {
		expect(secondPerson('see my details. I can check them and I am sure')).toBe('see your details. you can check them and you are sure');
	});

	it('builds problem rows from field messages with an action, never from labels or test data', () => {
		const expected = [
			'The form stays open.',
			'The "Title" field shows "Enter a title."',
			'The list does not show "Task without a title".',
			'The message "Organization name is required" appears.',
			'The "Your question" field shows exactly 300 characters.',
		];
		expect(validationRows(expected, 'en', loc)).toEqual([
			{ msg: 'Enter a title.', todo: 'Correct the "Title" field and try again.' },
			{ msg: 'Organization name is required', todo: 'Correct what you entered and try again.' },
		]);
		expect(validationRows(expected.slice(0, 2), 'it', loc)).toEqual([
			{ msg: 'Enter a title.', todo: 'Correggi il campo "Titolo" e riprova.' },
		]);
	});
});

describe('manual:generate drafts', () => {
	it('keeps test data and labels out of the page and marks the Italian title', () => {
		const root = tmpRepo(false);
		replaceIn(root, 'docs/areas/tasks/test-cases/TASK-CREATE-002.md', '- The form stays open.', '- The list does not show "Task without a title".\n- The "Title" field shows "Enter a title."');
		replaceIn(root, 'docs/areas/tasks/test-cases/TASK-CREATE-001.md', '- The list already has two tasks.', '- "Dev User" is the Admin of "MyApp Dev Org".');
		generate(root, capture().io);
		const en = read(root, 'docs/manual/en/tasks/US-TASK-01.md');
		expect(en).not.toContain('It is not possible to');
		expect(en).not.toContain('| "Title" |');
		expect(en).not.toContain('| "Task without a title"');
		expect(en).not.toContain('Dev User');
		expect(en).toContain('| "Enter a title." | Correct the "Title" field and try again. |');
		const it = read(root, 'docs/manual/it/tasks/US-TASK-01.md');
		expect(it).toContain('# Create a task (English, to translate)');
		expect(it).toContain('| "Inserisci un titolo." | Correggi il campo "Titolo" e riprova. |');
	});
});

describe('manual:generate', () => {
	it('creates drafts that match the golden output byte for byte', () => {
		const root = tmpRepo(false);
		const c = capture();
		expect(generate(root, c.io)).toBe(0);
		expect(c.out.at(-1)).toBe('manual:generate: 8 created, 0 updated, 0 stale, 0 without source');
		const files = walkFiles(join(root, 'docs/manual'));
		expect(files).toEqual(walkFiles(FIXTURE_GOLDEN));
		for (const f of files) expect(read(root, `docs/manual/${f}`)).toBe(readFileSync(join(FIXTURE_GOLDEN, f), 'utf8'));
	});

	it('is idempotent and never touches a page body', () => {
		const root = tmpRepo(false);
		generate(root, capture().io);
		const before = walkFiles(join(root, 'docs/manual')).map((f) => read(root, `docs/manual/${f}`));
		const c = capture();
		generate(root, c.io);
		expect(c.out).toEqual(['manual:generate: 0 created, 0 updated, 0 stale, 0 without source']);
		expect(walkFiles(join(root, 'docs/manual')).map((f) => read(root, `docs/manual/${f}`))).toEqual(before);
	});

	it('writes both hashes into new pages and nothing outside docs/manual', () => {
		const root = tmpRepo(false);
		generate(root, capture().io);
		const page = read(root, 'docs/manual/en/tasks/US-TASK-01.md');
		const src = readFileSync(join(root, 'docs/areas/tasks/user-stories/US-TASK-01.md'));
		const tc = readFileSync(join(root, 'docs/areas/tasks/test-cases/TASK-CREATE-001.md'));
		expect(page).toContain(`source_hash: ${gitBlobId(src)}`);
		expect(page).toContain(`steps_hash: ${gitBlobId(tc)}`);
		expect(page).toContain('steps_from: TASK-CREATE-001');
	});

	it('rewrites only derived fields of an existing page', () => {
		const root = tmpRepo();
		const p = 'docs/manual/en/tasks/US-TASK-01.md';
		replaceIn(root, p, 'page_id: tasks/US-TASK-01', 'page_id: wrong');
		replaceIn(root, p, 'screenshots: [images/task-list-new-task-form-open.png]', 'screenshots: []');
		replaceIn(root, p, 'routes: [/tasks]', 'routes: [/custom]');
		const before = read(root, p);
		const body = before.split('\n---\n')[1];
		const c = capture();
		generate(root, c.io);
		expect(c.out).toContain(`updated ${p}`);
		const after = read(root, p);
		expect(after).toContain('page_id: tasks/US-TASK-01');
		expect(after).toContain('screenshots: [images/task-list-new-task-form-open.png]');
		expect(after).toContain('routes: [/custom]');
		expect(after.split('\n---\n')[1]).toBe(body);
	});

	it('reports a stale page with the diff when the stored blob is in git history', () => {
		const root = tmpRepo();
		const p = 'docs/areas/tasks/user-stories/US-TASK-01.md';
		execFileSync('git', ['init', '-q'], { cwd: root });
		const old = execFileSync('git', ['hash-object', '-w', p], { cwd: root, encoding: 'utf8' }).trim();
		expect(old).toBe(gitBlobId(readFileSync(join(root, p))));
		replaceIn(root, p, 'Create a task', 'Create a new task');
		const c = capture();
		generate(root, c.io);
		const text = c.out.join('\n');
		expect(text).toContain('stale docs/manual/en/tasks/US-TASK-01.md');
		expect(text).toContain('-# US-TASK-01: Create a task');
		expect(text).toContain('+# US-TASK-01: Create a new task');
		expect(text).toContain('manual:generate: 0 created, 0 updated, 2 stale, 0 without source');
	});

	it('prints the full source when the stored blob is not in git history', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/areas/tasks/test-cases/TASK-CREATE-001.md', 'quarterly', 'monthly');
		const c = capture();
		generate(root, c.io);
		const text = c.out.join('\n');
		expect(text).toContain('previous version not in git history');
		expect(text).toContain('Prepare the monthly report');
	});

	it('reports that steps_from changed and refreshes it', () => {
		const root = tmpRepo();
		edit(root, 'docs/areas/tasks/test-cases/TASK-CREATE-003.md', (t) => t.replace('Error handling', 'Happy path').replace('TASK-CREATE-003', 'TASK-ACCESS-001'));
		writeFileSync(join(root, 'docs/areas/tasks/test-cases/TASK-ACCESS-001.md'), read(root, 'docs/areas/tasks/test-cases/TASK-CREATE-003.md'));
		rmSync(join(root, 'docs/areas/tasks/test-cases/TASK-CREATE-003.md'));
		const c = capture();
		generate(root, c.io);
		expect(read(root, 'docs/manual/en/tasks/US-TASK-01.md')).toContain('steps_from: TASK-ACCESS-001');
		expect(c.out.join('\n')).toContain('steps_hash differs');
	});

	it('prints pages that have no source any more', () => {
		const root = tmpRepo();
		rmSync(join(root, 'docs/areas/tasks/user-stories/US-TASK-02.md'));
		const c = capture();
		generate(root, c.io);
		expect(c.out.join('\n')).toContain('no source docs/manual/en/tasks/US-TASK-02.md');
		expect(existsSync(join(root, 'docs/manual/en/tasks/US-TASK-02.md'))).toBe(true);
	});

	it('rejects unknown options with exit code 2', () => {
		const c = capture();
		expect(main(['--nope'], c.io)).toBe(2);
	});
});

describe('manual:generate --accept', () => {
	function staleStory(root: string) {
		replaceIn(root, 'docs/areas/tasks/user-stories/US-TASK-01.md', 'Create a task', 'Create a new task');
		// pages follow the new title so only M05 remains
		replaceIn(root, 'docs/manual/en/tasks/US-TASK-01.md', '# Create a task', '# Create a new task');
		replaceIn(root, 'docs/manual/en/tasks/US-TASK-02.md', '[Create a task]', '[Create a new task]');
	}

	it('sets both hashes of both languages for one story', () => {
		const root = tmpRepo();
		staleStory(root);
		expect(runChecks(root).filter((f) => f.id === 'M05')).toHaveLength(2);
		const c = capture();
		expect(accept(root, 'US-TASK-01', c.io)).toBe(0);
		expect(c.out).toEqual([
			'accepted docs/manual/en/tasks/US-TASK-01.md',
			'accepted docs/manual/it/tasks/US-TASK-01.md',
		]);
		expect(runChecks(root).filter((f) => f.id === 'M05')).toEqual([]);
		const src = readFileSync(join(root, 'docs/areas/tasks/user-stories/US-TASK-01.md'));
		expect(read(root, 'docs/manual/it/tasks/US-TASK-01.md')).toContain(`source_hash: ${gitBlobId(src)}`);
	});

	it('accepts a whole area through main', () => {
		const root = tmpRepo();
		staleStory(root);
		const c = capture();
		expect(main(['--root', root, '--accept', 'tasks'], c.io)).toBe(0);
		expect(c.out).toHaveLength(6);
		expect(runChecks(root).filter((f) => f.id === 'M05')).toEqual([]);
	});

	it('refuses, changing nothing, when a page has the draft marker', () => {
		const root = tmpRepo();
		staleStory(root);
		const p = 'docs/manual/it/tasks/US-TASK-01.md';
		edit(root, p, (t) => t.replace('\n# ', '\n<!-- draft: written by manual:generate, remove after review -->\n\n# '));
		const before = read(root, 'docs/manual/en/tasks/US-TASK-01.md');
		const c = capture();
		expect(accept(root, 'US-TASK-01', c.io)).toBe(1);
		expect(c.err.join('\n')).toContain('still has the draft marker');
		expect(read(root, 'docs/manual/en/tasks/US-TASK-01.md')).toBe(before);
	});

	it('refuses when a page has a finding other than M05', () => {
		const root = tmpRepo();
		staleStory(root);
		replaceIn(root, 'docs/manual/en/tasks/US-TASK-01.md', '[Edit a task](US-TASK-02.md)', '[Edit a task](US-TASK-09.md)');
		const c = capture();
		expect(accept(root, 'US-TASK-01', c.io)).toBe(1);
		expect(c.err.join('\n')).toContain('M07');
	});

	it('refuses an unknown target', () => {
		const c = capture();
		expect(accept(tmpRepo(), 'US-NOPE-01', c.io)).toBe(1);
	});
});
