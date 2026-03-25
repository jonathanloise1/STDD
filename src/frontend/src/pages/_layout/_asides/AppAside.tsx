import React from 'react';
import { SlimSidebar, BottomTabBar } from '../../../components/app';

/**
 * App aside — replaces the old Facit DefaultAside.
 * Renders SlimSidebar (desktop, md+) and BottomTabBar (mobile, <md).
 * Both components handle their own fixed positioning.
 *
 * @userstory US-MENU-01
 */
const AppAside = () => {
	return (
		<>
			<SlimSidebar />
			<BottomTabBar />
		</>
	);
};

export default AppAside;
