import React from 'react';
import { Navigate } from 'react-router-dom';
import { usePermissions } from '../../contexts/permissionContext';
import SettingsHeader from './SettingsHeader';
import SettingsSidebar from './SettingsSidebar';

// US: US-MENU-06

interface SettingsLayoutProps {
	children: React.ReactNode;
}

const SettingsLayout: React.FC<SettingsLayoutProps> = ({ children }) => {
	const { isAdmin } = usePermissions();

	// Only Admin users can access settings
	if (!isAdmin) {
		return <Navigate to='/dashboard' replace />;
	}

	return (
		<div className='settings-layout d-flex flex-column vh-100'>
			<SettingsHeader />
			<div className='d-flex flex-grow-1'>
				<SettingsSidebar />
				<main className='settings-content flex-grow-1 p-4 overflow-auto bg-light'>
					{children}
				</main>
			</div>
		</div>
	);
};

export default SettingsLayout;
