import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import Backend from 'i18next-http-backend';
import LanguageDetector from 'i18next-browser-languagedetector';

/**
 * i18n initialization module — configures i18next for the entire application.
 *
 * Responsibilities:
 * - Defines the list of translation **namespaces** (feature-based JSON files)
 *   loaded lazily from `public/locales/{lang}/{ns}.json`.
 * - Declares the set of **supported languages** (it, en).
 * - Configures **language detection** via `i18next-browser-languagedetector`
 *   which reads/writes the user's preference to `localStorage`.
 * - Sets English (`en`) as the **fallback language** so that any missing
 *   translation key gracefully degrades to the English value.
 * - Enables **missing-key logging** in development mode to surface
 *   untranslated strings early during development.
 *
 * @module i18n
 */

/**
 * Supported namespaces for translations.
 * Each namespace corresponds to a JSON file in `public/locales/{lang}/`.
 */
export const NAMESPACES = [
	'common',
	'menu',
	'dashboard',
	'organization',
	'tasks',
	'team',
	'settings',
	'audit',
	'help',
] as const;

export type Namespace = (typeof NAMESPACES)[number];

/**
 * Supported languages in the platform.
 * Adding a new language entry here (together with the corresponding
 * locale JSON files and an entry in `shell/languages.ts`) is all that is
 * needed to enable a new language — no other code changes required.
 */
export const SUPPORTED_LANGUAGES = ['en', 'it'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

i18n
	// load translation using http -> see /public/locales
	.use(Backend)
	// detect user language
	.use(LanguageDetector)
	// pass the i18n instance to react-i18next
	.use(initReactI18next)
	// init i18next
	// for all options read: https://www.i18next.com/overview/configuration-options
	.init({
		// Language configuration
		fallbackLng: 'en',
		supportedLngs: SUPPORTED_LANGUAGES as unknown as string[],

		// Debug mode in development
		debug: import.meta.env.MODE === 'development',

		// Only the choice of the user counts: the language of the browser is ignored, English is the default
		detection: {
			order: ['localStorage'],
			caches: ['localStorage'],
		},

		// Namespace configuration
		ns: NAMESPACES as unknown as string[],
		defaultNS: 'common',
		fallbackNS: 'common', // Fallback to common namespace

		// Backend configuration for loading translation files
		backend: {
			loadPath: '/locales/{{lng}}/{{ns}}.json',
		},

		interpolation: {
			escapeValue: false, // not needed for react as it escapes by default
		},

		react: {
			useSuspense: true,
			bindI18n: 'languageChanged loaded',
		},

		// Missing key handling (development only)
		saveMissing: import.meta.env.MODE === 'development',
		missingKeyHandler: (lngs, ns, key) => {
			if (import.meta.env.MODE === 'development') {
				console.warn(
					`[i18n] Missing translation: ${ns}:${key} for languages: ${lngs.join(', ')}`,
				);
			}
		},
	});

// The page language follows the application language: screen readers and native fields (date inputs)
// use it, and it must match the text on screen.
const setPageLanguage = (lng: string) => {
	if (typeof document !== 'undefined') document.documentElement.lang = lng.substring(0, 2);
};
i18n.on('languageChanged', setPageLanguage);
if (i18n.language) setPageLanguage(i18n.language);

export default i18n;
