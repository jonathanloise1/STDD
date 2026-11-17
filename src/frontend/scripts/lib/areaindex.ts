import { sectionOf, splitSections } from './sections.ts';
import { parseTable } from './text.ts';

export interface AreaIndexStoryRow {
	id: string;
	title: string;
	priority: string;
}

export interface AreaIndex {
	/** The area title (the H1 without the ` - User Stories` suffix). */
	title: string;
	path: string;
	/** Overview paragraph, lines joined with a space. */
	overview: string;
	stories: AreaIndexStoryRow[];
}

/** Parses an area index (development.md 5). */
export function parseAreaIndex(text: string, path: string): AreaIndex {
	const f = splitSections(text);
	const t = parseTable(sectionOf(f, 'Stories')?.lines ?? []);
	return {
		title: (f.title ?? '').replace(/\s+-\s+User Stories\s*$/, ''),
		path,
		overview: (sectionOf(f, 'Overview')?.lines ?? []).join(' ').trim(),
		stories: (t?.rows ?? []).map((r) => ({
			id: /\[([^\]]+)\]/.exec(r[0] ?? '')?.[1] ?? r[0] ?? '',
			title: r[1] ?? '',
			priority: r[2] ?? '',
		})),
	};
}
