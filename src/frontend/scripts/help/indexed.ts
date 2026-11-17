/** The leave-one-out index of manual.md 6.9: the pack's index without one indexed question. */
import type { Analyzer } from '../../src/help/lexical/analyzer.ts';
import type { GlossaryEntry } from '../../src/help/lexical/glossary.ts';
import { buildIndex } from '../../src/help/lexical/indexer.ts';
import type { HelpPack, PackIndex, PackQuestion } from '../../src/help/lexical/types.ts';

/** Every section's index input, with the indexed questions of its story except `left` (once). */
export function indexWithout(pack: Pick<HelpPack, 'sections' | 'questions'>, left: PackQuestion, analyzer: Analyzer, glossary: readonly GlossaryEntry[]): PackIndex {
	const others = (story: string | null) =>
		story ? pack.questions.filter((x) => x.story === story && !(story === left.story && x.text === left.text)).map((x) => x.text) : [];
	return buildIndex(
		pack.sections.map((s) => ({ title: s.title, text: s.text, questions: others(s.story) })),
		analyzer,
		glossary,
	);
}
