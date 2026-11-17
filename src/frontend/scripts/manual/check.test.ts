// @vitest-environment node
import { rmSync, unlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { languageShare, main, runChecks, WARNING_IDS } from './check.ts';
import type { CheckId, Finding } from './check.ts';
import { capture, cleanup, edit, replaceIn, tmpRepo } from './testutil.ts';

afterEach(cleanup);

const EN1 = 'docs/manual/en/tasks/US-TASK-01.md';
const IT1 = 'docs/manual/it/tasks/US-TASK-01.md';
const EN2 = 'docs/manual/en/tasks/US-TASK-02.md';
const BENCH = 'docs/manual/bench.md';
const ids = (f: Finding[]) => [...new Set(f.map((x) => x.id))];
const only = (f: Finding[], id: CheckId) => f.filter((x) => x.id === id);

describe('baseline: the valid fixture', () => {
	it('has no finding at all, so every check passes', () => {
		expect(runChecks(tmpRepo())).toEqual([]);
	});
	it('exits 0 in normal and strict mode', () => {
		const root = tmpRepo();
		const c = capture();
		expect(main(['--root', root], c.io)).toBe(0);
		expect(main(['--root', root, '--strict'], c.io)).toBe(0);
		expect(c.out[0]).toBe('manual:check: 0 failures, 0 warnings');
	});
});

describe('one failing case per check', () => {
	it('M01: a file outside the layout', () => {
		const root = tmpRepo();
		writeFileSync(join(root, 'docs/manual/en/notes.txt'), 'x');
		const f = only(runChecks(root), 'M01');
		expect(f).toHaveLength(1);
		expect(f[0].path).toBe('docs/manual/en/notes.txt');
		expect(f[0].severity).toBe('fail');
	});

	it('M01: an image with a bad name and a stray folder', () => {
		const root = tmpRepo();
		writeFileSync(join(root, 'docs/manual/en/tasks/images/Bad_Name.png'), 'x');
		writeFileSync(join(root, 'docs/manual/fr.md'), 'x');
		expect(only(runChecks(root), 'M01').map((x) => x.path)).toEqual([
			'docs/manual/en/tasks/images/Bad_Name.png',
			'docs/manual/fr.md',
		]);
	});

	it('M02: a missing story page and a page without source', () => {
		const root = tmpRepo();
		unlinkSync(join(root, EN2));
		writeFileSync(join(root, 'docs/manual/en/tasks/US-TASK-09.md'), 'x');
		const f = only(runChecks(root), 'M02');
		expect(f.map((x) => [x.path, x.message.split(':')[0]])).toEqual([
			[EN2, 'page is missing'],
			['docs/manual/en/tasks/US-TASK-09.md', 'page has no source'],
		]);
	});

	it('M02: a missing home page', () => {
		const root = tmpRepo();
		unlinkSync(join(root, 'docs/manual/it/index.md'));
		expect(only(runChecks(root), 'M02').map((x) => x.path)).toEqual(['docs/manual/it/index.md']);
	});

	it('M03: wrong lang, wrong order, bad hash, bad screenshots', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, 'lang: en', 'lang: it');
		replaceIn(root, EN1, 'source_hash: ', 'source_hash: XYZ');
		replaceIn(root, EN1, 'screenshots: [images/task-list-new-task-form-open.png]', 'screenshots: []');
		const msgs = only(runChecks(root), 'M03').map((x) => x.message);
		expect(msgs.some((m) => m.startsWith('lang is it'))).toBe(true);
		expect(msgs.some((m) => m.startsWith('source_hash'))).toBe(true);
		expect(msgs.some((m) => m.startsWith('screenshots'))).toBe(true);
	});

	it('M03: missing field, unknown field and order', () => {
		const root = tmpRepo();
		edit(root, EN1, (t) => t.replace('contexts: [task-form]\n', ''));
		edit(root, IT1, (t) => t.replace('kind: story\nlang: it\n', 'lang: it\nkind: story\n').replace('---\n\n# ', '---\nextra: 1\n\n# '));
		const m = only(runChecks(root), 'M03').map((x) => `${x.path} ${x.message}`);
		expect(m).toContain(`${EN1} field contexts is missing`);
		expect(m.some((x) => x.includes('fields are not in the order'))).toBe(true);
	});

	it('M03: schema_version must equal the contract', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, 'schema_version: 1', 'schema_version: 2');
		expect(only(runChecks(root), 'M03')[0].message).toMatch(/schema_version is 2/);
	});

	it('M03: front matter that does not start with ---', () => {
		const root = tmpRepo();
		edit(root, EN2, (t) => t.replace(/^---\n/, ''));
		expect(only(runChecks(root), 'M03')[0].message).toMatch(/first line/);
	});

	it('M04: italics, unknown heading, missing steps, wrong title, long it title', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, 'type a value such as', 'type a *value* such as');
		replaceIn(root, EN1, '## Good to know', '## Extra notes');
		replaceIn(root, EN2, '# Edit a task', '# Edit the task');
		edit(root, IT1, (t) => t.replace(/# Crea un'attività/, `# ${'Titolo lungo '.repeat(8)}`));
		const m = only(runChecks(root), 'M04').map((x) => x.message).join('\n');
		expect(m).toMatch(/bold/);
		expect(m).toMatch(/"Extra notes" is not one of the en headings/);
		expect(m).toMatch(/the title is "Edit the task", expected "Edit a task"/);
		expect(m).toMatch(/limit 70/);
	});

	it('M04: required sections, section order and size limits', () => {
		const root = tmpRepo();
		edit(root, EN2, (t) => t.replace(/## Steps[\s\S]*?## What/, '## What'));
		edit(root, EN1, (t) => t.replace(/\n## If something goes wrong[\s\S]*?\n## Related/, '\n## Related'));
		edit(root, IT1, (t) => `${t}\n${'- riga\n'.repeat(160)}`);
		const m = only(runChecks(root), 'M04').map((x) => `${x.path}: ${x.message}`);
		expect(m).toContain(`${EN2}: required section "Steps" is missing`);
		expect(m.some((x) => x.startsWith(EN1) && x.includes('"If something goes wrong" is missing'))).toBe(true);
		expect(m.some((x) => x.startsWith(IT1) && x.includes('limit 150'))).toBe(true);
	});

	it('M04: out-of-order sections, more than 12 steps, bad table columns', () => {
		const root = tmpRepo();
		edit(root, EN2, (t) => t.replace('## What you will see\n\n- The first row shows the new title.\n\n', '').replace('## Steps', '## What you will see\n\n- x\n\n## Steps'));
		edit(root, IT1, (t) => t.replace('| Cosa vedi | Cosa fare |', '| A | B |'));
		edit(root, EN1, (t) => t.replace('5. Click "Create Task".', Array.from({ length: 9 }, (_, i) => `${i + 5}. Click "Done".`).join('\n')));
		const m = only(runChecks(root), 'M04').map((x) => x.message).join('\n');
		expect(m).toMatch(/out of order/);
		expect(m).toMatch(/table columns must be "Cosa vedi" \/ "Cosa fare"/);
		expect(m).toMatch(/has 13 items \(limit 12\)/);
	});

	it('M04: a home page with an H2', () => {
		const root = tmpRepo();
		edit(root, 'docs/manual/en/index.md', (t) => `${t}\n## Steps\n\n1. x\n`);
		expect(only(runChecks(root), 'M04')[0].message).toMatch(/only the H1 and the introduction/);
	});

	it('M05: a changed story and a changed test case are warnings', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/areas/tasks/user-stories/US-TASK-02.md', 'High', 'Medium');
		replaceIn(root, 'docs/areas/tasks/test-cases/TASK-CREATE-001.md', 'quarterly', 'monthly');
		const f = only(runChecks(root), 'M05');
		expect(f.map((x) => x.path)).toEqual([EN1, IT1, EN2, 'docs/manual/it/tasks/US-TASK-02.md'].sort());
		expect(f.every((x) => x.severity === 'warn')).toBe(true);
		expect(ids(runChecks(root)).filter((i) => !WARNING_IDS.includes(i))).toEqual([]);
	});

	it('M05: steps_from is no longer the lowest Happy path', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/areas/tasks/test-cases/TASK-CREATE-002.md', 'Validation', 'Happy path');
		writeFileSync(join(root, 'docs/areas/tasks/test-cases/TASK-CREATE-000.md'), 'x');
		// 002 is now a Happy path but 001 stays the lowest: still current
		expect(only(runChecks(root), 'M05')).toEqual([]);
		replaceIn(root, 'docs/areas/tasks/test-cases/TASK-CREATE-001.md', 'Happy path', 'UX');
		expect(only(runChecks(root), 'M05').some((x) => /lowest-numbered Happy path test case is now TASK-CREATE-002/.test(x.message))).toBe(true);
	});

	it('M06: draft marker is a warning', () => {
		const root = tmpRepo();
		edit(root, EN2, (t) => t.replace('\n# Edit', '\n<!-- draft: written by manual:generate, remove after review -->\n\n# Edit'));
		const f = only(runChecks(root), 'M06');
		expect(f).toHaveLength(1);
		expect(f[0].severity).toBe('warn');
		expect(ids(runChecks(root))).toEqual(['M06']);
	});

	it('M07: a link that does not resolve', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, '(US-TASK-02.md)', '(US-TASK-07.md)');
		replaceIn(root, IT1, '(US-TASK-02.md)', '(../en/tasks/US-TASK-02.md)');
		expect(only(runChecks(root), 'M07').map((x) => x.path)).toEqual([EN1, IT1]);
	});

	it('M08: missing image, wrong hash, not a PNG, no entry', () => {
		const root = tmpRepo();
		const img = 'docs/manual/en/tasks/images/task-list-new-task-form-open.png';
		writeFileSync(join(root, img), 'not a png');
		let m = only(runChecks(root), 'M08').map((x) => x.message).join('\n');
		expect(m).toMatch(/not a PNG/);
		expect(m).toMatch(/sha256 in captures.json differs/);
		unlinkSync(join(root, img));
		m = only(runChecks(root), 'M08').map((x) => x.message).join('\n');
		expect(m).toMatch(/does not exist/);
	});

	it('M08: an image without a captures.json entry', () => {
		const root = tmpRepo();
		writeFileSync(join(root, 'docs/manual/captures.json'), '{ "schema_version": 1, "images": [] }');
		expect(only(runChecks(root), 'M08').map((x) => x.message)).toEqual([
			'image images/task-list-new-task-form-open.png has no entry in captures.json',
			'image images/task-list-new-task-form-open.png has no entry in captures.json',
		]);
		unlinkSync(join(root, 'docs/manual/captures.json'));
		expect(only(runChecks(root), 'M08')[0].message).toMatch(/captures.json does not exist/);
	});

	it('M09: an orphan image and an orphan manifest entry', () => {
		const root = tmpRepo();
		writeFileSync(join(root, 'docs/manual/en/tasks/images/orphan.png'), 'x');
		edit(root, 'docs/manual/captures.json', (t) => t.replace('"images": [', '"images": [{"path":"docs/manual/en/tasks/images/ghost.png","lang":"en","capture":"x","viewport":"1x1","sha256":"0"},'));
		const m = only(runChecks(root), 'M09').map((x) => `${x.path}: ${x.message}`);
		expect(m).toContain('docs/manual/en/tasks/images/orphan.png: image is not referenced by any page');
		expect(m.some((x) => x.includes('ghost.png has no file'))).toBe(true);
	});

	it('M10: unknown route, and a story page without route', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, 'routes: [/tasks]', 'routes: [/nowhere]');
		replaceIn(root, EN2, 'routes: [/tasks/:id]', 'routes: []');
		const m = only(runChecks(root), 'M10').map((x) => `${x.path}: ${x.message}`);
		expect(m.some((x) => x.startsWith(EN1) && x.includes('/nowhere'))).toBe(true);
		expect(m).toContain(`${EN2}: a story page needs at least one route`);
	});

	it('M11: parity of steps, rows, sections, images and metadata', () => {
		const root = tmpRepo();
		edit(root, IT1, (t) => t.replace('4. Nel campo "Stato" scegli "Completata".\n', '').replace('5. Fai', '4. Fai'));
		edit(root, IT1, (t) => t.replace(/\| "Inserisci un titolo\.".*\n/, ''));
		edit(root, IT1, (t) => t.replace(/\n## Da sapere[\s\S]*?\n## Se/, '\n## Se'));
		replaceIn(root, IT1, 'contexts: [task-form]', 'contexts: []');
		const m = only(runChecks(root), 'M11').map((x) => x.message).join('\n');
		expect(m).toMatch(/steps count differs: en 5, it 4/);
		expect(m).toMatch(/problems rows differ: en 2, it 1/);
		expect(m).toMatch(/H2 sections differ/);
		expect(m).toMatch(/contexts differs/);
	});

	it('M11: image under a different step, and an image missing in it', () => {
		const root = tmpRepo();
		edit(root, IT1, (t) => t.replace('\n   ![Il modulo Nuova attività sopra l\'elenco delle attività](images/task-list-new-task-form-open.png)\n3.', '\n3.').replace('3. Nel campo', '3. Nel campo'));
		edit(root, IT1, (t) => t.replace('4. Nel campo "Stato" scegli "Completata".', '4. Nel campo "Stato" scegli "Completata".\n\n   ![Il modulo Nuova attività sopra l\'elenco delle attività](images/task-list-new-task-form-open.png)'));
		expect(only(runChecks(root), 'M11').some((x) => x.message.startsWith('images differ'))).toBe(true);
		unlinkSync(join(root, 'docs/manual/it/tasks/images/task-list-new-task-form-open.png'));
		expect(only(runChecks(root), 'M11').some((x) => /has no counterpart/.test(x.message))).toBe(true);
	});

	it('M12: an Italian page written in English, and an English page written in Italian', () => {
		const root = tmpRepo();
		edit(root, IT1, (t) => t.replace(/Usa questa pagina per aggiungere[^\n]*/, 'Use this page to add a task with a title and the team can see what needs to be done at the end of the day. You can do it when you are on the page and the list is open, and then you will see that the new task is there for all of us.'));
		edit(root, 'docs/manual/en/tasks/index.md', (t) => t.replace(/The Tasks area[^\n]*/, 'Questa è la pagina delle attività che il team può usare per tenere il lavoro in ordine, come una lista condivisa.'));
		const f = only(runChecks(root), 'M12');
		expect(f.map((x) => x.path)).toEqual(['docs/manual/en/tasks/index.md', IT1]);
	});

	it('M12 measures the share outside quotes only', () => {
		const own = new Set(['la']);
		const other = new Set(['the', 'and', 'la']);
		expect(languageShare('the "the and the and" cat', other, own).share).toBeCloseTo(1 / 2);
		expect(languageShare('', other, own)).toEqual({ share: 0, tokens: 0 });
		expect(languageShare('la la la', other, own).share).toBe(0);
	});

	it('M13: an Error States message missing from the en page and its Italian text from the it page', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, '"Your role does not allow this change."', '"Not allowed."');
		replaceIn(root, IT1, '"Il tuo ruolo non consente questa modifica."', '"Non permesso."');
		const f = only(runChecks(root), 'M13');
		expect(f.map((x) => x.path)).toEqual([EN1, IT1]);
		expect(f[0].message).toMatch(/Your role does not allow this change/);
		expect(f[1].message).toMatch(/Il tuo ruolo non consente questa modifica/);
	});

	it('M14: a quoted string that is neither a UI value nor Test Data is a warning', () => {
		const root = tmpRepo();
		replaceIn(root, EN2, 'Click "Update".', 'Click "Apply".');
		const f = only(runChecks(root), 'M14');
		expect(f).toHaveLength(1);
		expect(f[0]).toMatchObject({ path: EN2, severity: 'warn' });
		expect(f[0].message).toContain('"Apply"');
	});

	it('M15: a context key not declared, and declared twice', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, 'contexts: [task-form]', 'contexts: []');
		replaceIn(root, EN2, 'contexts: []', 'contexts: [task-form]');
		replaceIn(root, 'docs/manual/it/tasks/US-TASK-02.md', 'contexts: []', 'contexts: [task-form]');
		const f = only(runChecks(root), 'M15');
		expect(f).toHaveLength(1);
		expect(f[0].message).toMatch(/declared by 2 it story pages/);
	});

	it('M15: no page declares the context', () => {
		const root = tmpRepo();
		replaceIn(root, EN1, 'contexts: [task-form]', 'contexts: []');
		replaceIn(root, IT1, 'contexts: [task-form]', 'contexts: []');
		const f = only(runChecks(root), 'M15');
		expect(f.map((x) => x.message)).toEqual([
			'context "task-form" is declared by 0 en story pages (expected exactly 1)',
			'context "task-form" is declared by 0 it story pages (expected exactly 1)',
		]);
		expect(f[0].path).toBe('src/frontend/src/components/TaskForm.tsx');
	});

	it('M16: missing file, wrong headings, too few out-of-scope questions, extra rules', () => {
		const root = tmpRepo();
		unlinkSync(join(root, BENCH));
		expect(only(runChecks(root), 'M16')[0].message).toMatch(/missing/);
		writeFileSync(join(root, BENCH), '# Help bench\n\n## Extra\n');
		expect(only(runChecks(root), 'M16')[0].message).toMatch(/headings must be exactly/);
		writeFileSync(join(root, BENCH), '# Help bench\n\n## Out of scope\n\n### en\n\n1. One?\n\n### it\n\nNone.\n\n## Extra\n\nNone.\n');
		const m = only(runChecks(root), 'M16').map((x) => x.message);
		expect(m).toEqual([
			'Out of scope / en: 1 questions, at least 10 needed (10, and 20% of 3 in-scope bench questions)',
			'Out of scope / it: 0 questions, at least 10 needed (10, and 20% of 2 in-scope bench questions)',
		]);
	});

	it('M16: the Extra table is validated, and an indexed question fails', () => {
		const root = tmpRepo();
		const base = readBenchBase(root);
		const text = (rows: string) => {
			return base.replace('## Extra\n\nNone.\n', `## Extra\n\n| Question | Language | Story |\n|---|---|---|\n${rows}\n`);
		};
		writeFileSync(join(root, BENCH), text('| "Where is the list?" | en | US-TASK-01 |'));
		expect(only(runChecks(root), 'M16')).toEqual([]);
		writeFileSync(join(root, BENCH), text('| Where | fr | US-NOPE-01 |'));
		const m = only(runChecks(root), 'M16').map((x) => x.message).join('\n');
		expect(m).toMatch(/must be in double quotes/);
		expect(m).toMatch(/Language "fr"/);
		expect(m).toMatch(/Story "US-NOPE-01"/);
		// every question of the story is tried: exactly one of them is bench and the others are indexed
		const questions = ['How do I rename a task?', 'Can I fix a typo in a task?', 'Where do I change the name of an item?'];
		writeFileSync(join(root, BENCH), text(questions.map((q) => `| "${q}" | en | US-TASK-02 |`).join('\n')));
		expect(only(runChecks(root), 'M16').filter((x) => /equals an indexed question/.test(x.message))).toHaveLength(2);
	});
});

function readBenchBase(root: string): string {
	let t = '';
	edit(root, BENCH, (x) => {
		t = x;
		return x;
	});
	return t;
}

describe('options, severities and exit codes', () => {
	it('warnings are M05, M06 and M14 only', () => {
		expect(WARNING_IDS).toEqual(['M05', 'M06', 'M14']);
	});

	it('a warning passes normally and fails with --strict', () => {
		const root = tmpRepo();
		replaceIn(root, 'docs/areas/tasks/user-stories/US-TASK-02.md', 'High', 'Medium');
		const c = capture();
		expect(main(['--root', root], c.io)).toBe(0);
		expect(main(['--root', root, '--strict'], c.io)).toBe(1);
		expect(c.out[0]).toMatch(/^M05 docs\/manual\/en\/tasks\/US-TASK-02\.md: stale: /);
		expect(c.out.at(-1)).toBe('manual:check: 0 failures, 2 warnings (strict: warnings fail)');
	});

	it('a failure exits 1 in normal mode', () => {
		const root = tmpRepo();
		unlinkSync(join(root, EN2));
		const c = capture();
		expect(main(['--root', root], c.io)).toBe(1);
		expect(c.out[0]).toBe(`M02 ${EN2}: page is missing`);
	});

	it('--no-images skips M08 and M09', () => {
		const root = tmpRepo();
		rmSync(join(root, 'docs/manual/captures.json'));
		rmSync(join(root, 'docs/manual/en/tasks/images'), { recursive: true });
		writeFileSync(join(root, 'docs/manual/it/tasks/images/orphan.png'), 'x');
		expect(ids(runChecks(root))).toEqual(['M08', 'M09', 'M11']);
		expect(ids(runChecks(root, { noImages: true }))).toEqual(['M11']);
		const c = capture();
		expect(main(['--root', root, '--no-images'], c.io)).toBe(1);
	});

	it('works on a tree without src/frontend (synthetic domain)', () => {
		const root = tmpRepo();
		rmSync(join(root, 'src'), { recursive: true });
		expect(runChecks(root)).toEqual([]);
	});

	it('rejects unknown options with exit code 2', () => {
		const c = capture();
		expect(main(['--bogus'], c.io)).toBe(2);
		expect(c.err[0]).toMatch(/unknown option/);
	});

	it('findings are sorted by check id and path', () => {
		const root = tmpRepo(false);
		const f = runChecks(root);
		expect(f.map((x) => x.id + x.path)).toEqual([...f.map((x) => x.id + x.path)].sort());
		expect(ids(f)).toContain('M02');
		expect(ids(f)).toContain('M16');
	});
});
