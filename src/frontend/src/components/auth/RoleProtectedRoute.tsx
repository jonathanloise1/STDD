import React, { FC, ReactNode, use } from 'react';
import { Navigate } from 'react-router-dom';
import AuthContext from '../../contexts/authContext';
import { UserType } from '../../common/data/dummyRolesData';
import Loading from '../Loading';

interface IRoleProtectedRouteProps {
	children: ReactNode;
	allowedRoles?: UserType[];
	unauthorizedComponent?: ReactNode;
}

const RoleProtectedRoute: FC<IRoleProtectedRouteProps> = ({
	children,
	allowedRoles,
	unauthorizedComponent,
}) => {
	const { isAuthenticated, isLoading, userData } = use(AuthContext);
	const userRole = userData?.role;

	// Show loading indicator while authentication state is being determined
	if (isLoading) {
		return <Loading />;
	}

	// Redirect to login page if not authenticated
	if (!isAuthenticated) {
		return <Navigate to='/' replace />;
	}

	// Check explicit allowedRoles for specific route components
	if (userRole && allowedRoles && !allowedRoles.some((role) => role === userRole)) {
		if (unauthorizedComponent) {
			return <>{unauthorizedComponent}</>;
		}
		return <Navigate to='/dashboard' replace />;
	}

	return <>{children}</>;
};

export default RoleProtectedRoute;
