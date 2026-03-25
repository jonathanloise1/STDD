import React, { useLayoutEffect, use } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Icon from '../../../components/icon/Icon';
import Button from '../../../components/bootstrap/Button';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../components/bootstrap/Dropdown';
import Spinner from '../../../components/bootstrap/Spinner';
import LANG, { getLangWithKey, ILang } from '../../../lang';
import showNotification from '../../../components/extras/showNotification';
import AuthContext from '../../../contexts/authContext';
import userProfileService from '../../../services/api/userProfileService';

/**
 * AppHeader — Top bar for the application.
 *
 * Mobile:  🛡️ MyApp                       👤   (52px)
 * Desktop: 🛡️ MyApp   🔍  🌐DE            👤  (56px)
 */
const AppHeader = () => {
	const { i18n, t } = useTranslation(['menu', 'settings', 'common']);
	const { userData, logout } = use(AuthContext);
	const navigate = useNavigate();

	/**
	 * US-AUTH-08: Switches the application language, persists to backend,
	 * and shows a confirmation notification.
	 */
	const changeLanguage = (lng: ILang['key']['lng']) => {
		i18n.changeLanguage(lng).then(() => {
			const lang = getLangWithKey(lng);
			showNotification(
				t('settings:languageChanged', { language: lang?.text }),
				t('settings:languageUpdated'),
				'success',
				<Icon icon={lang?.icon} size='lg' />,
			);
			// US-AUTH-08: Persist language preference to backend
			userProfileService.updateLanguage(lng).catch((err) => {
				console.warn('[Language] Failed to persist language preference:', err);
			});
		});
	};

	useLayoutEffect(() => {
		document.documentElement.setAttribute('lang', i18n.language.substring(0, 2));
	});

	const userName =
		`${userData?.name || ''} ${userData?.surname || ''}`.trim() ||
		userData?.email ||
		'User';
	const initials = userName
		.split(' ')
		.map((w) => w[0])
		.join('')
		.substring(0, 2)
		.toUpperCase();

	return (
		<header className='app-header'>
			<div className='app-header__inner'>
				{/* Right — actions */}
				<div className='app-header__actions'>
					{/* Search — desktop only */}
					<button
						type='button'
						className='app-header__icon-btn d-none d-md-flex'
						aria-label={t('common:Search') || 'Search'}
						onClick={() => {
							/* TODO: open search overlay */
						}}>
						<Icon icon='Search' size='lg' />
					</button>

					{/* Language — desktop only */}
					<div className='d-none d-md-flex'>
						<Dropdown>
							<DropdownToggle hasIcon={false}>
								{typeof getLangWithKey(
									i18n.language as ILang['key']['lng'],
								)?.icon === 'undefined' ? (
									<button
										type='button'
										className='app-header__icon-btn'
										id='btn-language-selector'
										aria-label='Change language'>
										<Spinner isSmall inButton='onlyIcon' isGrow />
									</button>
								) : (
									<button
										type='button'
										className='app-header__icon-btn'
										id='btn-language-selector'
										aria-label='Change language'>
										<Icon icon='Language' size='lg' />
										<span className='app-header__lang-code'>
											{i18n.language.substring(0, 2).toUpperCase()}
										</span>
									</button>
								)}
							</DropdownToggle>
							<DropdownMenu isAlignmentEnd>
								{Object.keys(LANG).map((i) => (
									<DropdownItem key={LANG[i].lng}>
										<Button
											icon={LANG[i].icon}
											id={`btn-lang-${LANG[i].lng}`}
											onClick={() => changeLanguage(LANG[i].lng)}>
											{LANG[i].text}
										</Button>
									</DropdownItem>
								))}
							</DropdownMenu>
						</Dropdown>
					</div>

					{/* User avatar */}
					<Dropdown>
						<DropdownToggle hasIcon={false}>
							<button
								type='button'
								className='app-header__avatar'
								aria-label={userName}>
								{userData?.src ? (
									<img
										src={userData.src}
										srcSet={userData.srcSet}
										alt={userName}
										className='app-header__avatar-img'
									/>
								) : (
									<span className='app-header__avatar-initials'>
										{initials}
									</span>
								)}
							</button>
						</DropdownToggle>
						<DropdownMenu isAlignmentEnd>
							<DropdownItem>
								<Button icon='Settings' onClick={() => navigate('/settings')}>
									{t('menu:Settings')}
								</Button>
							</DropdownItem>
							<DropdownItem isDivider />
							<DropdownItem>
								<Button icon='Logout' onClick={() => logout()}>
									{t('menu:Logout')}
								</Button>
							</DropdownItem>
						</DropdownMenu>
					</Dropdown>
				</div>
			</div>
		</header>
	);
};

export default AppHeader;
