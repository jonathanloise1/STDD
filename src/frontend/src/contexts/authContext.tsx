import React, {
	createContext,
	FC,
	ReactNode,
	useCallback,
	useEffect,
	useMemo,
	useRef,
	useState,
} from 'react';
import {
	PublicClientApplication,
	InteractionRequiredAuthError,
	SilentRequest,
	PopupRequest,
} from '@azure/msal-browser';
import { useNavigate } from 'react-router-dom';
import authService, { IUserProfile } from '../services/api/authService';
import userProfileService from '../services/api/userProfileService';
import { ApiProblem, toApiProblem } from '../services/api/apiProblem';
import { showApiError } from '../ui';
import msalConfig, { apiScopes } from '../config/msalConfig';
import i18n from '../i18n';
import { runtimeSetting } from '../config/runtimeConfig';

// ============================================================================
// E2E MOCK AUTHENTICATION
// ============================================================================
// When running E2E tests, we bypass MSAL entirely using impersonation.
// The E2E tests inject a mock user into localStorage which we detect here.
// This allows tests to run without requiring manual OTP code entry.
//
// HOW IT WORKS:
// 1. E2E tests set localStorage['E2E_MOCK_USER'] with mock user JSON
// 2. E2E tests intercept API calls to add X-MyApp-Impersonate-UserId header
// 3. Frontend detects mock user and skips MSAL, using mock data instead
// 4. Backend ImpersonationMiddleware accepts the header and creates ClaimsPrincipal
//
// SECURITY: This only works when backend Impersonation:Enabled=true
// which should NEVER be enabled in production.
// ============================================================================
const E2E_MOCK_USER_KEY = 'E2E_MOCK_USER';

/**
 * DEV convenience: set VITE_DEV_MOCK_USER_ID in .env.local to auto-populate
 * the E2E mock user in localStorage so you don't need to type it in the console.
 * Requires a matching user AadId in the database and backend Impersonation:Enabled=true.
 */
const DEV_MOCK_USER_ID =
	runtimeSetting('VITE_DEV_MOCK_USER_ID') ??
	(import.meta.env.VITE_DEV_MOCK_USER_ID as string | undefined);
const DEV_MOCK_USER_EMAIL =
	(runtimeSetting('VITE_DEV_MOCK_USER_EMAIL') ??
		(import.meta.env.VITE_DEV_MOCK_USER_EMAIL as string)) ||
	'dev@myapp.local';

if (DEV_MOCK_USER_ID && !localStorage.getItem(E2E_MOCK_USER_KEY)) {
	console.log('[DEV] Auto-populating mock user from VITE_DEV_MOCK_USER_ID');
	localStorage.setItem(
		E2E_MOCK_USER_KEY,
		JSON.stringify({
			id: DEV_MOCK_USER_ID,
			email: DEV_MOCK_USER_EMAIL,
			isE2EMode: true,
		}),
	);
}

interface E2EMockUser {
	id: string;
	email: string;
	isE2EMode: boolean;
}

/**
 * Check if we're running in E2E mock mode
 */
const getE2EMockUser = (): E2EMockUser | null => {
	try {
		const mockData = localStorage.getItem(E2E_MOCK_USER_KEY);
		if (mockData) {
			const parsed = JSON.parse(mockData) as E2EMockUser;
			if (parsed.isE2EMode) {
				console.log('[E2E] Mock authentication mode detected for user:', parsed.email);
				return parsed;
			}
		}
	} catch (e) {
		console.warn('[E2E] Failed to parse mock user data:', e);
	}
	return null;
};

const msalInstance = new PublicClientApplication(msalConfig);

// === Context types ===
/** The signed-in user as the header shows it. */
export interface IUserData {
	id: string;
	name: string;
	surname: string;
	email?: string;
}

/** Authentication context value exposed to the entire React app. */
export interface IAuthContextProps {
	userData: Partial<IUserData>;
	isAuthenticated: boolean;
	isInitialized: boolean;
	isLoading: boolean;
	isBootstrapped: boolean;
	/** Set when the account could not be loaded after signing in; the app shows the message instead of its pages. */
	syncError: ApiProblem | null;
	login: () => Promise<void>;
	logout: () => void;
	acquireToken: () => Promise<string | null>;

	// Backward compatibility
	userToken?: string;
	setUserToken?: (token: string) => void;
	setUserData?: (userData: Partial<IUserData>) => void;
	syncUserData?: (force?: boolean, organizationId?: string) => Promise<any>;
}

const initialAuthContext: IAuthContextProps = {
	userData: {},
	isAuthenticated: false,
	isInitialized: false,
	isLoading: true,
	isBootstrapped: false,
	syncError: null,
	login: async () => {},
	logout: () => {},
	acquireToken: async () => null,
};

const AuthContext = createContext<IAuthContextProps>(initialAuthContext);

interface IAuthContextProviderProps {
	children: ReactNode;
}

/**
 * Provides authentication state and operations to the React component tree.
 *
 * Lifecycle:
 *   1. MSAL initialization → silent token check → processIdToken
 *   2. Bootstrap → syncUserData → dispatches 'auth-features-synced' event
 *   3. Preemptive token refresh every 60s
 *   4. Global event listeners for token errors / refresh requests
 */
// US-AUTH-01, US-AUTH-02, US-AUTH-04
export const AuthContextProvider: FC<IAuthContextProviderProps> = ({ children }) => {
	const navigate = useNavigate();

	// ----- State -----
	const [userData, setUserData] = useState<Partial<IUserData>>({});
	const [isAuthenticated, setIsAuthenticated] = useState(false);
	const [isInitialized, setIsInitialized] = useState(false);
	const [isLoading, setIsLoading] = useState(true);
	const [isBootstrapped, setIsBootstrapped] = useState(false);
	const [syncError, setSyncError] = useState<ApiProblem | null>(null);
	const [userToken, setUserTokenState] = useState('');

	// ----- Refs -----
	const initOnceRef = useRef(false);
	const bootOnceRef = useRef(false);
	const bootDoneRef = useRef(false);
	const isRefreshingRef = useRef(false);

	const syncPromiseRef = useRef<Promise<any> | null>(null);
	const lastSyncRef = useRef<number>(0);
	const SYNC_INTERVAL = 30 * 60 * 1000; // 30 minutes

	// ----- Helpers -----
	/**
	 * Processes a valid ID token: stores it, extracts the user profile (id, email, name)
	 * from the access token (preferred) or ID token (fallback), and marks the user as authenticated.
	 */
	const processIdToken = useCallback((idToken: string, maybeAccessToken?: string | null) => {
		authService.setIdToken(idToken);
		setUserTokenState(idToken);

		const preferredAccess = maybeAccessToken ?? authService.getAccessToken();
		let profile: IUserProfile | null = null;

		if (preferredAccess) {
			profile = authService.extractUserProfileFromAccessToken(preferredAccess);
		}
		if (!profile) {
			profile = authService.extractUserProfileFromIdToken(idToken);
		}

		if (profile) {
			setUserData((prev) => ({
				...prev,
				id: profile.id,
				email: profile.email,
				name: profile.name,
				surname: profile.surname,
			}));
		}

		// IMPORTANT: mark authenticated once we have a valid ID token
		setIsAuthenticated(true);
		setIsLoading(false); // Ensure loading is set to false
	}, []);

	/**
	 * Calls POST /api/auth/sync to synchronize the user with the backend.
	 * Rate-limited to once per 30 minutes (unless force=true).
	 * Uses a single-flight pattern to prevent duplicate concurrent requests.
	 * On success, dispatches the 'auth-features-synced' custom event with
	 * organization memberships for PermissionContext consumption.
	 * When the service does not answer, the problem is kept in `syncError` while the
	 * app is starting and shown as a message afterwards.
	 * @param force If true, bypasses rate limiting.
	 * @param organizationId Optional organization ID to scope the sync.
	 */
	const syncUserData = useCallback(
		async (force = false, organizationId?: string) => {
			const now = Date.now();

			if (!force && now - lastSyncRef.current < SYNC_INTERVAL) return null;
			if (syncPromiseRef.current) return syncPromiseRef.current;

			syncPromiseRef.current = (async () => {
				try {
					const user = await authService.syncUser(organizationId);

					// The name comes from the sync response or, failing that, from the profile
					let firstName = user.firstName;
					let lastName = user.lastName;
					if (firstName === undefined && lastName === undefined) {
						try {
							const profile = await userProfileService.getProfile();
							firstName = profile.firstName;
							lastName = profile.lastName;
						} catch {
							// The name from the sign-in token stays; it is only a label
						}
					}

					setUserData((prev) => ({
						...prev,
						id: user.id,
						email: user.email,
						...(firstName || lastName
							? { name: firstName ?? '', surname: lastName ?? '' }
							: {}),
					}));
					lastSyncRef.current = now;
					setSyncError(null);

					// The language chosen by the user, or English
					const preferred = user.preferredLanguage || 'en';
					if (preferred !== i18n.language) {
						i18n.changeLanguage(preferred);
					}

					// Dispatch event for PermissionContext and OrganizationContext
					window.dispatchEvent(
						new CustomEvent('auth-features-synced', {
							detail: {
								organizations: user.organizations || [],
							},
						}),
					);

					return user;
				} catch (error) {
					const problem = toApiProblem(error);
					if (bootOnceRef.current && !bootDoneRef.current) {
						setSyncError(problem);
					} else {
						showApiError(problem);
					}
					return null;
				} finally {
					syncPromiseRef.current = null;
				}
			})();

			return syncPromiseRef.current;
		},
		[SYNC_INTERVAL],
	);

	// ----- Token acquisition -----
	const acquireToken = useCallback(async (forceRefresh = false) => {
		// In E2E mock mode, tokens are not needed (backend uses impersonation header)
		const mockUser = getE2EMockUser();
		if (mockUser) {
			console.log('[E2E] Token acquisition skipped in mock mode');
			return 'E2E_MOCK_TOKEN';
		}

		if (isRefreshingRef.current && !forceRefresh) {
			await new Promise((r) => setTimeout(r, 100));
			return authService.getAccessToken();
		}

		try {
			isRefreshingRef.current = true;

			const account = msalInstance.getAllAccounts()[0];
			if (!account) throw new Error('No account found');

			const tokenRequest: SilentRequest = {
				scopes: apiScopes,
				account,
				authority: msalConfig.auth.authority,
				forceRefresh,
			};

			const response = await msalInstance.acquireTokenSilent(tokenRequest);
			if (response?.accessToken) {
				authService.setAccessToken(response.accessToken);
				return response.accessToken;
			}
			return null;
		} catch (err) {
			if (err instanceof InteractionRequiredAuthError) {
				try {
					const popupRequest: PopupRequest = {
						scopes: apiScopes,
						authority: msalConfig.auth.authority,
					};
					const response = await msalInstance.acquireTokenPopup(popupRequest);
					if (response?.accessToken) {
						authService.setAccessToken(response.accessToken);
						return response.accessToken;
					}
				} catch {
					await msalInstance.acquireTokenRedirect({
						scopes: apiScopes,
						redirectUri: window.location.origin,
					});
				}
			}
			return null;
		} finally {
			isRefreshingRef.current = false;
		}
	}, []);

	/**
	 * MSAL initialization effect (runs once).
	 * 1. Checks for E2E mock mode (bypasses MSAL entirely for Playwright tests)
	 * 2. Initializes MSAL PublicClientApplication
	 * 3. Handles redirect response if returning from Azure AD B2C
	 * 4. Checks existing valid tokens in localStorage
	 * 5. Attempts silent login for known MSAL accounts
	 * Falls back gracefully at each step; 10s timeout prevents stuck state.
	 */
	useEffect(() => {
		if (initOnceRef.current) return;
		initOnceRef.current = true;

		// =====================================================
		// E2E MOCK MODE CHECK - bypasses MSAL entirely
		// =====================================================
		const mockUser = getE2EMockUser();
		if (mockUser) {
			console.log('[E2E] Skipping MSAL initialization, using mock user:', mockUser.email);

			// Set user data from mock
			setUserData({
				id: mockUser.id,
				email: mockUser.email,
				name: mockUser.email.split('@')[0],
				surname: '',
			});

			// Mark as authenticated and ready
			setIsAuthenticated(true);
			setIsInitialized(true);
			setIsLoading(false);

			console.log('[E2E] Mock authentication complete');
			return;
		}

		// Add a timeout to prevent getting stuck
		const initTimeout = setTimeout(() => {
			console.warn('MSAL initialization timeout, forcing completion');
			setIsInitialized(true);
			setIsLoading(false);
		}, 10000); // 10 second timeout

		(async () => {
			try {
				await msalInstance.initialize();
				setIsInitialized(true);

				// Handle redirect response if present
				const response = await msalInstance.handleRedirectPromise();

				// 2) Redirect response path (handle this first if present)
				if (response?.idToken) {
					if (response.accessToken) {
						authService.setAccessToken(response.accessToken);
					}
					processIdToken(response.idToken, response.accessToken);
					if (!response.accessToken) {
						await acquireToken();
					}
					clearTimeout(initTimeout);
					return;
				}

				// 1) Check existing tokens - but be more strict about expiration
				const existingIdToken = authService.getIdToken();
				if (existingIdToken && authService.isTokenValid(existingIdToken, true)) {
					// Use margin for safety
					try {
						const at = await acquireToken(); // prefer to have AT early
						processIdToken(existingIdToken, at ?? undefined);
						clearTimeout(initTimeout);
						return;
					} catch (tokenError) {
						console.warn(
							'Failed to acquire access token with existing ID token:',
							tokenError,
						);
						// If we can't get an access token, the ID token might be stale
						// Clear it and try silent login instead
						authService.clearAuth();
					}
				}

				// 3) Silent login when an account is known (most reliable for returning users)
				const accounts = msalInstance.getAllAccounts();
				if (accounts.length > 0) {
					try {
						console.log('Attempting silent login for existing account...');
						const silent = await msalInstance.acquireTokenSilent({
							scopes: ['openid', 'profile', 'email', ...apiScopes],
							account: accounts[0],
						});
						if (silent.idToken) {
							if (silent.accessToken) {
								authService.setAccessToken(silent.accessToken);
							}
							processIdToken(silent.idToken, silent.accessToken);
							clearTimeout(initTimeout);
							return;
						}
					} catch (e) {
						console.warn('Silent login failed, will require user interaction:', e);
						// Clear any stale tokens and force re-authentication
						authService.clearAuth();
					}
				}

				// If we reach here, no valid tokens found and silent login failed
				// Clear any stale data and require fresh authentication
				authService.clearAuth();
				setIsAuthenticated(false);
				setIsLoading(false);
				clearTimeout(initTimeout);
			} catch (e) {
				console.error('MSAL initialization error:', e);
				// Clear any stale data on error
				authService.clearAuth();
				setIsInitialized(true);
				setIsLoading(false);
				clearTimeout(initTimeout);
			}
		})();
	}, [processIdToken, acquireToken]);

	/**
	 * Bootstrap effect (runs once after authentication).
	 * Calls syncUserData(force=true) to load features and organizations,
	 * Organization data is then loaded by the organization context.
	 * 15s timeout ensures the app always becomes interactive.
	 */
	useEffect(() => {
		if (!isAuthenticated || isBootstrapped || bootOnceRef.current) return;
		bootOnceRef.current = true;

		// Add a timeout to prevent getting stuck
		const bootstrapTimeout = setTimeout(() => {
			console.warn('Bootstrap timeout, forcing completion');
			bootDoneRef.current = true;
			setIsBootstrapped(true);
		}, 15000); // 15 second timeout

		(async () => {
			try {
				// A failed sync is kept in syncError and shown to the user by the app
				await syncUserData(true);

				bootDoneRef.current = true;
				setIsBootstrapped(true);
				clearTimeout(bootstrapTimeout);
			} catch (err) {
				console.error('Bootstrap error:', err);
				// Always mark as bootstrapped even if there are errors
				bootDoneRef.current = true;
				setIsBootstrapped(true);
				clearTimeout(bootstrapTimeout);
			}
		})();
	}, [isAuthenticated, isBootstrapped, syncUserData]);

	/**
	 * Preemptive token refresh effect.
	 * Checks every 60 seconds if the access token expires within 5 minutes;
	 * if so, forces a silent token refresh via MSAL.
	 */
	useEffect(() => {
		if (!isAuthenticated || !isInitialized) return;

		const check = async () => {
			const accessToken = authService.getAccessToken();
			if (!accessToken) return;
			const ms = authService.getTimeUntilExpiration(accessToken);
			if (ms > 0 && ms < 5 * 60 * 1000) {
				await acquireToken(true);
			}
		};

		check();
		const id = setInterval(check, 60 * 1000);
		return () => clearInterval(id);
	}, [isAuthenticated, isInitialized, acquireToken]);

	/**
	 * Global event listeners for token lifecycle management.
	 * - 'auth-token-error': forces logout and navigates to home.
	 * - 'auth-token-refresh-needed': acquires a fresh access token and re-syncs user data.
	 */
	useEffect(() => {
		const handleTokenError = () => {
			setIsAuthenticated(false);
			setUserData({});
			navigate('/');
		};

		const handleTokenRefreshNeeded = async () => {
			try {
				console.log('Token refresh needed, attempting to acquire new token...');
				const accessToken = await acquireToken(true);
				if (accessToken) {
					console.log('Token refresh successful, syncing user data...');
					syncUserData(false).catch(() => {});
					// Dispatch event to notify other components that token is refreshed
					window.dispatchEvent(new CustomEvent('auth-token-refreshed'));
				} else {
					console.log('Token refresh failed, no access token received');
					window.dispatchEvent(new CustomEvent('auth-token-error'));
				}
			} catch (error) {
				console.error('Token refresh failed with error:', error);
				window.dispatchEvent(new CustomEvent('auth-token-error'));
			}
		};

		window.addEventListener('auth-token-error', handleTokenError);
		window.addEventListener('auth-token-refresh-needed', handleTokenRefreshNeeded);
		return () => {
			window.removeEventListener('auth-token-error', handleTokenError);
			window.removeEventListener('auth-token-refresh-needed', handleTokenRefreshNeeded);
		};
	}, [navigate, acquireToken, syncUserData]);

	/**
	 * Initiates user login via MSAL.
	 * Strategy: silent acquisition for known accounts → loginRedirect with prompt='select_account'.
	 * Cleans up stale MSAL state from localStorage before attempting.
	 */
	const login = useCallback(async () => {
		if (!isInitialized) return;

		try {
			// Check for active interactions
			const hasActive = Object.keys(localStorage).some((k) => k.includes('msal.interaction'));
			if (hasActive) return;

			// Clean up any stale MSAL state first
			Object.keys(localStorage)
				.filter((k) => k.includes('msal.state') || k.includes('msal.interaction'))
				.forEach((k) => localStorage.removeItem(k));

			// Try silent login first for existing accounts
			const accounts = msalInstance.getAllAccounts();
			if (accounts.length > 0) {
				try {
					console.log('Attempting silent login during manual login...');
					const response = await msalInstance.acquireTokenSilent({
						scopes: ['openid', 'profile', 'email', ...apiScopes],
						account: accounts[0],
					});
					if (response.idToken) {
						if (response.accessToken) authService.setAccessToken(response.accessToken);
						processIdToken(response.idToken, response.accessToken);
						return;
					}
				} catch (silentError) {
					console.warn(
						'Silent login failed during manual login, proceeding with redirect:',
						silentError,
					);
					// Clear any stale tokens and force fresh authentication
					authService.clearAuth();
				}
			}

			// Force fresh authentication
			console.log('Initiating fresh authentication...');
			await msalInstance.loginRedirect({
				scopes: ['openid', 'profile', 'email', ...apiScopes],
				redirectStartPage: window.location.href,
				prompt: 'select_account',
			});
		} catch (error: any) {
			if (error?.errorCode !== 'interaction_in_progress') {
				console.error('Login error:', error);
				setIsLoading(false);
				// Don't set authenticated to false here, let the user try again
			}
		}
	}, [isInitialized, processIdToken]);

	/**
	 * Logs the user out: clears all tokens from localStorage, resets context state,
	 * and redirects to Azure AD B2C logout endpoint.
	 * Post-logout redirect goes to the application origin.
	 */
	// US-AUTH-03
	const logout = useCallback(() => {
		authService.clearAuth();
		setUserData({});
		setIsAuthenticated(false);
		msalInstance.logoutRedirect({ postLogoutRedirectUri: window.location.origin });
	}, []);

	// ----- Context value -----
	const value = useMemo(
		() => ({
			userData,
			isAuthenticated,
			isInitialized,
			isLoading,
			isBootstrapped,
			syncError,
			login,
			logout,
			acquireToken,
			// backward compat
			userToken,
			setUserToken: (token: string) => {
				setUserTokenState(token);
				authService.setIdToken(token);
			},
			setUserData,
			syncUserData,
		}),
		[
			userData,
			isAuthenticated,
			isInitialized,
			isLoading,
			isBootstrapped,
			syncError,
			login,
			logout,
			acquireToken,
			userToken,
			setUserData,
			syncUserData,
		],
	);

	return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export default AuthContext;
