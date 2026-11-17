/**
 * The project glossary of manual.md 6.9: words users say for the words the manual uses. Applied when the index is
 * built, never at query time, so the pack format and the runtime stay unchanged.
 */
import type { Analyzer } from './analyzer.ts';

export interface GlossaryRow {
	/** A word or phrase the manual uses. */
	term: string;
	/** Words or phrases users say for it. */
	aliases: string[];
}

export interface GlossaryEntry {
	/** Stems of the term, in order; matched as a contiguous run of a field's stems. */
	term: string[];
	/** Distinct stems of the aliases that are not stems of the term, in order of first appearance. */
	add: string[];
}

/** Analyzes the rows; a row whose term or aliases give no stem is dropped (the build reports it). */
export function compileGlossary(rows: readonly GlossaryRow[], analyzer: Analyzer): GlossaryEntry[] {
	const out: GlossaryEntry[] = [];
	for (const r of rows) {
		const term = analyzer.analyze(r.term);
		const add: string[] = [];
		for (const a of r.aliases)
			for (const s of analyzer.analyze(a))
				if (!term.includes(s) && !add.includes(s)) add.push(s);
		if (term.length && add.length) out.push({ term, add });
	}
	return out;
}

function containsRun(stems: readonly string[], run: readonly string[]): boolean {
	for (let i = 0; i + run.length <= stems.length; i++) {
		let ok = true;
		for (let j = 0; j < run.length && ok; j++) ok = stems[i + j] === run[j];
		if (ok) return true;
	}
	return false;
}

/**
 * The stems a field gains: for every entry whose term occurs in the field, the entry's alias stems. Each stem is
 * added once per field however many entries bring it, in order of first appearance.
 */
export function glossaryStems(
	stems: readonly string[],
	glossary: readonly GlossaryEntry[],
): string[] {
	const out: string[] = [];
	for (const e of glossary) {
		if (!containsRun(stems, e.term)) continue;
		for (const s of e.add) if (!out.includes(s)) out.push(s);
	}
	return out;
}
