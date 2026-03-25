import React, { FC, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';

interface TabItem {
	path: string;
	icon: string;
	label: string;
	badge?: number;
}

interface BottomTabBarProps {
	notificationCount?: number;
	className?: string;
}

const BottomTabBar: FC<BottomTabBarProps> = ({ notificationCount, className }) => {
	const navigate = useNavigate();
	const location = useLocation();
	// US-AUTH-08: Navigation menu items translate on language change
	const { t } = useTranslation('menu');
	const [showAltroMenu, setShowAltroMenu] = useState(false);

	const tabs: TabItem[] = [
		{ path: '/', icon: 'home', label: t('menu.dashboard') },
		{ path: '/tasks', icon: 'task_alt', label: t('menu.tasks') },
	];

	const isActive = (path: string): boolean => {
		if (path === '/') return location.pathname === '/';
		return location.pathname.startsWith(path);
	};

	const altroItems: TabItem[] = [
		{ path: '/settings/organization', icon: 'settings', label: t('Settings') },
	];

	const isAltroActive = altroItems.some((item) => location.pathname.startsWith(item.path));

	return (
		<>
			{/* Altro half-sheet overlay */}
			{showAltroMenu && (
				<>
					<div
						className='position-fixed top-0 start-0 w-100 h-100'
						style={{ backgroundColor: 'rgba(0,0,0,0.3)', zIndex: 1040 }}
						onClick={() => setShowAltroMenu(false)}
					/>
					<div
						className='position-fixed bottom-0 start-0 w-100 bg-white rounded-top-4 shadow-lg'
						style={{ zIndex: 1050, paddingBottom: 'env(safe-area-inset-bottom)' }}>
						<div className='d-flex justify-content-center pt-2 pb-1'>
							<div
								className='rounded-pill bg-secondary'
								style={{ width: 40, height: 4, opacity: 0.3 }}
							/>
						</div>
						{altroItems.map((item) => (
							<div
								key={item.path}
								className='d-flex align-items-center gap-3 px-4 py-3 cursor-pointer'
								role='button'
								tabIndex={0}
								onClick={() => {
									setShowAltroMenu(false);
									navigate(item.path);
								}}
								onKeyDown={(e) => {
									if (e.key === 'Enter') {
										setShowAltroMenu(false);
										navigate(item.path);
									}
								}}>
								<span className='material-icons text-muted'>{item.icon}</span>
								<span className='flex-grow-1'>{item.label}</span>
								{item.badge != null && item.badge > 0 && (
									<span className='badge bg-danger rounded-pill'>{item.badge}</span>
								)}
							</div>
						))}
					</div>
				</>
			)}

			{/* Bottom tab bar */}
			<nav
				className={classNames('bottom-tab-bar d-md-none', className)}
				style={{
					position: 'fixed',
					bottom: 0,
					left: 0,
					right: 0,
					height: 56,
					backgroundColor: '#FFFFFF',
					borderTop: '1px solid #E5E7EB',
					paddingBottom: 'env(safe-area-inset-bottom)',
					zIndex: 1030,
				}}>
				<div className='d-flex justify-content-around align-items-center h-100'>
					{tabs.map((tab) => (
						<button
							key={tab.path}
							type='button'
							className={classNames(
								'btn btn-link d-flex flex-column align-items-center text-decoration-none p-0',
								isActive(tab.path) ? 'text-primary' : 'text-muted',
							)}
							onClick={() => navigate(tab.path)}
							style={{ minWidth: 64 }}>
							<span className='material-icons' style={{ fontSize: 24 }}>
								{tab.icon}
							</span>
							<span style={{ fontSize: 10 }}>{tab.label}</span>
						</button>
					))}
					<button
						type='button'
						className={classNames(
							'btn btn-link d-flex flex-column align-items-center text-decoration-none p-0 position-relative',
							isAltroActive ? 'text-primary' : 'text-muted',
						)}
						onClick={() => setShowAltroMenu((prev) => !prev)}
						style={{ minWidth: 64 }}>
						<span className='material-icons' style={{ fontSize: 24 }}>
							more_horiz
						</span>
						<span style={{ fontSize: 10 }}>{t('More')}</span>
						{notificationCount != null && notificationCount > 0 && (
							<span
								className='position-absolute badge bg-danger rounded-pill'
								style={{ top: -2, right: 8, fontSize: 9 }}>
								{notificationCount}
							</span>
						)}
					</button>
				</div>
			</nav>
		</>
	);
};

export default BottomTabBar;
