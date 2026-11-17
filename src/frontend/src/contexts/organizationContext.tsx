import React, {
	createContext,
	useState,
	use,
	ReactNode,
	useCallback,
	useEffect,
	useRef,
} from 'react';
import organizationService, {
	Organization,
	CreateOrganizationRequest,
	UpdateOrganizationRequest,
} from '../services/api/organizationService';
import AuthContext from './authContext';
import { ApiProblem, toApiProblem } from '../services/api/apiProblem';

// =============================================================================
// TYPES
// =============================================================================

interface OrganizationContextType {
	selectedOrganization: Organization | null;
	setSelectedOrganization: (org: Organization | null) => void;
	organizations: Organization[];
	isLoading: boolean;
	isInitialized: boolean; // True after the initial fetch attempt completes (success or error)
	/** Set when the organizations could not be loaded; the app shows the message instead of its pages. */
	error: ApiProblem | null;

	// API Methods
	fetchOrganizations: () => Promise<Organization[]>;
	createOrganization: (data: CreateOrganizationRequest) => Promise<Organization>;
	updateOrganization: (id: string, data: UpdateOrganizationRequest) => Promise<Organization>;
	uploadLogo: (id: string, file: File) => Promise<{ logoUrl: string }>;
}

// =============================================================================
// CONTEXT
// =============================================================================

const OrganizationContext = createContext<OrganizationContextType | undefined>(undefined);

// =============================================================================
// PROVIDER
// =============================================================================

// US-AUTH-06
export const OrganizationProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
	const { isAuthenticated, isBootstrapped, syncUserData } = use(AuthContext);

	// ---------------------------------------------------------------------------
	// STATE
	// Only use useState for values that should trigger re-renders when changed
	// ---------------------------------------------------------------------------
	const [selectedOrganization, setSelectedOrganization] = useState<Organization | null>(null);
	const [organizations, setOrganizations] = useState<Organization[]>([]);
	const [isLoading, setIsLoading] = useState(false);
	const [isInitialized, setIsInitialized] = useState(false);
	const [error, setError] = useState<ApiProblem | null>(null);

	// ---------------------------------------------------------------------------
	// REFS
	// Use refs for internal tracking that should NOT cause re-renders or
	// invalidate useCallback dependencies. This is crucial to prevent infinite loops.
	// ---------------------------------------------------------------------------

	/**
	 * Tracks whether the initial fetch has been attempted.
	 * Using a ref prevents the callback from being recreated when this changes,
	 * which would cause the useEffect to re-run and create an infinite loop.
	 */
	const hasFetchedRef = useRef(false);

	/**
	 * Tracks whether a fetch is currently in progress.
	 * This prevents parallel/duplicate API calls without causing re-renders.
	 */
	const isFetchingRef = useRef(false);

	/**
	 * Stores the current fetch promise for request deduplication.
	 * If multiple components call fetchOrganizations() simultaneously,
	 * they will all receive the same promise instead of triggering multiple API calls.
	 */
	const fetchPromiseRef = useRef<Promise<Organization[]> | null>(null);

	// ---------------------------------------------------------------------------
	// ORGANIZATION CHANGED EVENT
	// Notify other parts of the app when the selected organization changes
	// Also persist to localStorage
	// ---------------------------------------------------------------------------
	useEffect(() => {
		if (selectedOrganization) {
			// Dispatch event for other components
			window.dispatchEvent(
				new CustomEvent('organization-changed', {
					detail: { organizationId: selectedOrganization.id },
				}),
			);

			// Persist to localStorage
			try {
				localStorage.setItem('selectedOrganizationId', selectedOrganization.id);
			} catch {
				// Storage may be blocked; the selection then lasts for this page only
			}
		}
	}, [selectedOrganization]);

	// ---------------------------------------------------------------------------
	// SYNC FEATURES ON ORGANIZATION CHANGE
	// Re-sync user data when organization changes to update organization-specific features.
	// The auth sync response includes features scoped to the current organization.
	// ---------------------------------------------------------------------------
	const prevOrgIdRef = useRef<string | null>(null);
	useEffect(() => {
		const currentOrgId = selectedOrganization?.id;

		// Skip initial mount (no previous org yet)
		if (prevOrgIdRef.current === null && currentOrgId) {
			prevOrgIdRef.current = currentOrgId;
			return;
		}

		// Organization changed (not just initial load)
		if (currentOrgId && prevOrgIdRef.current !== currentOrgId && syncUserData) {
			syncUserData(true, currentOrgId);
			prevOrgIdRef.current = currentOrgId;
		}
	}, [selectedOrganization?.id, syncUserData]);

	// ---------------------------------------------------------------------------
	// FETCH ORGANIZATIONS
	// Core function to load organizations from the API.
	// Uses refs for guards to ensure the callback reference is stable.
	// ---------------------------------------------------------------------------
	const fetchOrganizations = useCallback(async (): Promise<Organization[]> => {
		// If a fetch is already in progress, return the existing promise
		// This prevents duplicate API calls when multiple components request data
		if (fetchPromiseRef.current) {
			return fetchPromiseRef.current;
		}

		// Guard against concurrent calls using ref (does not cause re-render)
		if (isFetchingRef.current) {
			return [];
		}

		// Mark as fetching BEFORE any async operations
		isFetchingRef.current = true;
		hasFetchedRef.current = true;
		setIsLoading(true);
		setError(null);

		// Create and store the fetch promise for deduplication
		fetchPromiseRef.current = (async () => {
			try {
				const data = await organizationService.getOrganizations();

				// Update state with fetched data
				setOrganizations(data);

				// Auto-select organization: try localStorage first, then fall back to first org
				// Using functional update to avoid dependency on current selectedOrganization
				setSelectedOrganization((current) => {
					// If already selected, keep it (unless it's not in the new list)
					if (current && data.some((org) => org.id === current.id)) {
						return current;
					}

					// Try to restore from localStorage
					try {
						const savedOrgId = localStorage.getItem('selectedOrganizationId');
						if (savedOrgId) {
							const savedOrg = data.find((org) => org.id === savedOrgId);
							if (savedOrg) {
								return savedOrg;
							}
						}
					} catch {
						// Storage may be blocked; fall through to the first organization
					}

					// Fall back to first organization
					return data.length > 0 ? data[0] : null;
				});
				return data;
			} catch (err: unknown) {
				setError(toApiProblem(err));
				throw err;
			} finally {
				// Always clean up refs and loading state
				isFetchingRef.current = false;
				fetchPromiseRef.current = null;
				setIsLoading(false);
				// Mark as initialized - this tells App.tsx it's safe to make routing decisions
				setIsInitialized(true);
			}
		})();

		return fetchPromiseRef.current;
	}, []); // NO DEPENDENCIES - this callback is completely stable

	// ---------------------------------------------------------------------------
	// AUTO-FETCH ON MOUNT
	// Automatically fetch organizations when an authenticated user is detected.
	// Uses hasFetchedRef to ensure this only happens once.
	// **CRITICAL**: Waits for isBootstrapped to ensure access token is refreshed.
	// ---------------------------------------------------------------------------
	useEffect(() => {
		// Only fetch for authenticated users
		if (!isAuthenticated) {
			// Not authenticated: mark as initialized immediately
			setIsInitialized(true);
			return;
		}

		// **CRITICAL FIX**: Wait for authentication to be fully bootstrapped
		// This ensures the access token is properly refreshed before making API calls
		if (!isBootstrapped) {
			return;
		}

		// Only fetch once - use ref to track, not state
		if (hasFetchedRef.current) {
			return;
		}

		fetchOrganizations().catch(() => {
			// Error is already handled in fetchOrganizations
			// We catch here to prevent unhandled promise rejection
		});
	}, [isAuthenticated, isBootstrapped, fetchOrganizations]);

	// ---------------------------------------------------------------------------
	// LOGOUT CLEANUP
	// Clear selected organization and localStorage when the user logs out
	// ---------------------------------------------------------------------------
	useEffect(() => {
		if (!isAuthenticated) {
			// User logged out
			setSelectedOrganization(null);
			setOrganizations([]);
			hasFetchedRef.current = false;
			setIsInitialized(true); // Mark as initialized when logged out

			// Clear localStorage
			try {
				localStorage.removeItem('selectedOrganizationId');
			} catch {
				// Storage may be blocked; nothing to clear
			}
		} else {
			// User is authenticated - reset initialization flag if we haven't fetched yet
			// This handles the case where the user logs in
			if (!hasFetchedRef.current) {
				setIsInitialized(false);
			}
		}
	}, [isAuthenticated]);

	// ---------------------------------------------------------------------------
	// TOKEN REFRESH RETRY
	// When authentication token is refreshed after an error, retry the fetch.
	// This handles the case where initial fetch failed due to expired token.
	// ---------------------------------------------------------------------------
	useEffect(() => {
		const handleTokenRefreshed = () => {
			// Only retry if the previous attempt failed
			if (error && !isFetchingRef.current) {
				// Reset the flag to allow a new fetch attempt
				hasFetchedRef.current = false;
				setError(null);
				fetchOrganizations().catch(() => {
					// Error handled in fetchOrganizations
				});
			}
		};

		window.addEventListener('auth-token-refreshed', handleTokenRefreshed);
		return () => {
			window.removeEventListener('auth-token-refreshed', handleTokenRefreshed);
		};
	}, [error, fetchOrganizations]);

	// ---------------------------------------------------------------------------
	// AUTH SYNC ORGANIZATION UPDATE
	// When auth sync returns organizations (e.g., after onboarding), refetch
	// the full organization data to update the context.
	// This handles the case where a new organization is created during onboarding.
	// ---------------------------------------------------------------------------
	useEffect(() => {
		const handleAuthSync = (event: Event) => {
			const customEvent = event as CustomEvent;
			const { organizations: authOrgs } = customEvent.detail || {};

			// Only refetch if authenticated and received organizations
			if (!isAuthenticated || !authOrgs?.length) {
				return;
			}

			// Check if we have new organizations that aren't in our current list
			const hasNewOrgs = authOrgs.some(
				(authOrg: any) => !organizations.find((org) => org.id === authOrg.organizationId),
			);

			if (hasNewOrgs && !isFetchingRef.current) {
				fetchOrganizations().catch(() => {
					// Error handled in fetchOrganizations
				});
			}
		};

		window.addEventListener('auth-features-synced', handleAuthSync);
		return () => {
			window.removeEventListener('auth-features-synced', handleAuthSync);
		};
	}, [isAuthenticated, organizations, fetchOrganizations]);

	// ---------------------------------------------------------------------------
	// CREATE ORGANIZATION
	// Creates a new organization and updates local state.
	// ---------------------------------------------------------------------------
	const createOrganization = useCallback(
		async (data: CreateOrganizationRequest): Promise<Organization> => {
			const newOrg = await organizationService.createOrganization(data);

			// Add to organizations list using functional update
			setOrganizations((prevOrgs) => [...prevOrgs, newOrg]);

			// Auto-select the newly created organization
			setSelectedOrganization(newOrg);

			return newOrg;
		},
		[],
	); // No dependencies - uses functional state updates

	// ---------------------------------------------------------------------------
	// UPDATE ORGANIZATION
	// Updates an existing organization and syncs local state.
	// ---------------------------------------------------------------------------
	const updateOrganization = useCallback(
		async (id: string, data: UpdateOrganizationRequest): Promise<Organization> => {
			const updatedOrg = await organizationService.updateOrganization(id, data);

			// Update in organizations list using functional update
			setOrganizations((prevOrgs) =>
				prevOrgs.map((org) => (org.id === id ? updatedOrg : org)),
			);

			// Update selected organization if it was the one being updated
			setSelectedOrganization((current) => (current?.id === id ? updatedOrg : current));

			return updatedOrg;
		},
		[],
	); // No dependencies - uses functional state updates

	// ---------------------------------------------------------------------------
	// UPLOAD LOGO
	// Uploads organization logo and updates local state with new URL.
	// ---------------------------------------------------------------------------
	const uploadLogo = useCallback(async (id: string, file: File): Promise<{ logoUrl: string }> => {
		const result = await organizationService.uploadLogo(id, file);

		// Update logo URL in organizations list
		setOrganizations((prevOrgs) =>
			prevOrgs.map((org) => (org.id === id ? { ...org, logoUrl: result.logoUrl } : org)),
		);

		// Update selected organization if it was the one with new logo
		setSelectedOrganization((current) =>
			current?.id === id ? { ...current, logoUrl: result.logoUrl } : current,
		);

		return result;
	}, []); // No dependencies - uses functional state updates

	// ---------------------------------------------------------------------------
	// RENDER
	// ---------------------------------------------------------------------------
	return (
		<OrganizationContext.Provider
			value={{
				selectedOrganization,
				setSelectedOrganization,
				organizations,
				isLoading,
				isInitialized,
				error,
				fetchOrganizations,
				createOrganization,
				updateOrganization,
				uploadLogo,
			}}>
			{children}
		</OrganizationContext.Provider>
	);
};

// =============================================================================
// HOOK
// =============================================================================

export const useOrganization = () => {
	const context = use(OrganizationContext);
	if (context === undefined) {
		throw new Error('useOrganization must be used within an OrganizationProvider');
	}
	return context;
};

export default OrganizationContext;
