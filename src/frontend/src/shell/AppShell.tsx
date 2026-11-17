import React, { lazy, Suspense, use, useCallback, useEffect, useState } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { Loading, LoadingBlock } from '../ui';
import AuthContext from '../contexts/authContext';
import { useOrganization } from '../contexts/organizationContext';
import useKeyboardShortcuts from '../hooks/useKeyboardShortcuts';
import { HelpPanel } from '../help/components/HelpPanel';
import contents from '../routes/contentRoutes';
import AppHeader from './AppHeader';
import BottomNav from './BottomNav';
import KeyboardShortcutsDialog from './KeyboardShortcutsDialog';
import SideNav from './SideNav';

const NOT_FOUND = lazy(() => import('../pages/company/NotFoundPage'));

const PINNED_KEY = 'myapp_sidebarPinned';

const readPinned = (): boolean => {
	try {
		return localStorage.getItem(PINNED_KEY) === 'true';
	} catch {
		return false;
	}
};

/**
 * The frame of the application (frontend.md 6.1): side navigation, header, content, bottom navigation,
 * the help panel and the keyboard shortcuts dialog. A user who belongs to no organization has nothing to
 * open but the setup page, so no menu is shown.
 */
// US-AUTH-06
const AppShell = () => {
	const { isLoading, isBootstrapped } = use(AuthContext);
	const { organizations } = useOrganization();
	const { isHelpOpen, setIsHelpOpen, shortcuts } = useKeyboardShortcuts();
	const [pinned, setPinned] = useState<boolean>(readPinned);

	useEffect(() => {
		try {
			localStorage.setItem(PINNED_KEY, String(pinned));
		} catch {
			// The choice is not kept on this device.
		}
	}, [pinned]);

	const togglePin = useCallback(() => setPinned((prev) => !prev), []);
	const hasOrganization = organizations.length > 0;

	return (
		<div className='app-shell' data-sidenav={pinned ? 'pinned' : undefined}>
			{hasOrganization && <SideNav pinned={pinned} onTogglePin={togglePin} />}

			<div className='app-shell__main'>
				<AppHeader />
				<main className='app-content'>
					{/* Block routing until bootstrap is complete */}
					{isLoading || !isBootstrapped ? (
						<div id='app-loading'>
							<Loading />
						</div>
					) : (
						<Suspense fallback={<LoadingBlock />}>
							<Routes>
								{/* Root path: the application root redirects once the organizations are known */}
								<Route path='/' element={<Loading />} />
								<Route path='/login' element={<Navigate to='/' replace />} />
								<Route path='/auth/login' element={<Navigate to='/' replace />} />

								{contents.map((page) =>
									page.path ? (
										<Route
											key={page.path}
											path={page.path}
											element={page.element}
										/>
									) : null,
								)}

								{/* An address that matches no page */}
								<Route path='*' element={<NOT_FOUND />} />
							</Routes>
						</Suspense>
					)}
				</main>
			</div>

			{hasOrganization && <BottomNav />}

			<HelpPanel />
			<KeyboardShortcutsDialog
				show={isHelpOpen}
				onHide={() => setIsHelpOpen(false)}
				shortcuts={shortcuts}
			/>
		</div>
	);
};

export default AppShell;
