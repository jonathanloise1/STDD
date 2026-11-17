import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { helpLoader } from '../help/runtime/loader';
import { createPackSearcher } from '../help/runtime/searcher';
import type { PackSearcher } from '../help/runtime/searcher';
import type { HelpLang } from '../help';
import type { ManualBundle } from '../help/runtime/types';

export type HelpResource<T> =
	| { status: 'loading'; data: null }
	| { status: 'ready'; data: T }
	| { status: 'error'; data: null };

/** The language of the application as a help language; anything but Italian reads the English manual. */
export const useHelpLang = (): HelpLang => {
	const { i18n } = useTranslation();
	return i18n.language?.substring(0, 2) === 'it' ? 'it' : 'en';
};

/** Loads a resource of the help files for a language; changing the language loads it again. */
function useHelpResource<T>(
	enabled: boolean,
	lang: HelpLang,
	load: (lang: HelpLang) => Promise<T>,
): HelpResource<T> {
	const [state, setState] = useState<{ lang: HelpLang; resource: HelpResource<T> } | null>(null);

	useEffect(() => {
		if (!enabled) return undefined;
		let cancelled = false;
		load(lang).then(
			(data) => !cancelled && setState({ lang, resource: { status: 'ready', data } }),
			() => !cancelled && setState({ lang, resource: { status: 'error', data: null } }),
		);
		return () => {
			cancelled = true;
		};
	}, [enabled, lang, load]);

	if (state && state.lang === lang) return state.resource;
	return { status: 'loading', data: null };
}

const loadBundle = (lang: HelpLang): Promise<ManualBundle> => helpLoader.bundle(lang);
const loadPackSearcher = async (lang: HelpLang) => {
	const loaded = await helpLoader.pack(lang);
	return {
		searcher: createPackSearcher(loaded.pack, loaded.embeddings ?? null),
		sha256: loaded.sha256,
	};
};

/** The manual of the application language (manual.md 5.1). */
export const useManualBundle = (enabled = true): HelpResource<ManualBundle> =>
	useHelpResource(enabled, useHelpLang(), loadBundle);

/** The searcher over the help pack of the application language. */
export const useHelpSearcher = (
	enabled = true,
): HelpResource<{ searcher: PackSearcher; sha256: string }> =>
	useHelpResource(enabled, useHelpLang(), loadPackSearcher);

/** The same resource as a memo-stable value for components that pass it on. */
export const usePageIndex = (bundle: ManualBundle | null) =>
	useMemo(() => new Map((bundle?.pages ?? []).map((p) => [p.page_id, p])), [bundle]);
