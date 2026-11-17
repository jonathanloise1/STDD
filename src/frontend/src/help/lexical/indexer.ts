/** Builds the `index` block of a pack (manual.md 6.3) from sections and their indexed questions. */
import type { Analyzer } from './analyzer.ts';
import { glossaryStems } from './glossary.ts';
import type { GlossaryEntry } from './glossary.ts';
import { INDEX_FIELDS } from './types.ts';
import type { PackIndex, Posting } from './types.ts';

export interface IndexInput {
	title: string;
	text: string;
	/** Indexed questions of the section's story; joined with a line break. */
	questions: string[];
}

/** Fields that gain the stems of the glossary (manual.md 6.9): title and text, not the users' own questions. */
const GLOSSARY_FIELDS = new Set([0, 1]);

/**
 * `glossary` adds alias stems to the title and text fields where their term occurs (tf + 1 each); field lengths
 * stay the token counts of the analysed text.
 */
export function buildIndex(
	sections: IndexInput[],
	analyzer: Analyzer,
	glossary: readonly GlossaryEntry[] = [],
): PackIndex {
	const nFields = INDEX_FIELDS.length;
	const perTerm = new Map<string, Array<[number, number, number]>>();
	const lengths: number[][] = [];
	sections.forEach((s, doc) => {
		const texts = [s.title, s.text, s.questions.join('\n')];
		const row: number[] = [];
		texts.forEach((text, field) => {
			const stems = analyzer.analyze(text);
			row.push(stems.length);
			const counts = new Map<string, number>();
			const gained = GLOSSARY_FIELDS.has(field) ? glossaryStems(stems, glossary) : [];
			for (const t of [...stems, ...gained]) counts.set(t, (counts.get(t) ?? 0) + 1);
			for (const [t, tf] of counts) {
				let list = perTerm.get(t);
				if (!list) perTerm.set(t, (list = []));
				list.push([doc, field, tf]);
			}
		});
		lengths.push(row);
	});
	const avg: number[] = [];
	for (let f = 0; f < nFields; f++)
		avg.push(sections.length ? lengths.reduce((s, r) => s + r[f], 0) / sections.length : 0);
	const terms: Record<string, Posting> = {};
	for (const t of [...perTerm.keys()].sort((a, b) => (a < b ? -1 : a > b ? 1 : 0))) {
		const list = perTerm.get(t)!;
		const docs = new Set(list.map((p) => p[0]));
		terms[t] = [docs.size, ...list];
	}
	return {
		fields: [...INDEX_FIELDS],
		doc_count: sections.length,
		avg_length: avg,
		lengths,
		terms,
	};
}
