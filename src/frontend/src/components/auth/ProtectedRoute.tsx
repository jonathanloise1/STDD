import React, { FC, ReactNode, use } from 'react';
import { Navigate } from 'react-router-dom';
import AuthContext from '../../contexts/authContext';
import Loading from '../Loading';

interface IProtectedRouteProps {
	children: ReactNode;
	redirectPath?: string;
}

const ProtectedRoute: FC<IProtectedRouteProps> = ({ children, redirectPath = '/' }) => {
	const { isAuthenticated, isLoading, isInitialized, isBootstrapped } = use(AuthContext);

	// Show loading indicator while authentication state is being determined
	if (!isInitialized || isLoading || !isBootstrapped) return <Loading />;

	// Redirect to login page if not authenticated
	if (!isAuthenticated) {
		return <Navigate to={redirectPath} state={{ from: location }} replace />;
	}

	// If authenticated, render the protected content
	return <>{children}</>;
};

export default ProtectedRoute;
