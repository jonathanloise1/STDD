import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePermissions } from '../contexts/permissionContext';
import type { IconName } from '../ui';

export interface IShortcut {
	/** Second key of the G-prefix sequence. */
	key: string;
	path: string;
	icon: IconName;
	/** Key in the `common` namespace. */
	labelKey: string;
	adminOnly: boolean;
}

/** G-prefix navigation shortcuts; the Team, Audit Log and Settings ones are for Admins only. */
const NAVIGATION_SHORTCUTS: IShortcut[] = [
	{
		key: 'd',
		path: '/dashboard',
		icon: 'Dashboard',
		labelKey: 'shortcuts.dashboard',
		adminOnly: false,
	},
	{ key: 't', path: '/tasks', icon: 'TaskAlt', labelKey: 'shortcuts.tasks', adminOnly: false },
	{ key: 'a', path: '/audit', icon: 'History', labelKey: 'shortcuts.audit', adminOnly: true },
	{
		key: 's',
		path: '/settings/organization',
		icon: 'Settings',
		labelKey: 'shortcuts.settings',
		adminOnly: true,
	},
	{
		key: 'm',
		path: '/settings/team',
		icon: 'Groups',
		labelKey: 'shortcuts.team',
		adminOnly: true,
	},
];

/** Timeout for the G-prefix sequence (ms) */
const PREFIX_TIMEOUT = 1000;

/**
 * Returns true if the active element is a form control where typing should not trigger shortcuts.
 */
function isTypingInFormControl(): boolean {
	const el = document.activeElement;
	if (!el) return false;
	const tag = el.tagName.toLowerCase();
	if (tag === 'input' || tag === 'textarea' || tag === 'select') return true;
	if ((el as HTMLElement).isContentEditable) return true;
	return false;
}

/**
 * Returns true if any Bootstrap modal is currently open.
 */
function isModalOpen(): boolean {
	return document.querySelectorAll('.modal.show').length > 0;
}

export interface UseKeyboardShortcutsReturn {
	/** Whether the keyboard shortcuts help modal is open */
	isHelpOpen: boolean;
	/** Toggle help modal */
	setIsHelpOpen: (open: boolean) => void;
	/** The navigation shortcuts the user can use (for rendering the help dialog) */
	shortcuts: IShortcut[];
}

/**
 * Hook that registers global keyboard shortcuts.
 *
 * Navigation: G → {key} navigates to a section the role allows.
 * Help: ? opens the help dialog.
 */
// US-AUTH-09
export default function useKeyboardShortcuts(): UseKeyboardShortcutsReturn {
	const navigate = useNavigate();
	const { isAdmin } = usePermissions();
	const [isHelpOpen, setIsHelpOpen] = useState(false);
	const shortcuts = useMemo(
		() => NAVIGATION_SHORTCUTS.filter((shortcut) => isAdmin || !shortcut.adminOnly),
		[isAdmin],
	);

	// Track the G-prefix state
	const prefixActiveRef = useRef(false);
	const prefixTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

	const clearPrefixTimer = useCallback(() => {
		if (prefixTimerRef.current) {
			clearTimeout(prefixTimerRef.current);
			prefixTimerRef.current = null;
		}
		prefixActiveRef.current = false;
	}, []);

	useEffect(() => {
		const handleKeyDown = (e: KeyboardEvent) => {
			// Never intercept when typing in form controls
			if (isTypingInFormControl()) return;

			const key = e.key.toLowerCase();

			// '?' opens the help dialog
			if (e.key === '?' && !isModalOpen()) {
				e.preventDefault();
				setIsHelpOpen(true);
				return;
			}

			// If a modal is open, don't process navigation shortcuts
			if (isModalOpen()) return;

			// G-prefix sequence
			if (!prefixActiveRef.current && key === 'g' && !e.ctrlKey && !e.altKey && !e.metaKey) {
				e.preventDefault();
				prefixActiveRef.current = true;
				// Auto-reset after timeout
				prefixTimerRef.current = setTimeout(() => {
					prefixActiveRef.current = false;
				}, PREFIX_TIMEOUT);
				return;
			}

			// Second key after G
			if (prefixActiveRef.current) {
				clearPrefixTimer();
				const shortcut = shortcuts.find((candidate) => candidate.key === key);
				if (shortcut) {
					e.preventDefault();
					navigate(shortcut.path);
				}
			}
		};

		window.addEventListener('keydown', handleKeyDown);
		return () => {
			window.removeEventListener('keydown', handleKeyDown);
			clearPrefixTimer();
		};
	}, [navigate, clearPrefixTimer, shortcuts]);

	return {
		isHelpOpen,
		setIsHelpOpen,
		shortcuts,
	};
}
