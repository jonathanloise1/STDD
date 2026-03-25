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
 * - Sets Italian (`it`) as the **fallback language** so that any missing
 *   translation key gracefully degrades to the Italian value.
 * - Enables **missing-key logging** in development mode to surface
 *   untranslated strings early during development.
 *
 * @userstory US-I18N-01  — Platform displayed in user's preferred language
 * @userstory US-I18N-03  — Language preference persisted (LanguageDetector → localStorage)
 * @userstory US-I18N-06  — Fallback to Italian for missing translations
 * @userstory US-I18N-07  — Namespace-based file organisation (small, focused files)
 * @userstory US-I18N-08  — Missing-key detection in development mode
 * @userstory US-I18N-10  — Adding a new language only requires updating SUPPORTED_LANGUAGES
 *
 * @module i18n
 */

/**
 * Supported namespaces for translations.
 * Each namespace corresponds to a JSON file in `public/locales/{lang}/`.
 *
 * @userstory US-I18N-07 — Feature-based file organisation
 * @userstory US-I18N-11 — Small focused files for translators
 */
export const NAMESPACES = [
	'common',
	'menu',
	'dashboard',
	'organization',
	'tasks',
	'projects',
	'team',
	'settings',
	'reports',
	'assets',
	'import',
	'audit',
] as const;

export type Namespace = (typeof NAMESPACES)[number];

/**
 * Supported languages in the platform.
 * Adding a new language entry here (together with the corresponding
 * locale JSON files and a `LANG` entry in `lang.ts`) is all that is
 * needed to enable a new language — no other code changes required.
 *
 * @userstory US-I18N-10 — Adding new languages easily
 */
export const SUPPORTED_LANGUAGES = ['it', 'en', 'de', 'fr'] as const;
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
		// @userstory US-I18N-06 — Italian fallback ensures no untranslated content
		fallbackLng: 'it',
		supportedLngs: SUPPORTED_LANGUAGES as unknown as string[],

		// Debug mode in development
		debug: import.meta.env.MODE === 'development',

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
		// @userstory US-I18N-08 — Logs missing translation keys so developers catch them before deployment
		saveMissing: import.meta.env.MODE === 'development',
		missingKeyHandler: (lngs, ns, key) => {
			if (import.meta.env.MODE === 'development') {
				console.warn(
					`[i18n] Missing translation: ${ns}:${key} for languages: ${lngs.join(', ')}`,
				);
			}
		},
	});

export default i18n;
