/**
 * @userstory US-MENU-06
 *
 * Route guard that restricts access to Admin-only areas.
 * Members navigating to protected routes (e.g. /settings/*)
 * are silently redirected to /dashboard (BR-MENU-07).
 */
import React, { FC, ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { usePermissions } from '../../contexts/permissionContext';

interface IAdminProtectedRouteProps {
	children: ReactNode;
	/** Where to redirect non-admin users. Defaults to /dashboard */
	redirectPath?: string;
}

/** @userstory US-MENU-06 */
const AdminProtectedRoute: FC<IAdminProtectedRouteProps> = ({
	children,
	redirectPath = '/dashboard',
}) => {
	const { isAdmin } = usePermissions();

	// BR-MENU-07: Non-admin users are silently redirected
	if (!isAdmin) {
		return <Navigate to={redirectPath} replace />;
	}

	return <>{children}</>;
};

export default AdminProtectedRoute;
