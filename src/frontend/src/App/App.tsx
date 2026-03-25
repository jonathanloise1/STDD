import React, { Suspense, use, useEffect, useLayoutEffect, useRef } from 'react';
import { useFullscreen } from 'react-use';
import { TourProvider } from '@reactour/tour';
import { useLocation, useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import 'dayjs/locale/en';
import 'dayjs/locale/it';
import localizedFormat from 'dayjs/plugin/localizedFormat';
import relativeTime from 'dayjs/plugin/relativeTime';
import { ToastContainer, toast } from 'react-toastify';
import ToastTransition from '../components/extras/ToastTransition';
import ThemeContext from '../contexts/themeContext';
import AuthContext from '../contexts/authContext';
import { useOrganization } from '../contexts/organizationContext';
import Wrapper from '../layout/Wrapper/Wrapper';
import Portal from '../layout/Portal/Portal';
import useDarkMode from '../hooks/useDarkMode';
import { getOS } from '../helpers/helpers';
import steps, { styles } from '../steps';
import AsideRoutes from '../layout/Aside/AsideRoutes';
import { ToastCloseButton } from '../components/bootstrap/Toasts';
import authService from '../services/api/authService';
import Loading from '../components/Loading';

const App = () => {
	getOS();

	dayjs.extend(localizedFormat);
	dayjs.extend(relativeTime);

	// Get navigation and location functions
	const navigate = useNavigate();
	const location = useLocation();

	// Access AuthContext
	const { isAuthenticated, isInitialized, isBootstrapped, login, syncUserData, userData } =
		use(AuthContext);

	// Access OrganizationContext
	const {
		organizations,
		isLoading: isOrgLoading,
		isInitialized: isOrgInitialized,
	} = useOrganization();

	// Variable to track if login is in progress to prevent multiple login attempts
	const loginAttemptRef = useRef(false);

	// Handle API timeout errors
	useEffect(() => {
		const handleApiTimeout = (event: CustomEvent) => {
			// Show a user-friendly error message
			toast.error(
				`API request timed out. Please check your internet connection or try again later.`,
				{
					autoClose: 5000,
					closeButton: true,
					position: 'top-right',
				},
			);

			console.error('API timeout:', event.detail);
		};

		// Add event listener
		window.addEventListener('api-timeout-error', handleApiTimeout as EventListener);

		// Clean up
		return () => {
			window.removeEventListener('api-timeout-error', handleApiTimeout as EventListener);
		};
	}, []);

	// Handle role-based redirection after successful authentication
	useEffect(() => {
		console.log('[App/App.tsx] Redirect effect fired', {
			isAuthenticated,
			isBootstrapped,
			pathname: location.pathname,
			isOrgInitialized,
			isOrgLoading,
			organizationsCount: organizations?.length || 0,
		});
		
		if (
			isAuthenticated &&
			isBootstrapped &&
			(location.pathname === '/' || 
			 location.pathname === '/login' || 
			 location.pathname === '/auth/login' || 
			 location.pathname === '/auth-pages/login')
		) {
			// US-ONBOARD-01: If user hasn't completed onboarding, redirect there first
			if (!localStorage.getItem('myapp_onboarded')) {
				console.log('[App/App.tsx] Redirecting to /onboarding (not onboarded)');
				navigate('/onboarding');
				return;
			}

			// Wait for organization context to be initialized before making routing decisions
			if (isOrgInitialized && !isOrgLoading) {
				if (organizations && organizations.length > 0) {
					console.log('[App/App.tsx] Redirecting to /dashboard');
					navigate('/dashboard');
				} else {
					console.log('[App/App.tsx] Redirecting to /organizations (no orgs)');
					navigate('/organizations');
				}
			} else {
				console.log('[App/App.tsx] Waiting for organization context to initialize...');
			}
		}
	}, [
		isAuthenticated,
		isBootstrapped,
		location.pathname,
		navigate,
		organizations,
		isOrgLoading,
		isOrgInitialized,
	]);

	// Handle organization-based redirection — if user on dashboard with no orgs, redirect
	useEffect(() => {
		if (
			isAuthenticated &&
			isBootstrapped &&
			isOrgInitialized &&
			!isOrgLoading &&
			location.pathname === '/dashboard'
		) {
			if (!organizations || organizations.length === 0) {
				console.log('[App/App.tsx] Effect #2: On dashboard with no orgs → /organizations');
				navigate('/organizations');
			}
		}
	}, [
		isAuthenticated,
		isBootstrapped,
		organizations,
		isOrgLoading,
		isOrgInitialized,
		location.pathname,
		navigate,
	]);

	// Trigger login if not authenticated and MSAL is initialized
	useEffect(() => {
		// Skip if not initialized or already authenticated
		if (!isInitialized || isAuthenticated) {
			// If authenticated, reset login attempt flag
			if (isAuthenticated) {
				loginAttemptRef.current = false;

				// Sync user data only if essential data is missing to prevent redundant API calls
				if (syncUserData && (!userData.id || !userData.email)) {
					syncUserData().catch((error) => {
						console.error('Failed to sync user data:', error);
						// If sync fails due to invalid token, we should logout and login again
						if (error.response?.status === 401) {
							console.log('Auth token invalid, restarting authentication flow...');
							authService.clearAuth();
							// We'll let the next render cycle handle the login
						}
					});
				}
			}
			return;
		}

		// Only proceed if not authenticated and not already attempting login
		// Don't attempt login on Dashboard pages
		if (!loginAttemptRef.current && !location.pathname.includes('/dashboard')) {
			// Check if ID token exists and is valid
			const idToken = authService.getIdToken();

			// If no token or token is invalid, trigger login
			if (!idToken || !authService.isTokenValid(idToken)) {
				console.log('No valid access token found, initiating Azure AD B2C login flow...');
				// Set flag to prevent multiple login attempts
				loginAttemptRef.current = true;

				// Clear any stale MSAL state first
				Object.keys(localStorage)
					.filter((key) => key.includes('msal.state') || key.includes('msal.interaction'))
					.forEach((key) => localStorage.removeItem(key));

				// Short delay to avoid interaction_in_progress errors
				setTimeout(() => {
					// Trigger the Azure AD B2C login flow - only do this once
					login().catch((error) => {
						console.error('Login error:', error);
						// Don't immediately reset the flag on interaction_in_progress errors
						if (
							error &&
							typeof error === 'object' &&
							'errorCode' in error &&
							error.errorCode === 'interaction_in_progress'
						) {
							// For interaction_in_progress, let the ongoing auth complete
							console.log('Waiting for ongoing authentication to complete...');
						} else {
							// Reset flag for other errors so we can try again
							loginAttemptRef.current = false;
						}
					});
				}, 500);
			}
		}
	}, [
		isInitialized,
		isAuthenticated,
		login,
		syncUserData,
		userData.id,
		userData.email,
		location.pathname,
	]);

	/**
	 * Dark Mode
	 */
	const { darkModeStatus } = useDarkMode();

	useEffect(() => {
		if (darkModeStatus) {
			document.documentElement.setAttribute('theme', 'dark');
			document.documentElement.setAttribute('data-bs-theme', 'dark');
		}
		return () => {
			document.documentElement.removeAttribute('theme');
			document.documentElement.removeAttribute('data-bs-theme');
		};
	}, [darkModeStatus]);

	/**
	 * Full Screen
	 */
	const { fullScreenStatus, setFullScreenStatus } = use(ThemeContext);
	const ref = useRef<HTMLDivElement>(null);
	// @ts-ignore - react-use useFullscreen types not updated for React 19 RefObject
	useFullscreen(ref, fullScreenStatus, {
		onClose: () => setFullScreenStatus(false),
	});

	/**
	 * Modern Design
	 */
	useLayoutEffect(() => {
		if (import.meta.env.VITE_MODERN_DESGIN === 'true') {
			document.body.classList.add('modern-design');
		} else {
			document.body.classList.remove('modern-design');
		}
	});

	return (
		<TourProvider steps={steps} styles={styles} showNavigation={false} showBadge={false}>
				<div
					ref={ref}
					className='app'
					style={{
						backgroundColor: fullScreenStatus ? 'var(--bs-body-bg)' : undefined,
						zIndex: fullScreenStatus ? 1 : undefined,
						overflow: fullScreenStatus ? 'scroll' : undefined,
					}}>
					{isInitialized && isAuthenticated && isBootstrapped ? (
						<>
							<AsideRoutes />
							<Wrapper />
						</>
					) : (
						<Loading />
					)}
				</div>
				<ToastContainer closeButton={ToastCloseButton} toastClassName='toast' theme={darkModeStatus ? 'dark' : 'light'} transition={ToastTransition} />
			</TourProvider>
	);
};

export default App;
