import React, { createContext, use, useCallback, useMemo, useState, ReactNode } from 'react';

export type HelpTab = 'page' | 'ask';

export interface OpenHelpOptions {
	/** The context key of the component that opens help for itself (manual.md 5.4). */
	context?: string;
	tab?: HelpTab;
}

export interface HelpContextValue {
	isOpen: boolean;
	tab: HelpTab;
	/** The context key the panel was opened with, or null when it was opened from the header. */
	context: string | null;
	/** Opens the panel on the tab "This page", or on the tab given. */
	openHelp: (options?: OpenHelpOptions) => void;
	closeHelp: () => void;
	setTab: (tab: HelpTab) => void;
}

const defaultValue: HelpContextValue = {
	isOpen: false,
	tab: 'page',
	context: null,
	openHelp: () => {},
	closeHelp: () => {},
	setTab: () => {},
};

export const HelpContext = createContext<HelpContextValue>(defaultValue);

interface HelpProviderProps {
	children: ReactNode;
}

/**
 * State of the help panel: open or closed, the tab and the context key. Opening or closing it changes nothing else,
 * so the screen behind it keeps what the user typed.
 */
export const HelpProvider = ({ children }: HelpProviderProps) => {
	const [isOpen, setIsOpen] = useState(false);
	const [tab, setTab] = useState<HelpTab>('page');
	const [context, setContext] = useState<string | null>(null);

	const openHelp = useCallback((options?: OpenHelpOptions) => {
		setContext(options?.context ?? null);
		setTab(options?.tab ?? 'page');
		setIsOpen(true);
	}, []);
	const closeHelp = useCallback(() => setIsOpen(false), []);

	const value = useMemo(
		() => ({ isOpen, tab, context, openHelp, closeHelp, setTab }),
		[isOpen, tab, context, openHelp, closeHelp],
	);
	return <HelpContext value={value}>{children}</HelpContext>;
};

export const useHelp = (): HelpContextValue => use(HelpContext);
