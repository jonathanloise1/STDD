import React, { use, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import classNames from 'classnames';
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
import AuthContext from '../../../contexts/authContext';
import useDarkMode from '../../../hooks/useDarkMode';
import { UserType } from '../../../common/data/dummyRolesData';
import LANG, { getLangWithKey, ILang } from '../../../lang';
import showNotification from '../../../components/extras/showNotification';
import { useOrganization } from '../../../contexts/organizationContext';
import OrganizationDropdown from '../../../components/Header/OrganizationDropdown';
import userProfileService from '../../../services/api/userProfileService';

interface QuickStat {
	label: string;
	value: string | number;
	icon: string;
	color: string;
}

const DashboardHeader: React.FC = () => {
	const { t, i18n } = useTranslation(['dashboard', 'settings']);
	const { userData } = use(AuthContext);
	const { darkModeStatus, setDarkModeStatus } = useDarkMode();
	const isCompany = userData?.role === UserType.COMPANY;
	const userName = userData?.name || '';

	const { selectedOrganization } = useOrganization();

	const [currentTime, setCurrentTime] = useState(new Date());
	// eslint-disable-next-line @typescript-eslint/no-unused-vars
	const [stats, setStats] = useState<QuickStat[]>([]);

	// Update time every second for the clock
	useEffect(() => {
		const timer = setInterval(() => {
			setCurrentTime(new Date());
		}, 1000);
		return () => clearInterval(timer);
	}, []);

	// Button style
	const styledBtn: IButtonProps = {
		color: darkModeStatus ? 'dark' : 'light',
		hoverShadow: 'default',
		isLight: !darkModeStatus,
		size: 'lg',
	};

	// Format time
	const formatTime = (date: Date) => {
		return date.toLocaleTimeString(i18n.language, {
			hour: '2-digit',
			minute: '2-digit',
			second: '2-digit',
		});
	};

	// Format date
	const formatDate = (date: Date) => {
		return date.toLocaleDateString(i18n.language, {
			weekday: 'short',
			day: 'numeric',
			month: 'short',
		});
	};

	// Get greeting based on time
	const getGreeting = () => {
		const hour = currentTime.getHours();
		if (hour < 12) return t('welcome.greetings.morning');
		if (hour < 18) return t('welcome.greetings.afternoon');
		return t('welcome.greetings.evening');
	};

	// Get motivational message
	const getMotivationalMessage = () => {
		const day = currentTime.getDay();
		const hour = currentTime.getHours();

		if (day === 0 || day === 6) return t('welcome.messages.weekend');
		if (hour < 10) return t('welcome.messages.earlyMorning');
		if (hour < 14) return t('welcome.messages.productive');
		if (hour < 18) return t('welcome.messages.afternoon');
		return t('welcome.messages.evening');
	};

	// Get time emoji
	const getTimeEmoji = () => {
		const hour = currentTime.getHours();
		if (hour < 12) return '☀️';
		if (hour < 18) return '🌤️';
		return '🌙';
	};

	// Gradient colors based on user type
	const gradientColors = isCompany
		? 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 50%, #a855f7 100%)'
		: 'linear-gradient(135deg, #10b981 0%, #14b8a6 50%, #06b6d4 100%)';

	/**
	 * Switches the application language and shows a confirmation notification.
	 * Same behaviour as in CommonHeaderRight and SimpleHeader: delegates to
	 * `i18n.changeLanguage()`, triggering a re-render of all translated
	 * components, and persists the choice via LanguageDetector (localStorage).
	 *
	 * @userstory US-I18N-02 — Switch language at any time without logging out
	 * @userstory US-I18N-03 — Preference is automatically saved by LanguageDetector
	 */
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

	// ============================================
	// MOBILE CENTER CONTENT (shown in mobile-header bar)
	// ============================================
	const mobileGreeting = (
		<div className='d-flex align-items-center'>
			<span style={{ fontSize: '1.25rem' }} className='me-2'>
				{getTimeEmoji()}
			</span>
			<span className={classNames('fw-bold', { 'text-light': darkModeStatus })}>
				{getGreeting().split(',')[0]},{' '}
				<span
					style={{
						background: gradientColors,
						WebkitBackgroundClip: 'text',
						WebkitTextFillColor: 'transparent',
						backgroundClip: 'text',
					}}>
					{userName.split(' ')[0]}
				</span>
				!
			</span>
		</div>
	);

	return (
		<Header mobileCenter={mobileGreeting}>
			{/* ============================================ */}
			{/* DESKTOP HEADER LEFT - Greeting */}
			{/* ============================================ */}
			<HeaderLeft>
				<div className='d-flex align-items-center w-100'>
					<div className='d-flex align-items-center'>
						<motion.span
							animate={{ y: [0, -2, 0] }}
							transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
							style={{ fontSize: '1.75rem' }}
							className='me-2'>
							{getTimeEmoji()}
						</motion.span>
						<div className='d-flex flex-column' style={{ marginTop: '-4px' }}>
							<span
								className={classNames('fw-bold', { 'text-light': darkModeStatus })}
								style={{ fontSize: '1.35rem', lineHeight: '1.2' }}>
								{getGreeting()},{' '}
								<span
									style={{
										background: gradientColors,
										WebkitBackgroundClip: 'text',
										WebkitTextFillColor: 'transparent',
										backgroundClip: 'text',
									}}>
									{userName}
								</span>
								! 👋
							</span>
							<small
								className={darkModeStatus ? 'text-light opacity-60' : 'text-muted'}
								style={{ fontSize: '0.85rem', marginTop: '2px' }}>
								{getMotivationalMessage()}
							</small>
						</div>
					</div>

					{/* Organization Switcher for Company users */}
					{isCompany && (
						<div className='ms-auto d-none d-md-flex align-items-center'>
							<div className='bg-l10-primary rounded px-3 py-2'>
								<OrganizationDropdown />
							</div>
						</div>
					)}
				</div>
			</HeaderLeft>

			{/* ============================================ */}
			{/* HEADER RIGHT - Mobile panel + Desktop controls */}
			{/* ============================================ */}
			<HeaderRight>
				{/* ----- MOBILE PANEL CONTENT (< md) ----- */}
				<div className='d-md-none d-flex flex-column align-items-center w-100 py-4 px-3'>
					{/* Organization Switcher for Company users (Mobile) */}
					{isCompany && (
						<div className='mb-4 w-100'>
							<div className='bg-l10-primary rounded px-3 py-2 text-center'>
								<OrganizationDropdown />
							</div>
						</div>
					)}

					{/* Time & Date Section */}
					<div
						className='text-center mb-4 pb-3 w-100 rounded-3 py-3'
						style={{
							background: darkModeStatus
								? 'rgba(255,255,255,0.03)'
								: 'rgba(0,0,0,0.02)',
						}}>
						<motion.div
							initial={{ scale: 0.9 }}
							animate={{ scale: 1 }}
							className='fw-bold mb-1'
							style={{
								fontSize: '2.5rem',
								fontFamily: 'monospace',
								background: gradientColors,
								WebkitBackgroundClip: 'text',
								WebkitTextFillColor: 'transparent',
								backgroundClip: 'text',
								letterSpacing: '3px',
							}}>
							{formatTime(currentTime)}
						</motion.div>
						<div
							className={classNames({
								'text-light opacity-75': darkModeStatus,
								'text-muted': !darkModeStatus,
							})}
							style={{ textTransform: 'capitalize', fontSize: '0.95rem' }}>
							{formatDate(currentTime)}
						</div>
					</div>

					{/* Motivational Message */}
					<div
						className={classNames('text-center mb-4', {
							'text-light': darkModeStatus,
						})}
						style={{ fontSize: '1rem' }}>
						<span style={{ fontSize: '1.25rem' }} className='me-2'>
							{getTimeEmoji()}
						</span>
						{getMotivationalMessage()}
					</div>

					<hr
						className={classNames('w-75 my-3', {
							'border-secondary': darkModeStatus,
						})}
					/>

					{/* Quick Stats */}
					{stats.length > 0 && (
						<div className='d-flex gap-3 mb-4'>
							{stats.map((stat) => (
								<div
									key={stat.label}
									className='d-flex align-items-center px-3 py-2 rounded-2'
									style={{
										background: darkModeStatus
											? 'rgba(255,255,255,0.08)'
											: 'rgba(0,0,0,0.04)',
									}}>
									<Icon
										icon={stat.icon}
										className={`text-${stat.color} me-2`}
									/>
									<span
										className={classNames('fw-bold me-2', {
											'text-light': darkModeStatus,
										})}
										style={{ fontSize: '1.1rem' }}>
										{stat.value}
									</span>
									<span
										className={
											darkModeStatus
												? 'text-light opacity-75'
												: 'text-muted'
										}
										style={{ fontSize: '0.9rem' }}>
										{stat.label}
									</span>
								</div>
							))}
						</div>
					)}

					{/* Two buttons centered */}
					<div className='d-flex justify-content-center gap-4 mt-2'>
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

						<Dropdown>
							<DropdownToggle hasIcon={false}>
								{typeof getLangWithKey(i18n.language as ILang['key']['lng'])
									?.icon === 'undefined' ? (
									<Button
										{...styledBtn}
										className='btn-only-icon'
										aria-label='Change language'
										id='btn-language-selector-mobile'>
										<Spinner isSmall inButton='onlyIcon' isGrow />
									</Button>
								) : (
									<Button
										{...styledBtn}
										icon={
											getLangWithKey(i18n.language as ILang['key']['lng'])
												?.icon
										}
										aria-label='Change language'
										id='btn-language-selector-mobile'
									/>
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
				</div>

				{/* ----- DESKTOP CONTROLS (md+) ----- */}
				<div className='d-none d-md-block'>
					<div className='row g-3 align-items-center'>
						{/* Quick Stats - md+ (tablet and up) */}
						{stats.length > 0 &&
							stats.map((stat) => (
								<div key={stat.label} className='col-auto'>
									<div
										className='d-flex align-items-center px-3 py-2 rounded-2'
										style={{
											background: darkModeStatus
												? 'rgba(255,255,255,0.08)'
												: 'rgba(0,0,0,0.04)',
										}}>
										<Icon
											icon={stat.icon}
											className={`text-${stat.color} me-2`}
											size='lg'
										/>
										<span
											className={classNames('fw-bold me-2', {
												'text-light': darkModeStatus,
											})}
											style={{ fontSize: '1rem' }}>
											{stat.value}
										</span>
										<span
											className={
												darkModeStatus ? 'text-light opacity-75' : 'text-muted'
											}
											style={{ fontSize: '0.85rem' }}>
											{stat.label}
										</span>
									</div>
								</div>
							))}

						{/* Dark Mode Toggle */}
						<div className='col-auto'>
							<Popovers trigger='hover' desc='Dark / Light mode'>
								<Button
									{...styledBtn}
									onClick={() => setDarkModeStatus(!darkModeStatus)}
									className='btn-only-icon'
									data-tour='dark-mode'
									aria-label='Toggle dark mode'>
									<Icon
										icon={darkModeStatus ? 'DarkMode' : 'LightMode'}
										color={darkModeStatus ? 'info' : 'warning'}
										className='btn-icon'
									/>
								</Button>
							</Popovers>
						</div>

						{/* Language Selector */}
						<div className='col-auto'>
							<Dropdown>
								<DropdownToggle hasIcon={false}>
									{typeof getLangWithKey(i18n.language as ILang['key']['lng'])
										?.icon === 'undefined' ? (
										<Button
											{...styledBtn}
											className='btn-only-icon'
											aria-label='Change language'
											id='btn-language-selector'
											data-tour='lang-selector'>
											<Spinner isSmall inButton='onlyIcon' isGrow />
										</Button>
									) : (
										<Button
											{...styledBtn}
											icon={
												getLangWithKey(i18n.language as ILang['key']['lng'])
													?.icon
											}
											aria-label='Change language'
											id='btn-language-selector'
											data-tour='lang-selector'
										/>
									)}
								</DropdownToggle>
								<DropdownMenu isAlignmentEnd data-tour='lang-selector-menu'>
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
					</div>
				</div>
			</HeaderRight>
		</Header>
	);
};

export default DashboardHeader;
