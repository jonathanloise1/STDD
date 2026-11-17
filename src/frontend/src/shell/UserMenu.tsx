import React, { use } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Menu } from '../ui';
import AuthContext from '../contexts/authContext';
import { usePermissions } from '../contexts/permissionContext';

/** The avatar button of the header and its menu: Settings (Admins) and Logout. */
// US-AUTH-01, US-AUTH-03
const UserMenu = () => {
	const { t } = useTranslation('menu');
	const { userData, logout } = use(AuthContext);
	const { isAdmin } = usePermissions();
	const navigate = useNavigate();

	const userName =
		`${userData?.name || ''} ${userData?.surname || ''}`.trim() || userData?.email || '';
	const initials = userName
		.split(' ')
		.filter(Boolean)
		.map((w) => w[0])
		.join('')
		.substring(0, 2)
		.toUpperCase();

	return (
		<Menu align='end'>
			<Menu.Toggle className='app-header__avatar' aria-label={userName || t('menu.user')}>
				<span className='app-header__avatar-initials'>{initials}</span>
			</Menu.Toggle>
			<Menu.Items>
				{isAdmin && (
					<Menu.Item icon='Settings' onSelect={() => navigate('/settings/organization')}>
						{t('Settings')}
					</Menu.Item>
				)}
				<Menu.Item icon='Logout' data-testid='btn-logout' onSelect={() => logout()}>
					{t('Logout')}
				</Menu.Item>
			</Menu.Items>
		</Menu>
	);
};

export default UserMenu;
