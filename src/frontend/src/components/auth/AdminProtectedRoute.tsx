import React, { FC, ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { usePermissions } from '../../contexts/permissionContext';
import { Loading } from '../../ui';

interface IAdminProtectedRouteProps {
	children: ReactNode;
}

/**
 * Route guard for the pages only Admins may open (Team, Audit Log, Organization,
 * Update Organization). Anyone else is taken to the Dashboard page, which shows
 * the message "You don't have access to this page."
 * The role that counts is the one in the organization selected in the header.
 */
// US-AUTH-07
const AdminProtectedRoute: FC<IAdminProtectedRouteProps> = ({ children }) => {
	const { isAdmin, isLoading } = usePermissions();

	// The memberships are not loaded yet: do not decide before they are
	if (isLoading) {
		return <Loading />;
	}

	// Loaded, and no Admin role in the selected organization (a member without a role there counts as not
	// allowed): never wait for a role that will not come
	if (!isAdmin) {
		return <Navigate to='/dashboard' replace state={{ accessDenied: true }} />;
	}

	return <>{children}</>;
};

export default AdminProtectedRoute;
