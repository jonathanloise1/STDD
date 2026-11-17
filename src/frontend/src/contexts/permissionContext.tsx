import React, {
	createContext,
	use,
	useMemo,
	useCallback,
	useState,
	useEffect,
	ReactNode,
} from 'react';
import type { OrganizationRole } from '../common/constants/organizationRoles';
import { OrganizationMembership } from '../type/organization-types';

/**
 * Permission context value interface.
 * Access is derived from the user's role in the current organization
 * (Admin, Editor or Viewer), as returned by the backend.
 */
export interface PermissionContextValue {
	/** Current organization membership */
	membership: OrganizationMembership | null;
	/** Current organization ID */
	currentOrganizationId: string | null;
	/** User's role in current organization */
	role: OrganizationRole | null;
	/** Whether user is Admin (full access, including settings, team and audit log) */
	isAdmin: boolean;
	/** Whether user can create, edit and delete data (Admin or Editor) */
	canEdit: boolean;
	/** Whether permissions are loading */
	isLoading: boolean;

	// Actions
	/** Set current organization ID */
	setCurrentOrganizationId: (id: string | null) => void;
	/** Update memberships from auth sync */
	setMemberships: (memberships: OrganizationMembership[]) => void;
	/** Clear permissions (on logout) */
	clearPermissions: () => void;
}

const defaultContextValue: PermissionContextValue = {
	membership: null,
	currentOrganizationId: null,
	role: null,
	isAdmin: false,
	canEdit: false,
	isLoading: true,
	setCurrentOrganizationId: () => {},
	setMemberships: () => {},
	clearPermissions: () => {},
};

export const PermissionContext = createContext<PermissionContextValue>(defaultContextValue);

interface PermissionProviderProps {
	children: ReactNode;
}

/**
 * Provider component for permission context.
 * Listens for the 'auth-features-synced' custom event dispatched by AuthContextProvider
 * after a successful POST /api/auth/sync call. Receives organization memberships
 * (organization and role) and makes them available via usePermissions().
 * Auto-selects the first organization if none is currently selected.
 */
// US-AUTH-07
export const PermissionProvider: React.FC<PermissionProviderProps> = ({ children }) => {
	const [memberships, setMembershipsState] = useState<OrganizationMembership[]>([]);
	const [currentOrganizationId, setCurrentOrganizationId] = useState<string | null>(null);
	const [isLoading, setIsLoading] = useState(true);

	// Listen for auth sync events to update memberships
	useEffect(() => {
		const handleAuthSync = (
			event: CustomEvent<{ organizations?: OrganizationMembership[] }>,
		) => {
			if (event.detail.organizations) {
				setMembershipsState(event.detail.organizations);
				setIsLoading(false);

				// Auto-select first organization if none selected
				if (!currentOrganizationId && event.detail.organizations.length > 0) {
					setCurrentOrganizationId(event.detail.organizations[0].organizationId);
				}
			}
		};

		window.addEventListener('auth-features-synced', handleAuthSync as EventListener);

		return () => {
			window.removeEventListener('auth-features-synced', handleAuthSync as EventListener);
		};
	}, [currentOrganizationId]);

	// Also listen for organization changes from organization context
	useEffect(() => {
		const handleOrgChange = (event: CustomEvent<{ organizationId: string }>) => {
			if (event.detail.organizationId) {
				setCurrentOrganizationId(event.detail.organizationId);
			}
		};

		window.addEventListener('organization-changed', handleOrgChange as EventListener);

		return () => {
			window.removeEventListener('organization-changed', handleOrgChange as EventListener);
		};
	}, []);

	// Get current membership based on selected organization
	const membership = useMemo(() => {
		if (!currentOrganizationId) return null;
		return memberships.find((m) => m.organizationId === currentOrganizationId) || null;
	}, [currentOrganizationId, memberships]);

	// Derived values
	const role = membership?.role ?? null;
	const isAdmin = role === 'Admin';
	const canEdit = role === 'Admin' || role === 'Editor';

	// Actions
	const setMemberships = useCallback((newMemberships: OrganizationMembership[]) => {
		setMembershipsState(newMemberships);
		setIsLoading(false);
	}, []);

	const clearPermissions = useCallback(() => {
		setMembershipsState([]);
		setCurrentOrganizationId(null);
		setIsLoading(true);
	}, []);

	const value: PermissionContextValue = useMemo(
		() => ({
			membership,
			currentOrganizationId,
			role,
			isAdmin,
			canEdit,
			isLoading,
			setCurrentOrganizationId,
			setMemberships,
			clearPermissions,
		}),
		[
			membership,
			currentOrganizationId,
			role,
			isAdmin,
			canEdit,
			isLoading,
			setMemberships,
			clearPermissions,
		],
	);

	return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
};

/**
 * Hook to access permission context
 */
export const usePermissions = (): PermissionContextValue => {
	const context = use(PermissionContext);
	if (!context) {
		throw new Error('usePermissions must be used within a PermissionProvider');
	}
	return context;
};

/**
 * Hook to check if user is Admin
 */
export const useIsAdmin = (): boolean => {
	const { isAdmin } = usePermissions();
	return isAdmin;
};

export default PermissionContext;
