/**
 * The golden file of the analyzer and the ranking, in a language-neutral format that the TypeScript analyzer
 * (`src/help/lexical/`) and its Python port (`training/assistant/lexical.py`) are both tested against.
 *
 * Regenerate with `node --disable-warning=ExperimentalWarning scripts/help/golden.ts --write` after an intended change
 * of the analyzer, the stopword lists, the glossary expansion or the retrieval parameters (bump `ANALYZER_VERSION` for the
 * first two), and
 * run the Python test as well. Without `--write` the files are only compared.
 *
 * The same command writes `fixtures/embeddings.golden.json` (manual.md 6.7.9): section vectors, query vectors, embedding, lexical
 * and fused lists and decisions, computed by the TypeScript implementation from `fixtures/embeddings.table.golden.json`. That table
 * file is written by the Python builder (`python training/tests/embed_fixture.py --write`), so regenerate in this order: the table
 * first, then this file.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createAnalyzer, tokenize, ANALYZER_VERSION } from '../../src/help/lexical/analyzer.ts';
import { buildIndex } from '../../src/help/lexical/indexer.ts';
import { compileGlossary } from '../../src/help/lexical/glossary.ts';
import type { GlossaryRow } from '../../src/help/lexical/glossary.ts';
import { createEmbeddingScorer, readEmbeddingTable } from '../../src/help/lexical/embeddings.ts';
import { createSearcher } from '../../src/help/lexical/ranking.ts';
import type { HelpLang, RetrievalParams } from '../../src/help/lexical/types.ts';
import { loadTemplateStopwords, TEMPLATE_ROOT } from './paths.ts';
import { isEntry } from './cli.ts';

const HERE = dirname(fileURLToPath(import.meta.url));
export const GOLDEN_PATH = join(HERE, 'fixtures/lexical.golden.json');
export const TABLE_GOLDEN_PATH = join(HERE, 'fixtures/embeddings.table.golden.json');
export const EMBEDDINGS_GOLDEN_PATH = join(HERE, 'fixtures/embeddings.golden.json');

const TEXTS: Record<HelpLang, string[]> = {
	en: [
		'How do I add something new to the team\'s to-do list?',
		'Running, runs and ran: the tasks were created quickly',
		'Click "New Task" and type a title.',
		'Can I set a deadline when I make a new item?',
		'The L\'elenco of tasks is empty, 2 of them are done in 2024.',
		'UPPER case, MiXeD Case and trailing spaces   ',
		'A single x and I do not count; but ok does',
		'Café, naïve, résumé and façade keep their meaning',
		'e-mail addresses like name_surname@example.com split on every symbol',
		'Notifications, notified, notify: what happens when someone assigns me a task?',
		'generalization of organization, communities and operational procedures',
		'',
		'   ',
		'the and of to a',
		'Why can I not sign in myself, and why would it log me out?',
	],
	it: [
		'Come aggiungo un lavoro da fare per il mio team?',
		"Dov'è l'elenco dei compiti? Non lo trovo",
		'Perché non mi salva se non scrivo il titolo?',
		'Posso correggere un errore di battitura?',
		'Cliccare su "Nuovo compito" e scrivere un titolo.',
		'Più attività completate: 3 su 12 nel 2024',
		'Le città, le università e la libertà',
		'MAIUSCOLO, Misto e spazi finali   ',
		"un'altra volta, quell'uomo e l'altro",
		'Notifiche, notificare e notificato: cosa succede quando mi assegnano un compito?',
		'organizzazione, organizzazioni e organizzativo',
		'il lo la i gli le di a da in con su per',
		"Posso fare qualcosa? Devo aprire l'elenco, apro e apri: dov'è finito?",
		'Faccio, facevo, stavo, sarei e avrebbe sono parole vuote; ogni altro termine resta',
	],
};

interface Corpus {
	sections: { id: string; title: string; text: string; questions: string[] }[];
	glossary: GlossaryRow[];
	queries: string[];
}

const CORPORA: Record<HelpLang, Corpus> = {
	en: {
		sections: [
			{ id: 'tasks/US-TASK-01#overview', title: 'Create a task', text: 'Use this page to add a task with a title. The team can see what needs to be done.', questions: ["How do I add something new to the team's to-do list?"] },
			{ id: 'tasks/US-TASK-01#steps', title: 'Create a task - Steps', text: '1. Open the Tasks page.\n2. Click "New Task".\n3. In the "Title" field, type a value.\n4. Click "Create Task".', questions: ["How do I add something new to the team's to-do list?"] },
			{ id: 'tasks/US-TASK-01#problems', title: 'Create a task - If something goes wrong', text: '"Enter a title.". What to do: Type a title in the Title field.', questions: ['Why does the form refuse to save when the name is blank?'] },
			{ id: 'tasks/US-TASK-02#overview', title: 'Edit a task', text: 'Use this page to change the title or the status of a task.', questions: ['Can I fix a typo in a task?'] },
			{ id: 'tasks/US-TASK-02#steps', title: 'Edit a task - Steps', text: '1. Open the Tasks page.\n2. Click the pencil next to the task.\n3. Change the title or the status.\n4. Click "Save".', questions: ['Can I fix a typo in a task?'] },
			{ id: 'team/US-TEAM-01#overview', title: 'Invite a colleague', text: 'Send an invitation by e-mail so that a colleague can join your organization.', questions: [] },
			{ id: 'team/US-TEAM-01#rules', title: 'Invite a colleague - Good to know', text: '- An invitation expires after seven days.\n- Only an admin can invite people.', questions: [] },
			{ id: 'home#overview', title: 'MyApp', text: 'Use this manual to find out how to use MyApp.', questions: [] },
		],
		glossary: [
			{ term: 'task', aliases: ['job', 'to-do item'] },
			{ term: 'organization', aliases: ['company'] },
			{ term: 'due date', aliases: ['deadline'] },
			{ term: 'manual', aliases: ['guide', 'the and of'] },
		],
		queries: [
			'How do I rename a task?',
			'add a new task',
			'invite someone to my organization',
			'how long is an invitation valid',
			'pay my invoice',
			'what is the weather today',
			'task task task',
			'the of and',
			'',
			'Edit title status',
			'zzzz qqqq',
			'Create Task button',
			'rename a job',
			'invite someone to my company',
			'where is the guide',
			'job item task',
		],
	},
	it: {
		sections: [
			{ id: 'tasks/US-TASK-01#overview', title: 'Creare un compito', text: 'Usa questa pagina per aggiungere un compito con un titolo. Il team vede cosa c\'è da fare.', questions: ['Come aggiungo un lavoro da fare per il mio team?'] },
			{ id: 'tasks/US-TASK-01#steps', title: 'Creare un compito - Passaggi', text: '1. Apri la pagina Compiti.\n2. Clicca "Nuovo compito".\n3. Nel campo "Titolo" scrivi un valore.\n4. Clicca "Crea compito".', questions: ['Come aggiungo un lavoro da fare per il mio team?'] },
			{ id: 'tasks/US-TASK-02#overview', title: 'Modificare un compito', text: 'Usa questa pagina per cambiare il titolo o lo stato di un compito.', questions: ['Posso correggere un errore di battitura?'] },
			{ id: 'tasks/US-TASK-02#steps', title: 'Modificare un compito - Passaggi', text: '1. Apri la pagina Compiti.\n2. Clicca la matita accanto al compito.\n3. Cambia il titolo o lo stato.\n4. Clicca "Salva".', questions: [] },
			{ id: 'team/US-TEAM-01#overview', title: 'Invitare un collega', text: "Invia un invito per e-mail così che un collega possa entrare nell'organizzazione.", questions: [] },
			{ id: 'team/US-TEAM-01#rules', title: 'Invitare un collega - Da sapere', text: "- Un invito scade dopo sette giorni.\n- Solo un amministratore può invitare le persone.", questions: [] },
			{ id: 'home#overview', title: 'MyApp', text: 'Usa questo manuale per scoprire come usare MyApp.', questions: [] },
		],
		glossary: [
			{ term: 'compito', aliases: ['lavoro', 'attività da fare'] },
			{ term: 'organizzazione', aliases: ['azienda'] },
			{ term: 'manuale', aliases: ['guida'] },
		],
		queries: [
			'Come rinomino un compito?',
			'aggiungere un nuovo compito',
			"invitare qualcuno nell'organizzazione",
			'quanto dura un invito',
			'pagare la fattura',
			'che tempo fa oggi',
			'compito compito compito',
			'il lo la di',
			'',
			'Modifica titolo e stato',
			'zzzz qqqq',
			"Crea un'attività",
			'Come rinomino un lavoro?',
			"invitare qualcuno nell'azienda",
			'dove trovo la guida',
		],
	},
};

export interface Golden {
	schema_version: number;
	analyzer_version: number;
	stopwords_sha256: Record<string, string>;
	retrieval: RetrievalParams;
	analyze: { lang: string; text: string; tokens: string[]; stems: string[] }[];
	ranking: {
		lang: string;
		sections: Corpus['sections'];
		glossary: GlossaryRow[];
		queries: { query: string; stems: string[]; ranked: { id: string; score: number }[]; coverage: number; in_scope: boolean }[];
	}[];
}

export function buildGolden(): Golden {
	const retrieval = JSON.parse(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json'), 'utf8')) as RetrievalParams;
	const golden: Golden = {
		schema_version: 2,
		analyzer_version: ANALYZER_VERSION,
		stopwords_sha256: {},
		retrieval,
		analyze: [],
		ranking: [],
	};
	for (const lang of ['en', 'it'] as const) {
		const sw = loadTemplateStopwords(lang);
		golden.stopwords_sha256[lang] = sw.sha256;
		const analyzer = createAnalyzer(lang, sw.words);
		for (const text of TEXTS[lang]) golden.analyze.push({ lang, text, tokens: tokenize(text, sw.words), stems: analyzer.analyze(text) });
		const corpus = CORPORA[lang];
		const index = buildIndex(corpus.sections, analyzer, compileGlossary(corpus.glossary, analyzer));
		const searcher = createSearcher({ index, sectionIds: corpus.sections.map((s) => s.id), retrieval, analyzer });
		golden.ranking.push({
			lang,
			sections: corpus.sections,
			glossary: corpus.glossary,
			queries: corpus.queries.map((query) => {
				const hits = searcher.search(query);
				const d = searcher.decide(query, hits);
				return { query, stems: searcher.stems(query), ranked: hits.map((h) => ({ id: h.id, score: h.score })), coverage: d.coverage, in_scope: d.inScope };
			}),
		});
	}
	return golden;
}

export const serializeGolden = (g: Golden): string => `${JSON.stringify(g, null, 1)}\n`;

// ---------------------------------------------------------------- static embeddings (manual.md 6.7.9)

interface TableGolden {
	languages: Record<
		string,
		{
			inputs: {
				pack: { sections: { id: string; title: string; text: string; story: string | null }[]; questions: { story: string; text: string }[] };
				glossary: GlossaryRow[];
			};
			table_base64: string;
			table_sha256: string;
			decoded: { dims: number; count: number; vocab: string[]; weights: number[]; scales: number[]; vectors: number[][] };
		}
	>;
}

export interface EmbeddingCase {
	query: string;
	/** Overrides of the retrieval parameters for this case. */
	overrides: { candidates?: number; diversify?: 'none' | 'page'; fusion?: Record<string, number>; out_of_scope?: Record<string, number> };
	retrieval: RetrievalParams;
	stems: string[];
	stems_in_table: string[];
	query_vector: number[] | null;
	lexical_total: number;
	embedding_total: number;
	lexical_list: { id: string; score: number }[];
	embedding_list: { id: string; cosine: number }[];
	fused: { id: string; score: number }[];
	decision: { top_id: string | null; coverage: number; cosine: number | null; in_scope: boolean };
	tags: string[];
}

export interface EmbeddingsGolden {
	schema_version: number;
	retrieval: RetrievalParams;
	languages: Record<
		string,
		{
			table_sha256: string;
			sections: { id: string; title: string; text: string; questions: string[] }[];
			glossary: GlossaryRow[];
			section_vectors: (number[] | null)[];
			cases: EmbeddingCase[];
		}
	>;
}

type Overrides = EmbeddingCase['overrides'];
const EMB_QUERIES: Record<HelpLang, { query: string; overrides?: Overrides }[]> = {
	en: [
		{ query: 'How do I rename a task?' },
		{ query: 'add a new task' },
		{ query: 'invite someone to my organization' },
		{ query: 'how long is an invitation valid' },
		{ query: 'what is the weather today' },
		{ query: 'pay my invoice' },
		{ query: 'zzzz qqqq' },
		{ query: 'rename a job' },
		{ query: 'weather forecast' },
		{ query: 'invitation expires', overrides: { out_of_scope: { min_cosine: 0.99, min_cosine_lexical: 0.99 } } },
		{ query: 'invitation expires', overrides: { out_of_scope: { min_cosine: 0.9, min_cosine_lexical: -1 } } },
		{ query: 'add a new task', overrides: { out_of_scope: { min_cosine: 0.97, min_cosine_lexical: 0.5 } } },
		{ query: 'weather forecast', overrides: { out_of_scope: { min_cosine: 0.2 } } },
		{ query: 'zyzzyva task', overrides: { fusion: { embedding_weight: 0.01 } } },
		{ query: 'create a task title', overrides: { fusion: { depth: 1 } } },
		{ query: 'team colleague organization', overrides: { fusion: { depth: 2 } } },
		{ query: 'create a task title', overrides: { diversify: 'none' } },
		{ query: 'create a task title' },
		{ query: 'the of and' },
		{ query: 'colleague invitation task', overrides: { fusion: { rrf_k: 10, embedding_weight: 2 } } },
	],
	it: [
		{ query: 'Come rinomino un compito?' },
		{ query: 'aggiungere un nuovo compito' },
		{ query: "invitare qualcuno nell'organizzazione" },
		{ query: 'quanto dura un invito' },
		{ query: 'pagare la fattura' },
		{ query: 'che tempo fa oggi' },
		{ query: 'zzzz qqqq' },
		{ query: 'Come rinomino un lavoro?' },
		{ query: 'tempo pioggia' },
		{ query: 'invito scade', overrides: { out_of_scope: { min_cosine: 0.99, min_cosine_lexical: 0.99 } } },
		{ query: 'crea un compito', overrides: { fusion: { depth: 1 } } },
	],
};

/** The case with its retrieval parameters laid out and every list computed by the TypeScript implementation. */
export function buildEmbeddingsGolden(): EmbeddingsGolden {
	const base = JSON.parse(readFileSync(join(TEMPLATE_ROOT, 'training/assistant/retrieval.json'), 'utf8')) as RetrievalParams;
	const tableGolden = JSON.parse(readFileSync(TABLE_GOLDEN_PATH, 'utf8')) as TableGolden;
	const out: EmbeddingsGolden = { schema_version: 1, retrieval: base, languages: {} };
	for (const lang of ['en', 'it'] as const) {
		const tg = tableGolden.languages[lang];
		const table = readEmbeddingTable(new Uint8Array(Buffer.from(tg.table_base64, 'base64')));
		const analyzer = createAnalyzer(lang, loadTemplateStopwords(lang).words);
		const sections = tg.inputs.pack.sections.map((x) => ({
			id: x.id,
			title: x.title,
			text: x.text,
			questions: x.story ? tg.inputs.pack.questions.filter((q) => q.story === x.story).map((q) => q.text) : [],
		}));
		const ids = sections.map((x) => x.id);
		const index = buildIndex(sections, analyzer, compileGlossary(tg.inputs.glossary, analyzer));
		const scorer = createEmbeddingScorer(table, index, ids, base.bm25.field_weights);
		const cases: EmbeddingCase[] = EMB_QUERIES[lang].map(({ query, overrides = {} }) => {
			const retrieval: RetrievalParams = {
				...base,
				candidates: overrides.candidates ?? base.candidates,
				diversify: overrides.diversify ?? base.diversify,
				out_of_scope: { ...base.out_of_scope, ...overrides.out_of_scope },
				fusion: { ...base.fusion, ...overrides.fusion } as RetrievalParams['fusion'],
			};
			const make = (r: RetrievalParams, emb: typeof scorer | null) => createSearcher({ index, sectionIds: ids, retrieval: r, analyzer, embeddings: emb });
			const searcher = make(retrieval, scorer);
			const stems = searcher.stems(query);
			const depth = retrieval.fusion.depth ?? 30;
			const lexicalOf = (n: number) => make({ ...retrieval, diversify: 'none', candidates: n, fusion: { ...retrieval.fusion, method: 'none' } }, null).search(query);
			const hits = searcher.search(query);
			const d = searcher.decide(query, hits);
			const vector = scorer.queryVector(stems);
			const c: EmbeddingCase = {
				query,
				overrides,
				retrieval,
				stems,
				stems_in_table: stems.filter((t) => table.index.has(t)),
				query_vector: vector ? [...vector] : null,
				lexical_total: lexicalOf(1000).length,
				embedding_total: scorer.rank(stems, 1000).length,
				lexical_list: lexicalOf(depth).map((h) => ({ id: h.id, score: h.score })),
				embedding_list: scorer.rank(stems, depth).map((h) => ({ id: ids[h.index], cosine: h.cosine })),
				fused: hits.map((h) => ({ id: h.id, score: h.score })),
				decision: { top_id: d.topId, coverage: d.coverage, cosine: d.cosine, in_scope: d.inScope },
				tags: [],
			};
			c.tags = tagsOf(c, scorer, ids, make, hits.map((h) => h.id));
			return c;
		});
		out.languages[lang] = {
			table_sha256: tg.table_sha256,
			sections,
			glossary: tg.inputs.glossary,
			section_vectors: ids.map((_, i) => {
				const v = scorer.sectionVector(i);
				return v ? [...v] : null;
			}),
			cases,
		};
	}
	return out;
}

/** What a case exercises, read from its results (so the file proves its own coverage, manual.md 6.7.9). */
function tagsOf(
	c: EmbeddingCase,
	scorer: ReturnType<typeof createEmbeddingScorer>,
	ids: string[],
	make: (r: RetrievalParams, emb: typeof scorer | null) => ReturnType<typeof createSearcher>,
	fusedIds: string[],
): string[] {
	const tags: string[] = [];
	const oos = c.retrieval.out_of_scope;
	const d = c.decision;
	const topIndex = d.top_id === null ? -1 : ids.indexOf(d.top_id);
	if (c.stems.length && c.query_vector === null) tags.push('no_table_stem');
	if (c.lexical_total === 0 && c.embedding_total > 0) tags.push('no_lexical_hit');
	if (c.fused.some((h, i) => i > 0 && h.score === c.fused[i - 1].score)) tags.push('fused_tie');
	const depth = c.retrieval.fusion.depth ?? 30;
	if (depth < c.lexical_total && depth < c.embedding_total) tags.push('depth_cut');
	if (c.retrieval.diversify === 'page') {
		const undiversified = make({ ...c.retrieval, diversify: 'none' }, scorer).search(c.query).map((h) => h.id);
		if (JSON.stringify(undiversified) !== JSON.stringify(fusedIds)) tags.push('diversify_after_fusion');
	}
	if (d.in_scope && d.cosine !== null && d.cosine >= oos.min_cosine && d.coverage < oos.min_coverage) tags.push('semantic_accept');
	const lexicalAccept = d.in_scope && d.coverage >= oos.min_coverage && (d.cosine === null || d.cosine < oos.min_cosine);
	if (lexicalAccept) tags.push('lexical_accept');
	if (!d.in_scope && d.coverage >= oos.min_coverage && d.cosine !== null && d.cosine < oos.min_cosine && d.cosine < (oos.min_cosine_lexical ?? -1)) tags.push('veto');
	if (lexicalAccept && (oos.min_cosine_lexical ?? -1) === -1 && d.cosine !== null) tags.push('veto_off');
	if (topIndex >= 0 && scorer.sectionVector(topIndex) === null && c.query_vector !== null) tags.push('section_without_vector');
	return tags;
}

export const serializeEmbeddingsGolden = (g: EmbeddingsGolden): string => `${JSON.stringify(g, null, 1)}\n`;

if (isEntry(import.meta.url)) {
	const files: [string, string][] = [
		[GOLDEN_PATH, serializeGolden(buildGolden())],
		[EMBEDDINGS_GOLDEN_PATH, serializeEmbeddingsGolden(buildEmbeddingsGolden())],
	];
	for (const [path, text] of files) {
		if (process.argv.includes('--write')) {
			writeFileSync(path, text);
			console.log(`wrote ${path}`);
		} else console.log(readFileSync(path, 'utf8') === text ? `${path}: up to date` : `${path}: differs, run with --write`);
	}
}
