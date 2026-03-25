import React, { lazy, use, useEffect, useState, Suspense } from 'react';
import { Route, Routes, Navigate, useSearchParams } from 'react-router-dom';
import contents from '../../routes/contentRoutes';
import AuthContext from '../../contexts/authContext';
import Loading from '../../components/Loading';

const PAGE_404 = lazy(() => import('../../pages/presentation/auth/Page404'));

const ContentRoutes = () => {
	const { isAuthenticated, isLoading, isBootstrapped } = use(AuthContext);

	// Block routing until bootstrap is complete
	if (isLoading || !isBootstrapped) {
		return <div id='app-loading'><Loading /></div>;
	}

	return (
		<Suspense fallback={<Loading />}>
			<Routes>
				{/* Root path - let App/App.tsx handle redirection with proper organization checks */}
				<Route 
					path='/' 
					element={
						<>
							{console.log('[ContentRoutes] Rendering root path (/), showing Loading')}
							<Loading />
						</>
					} 
				/>
				<Route path='/login' element={<Navigate to='/' replace />} />
				<Route path='/auth/login' element={<Navigate to='/' replace />} />
				<Route path='/auth-pages/login' element={<Navigate to='/' replace />} />

				{/* Content routes */}
				{contents.map((page) => {
					if (!page.path) return null;
					return <Route key={page.path} {...page} />;
				})}

				{/* 404 */}
				<Route path='/auth-pages/404' element={<PAGE_404 />} />
				<Route path='*' element={<Navigate to='/auth-pages/404' replace />} />
			</Routes>
		</Suspense>
	);
};

export default ContentRoutes;
