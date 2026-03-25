import React from 'react';
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Icon from '../../components/icon/Icon';

// US-MENU-05: Settings menu configuration — all flat direct links (no sub-menus)
// Master Agreement permanently removed.
const settingsMenuConfig = {
	organization: {
		id: 'organization',
		text: 'Organization',
		path: 'settings/organization',
		icon: 'Business',
	},
	team: {
		id: 'team',
		text: 'Team',
		path: 'settings/team',
		icon: 'Groups',
	},
};

interface SettingsSidebarProps {
	className?: string;
}

const SettingsSidebar: React.FC<SettingsSidebarProps> = ({ className = '' }) => {
	const { t } = useTranslation('menu');

	// US-MENU-05: All items are flat direct links
	const renderItem = (sectionKey: string, section: any) => {
		return (
			<NavLink
				key={sectionKey}
				to={`/${section.path}`}
				className={({ isActive }) =>
					`nav-link px-3 py-2 d-flex align-items-center ${isActive ? 'active bg-primary-subtle text-primary fw-semibold' : 'text-dark'}`
				}>
				{section.icon && (
					<Icon icon={section.icon} className='me-2' size='lg' />
				)}
				<span className='fw-semibold'>
					{String(t(`settings.${sectionKey}`, section.text))}
				</span>
			</NavLink>
		);
	};

	return (
		<aside
			className={`settings-sidebar bg-white border-end ${className}`}
			style={{ width: 260, minHeight: 'calc(100vh - 56px)' }}>
			<div className='py-4'>
				<nav className='nav flex-column'>
					{Object.entries(settingsMenuConfig).map(([key, section]) =>
						renderItem(key, section),
					)}
				</nav>
			</div>
		</aside>
	);
};

export default SettingsSidebar;
