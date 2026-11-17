/** The languages of the application, in display order, each named in its own language (frontend.md 14). */
export const LANGUAGES = [
	{ code: 'en', name: 'English' },
	{ code: 'it', name: 'Italiano' },
] as const;

export type LanguageCode = (typeof LANGUAGES)[number]['code'];
