// US-SHORT-01: Keyboard shortcut navigation hook
// US-SHORT-02: Help modal trigger via '?' key
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

/** Map of second-key → route path for G-prefix navigation shortcuts */
const NAVIGATION_SHORTCUTS: Record<string, string> = {
	d: 'dashboard',
	n: 'model/nodes',
	c: 'cost-entries',
	r: 'allocation-rules',
	e: 'personnel/employees',
	t: 'personnel/timesheet',
	a: 'assets',
	k: 'calculations',
	p: 'reports',
	i: 'import',
	s: 'settings/organization',
};

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
	/** The navigation shortcuts map (for rendering the help modal) */
	shortcuts: typeof NAVIGATION_SHORTCUTS;
}

/**
 * Hook that registers global keyboard shortcuts.
 *
 * Navigation: G → {key} navigates to a section.
 * Help: ? opens the help modal.
 *
 * @userstory US-SHORT-01, US-SHORT-02
 */
export default function useKeyboardShortcuts(): UseKeyboardShortcutsReturn {
	const navigate = useNavigate();
	const [isHelpOpen, setIsHelpOpen] = useState(false);

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

			// US-SHORT-02: '?' opens help modal
			if (e.key === '?' && !isModalOpen()) {
				e.preventDefault();
				setIsHelpOpen(true);
				return;
			}

			// If a modal is open, don't process navigation shortcuts
			if (isModalOpen()) return;

			// US-SHORT-01: G-prefix sequence
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
				const path = NAVIGATION_SHORTCUTS[key];
				if (path) {
					e.preventDefault();
					navigate(`/${path}`);
				}
			}
		};

		window.addEventListener('keydown', handleKeyDown);
		return () => {
			window.removeEventListener('keydown', handleKeyDown);
			clearPrefixTimer();
		};
	}, [navigate, clearPrefixTimer]);

	return {
		isHelpOpen,
		setIsHelpOpen,
		shortcuts: NAVIGATION_SHORTCUTS,
	};
}
