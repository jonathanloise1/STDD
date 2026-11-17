import { join } from 'node:path';
import { parseAreaIndex } from './areaindex.ts';
import type { AreaIndex } from './areaindex.ts';
import { compareCodePoint, isFile, listDirs, listFiles, readText, rel } from './fsutil.ts';
import { gitBlobIdOfFile } from './hash.ts';
import { splitSections } from './sections.ts';
import { parseStory } from './story.ts';
import type { Story } from './story.ts';
import { parseTestCase } from './testcase.ts';
import type { TestCase } from './testcase.ts';
import { parseTable } from './text.ts';

export interface Area {
	folder: string;
	code: string;
	title: string;
	/** True when the area has a row in the registry of development.md 3.2. */
	registered: boolean;
	indexPath: string;
	index: AreaIndex | null;
	stories: Story[];
	testCases: TestCase[];
}

export interface Repo {
	/** Absolute root: a directory laid out like the repository (`docs/`, `src/frontend/`). */
	root: string;
	areas: Area[];
	/** Product name: the part of the H1 of docs/overview.md before ` - `, or `MyApp`. */
	productName: string;
}

export interface RegistryRow {
	folder: string;
	code: string;
	title: string;
}

/** Area registry: the table of development.md 3.2, in order. Empty when the file or table is missing. */
export function loadRegistry(root: string): RegistryRow[] {
	const abs = join(root, 'docs/guidelines/development.md');
	if (!isFile(abs)) return [];
	const text = readText(abs);
	const start = text.indexOf('### 3.2 Area registry');
	if (start < 0) return [];
	const after = text.slice(start + 1);
	const end = after.search(/\n#{2,3} /);
	const block = after.slice(0, end < 0 ? undefined : end).split('\n');
	const t = parseTable(block);
	if (!t) return [];
	return t.rows.map((r) => ({
		folder: (r[0] ?? '').replace(/`/g, ''),
		code: (r[1] ?? '').replace(/`/g, ''),
		title: r[2] ?? '',
	}));
}

/** Loads areas, stories and test cases from a repository-shaped root. */
export function loadRepo(root: string): Repo {
	const registry = loadRegistry(root);
	const folders = listDirs(join(root, 'docs/areas'));
	const ordered = [
		...registry.map((r) => r.folder).filter((f) => folders.includes(f)),
		...folders.filter((f) => !registry.some((r) => r.folder === f)),
	];
	const areas: Area[] = ordered.map((folder) => {
		const reg = registry.find((r) => r.folder === folder);
		const indexPath = rel('docs/areas', folder, 'userstories.md');
		const index = isFile(join(root, indexPath)) ? parseAreaIndex(readText(join(root, indexPath)), indexPath) : null;
		const storyDir = join(root, 'docs/areas', folder, 'user-stories');
		const stories = listFiles(storyDir)
			.filter((n) => /^US-[A-Z]{2,8}-\d{2}\.md$/.test(n))
			.map((n) => {
				const p = rel('docs/areas', folder, 'user-stories', n);
				return parseStory(readText(join(root, p)), p, folder);
			});
		const tcDir = join(root, 'docs/areas', folder, 'test-cases');
		const testCases = listFiles(tcDir)
			.filter((n) => n.endsWith('.md'))
			.map((n) => {
				const p = rel('docs/areas', folder, 'test-cases', n);
				return parseTestCase(readText(join(root, p)), p, folder);
			});
		const code = reg?.code ?? /^US-([A-Z]+)-/.exec(stories[0]?.id ?? '')?.[1] ?? '';
		return {
			folder,
			code,
			title: reg?.title ?? index?.title ?? folder,
			registered: Boolean(reg),
			indexPath,
			index,
			stories,
			testCases,
		};
	});
	let productName = 'MyApp';
	const ov = join(root, 'docs/overview.md');
	if (isFile(ov)) {
		const h1 = splitSections(readText(ov)).title;
		if (h1) productName = h1.split(' - ')[0].trim() || productName;
	}
	return { root, areas, productName };
}

export function allStories(repo: Repo): Story[] {
	return repo.areas.flatMap((a) => a.stories);
}

export function findStory(repo: Repo, id: string): { area: Area; story: Story } | null {
	for (const area of repo.areas) {
		const story = area.stories.find((s) => s.id === id);
		if (story) return { area, story };
	}
	return null;
}

/** The lowest-numbered `Happy path` test case of a story (id in code point order), or null. */
export function lowestHappyPath(area: Area, storyId: string): TestCase | null {
	const list = area.testCases
		.filter((t) => t.storyId === storyId && t.type === 'Happy path')
		.sort((a, b) => compareCodePoint(a.id, b.id));
	return list[0] ?? null;
}

/** Validation test cases of a story, in id order. */
export function validationCases(area: Area, storyId: string): TestCase[] {
	return area.testCases
		.filter((t) => t.storyId === storyId && t.type === 'Validation')
		.sort((a, b) => compareCodePoint(a.id, b.id));
}

/** Git blob id of a repository-relative file, or null when it does not exist. */
export function blobIdOf(root: string, relPath: string): string | null {
	const abs = join(root, relPath);
	return isFile(abs) ? gitBlobIdOfFile(abs) : null;
}

export function testCaseById(repo: Repo, id: string): { area: Area; testCase: TestCase } | null {
	for (const area of repo.areas) {
		const testCase = area.testCases.find((t) => t.id === id);
		if (testCase) return { area, testCase };
	}
	return null;
}
