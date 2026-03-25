import React, { use, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
// import classNames from 'classnames';
import { useTranslation } from 'react-i18next';
import Brand from '../../../layout/Brand/Brand';
import Navigation, { NavigationLine } from '../../../layout/Navigation/Navigation';
import User from '../../../layout/User/User';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import {
	companyPagesMenu,
	tasksMenu,
	settingsMenu,
} from '../../../menu';
import ThemeContext from '../../../contexts/themeContext';
// import Card, { CardBody } from '../../../components/bootstrap/Card';
// import Hand from '../../../assets/img/hand.png';
// import HandWebp from '../../../assets/img/hand.webp';
// import Icon from '../../../components/icon/Icon';
// import Button from '../../../components/bootstrap/Button';
// import useDarkMode from '../../../hooks/useDarkMode';
import Aside, { AsideBody, AsideFoot, AsideHead } from '../../../layout/Aside/Aside';
import AuthContext from '../../../contexts/authContext';
import { UserType } from '../../../common/data/dummyRolesData';
import { useOrganization } from '../../../contexts/organizationContext';
import { usePermissions } from '../../../contexts/permissionContext';
import { canShowMenuItem } from '../../../common/constants/menuPermissionMapping';
import { PermissionCode } from '../../../common/constants/permissionCodes';

/** @userstory US-MENU-01, US-MENU-02, US-MENU-04, US-MENU-05, US-MENU-06, US-MENU-08, US-MENU-09 */

// Animation variants for Apple-style menu transitions
const menuTransitionVariants = {
	initial: (direction: number) => ({
		opacity: 0,
		x: direction > 0 ? 20 : -20,
	}),
	animate: {
		opacity: 1,
		x: 0,
		transition: {
			duration: 0.2,
			ease: 'easeInOut' as const,
		},
	},
	exit: (direction: number) => ({
		opacity: 0,
		x: direction > 0 ? -20 : 20,
		transition: {
			duration: 0.15,
			ease: 'easeInOut' as const,
		},
	}),
};

// Back button hover animation
const backButtonVariants = {
	initial: { x: 0 },
	hover: { x: -4 },
	tap: { scale: 0.96 },
};

/**
 * Filter menu items based on user permissions
 * @param menu The menu object to filter
 * @param isAdmin Whether the user is an Admin
 * @param permissions The user's permissions array
 */
const filterMenuByPermissions = (
	menu: Record<string, any>,
	isAdmin: boolean,
	permissions: PermissionCode[],
): Record<string, any> => {
	const filtered: Record<string, any> = {};

	Object.entries(menu).forEach(([key, item]) => {
		if (!item || typeof item !== 'object') return;

		const menuId = item.id?.toLowerCase() || key.toLowerCase();

		// Check if this menu item should be visible
		if (!canShowMenuItem(menuId, isAdmin, permissions)) {
			return;
		}

		// If there's a subMenu, filter it recursively
		if (item.subMenu && typeof item.subMenu === 'object') {
			const filteredSubMenu = filterMenuByPermissions(item.subMenu, isAdmin, permissions);
			// Only include parent if at least one subMenu item is visible
			if (Object.keys(filteredSubMenu).length > 0) {
				filtered[key] = { ...item, subMenu: filteredSubMenu };
			}
		} else {
			filtered[key] = item;
		}
	});

	return filtered;
};

/**
 * Get operational menu (excluding Settings which is rendered separately)
 */
const getOperationalMenu = (
	menu: Record<string, any>,
	isAdmin: boolean,
	permissions: PermissionCode[],
): Record<string, any> => {
	const filtered = filterMenuByPermissions(menu, isAdmin, permissions);
	// Remove Settings from operational menu (rendered separately at bottom)
	// eslint-disable-next-line @typescript-eslint/no-unused-vars
	const { Settings, ...operationalMenu } = filtered;
	return operationalMenu;
};

const DefaultAside = () => {
	const { asideStatus, setAsideStatus } = use(ThemeContext);
	const { userData } = use(AuthContext);
	const { organizations } = useOrganization();
	const { isAdmin, permissions } = usePermissions();
	const location = useLocation();
	const navigate = useNavigate();

	// Check if we're in the settings area
	const isInSettingsArea = location.pathname.startsWith('/settings');

	// const [doc, setDoc] = useState(
	// 	localStorage.getItem('MyApp_asideDocStatus') === 'true' || false,
	// );

	const { t } = useTranslation('menu');

	// const { darkModeStatus } = useDarkMode();

	// Get operational menu (without Settings)
	const operationalMenu = useMemo(() => {
		const filtered = getOperationalMenu(companyPagesMenu, isAdmin, permissions);
		return filtered;
	}, [isAdmin, permissions]);

	// Settings entry (only visible to Admin)
	const settingsEntry = useMemo(() => {
		if (!isAdmin) return null;
		return { Settings: companyPagesMenu.Settings };
	}, [isAdmin]);

	return (
		<Aside>
			<AsideHead>
				<Brand asideStatus={asideStatus} setAsideStatus={setAsideStatus} />
			</AsideHead>
			<AsideBody>
				{/* {!doc && (
					<>
						<Navigation menu={dashboardPagesMenu} id='aside-dashboard' />
						<NavigationLine />
						<Navigation menu={demoPagesMenu} id='aside-demo-pages' />
						<NavigationLine />
						<Navigation menu={pageLayoutTypesPagesMenu} id='aside-menu' />
					</>
				)} */}

				{/* {doc && (
					<>
						<Navigation menu={gettingStartedPagesMenu} id='aside-docMenu' />
						<NavigationLine />
						<Navigation menu={componentPagesMenu} id='aside-componentsMenu' />
						<NavigationLine />
					</>
				)} */}

				{/* {asideStatus && doc && (
					<Card className='m-3 '>
						<CardBody className='pt-0'>
							<img srcSet={HandWebp} src={Hand} alt='Hand' width={130} height={130} />
							<p
								className={classNames('h4', {
									'text-dark': !darkModeStatus,
									'text-light': darkModeStatus,
								})}>
								{t('Everything is ready!') as ReactNode}
							</p>
							<Button
								color='info'
								isLight
								className='w-100'
								onClick={() => {
								localStorage.setItem('MyApp_asideDocStatus', 'false');
									setDoc(false);
								}}>
								{t('Demo Pages') as ReactNode}
							</Button>
						</CardBody>
					</Card>
				)} */}

				{/* Company Route */}
				{userData.role === UserType.COMPANY && (
					<AnimatePresence mode='wait' custom={isInSettingsArea ? 1 : -1}>
						{isInSettingsArea ? (
							// Settings Area Menu with Apple-style transition
							<motion.div
								key='settings-menu'
								custom={1}
								variants={menuTransitionVariants}
								initial='initial'
								animate='animate'
								exit='exit'>
								{/* Back to Dashboard button with micro-interaction */}
								<motion.div
									className='px-3 py-2'
									variants={backButtonVariants}
									initial='initial'
									whileHover='hover'
									whileTap='tap'
									transition={{ type: 'spring', stiffness: 400, damping: 25 }}>
									<Button
										id='company-settings-back-to-dashboard'
										color='link'
										className='settings-back-button p-0 text-decoration-none w-100 text-start'
										onClick={() => navigate('/dashboard')}>
										<Icon icon='ArrowBack' className='back-icon' />
										<span className='back-text'>{t('Back to Dashboard')}</span>
									</Button>
								</motion.div>
								<NavigationLine />
								{/* Settings Menu */}
								<Navigation menu={settingsMenu} id='settings-menu' />
							</motion.div>
						) : (
							// Operational Area Menu with Apple-style transition
							<motion.div
								key='operational-menu'
								custom={-1}
								variants={menuTransitionVariants}
								initial='initial'
								animate='animate'
								exit='exit'>
								{/* Operational Menu */}
								<Navigation menu={operationalMenu} id='company-dashboard' />
								<NavigationLine />

								{/* Tasks Domain Menu */}
								<Navigation menu={tasksMenu} id='tasks-menu' />
								<NavigationLine />

								{/* Settings Entry (Admin only) */}
								{settingsEntry && (
									<Navigation menu={settingsEntry} id='company-settings' />
								)}
							</motion.div>
						)}
					</AnimatePresence>
				)}

			</AsideBody>
			<AsideFoot>
				{/* <nav aria-label='aside-bottom-menu'>
					<div className='navigation'>
						<div
							role='presentation'
							className='navigation-item cursor-pointer'
							onClick={() => {
							localStorage.setItem('MyApp_asideDocStatus', String(!doc));
								setDoc(!doc);
							}}
							data-tour='documentation'>
							<span className='navigation-link navigation-link-pill'>
								<span className='navigation-link-info'>
									<Icon
										icon={doc ? 'ToggleOn' : 'ToggleOff'}
										color={doc ? 'success' : undefined}
										className='navigation-icon'
									/>
									<span className='navigation-text'>
										{t('menu:Documentation') as ReactNode}
									</span>
								</span>
								<span className='navigation-link-extra'>
									<Icon
										icon='Circle'
										className={classNames(
											'navigation-notification',
											'text-success',
											'animate__animated animate__heartBeat animate__infinite animate__slower',
										)}
									/>
								</span>
							</span>
						</div>
					</div>
				</nav> */}
				<User />
			</AsideFoot>
		</Aside>
	);
};

export default DefaultAside;
