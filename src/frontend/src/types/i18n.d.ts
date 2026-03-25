/**
 * i18next TypeScript Type Definitions
 *
 * This file provides type safety for translation keys.
 * It augments the react-i18next module to add proper typing.
 *
 * @see https://www.i18next.com/overview/typescript
 */

import 'react-i18next';
import type { Namespace, SupportedLanguage } from '../i18n';

// Import the namespace JSON types for autocomplete
// Note: These are the type shapes, not the actual JSON imports
import type common from '../../public/locales/it/common.json';
import type menu from '../../public/locales/it/menu.json';
import type dashboard from '../../public/locales/it/dashboard.json';
import type organization from '../../public/locales/it/organization.json';
import type projects from '../../public/locales/it/projects.json';
import type team from '../../public/locales/it/team.json';
import type settings from '../../public/locales/it/settings.json';

declare module 'react-i18next' {
	interface CustomTypeOptions {
		// Default namespace when calling useTranslation()
		defaultNS: 'common';

		// Type definition for all namespaces
		resources: {
			common: typeof common;
			menu: typeof menu;
			dashboard: typeof dashboard;
			organization: typeof organization;
			projects: typeof projects;
			team: typeof team;
			settings: typeof settings;
		};
	}
}

// Re-export types for use in components
export type { Namespace, SupportedLanguage };
