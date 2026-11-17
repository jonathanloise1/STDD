/** The fixed H2 headings of a manual page and their keys (manual.md 2.4). */
import type { HelpLang } from '../index.ts';

export const SECTION_HEADINGS: Record<HelpLang, Record<string, string>> = {
	en: {
		before: 'Before you start',
		steps: 'Steps',
		result: 'What you will see',
		rules: 'Good to know',
		problems: 'If something goes wrong',
		related: 'Related pages',
	},
	it: {
		before: 'Prima di iniziare',
		steps: 'Passaggi',
		result: 'Cosa vedrai',
		rules: 'Da sapere',
		problems: 'Se qualcosa non va',
		related: 'Pagine correlate',
	},
};

/** The key of an H2 heading (`steps`), or null when the text is not one of the fixed headings. */
export function headingKey(text: string, lang: HelpLang): string | null {
	const found = Object.entries(SECTION_HEADINGS[lang]).find(([, heading]) => heading === text);
	return found ? found[0] : null;
}
