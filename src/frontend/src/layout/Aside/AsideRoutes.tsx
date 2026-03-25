import React from 'react';
import { Route, Routes } from 'react-router-dom';
import asides from '../../routes/asideRoutes';

const AsideRoutes = () => {
	return (
		<Routes>
			{asides.map((page) => (
				<Route key={page.path} {...page} />
			))}
		</Routes>
	);
};

export default AsideRoutes;
