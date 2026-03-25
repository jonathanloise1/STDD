/**
 * Interface describing a single language entry used by the language
 * selector dropdowns across all header components.
 *
 * @userstory US-I18N-02 — Provides the display data for the language-switch UI
 * @userstory US-I18N-10 — Adding a new language only requires a new entry here
 */
export interface ILang {
	[key: string]: {
		text: string;
		lng: 'it' | 'en' | 'de' | 'fr';
		icon: string;
	};
}

/**
 * Supported languages in display order with human-readable name and
 * country-flag icon:
 * Italiano, English, Deutsch, Français.
 *
 * Used by the language-selector dropdowns in all header variants
 * (CommonHeaderRight, DashboardHeader, SimpleHeader).
 *
 * @userstory US-I18N-01 — Each language has display metadata for the UI
 * @userstory US-I18N-02 — Dropdown options rendered from this map
 * @userstory US-I18N-10 — New languages are added as entries here + in SUPPORTED_LANGUAGES (i18n.ts)
 */
const LANG: ILang = {
	IT: {
		text: 'Italiano',
		lng: 'it',
		icon: 'CustomItaly',
	},
	EN: {
		text: 'English',
		lng: 'en',
		icon: 'CustomUsa',
	},
	DE: {
		text: 'Deutsch',
		lng: 'de',
		icon: 'CustomGermany',
	},
	FR: {
		text: 'Français',
		lng: 'fr',
		icon: 'CustomFrance',
	},
};

/**
 * Returns the `LANG` entry matching the given language code.
 * Used by header components to display the correct flag icon and
 * language name after a language switch.
 *
 * @userstory US-I18N-02 — Resolves display metadata for the selected language
 */
export const getLangWithKey = (key: ILang['key']['lng']): ILang['key'] => {
	// @ts-ignore
	return LANG[Object.keys(LANG).filter((f) => key.includes(LANG[f].lng))];
};

export default LANG;
