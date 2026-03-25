import React from 'react';
import { Route, Routes } from 'react-router-dom';
import footers from '../../routes/footerRoutes';

const FooterRoutes = () => {
	return (
		<Routes>
			{footers.map((page) => (
				<Route key={page.path} {...page} />
			))}
		</Routes>
	);
};

export default FooterRoutes;
