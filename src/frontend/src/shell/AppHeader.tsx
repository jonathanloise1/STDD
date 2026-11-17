import React from 'react';
import { HelpButton } from '../help/components/HelpButton';
import LanguageMenu from './LanguageMenu';
import OrganizationSwitcher from './OrganizationSwitcher';
import UserMenu from './UserMenu';

/**
 * AppHeader: the one top bar of every page.
 * Left: the organization name, or the switcher when the user belongs to several.
 * Right: Help, the language button and the avatar button with its menu.
 */
// US-AUTH-01, US-AUTH-03, US-AUTH-06, US-AUTH-08
const AppHeader = () => (
	<header className='app-header'>
		<div className='app-header__inner'>
			<div className='app-header__org'>
				<OrganizationSwitcher />
			</div>

			<div className='app-header__actions'>
				{/* US-HELP-03 */}
				<HelpButton className='app-header__icon-btn' showLabel={false} />
				<LanguageMenu />
				<UserMenu />
			</div>
		</div>
	</header>
);

export default AppHeader;
