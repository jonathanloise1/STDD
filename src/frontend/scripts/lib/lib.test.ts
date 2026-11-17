// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { parseAreaIndex } from './areaindex.ts';
import { gitBlobId, sha256Hex } from './hash.ts';
import { italianUiText } from './locales.ts';
import { DRAFT_MARKER, imageRefs, linksOf, parseMarkdown } from './markdown.ts';
import { analyzeBody, classifyPagePath, parseFlowList, parsePage, splitPage } from './page.ts';
import { normalizeQuestion, splitQuestions } from './questions.ts';
import { loadRegistry, loadRepo, lowestHappyPath } from './repo.ts';
import { scanContextUses, scanRoutePaths } from './sourcescan.ts';
import { parseStory } from './story.ts';
import { parseTestCase } from './testcase.ts';
import { parseTable, splitRow } from './text.ts';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { FIXTURE_REPO } from '../manual/testutil.ts';

const read = (p: string) => readFileSync(join(FIXTURE_REPO, p), 'utf8');

describe('hash', () => {
	it('computes git blob ids', () => {
		expect(gitBlobId(Buffer.from(''))).toBe('e69de29bb2d1d6434b8b29ae775ad8c2e48c5391');
		expect(gitBlobId(Buffer.from('hello\n'))).toBe('ce013625030ba8dba906f756967f9e9ca394464a');
	});
	it('computes sha256', () => {
		expect(sha256Hex('abc')).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
	});
});

describe('tables', () => {
	it('splits rows with escaped pipes', () => {
		expect(splitRow('| a \\| b | c |')).toEqual(['a | b', 'c']);
		expect(parseTable(['| H1 | H2 |', '|---|---|', '| x | |'])).toEqual({ header: ['H1', 'H2'], rows: [['x', '']] });
	});
});

describe('story parser', () => {
	const s = parseStory(read('docs/areas/tasks/user-stories/US-TASK-01.md'), 'p', 'tasks');
	it('reads fields and story lines', () => {
		expect(s).toMatchObject({
			id: 'US-TASK-01',
			title: 'Create a task',
			priority: 'Critical',
			feature: 'F-TASK-01',
			userInput: true,
			asA: 'team member',
			iWant: 'to add a task with a title',
			soThat: 'the team can see what needs to be done',
		});
	});
	it('reads lists and tables', () => {
		expect(s.criteria).toHaveLength(2);
		expect(s.access).toEqual([
			{ role: 'Admin', allowed: 'Yes' },
			{ role: 'Editor', allowed: 'Yes' },
			{ role: 'Viewer', allowed: 'No' },
		]);
		expect(s.rules).toEqual(['A new task always starts with status "Todo".']);
		expect(s.errorStates[0].message).toBe('Your role does not allow this change.');
		expect(s.nonGoals).toEqual(['Choosing who the task is assigned to.']);
		expect(s.questions.en).toHaveLength(4);
		expect(s.questions.it).toHaveLength(3);
	});
	it('treats None. as empty', () => {
		const t = parseStory(read('docs/areas/tasks/user-stories/US-TASK-02.md'), 'p', 'tasks');
		expect(t.rules).toEqual([]);
		expect(t.errorStates).toEqual([]);
		expect(t.nonGoals).toEqual([]);
		expect(t.userInput).toBe(false);
	});
});

describe('test case parser', () => {
	it('reads a full test case with a trace', () => {
		const t = parseTestCase(read('docs/areas/tasks/test-cases/TASK-CREATE-003.md'), 'p', 'tasks');
		expect(t).toMatchObject({ id: 'TASK-CREATE-003', storyId: 'US-TASK-01', type: 'Error handling', priority: 'High' });
		expect(t.trace).toEqual({
			message: 'Your role does not allow this change.',
			logKey: 'task.create.not_allowed',
			severity: 'Warning',
			runbook: '[task-create-not-allowed](../operations.md#task-create-not-allowed)',
		});
		expect(t.testData).toEqual([]);
	});
	it('reads steps, expected results and test data', () => {
		const t = parseTestCase(read('docs/areas/tasks/test-cases/TASK-CREATE-001.md'), 'p', 'tasks');
		expect(t.steps).toHaveLength(6);
		expect(t.expected).toHaveLength(2);
		expect(t.testData).toEqual([
			{ field: 'Title', value: 'Prepare the quarterly report' },
			{ field: 'Status', value: 'Done' },
		]);
		expect(t.preconditions[0]).toBe('Signed in as Editor of "MyApp Dev Org".');
		expect(t.trace).toBeNull();
	});
});

describe('area index and repository', () => {
	it('parses the area index', () => {
		const a = parseAreaIndex(read('docs/areas/tasks/userstories.md'), 'p');
		expect(a.title).toBe('Tasks');
		expect(a.stories.map((s) => s.id)).toEqual(['US-TASK-01', 'US-TASK-02']);
		expect(a.overview).toMatch(/^The Tasks area/);
	});
	it('loads the registry and the repository', () => {
		expect(loadRegistry(FIXTURE_REPO)).toEqual([{ folder: 'tasks', code: 'TASK', title: 'Tasks' }]);
		const repo = loadRepo(FIXTURE_REPO);
		expect(repo.productName).toBe('MyApp');
		expect(repo.areas).toHaveLength(1);
		expect(repo.areas[0].stories).toHaveLength(2);
		expect(repo.areas[0].testCases).toHaveLength(4);
		expect(lowestHappyPath(repo.areas[0], 'US-TASK-01')?.id).toBe('TASK-CREATE-001');
		expect(lowestHappyPath(repo.areas[0], 'US-NONE-01')).toBeNull();
	});
});

describe('markdown subset', () => {
	const ok = (t: string) => {
		const r = parseMarkdown(t);
		expect(r.violations).toEqual([]);
		return r.blocks;
	};
	it('parses the allowed constructs', () => {
		const blocks = ok(
			[
				DRAFT_MARKER,
				'',
				'# Title',
				'',
				'Intro with **bold** and a [link](US-TASK-02.md).',
				'',
				'## Steps',
				'',
				'1. One',
				'2. Two',
				'',
				'   ![Alt text](images/a-b.png)',
				'3. Three',
				'',
				'- bullet',
				'',
				'| A | B |',
				'|---|---|',
				'| 1 | **2** |',
			].join('\n'),
		);
		expect(blocks.map((b) => b.type)).toEqual(['draft', 'heading', 'paragraph', 'heading', 'ordered', 'bullets', 'table']);
		expect(imageRefs(blocks)).toEqual(['images/a-b.png']);
		expect(linksOf(blocks)).toEqual([{ href: 'US-TASK-02.md', text: 'link', line: 5 }]);
		const list = blocks[4];
		expect(list.type === 'ordered' && list.items.map((i) => i.number)).toEqual([1, 2, 3]);
	});
	it('accepts an image as a paragraph', () => {
		expect(ok('![Alt](images/x.png)').map((b) => b.type)).toEqual(['image']);
	});
	const bad: [string, string, RegExp][] = [
		['html', '<div>x</div>', /HTML/],
		['code span', 'a `b` c', /code/],
		['code block', '```\nx\n```', /code block/],
		['italics star', 'a *b* c', /bold/],
		['italics underscore', 'a _b_ c', /italics/],
		['h3', '### Three', /heading level 3/],
		['nested list', '- a\n  - b', /nested/],
		['external link', '[x](https://example.com)', /external/],
		['bare url', 'see https://example.com', /external/],
		['anchor link', '[x](a.md#top)', /anchor/],
		['non page link', '[x](a.txt)', /page/],
		['list numbers', '1. a\n3. b', /should be 2/],
		['image without alt', '![](images/a.png)', /alt/],
		['image outside images', '![A](pics/a.png)', /images\/\{file\}\.png/],
		['image alt too long', `![${'a'.repeat(121)}](images/a.png)`, /121/],
		['quote', '> quoted', /quotes/],
		['rule', '---', /rules/],
		['table without separator', '| A |\n| 1 |', /separator/],
		['table width', '| A | B |\n|---|---|\n| 1 |', /cells/],
		['two images on an item', '1. a\n\n   ![A](images/a.png)\n   ![B](images/b.png)', /one image/],
	];
	for (const [name, text, re] of bad) {
		it(`flags ${name}`, () => {
			const v = parseMarkdown(text).violations;
			expect(v.some((x) => re.test(x.message))).toBe(true);
		});
	}
	it('is deterministic', () => {
		const t = '# A\n\nText **x**\n\n- a\n- b\n';
		expect(parseMarkdown(t)).toEqual(parseMarkdown(t));
	});
});

describe('manual page', () => {
	it('splits front matter and body', () => {
		const s = splitPage('---\na: 1\nb: [x, y]\n---\nBody\n');
		expect(s.frontMatter?.entries.map((e) => [e.key, e.value])).toEqual([['a', '1'], ['b', '[x, y]']]);
		expect(s.body).toBe('Body\n');
		expect(s.bodyStartLine).toBe(5);
		expect(splitPage('no front matter').error).toBeTruthy();
		expect(splitPage('---\na: 1\n').error).toMatch(/not closed/);
	});
	it('parses flow lists', () => {
		expect(parseFlowList('[]')).toEqual([]);
		expect(parseFlowList('[a, b]')).toEqual(['a', 'b']);
		expect(parseFlowList('["a"]')).toBeNull();
		expect(parseFlowList('a, b')).toBeNull();
	});
	it('classifies paths', () => {
		expect(classifyPagePath('index.md')?.kind).toBe('home');
		expect(classifyPagePath('tasks/index.md')).toMatchObject({ kind: 'area', area: 'tasks' });
		expect(classifyPagePath('tasks/US-TASK-01.md')).toMatchObject({ kind: 'story', storyId: 'US-TASK-01' });
		expect(classifyPagePath('tasks/other.md')).toBeNull();
	});
	it('reads the valid fixture page', () => {
		const text = read('docs/manual/en/tasks/US-TASK-01.md');
		const p = parsePage(text, 'en', 'docs/manual/en/tasks/US-TASK-01.md', 'tasks/US-TASK-01.md');
		expect(p.fields['page_id']).toBe('tasks/US-TASK-01');
		expect(p.structure.violations).toEqual([]);
		expect(p.structure.sections.map((s) => s.key)).toEqual(['before', 'steps', 'result', 'rules', 'problems', 'related']);
		expect(p.structure.hasDraftMarker).toBe(false);
	});
	it('maps headings per language', () => {
		const s = analyzeBody('# T\n\nIntro\n\n## Passaggi\n\n1. a\n\n## Steps\n\n1. a\n', 'it');
		expect(s.sections.map((x) => x.key)).toEqual(['steps', null]);
	});
});

describe('questions and locales', () => {
	it('normalises questions', () => {
		expect(normalizeQuestion('  a   b\t c ')).toBe('a b c');
	});
	it('splits bench and indexed questions by hash', () => {
		const qs = ['one?', 'two?', 'three?', 'four?', 'five?'];
		const a = splitQuestions('US-X-01', 'en', qs);
		expect(a.bench).toHaveLength(2);
		expect(a.indexed).toHaveLength(3);
		expect(splitQuestions('US-X-01', 'en', [...qs].reverse())).toEqual(a);
		expect(splitQuestions('US-X-01', 'en', qs.slice(0, 3)).bench).toHaveLength(1);
		expect(splitQuestions('US-X-01', 'en', qs.slice(0, 4)).bench).toHaveLength(2);
		const key = (q: string) => sha256Hex(`US-X-01\nen\n${q}`);
		const sorted = [...qs].sort((x, y) => (key(x) < key(y) ? -1 : 1));
		expect(a.bench).toEqual(sorted.slice(0, 2));
	});
	it('looks up unique Italian UI text', () => {
		const loc = {
			available: true,
			en: { 'a.x': 'Save', 'b.y': 'Save', 'a.c': 'Cancel', 'b.c': 'Cancel', 'a.v': 'Hi {{n}}' },
			it: { 'a.x': 'Salva', 'b.y': 'Salva', 'a.c': 'Annulla', 'b.c': 'Chiudi', 'a.v': 'Ciao {{n}}' },
		};
		expect(italianUiText(loc, 'Save')).toBe('Salva');
		expect(italianUiText(loc, 'Cancel')).toBeNull();
		expect(italianUiText(loc, 'Hi {{n}}')).toBeNull();
		expect(italianUiText(loc, 'Unknown')).toBeNull();
	});
});

describe('source scans', () => {
	it('finds route paths and contexts', () => {
		const r = scanRoutePaths(FIXTURE_REPO);
		expect(r.scanned).toBe(true);
		expect([...r.found].sort()).toEqual(['/tasks', '/tasks/:id']);
		const c = scanContextUses(FIXTURE_REPO);
		expect(c.uses.map((u) => u.key)).toEqual(['task-form', 'task-form']);
	});
});
