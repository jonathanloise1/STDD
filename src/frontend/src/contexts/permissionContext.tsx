import React, {
	createContext,
	use,
	useMemo,
	useCallback,
	useState,
	useEffect,
	ReactNode,
} from 'react';
import { PermissionCode, OrganizationRole } from '../common/constants/permissionCodes';
import { OrganizationMembership } from '../type/organization-types';

/** @userstory US-MENU-02, US-MENU-06, US-TEAM-08, US-TEAM-09 */

/**
 * Permission context value interface
 */
export interface PermissionContextValue {
	/** Current organization membership */
	membership: OrganizationMembership | null;
	/** Current organization ID */
	currentOrganizationId: string | null;
	/** User's role in current organization */
	role: OrganizationRole | null;
	/** Whether user is Admin */
	isAdmin: boolean;
	/** Whether user is Member */
	isMember: boolean;
	/** User's permissions array */
	permissions: PermissionCode[];
	/** Whether user has signing authority */
	hasSigningAuthority: boolean;
	/** Whether permissions are loading */
	isLoading: boolean;

	// Permission check methods
	/** Check if user has a specific permission */
	hasPermission: (permission: PermissionCode) => boolean;
	/** Check if user has any of the given permissions */
	hasAnyPermission: (permissions: PermissionCode[]) => boolean;
	/** Check if user has all of the given permissions */
	hasAllPermissions: (permissions: PermissionCode[]) => boolean;
	/** Check if user can sign contracts (has contracts permission + signing authority) */
	canSign: () => boolean;

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
	isMember: false,
	permissions: [],
	hasSigningAuthority: false,
	isLoading: true,
	hasPermission: () => false,
	hasAnyPermission: () => false,
	hasAllPermissions: () => false,
	canSign: () => false,
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
 * (roles, permissions, signing authority) and makes them available via usePermissions().
 * Auto-selects the first organization if none is currently selected.
 * @userstory US-AUTH-04
 */
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
					console.log(
						'[PermissionContext] Auto-selected org:',
						event.detail.organizations[0].organizationId,
					);
				}
			} else {
				console.log('[PermissionContext] No organizations in event detail');
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
	const isMember = role === 'Member';
	const permissions = membership?.permissions ?? [];
	const hasSigningAuthority = membership?.hasSigningAuthority ?? false;

	// Permission check methods
	const hasPermission = useCallback(
		(permission: PermissionCode): boolean => {
			if (!membership) return false;
			if (isAdmin) return true;
			return permissions.includes(permission);
		},
		[membership, isAdmin, permissions],
	);

	const hasAnyPermission = useCallback(
		(perms: PermissionCode[]): boolean => {
			if (!membership) return false;
			if (isAdmin) return true;
			return perms.some((p) => permissions.includes(p));
		},
		[membership, isAdmin, permissions],
	);

	const hasAllPermissions = useCallback(
		(perms: PermissionCode[]): boolean => {
			if (!membership) return false;
			if (isAdmin) return true;
			return perms.every((p) => permissions.includes(p));
		},
		[membership, isAdmin, permissions],
	);

	const canSign = useCallback((): boolean => {
		if (!membership) return false;
		return hasPermission('quotes' as PermissionCode) && hasSigningAuthority;
	}, [membership, hasPermission, hasSigningAuthority]);

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
			isMember,
			permissions,
			hasSigningAuthority,
			isLoading,
			hasPermission,
			hasAnyPermission,
			hasAllPermissions,
			canSign,
			setCurrentOrganizationId,
			setMemberships,
			clearPermissions,
		}),
		[
			membership,
			currentOrganizationId,
			role,
			isAdmin,
			isMember,
			permissions,
			hasSigningAuthority,
			isLoading,
			hasPermission,
			hasAnyPermission,
			hasAllPermissions,
			canSign,
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
 * Hook to check a single permission
 */
export const useHasPermission = (permission: PermissionCode): boolean => {
	const { hasPermission } = usePermissions();
	return hasPermission(permission);
};

/**
 * Hook to check if user is Admin
 */
export const useIsAdmin = (): boolean => {
	const { isAdmin } = usePermissions();
	return isAdmin;
};

/**
 * Hook to check if user can sign contracts
 */
export const useCanSign = (): boolean => {
	const { canSign } = usePermissions();
	return canSign();
};

export default PermissionContext;
