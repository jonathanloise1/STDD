import React, { useLayoutEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Header, { HeaderLeft, HeaderRight } from '../../../layout/Header/Header';
import Button, { IButtonProps } from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../components/bootstrap/Dropdown';
import Popovers from '../../../components/bootstrap/Popovers';
import Spinner from '../../../components/bootstrap/Spinner';
import useDarkMode from '../../../hooks/useDarkMode';
import LANG, { getLangWithKey, ILang } from '../../../lang';
import showNotification from '../../../components/extras/showNotification';
import OrganizationDropdown from '../../../components/Header/OrganizationDropdown';

// Map routes to section info (icon + translationKey)
// Order matters: more specific routes first!
const getSectionInfo = (
	pathname: string,
): { icon: string; translationKey: string } | null => {
	const path = pathname.replace(/^\//, ''); // Remove leading slash

	// ============================================
	// SETTINGS & ORGANIZATIONS
	// ============================================
	if (path.startsWith('settings') || path.startsWith('organizations'))
		return { icon: 'Settings', translationKey: 'Settings' };

	// ============================================
	// GENERIC FALLBACKS
	// ============================================
	if (path.includes('/projects'))
		return { icon: 'BusinessCenter', translationKey: 'Projects' };
	if (path.includes('/milestones'))
		return { icon: 'Flag', translationKey: 'Milestones' };
	if (path.includes('/timesheets'))
		return { icon: 'Schedule', translationKey: 'Timesheets' };
	if (path.includes('/messages'))
		return { icon: 'Chat', translationKey: 'Messages' };

	// Default - no section info
	return null;
};

const SimpleHeader = () => {
	const { darkModeStatus, setDarkModeStatus } = useDarkMode();
	const { i18n, t } = useTranslation(['menu', 'settings']);
	const location = useLocation();

	const sectionInfo = getSectionInfo(location.pathname);

	const styledBtn: IButtonProps = {
		color: darkModeStatus ? 'dark' : 'light',
		hoverShadow: 'default',
		isLight: !darkModeStatus,
		size: 'lg',
	};

	/**
	 * Switches the application language and shows a confirmation notification.
	 * Same behaviour as in CommonHeaderRight and DashboardHeader: delegates to
	 * `i18n.changeLanguage()`, triggering a re-render of all translated
	 * components, and persists the choice via LanguageDetector (localStorage).
	 *
	 * @userstory US-I18N-02 — Switch language at any time without logging out
	 * @userstory US-I18N-03 — Preference is automatically saved by LanguageDetector
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
		});
	};

	/**
	 * Keeps the HTML `lang` attribute in sync with the current i18n language.
	 * @userstory US-I18N-01 — Platform reflects the user's preferred language
	 */
	useLayoutEffect(() => {
		document.documentElement.setAttribute('lang', i18n.language.substring(0, 2));
	});

	return (
		<Header>
			<HeaderLeft>
				<div className='d-flex align-items-center w-100'>
					{sectionInfo && (
						<div className='d-flex align-items-center'>
							<Icon
								icon={sectionInfo.icon}
								size='2x'
								className='me-3'
								color='primary'
							/>
							<span
								className='fw-semibold text-primary'
								style={{ fontSize: '1.25rem' }}>
								{t(sectionInfo.translationKey)}
							</span>
						</div>
					)}
					
					{/* Organization Switcher */}
					<div className='ms-auto d-none d-md-flex align-items-center'>
						<div className='bg-l10-primary rounded px-3 py-2'>
							<OrganizationDropdown />
						</div>
					</div>
				</div>
			</HeaderLeft>

			<HeaderRight>
				<div className='row g-3'>
					{/* Dark Mode */}
					<div className='col-auto'>
						<Popovers trigger='hover' desc='Dark / Light mode'>
							<Button
								{...styledBtn}
								onClick={() => setDarkModeStatus(!darkModeStatus)}
								className='btn-only-icon'
								aria-label='Toggle dark mode'>
								<Icon
									icon={darkModeStatus ? 'DarkMode' : 'LightMode'}
									color={darkModeStatus ? 'info' : 'warning'}
									className='btn-icon'
								/>
							</Button>
						</Popovers>
					</div>

					{/* Lang Selector */}
					<div className='col-auto'>
						<Dropdown>
							<DropdownToggle hasIcon={false}>
								{typeof getLangWithKey(i18n.language as ILang['key']['lng'])?.icon ===
								'undefined' ? (
									<Button
										{...styledBtn}
										className='btn-only-icon'
										aria-label='Change language'>
										<Spinner isSmall inButton='onlyIcon' isGrow />
									</Button>
								) : (
									<Button
										{...styledBtn}
										icon={getLangWithKey(i18n.language as ILang['key']['lng'])?.icon}
										aria-label='Change language'
									/>
								)}
							</DropdownToggle>
							<DropdownMenu isAlignmentEnd>
								{Object.keys(LANG).map((i) => (
									<DropdownItem key={LANG[i].lng}>
										<Button
											icon={LANG[i].icon}
											onClick={() => changeLanguage(LANG[i].lng)}>
											{LANG[i].text}
										</Button>
									</DropdownItem>
								))}
							</DropdownMenu>
						</Dropdown>
					</div>
				</div>
			</HeaderRight>
		</Header>
	);
};

export default SimpleHeader;
