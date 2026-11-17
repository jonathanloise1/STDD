import React, { use, useEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiErrorMessage, Loading, ToastHost } from '../ui';
import AuthContext from '../contexts/authContext';
import { useOrganization } from '../contexts/organizationContext';
import AppShell from '../shell/AppShell';
import authService from '../services/api/authService';

const SIGN_IN_PATHS = ['/', '/login', '/auth/login'];

const App = () => {
	// Get navigation and location functions
	const navigate = useNavigate();
	const location = useLocation();

	// Access AuthContext
	const { isAuthenticated, isInitialized, isBootstrapped, login, syncError } = use(AuthContext);

	// Access OrganizationContext
	const {
		organizations,
		isLoading: isOrgLoading,
		isInitialized: isOrgInitialized,
		error: orgError,
	} = useOrganization();

	// Variable to track if login is in progress to prevent multiple login attempts
	const loginAttemptRef = useRef(false);

	const loadError = syncError ?? orgError;
	const isOrgReady = isOrgInitialized && !isOrgLoading;

	// Routing is decided on organization membership: a user who belongs to no organization
	// sees the setup page on any page; a member never sees it and lands on the Dashboard page.
	useEffect(() => {
		if (!isAuthenticated || !isBootstrapped || !isOrgReady || loadError) return;

		if (organizations.length === 0) {
			if (location.pathname !== '/onboarding') navigate('/onboarding', { replace: true });
			return;
		}

		if (SIGN_IN_PATHS.includes(location.pathname) || location.pathname === '/onboarding') {
			navigate('/dashboard', { replace: true });
		}
	}, [
		isAuthenticated,
		isBootstrapped,
		isOrgReady,
		loadError,
		location.pathname,
		navigate,
		organizations.length,
	]);

	// Ask a visitor who is not signed in to sign in; the page asked for opens afterwards
	useEffect(() => {
		if (!isInitialized) return;
		if (isAuthenticated) {
			loginAttemptRef.current = false;
			return;
		}

		if (!loginAttemptRef.current) {
			// Check if ID token exists and is valid
			const idToken = authService.getIdToken();

			// If no token or token is invalid, trigger login
			if (!idToken || !authService.isTokenValid(idToken)) {
				// Set flag to prevent multiple login attempts
				loginAttemptRef.current = true;

				// Clear any stale MSAL state first
				Object.keys(localStorage)
					.filter((key) => key.includes('msal.state') || key.includes('msal.interaction'))
					.forEach((key) => localStorage.removeItem(key));

				// Short delay to avoid interaction_in_progress errors
				setTimeout(() => {
					login().catch((error) => {
						// An ongoing authentication is left to finish; anything else may be retried
						if (error?.errorCode !== 'interaction_in_progress') {
							loginAttemptRef.current = false;
						}
					});
				}, 500);
			}
		}
	}, [isInitialized, isAuthenticated, login]);

	return (
		<>
			<div className='app'>
				{isInitialized && isAuthenticated && isBootstrapped && (isOrgReady || loadError) ? (
					loadError ? (
						<div className='container py-5'>
							<ApiErrorMessage problem={loadError} />
						</div>
					) : (
						<AppShell />
					)
				) : (
					<Loading />
				)}
			</div>
			<ToastHost />
		</>
	);
};

export default App;
