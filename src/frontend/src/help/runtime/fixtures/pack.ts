/** A small hand-built pack and bundle for the unit tests of the help runtime. Not shipped: only tests import it. */
import { buildIndex, createAnalyzer, parseStopwords } from '../../index.ts';
import type { HelpPack, PackSection } from '../../index.ts';
import stopwordsEn from '../../lexical/stopwords.en.txt?raw';
import type { ManualBundle } from '../types.ts';

const analyzer = createAnalyzer('en', parseStopwords(stopwordsEn));

const section = (
	page: string,
	key: string,
	title: string,
	text: string,
	routes: string[] = [],
): PackSection => ({
	id: `${page}#${key}`,
	page_id: page,
	key,
	area: page === 'home' ? null : page.split('/')[0],
	story: page.includes('/') ? page.split('/')[1] : null,
	title,
	text,
	url: page === 'home' ? `/manual#${key}` : `/manual/${page}#${key}`,
	routes,
});

export const SECTIONS: PackSection[] = [
	section('home', 'overview', 'MyApp manual', 'Learn how to use the application.'),
	section('tasks', 'overview', 'Tasks', 'Everything about tasks.'),
	section('tasks/US-TASK-01', 'overview', 'Create a task', 'Add a task with a title.', [
		'/tasks',
	]),
	section(
		'tasks/US-TASK-01',
		'steps',
		'Create a task - Steps',
		'1. Open the Tasks page.\n2. Click "New Task".\n3. Type the title and click "Create Task".',
		['/tasks'],
	),
	section('tasks/US-TASK-02', 'overview', 'Delete a task', 'Remove a task you no longer need.', [
		'/tasks',
	]),
	section(
		'tasks/US-TASK-02',
		'steps',
		'Delete a task - Steps',
		'1. Open the Tasks page.\n2. Click Delete on the task and confirm.',
		['/tasks'],
	),
];

const QUESTIONS = [{ story: 'US-TASK-01', text: 'Can I set a deadline when I make a new item?' }];

export function testPack(overrides: Partial<HelpPack['assistant']> = {}): HelpPack {
	return {
		schema_version: 1,
		kind: 'help-pack',
		lang: 'en',
		project: { name: 'MyApp', version: '0.1.0' },
		contracts: {},
		build: { commit: null, inputs_sha256: '0' },
		assistant: {
			protocol: {
				sha256: 'p',
				content: {
					context: {
						max_sections: 4,
						max_section_chars: 1200,
						max_context_chars: 4000,
						max_question_chars: 300,
						scan_limit: 8,
					},
					languages: { en: { refusal: "I can't find this in the manual." } },
					answer: { max_body_chars: 600, max_body_lines: 7 },
				},
			},
			retrieval: {
				sha256: 'r',
				content: {
					schema_version: 1,
					bm25: {
						k1: 1.2,
						b: 0.75,
						field_weights: { title: 2, text: 1, questions: 1.5 },
					},
					candidates: 10,
					out_of_scope: { min_coverage: 0.4, min_cosine: 0.55, min_query_terms: 1 },
					fusion: { method: 'none', rrf_k: 60, embedding_weight: 1 },
				},
			},
			model: null,
			compatible: true,
			...overrides,
		},
		analyzer: { id: 'snowball-en', version: 1, stopwords_sha256: '0' },
		sections: SECTIONS,
		questions: QUESTIONS,
		index: buildIndex(
			SECTIONS.map((s) => ({
				title: s.title,
				text: s.text,
				questions: s.story === 'US-TASK-01' ? QUESTIONS.map((q) => q.text) : [],
			})),
			analyzer,
		),
		embeddings: null,
	};
}

const page = (
	page_id: string,
	kind: 'home' | 'area' | 'story',
	title: string,
	routes: string[] = [],
	contexts: string[] = [],
) => ({
	page_id,
	kind,
	area: kind === 'home' ? null : page_id.split('/')[0],
	title,
	source_id: null,
	routes,
	contexts,
	blocks: [],
});

export const BUNDLE: ManualBundle = {
	schema_version: 1,
	lang: 'en',
	project_version: '0.1.0',
	pages: [
		page('home', 'home', 'MyApp manual'),
		page('tasks', 'area', 'Tasks'),
		page('tasks/US-TASK-01', 'story', 'Create a task', ['/tasks'], ['task-form']),
		page('tasks/US-TASK-02', 'story', 'Delete a task', ['/tasks']),
	],
	routes: { '/tasks': ['tasks/US-TASK-01', 'tasks/US-TASK-02'] },
	contexts: { 'task-form': 'tasks/US-TASK-01' },
};
