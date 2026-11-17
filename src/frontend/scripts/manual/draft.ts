/** Draft page content for `manual:generate` (manual.md 3.1). Deterministic: no clocks, no randomness. */
import { blobIdOf, lowestHappyPath, validationCases } from '../lib/repo.ts';
import type { Area, Repo } from '../lib/repo.ts';
import { contractVersion } from '../lib/contracts.ts';
import { italianUiText, localeValues } from '../lib/locales.ts';
import type { Locales } from '../lib/locales.ts';
import { DRAFT_MARKER } from '../lib/markdown.ts';
import { formatFrontMatter, HEADINGS, PROBLEM_COLUMNS } from '../lib/page.ts';
import type { Lang, SectionKey } from '../lib/page.ts';
import type { Story } from '../lib/story.ts';
import { joinList, quotedStrings, stripTrailingPeriod, upperFirst } from '../lib/text.ts';

const ELEMENTS = 'page|form|dialog|button|link|field|list|table|menu|tab|message';
const RE_ACTOR = /^As [A-Z][A-Za-z -]*: /;
const RE_OPEN = /^Open the (.+) page\.$/;
const PLACES = 'in the menu|in the dialog|in the header';
const RE_CLICK = new RegExp(`^Click the "([^"]+)" (?:${ELEMENTS})( (?:${PLACES}))?\\.$`);
const RE_CLICK_ROW = /^Click the "([^"]+)" button on the (first row of the table|row of "([^"]*)")\.$/;
const RE_CLEAR = /^Clear the "([^"]+)" field\.$/;
const PLACE_IT: Record<string, string> = {
	' in the menu': ' nel menu',
	' in the dialog': ' nella finestra di dialogo',
	' in the header': ' nell\'intestazione',
};
const RE_TYPE = /^Type "([^"]*)" in the "([^"]+)" field\.$/;
const RE_SELECT = /^Select "([^"]*)" in the "([^"]+)" field\.$/;

/** Italian UI text when the lookup is unique, else the English text. */
function ui(loc: Locales, lang: Lang, text: string): string {
	return lang === 'it' ? (italianUiText(loc, text) ?? text) : text;
}

/** The step table of manual.md 3.1. Returns null for a step that is dropped (a second actor's step). */
export function transformStep(step: string, lang: Lang, loc: Locales): string | null {
	if (RE_ACTOR.test(step)) return null;
	let m = RE_OPEN.exec(step);
	if (m) {
		if (lang === 'en') return step;
		const quoted = /^"(.*)"$/.exec(m[1]);
		const name = quoted ? `"${ui(loc, lang, quoted[1])}"` : ui(loc, lang, m[1]);
		return `Apri la pagina ${name}.`;
	}
	m = RE_CLICK.exec(step);
	if (m) return lang === 'en' ? step : `Fai clic su "${ui(loc, lang, m[1])}"${PLACE_IT[m[2] ?? ''] ?? ''}.`;
	m = RE_CLICK_ROW.exec(step);
	if (m) {
		if (lang === 'en') return step;
		const row = m[3] === undefined ? 'sulla prima riga della tabella' : `sulla riga di "${m[3]}"`;
		return `Fai clic su "${ui(loc, lang, m[1])}" ${row}.`;
	}
	m = RE_CLEAR.exec(step);
	if (m) return lang === 'en' ? step : `Svuota il campo "${ui(loc, lang, m[1])}".`;
	m = RE_TYPE.exec(step);
	if (m) {
		return lang === 'en'
			? `In the "${m[2]}" field, type a value such as "${m[1]}".`
			: `Nel campo "${ui(loc, lang, m[2])}" scrivi un valore come "${m[1]}".`;
	}
	m = RE_SELECT.exec(step);
	if (m) {
		return lang === 'en'
			? `In the "${m[2]}" field, choose "${m[1]}".`
			: `Nel campo "${ui(loc, lang, m[2])}" scegli "${ui(loc, lang, m[1])}".`;
	}
	return step;
}

/** Non-Goals bullet as a sentence: "This feature does not include {bullet, first letter lower case}." */
export function nonGoalSentence(text: string): string {
	const t = stripTrailingPeriod(text.trim());
	const lower = /^[A-Z][a-z]/.test(t) ? t[0].toLowerCase() + t.slice(1) : t;
	return `This feature does not include ${lower}.`;
}

/** Story text is written in the first person ("I can ..."); the page speaks to the reader. */
export function secondPerson(text: string): string {
	return text
		.replace(/\bI am\b/g, 'you are')
		.replace(/\bI'm\b/g, 'you are')
		.replace(/\bI've\b/g, "you've")
		.replace(/\bI\b/g, 'you')
		.replace(/\bmyself\b/g, 'yourself')
		.replace(/\bmy\b/g, 'your')
		.replace(/\bmine\b/g, 'yours')
		.replace(/\bme\b/g, 'you');
}

/** A Validation message the user sees, with what to do about it (manual.md 3.1). */
const RE_FIELD_MESSAGE = /\bThe (?:"([^"]+)"|([A-Z][\w /-]*?)) field shows "([^"]+)"/i;
const RE_PLAIN_MESSAGE = /^The message "([^"]+)" appears\.?$/;

export function validationRows(expected: string[], lang: Lang, loc: Locales): { msg: string; todo: string }[] {
	const rows: { msg: string; todo: string }[] = [];
	for (const e of expected) {
		const f = RE_FIELD_MESSAGE.exec(e);
		if (f) {
			const field = f[1] ?? f[2];
			rows.push({
				msg: f[3],
				todo:
					lang === 'it'
						? `Correggi il campo "${ui(loc, lang, field)}" e riprova.`
						: `Correct the "${field}" field and try again.`,
			});
			continue;
		}
		const p = RE_PLAIN_MESSAGE.exec(e.trim());
		if (p) rows.push({ msg: p[1], todo: lang === 'it' ? 'Correggi i dati inseriti e riprova.' : 'Correct what you entered and try again.' });
	}
	return rows;
}

const cell = (s: string): string => s.replace(/\|/g, '\\|');

function section(lang: Lang, key: SectionKey, body: string[]): string[] {
	return [`## ${HEADINGS[lang][key]}`, '', ...body, ''];
}

export function frontMatterFor(
	repo: Repo,
	kind: 'story' | 'area' | 'home',
	lang: Lang,
	ids: { area?: string; story?: string },
): string {
	const version = String(contractVersion(repo.root, 'manual-page') ?? 1);
	const base = { schema_version: version, kind, lang, routes: '[]', contexts: '[]', screenshots: '[]' };
	if (kind === 'home') {
		return formatFrontMatter({
			...base,
			page_id: 'home',
			source: 'none',
			source_hash: 'none',
			steps_from: 'none',
			steps_hash: 'none',
		});
	}
	const area = ids.area as string;
	if (kind === 'area') {
		const source = `docs/areas/${area}/userstories.md`;
		return formatFrontMatter({
			...base,
			page_id: area,
			source,
			source_hash: blobIdOf(repo.root, source) ?? 'none',
			steps_from: 'none',
			steps_hash: 'none',
		});
	}
	const found = repo.areas.find((a) => a.folder === area) as Area;
	const story = ids.story as string;
	const source = `docs/areas/${area}/user-stories/${story}.md`;
	const happy = lowestHappyPath(found, story);
	return formatFrontMatter({
		...base,
		page_id: `${area}/${story}`,
		source,
		source_hash: blobIdOf(repo.root, source) ?? 'none',
		steps_from: happy ? happy.id : 'none',
		steps_hash: happy ? (blobIdOf(repo.root, happy.path) ?? 'none') : 'none',
	});
}

function pageText(front: string, title: string, intro: string, sections: string[][]): string {
	const lines = [DRAFT_MARKER, '', `# ${title}`, '', intro, ''];
	for (const s of sections) lines.push(...s);
	return `${front}${lines.join('\n').replace(/\n+$/, '')}\n`;
}

export function draftHomePage(repo: Repo, lang: Lang): string {
	const name = repo.productName;
	return pageText(
		frontMatterFor(repo, 'home', lang, {}),
		name,
		`Use this manual to find out how to use ${name}.`,
		[],
	);
}

export function draftAreaPage(repo: Repo, area: Area, lang: Lang): string {
	const intro = area.index?.overview || `Find here the pages about ${area.title}.`;
	return pageText(frontMatterFor(repo, 'area', lang, { area: area.folder }), area.title, intro, []);
}

export function draftStoryPage(repo: Repo, area: Area, story: Story, lang: Lang, loc: Locales): string {
	const front = frontMatterFor(repo, 'story', lang, { area: area.folder, story: story.id });
	const want = stripTrailingPeriod(story.iWant).replace(/^to /, '');
	const intro = secondPerson(`Use this page to ${want}. ${upperFirst(stripTrailingPeriod(story.soThat))}.`).replace(/(^|\. )you\b/g, '$1You');
	const happy = lowestHappyPath(area, story.id);
	const sections: string[][] = [];

	// before: Access Control (only when a role has No) and the preconditions other than the sign-in line
	const before: string[] = [];
	const uiValues = localeValues(loc, 'en');
	if (story.access.some((a) => a.allowed === 'No')) {
		const yes = story.access.filter((a) => a.allowed === 'Yes').map((a) => a.role);
		if (yes.length) before.push(`- Only ${joinList(yes)} can do this.`);
	}
	for (const p of happy?.preconditions ?? []) {
		if (/^Signed in as /.test(p) || p === 'Not signed in.' || /^A second session is signed in as /.test(p)) continue;
		if (quotedStrings(p).some((q) => !uiValues.has(q))) continue; // test data, not something the reader sets up
		before.push(`- ${p}`);
	}
	if (before.length) sections.push(section(lang, 'before', before));

	if (happy) {
		const steps = happy.steps.map((s) => transformStep(s, lang, loc)).filter((s): s is string => s !== null);
		if (steps.length) sections.push(section(lang, 'steps', steps.map((s, i) => `${i + 1}. ${s}`)));
		if (happy.expected.length) sections.push(section(lang, 'result', happy.expected.map((e) => `- ${e}`)));
	}

	const rules = [
		...story.rules.map((r) => `- ${r}`),
		...story.nonGoals.map((n) => `- ${nonGoalSentence(n)}`),
	];
	if (rules.length) sections.push(section(lang, 'rules', rules));

	// problems: Error States rows, then the field messages of the Validation test cases (deduplicated)
	const rows: { msg: string; todo: string }[] = story.errorStates.map((e) => ({ msg: e.message, todo: e.action }));
	const seen = new Set(rows.map((r) => r.msg));
	for (const tc of validationCases(area, story.id)) {
		for (const r of validationRows(tc.expected, lang, loc)) {
			if (seen.has(r.msg)) continue;
			seen.add(r.msg);
			rows.push(r);
		}
	}
	if (rows.length) {
		const [c1, c2] = PROBLEM_COLUMNS[lang];
		sections.push(
			section(lang, 'problems', [
				`| ${c1} | ${c2} |`,
				'|---|---|',
				...rows.map((r) => `| "${cell(ui(loc, lang, r.msg))}" | ${cell(r.todo)} |`.replace(/\|  \|$/, '| |')),
			]),
		);
	}

	const others = area.stories.filter((s) => s.id !== story.id);
	if (others.length) sections.push(section(lang, 'related', others.map((s) => `- [${s.title}](${s.id}.md)`)));

	return pageText(front, lang === 'it' ? `${story.title} (English, to translate)` : story.title, intro, sections);
}
