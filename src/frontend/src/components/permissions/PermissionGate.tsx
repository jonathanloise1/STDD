/** @userstory US-TEAM-08 */
import React, { ReactNode } from 'react';
import { usePermissions } from '../../contexts/permissionContext';
import { PermissionCode } from '../../common/constants/permissionCodes';

interface PermissionGateProps {
	/** Content to render when authorized */
	children: ReactNode;
	/** Single permission to check */
	permission?: PermissionCode;
	/** Multiple permissions to check */
	permissions?: PermissionCode[];
	/** If true, require all permissions. If false (default), require any permission */
	requireAll?: boolean;
	/** If true, only allow Admin users */
	requireAdmin?: boolean;
	/** Content to render when not authorized */
	fallback?: ReactNode;
}

/**
 * Conditionally renders children based on user permissions.
 * Admin users always pass permission checks.
 *
 * @example
 * // Single permission
 * <PermissionGate permission="talents">
 *   <TalentsPage />
 * </PermissionGate>
 *
 * @example
 * // Multiple permissions (any)
 * <PermissionGate permissions={['talents', 'projects']}>
 *   <SharedComponent />
 * </PermissionGate>
 *
 * @example
 * // Multiple permissions (all required)
 * <PermissionGate permissions={['quotes', 'finance']} requireAll>
 *   <ContractFinanceView />
 * </PermissionGate>
 *
 * @example
 * // Admin only
 * <PermissionGate requireAdmin fallback={<AccessDenied />}>
 *   <TeamManagement />
 * </PermissionGate>
 */
const PermissionGate: React.FC<PermissionGateProps> = ({
	children,
	permission,
	permissions,
	requireAll = false,
	requireAdmin = false,
	fallback = null,
}) => {
	const { isAdmin, hasPermission, hasAnyPermission, hasAllPermissions } = usePermissions();

	// Admin-only gate
	if (requireAdmin) {
		return isAdmin ? <>{children}</> : <>{fallback}</>;
	}

	// Single permission check
	if (permission) {
		return hasPermission(permission) ? <>{children}</> : <>{fallback}</>;
	}

	// Multiple permissions check
	if (permissions && permissions.length > 0) {
		const hasAccess = requireAll
			? hasAllPermissions(permissions)
			: hasAnyPermission(permissions);
		return hasAccess ? <>{children}</> : <>{fallback}</>;
	}

	// No permission specified, render children
	return <>{children}</>;
};

export default PermissionGate;
