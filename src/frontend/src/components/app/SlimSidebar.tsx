import React, { FC, useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';

const PINNED_KEY = 'myapp_sidebarPinned';

interface SidebarItem {
	path: string;
	icon: string;
	label: string;
	badge?: number;
}

interface SlimSidebarProps {
	notificationCount?: number;
	className?: string;
}

const SlimSidebar: FC<SlimSidebarProps> = ({ notificationCount, className }) => {
	const navigate = useNavigate();
	const location = useLocation();
	// US-AUTH-08: Navigation menu items translate on language change
	const { t } = useTranslation('menu');
	const [isPinned, setIsPinned] = useState(() => localStorage.getItem(PINNED_KEY) === 'true');
	const [isHovered, setIsHovered] = useState(false);
	const isExpanded = isPinned || isHovered;

	useEffect(() => {
		localStorage.setItem(PINNED_KEY, String(isPinned));
		document.body.classList.toggle('sidebar-pinned', isPinned);
		return () => { document.body.classList.remove('sidebar-pinned'); };
	}, [isPinned]);

	const mainItems: SidebarItem[] = [
		{ path: '/', icon: 'dashboard', label: t('menu.dashboard') },
		{ path: '/tasks', icon: 'task_alt', label: t('menu.tasks') },
	];

	const bottomItems: SidebarItem[] = [
		{ path: '/settings/organization', icon: 'settings', label: t('Settings') },
	];

	const isActive = (path: string): boolean => {
		if (path === '/') return location.pathname === '/';
		// Highlight settings icon for all /settings/* sub-pages
		if (path.startsWith('/settings/')) return location.pathname.startsWith('/settings');
		return location.pathname.startsWith(path);
	};

	const renderItem = (item: SidebarItem) => (
		<div
			key={item.path}
			id={`sidebar-${item.path === '/' ? 'dashboard' : item.path.slice(1)}`}
			className={classNames(
				'sidebar-item d-flex align-items-center gap-3 px-3 py-2 rounded-2 mb-1 cursor-pointer position-relative',
				isActive(item.path) ? 'bg-l10-primary text-primary' : 'text-muted',
			)}
			role='button'
			tabIndex={0}
			onClick={() => navigate(item.path)}
			onKeyDown={(e) => e.key === 'Enter' && navigate(item.path)}
			title={!isExpanded ? item.label : undefined}>
			<span className='material-icons' style={{ fontSize: 22, minWidth: 22 }}>
				{item.icon}
			</span>
			{isExpanded && <span className='small text-nowrap'>{item.label}</span>}
			{item.badge != null && item.badge > 0 && (
				<span
					className={classNames(
						'badge bg-danger rounded-pill',
						isExpanded ? 'ms-auto' : 'position-absolute',
					)}
					style={!isExpanded ? { top: 4, right: 4, fontSize: 9 } : undefined}>
					{item.badge}
				</span>
			)}
		</div>
	);

	return (
		<nav
			className={classNames('slim-sidebar d-none d-md-flex flex-column', className)}
			style={{
				position: 'fixed',
				top: 0,
				left: 0,
				bottom: 0,
				width: isExpanded ? 220 : 60,
				backgroundColor: '#FFFFFF',
				borderRight: '1px solid #E5E7EB',
				transition: 'width 200ms ease',
				zIndex: 1040,
				overflow: 'hidden',
			}}
			onMouseEnter={() => setIsHovered(true)}
			onMouseLeave={() => setIsHovered(false)}>
			{/* Brand + pin toggle */}
			<div className='d-flex align-items-center border-bottom'
				style={{ height: 56, minHeight: 56, paddingLeft: isExpanded ? 16 : 0, paddingRight: isExpanded ? 8 : 0, justifyContent: isExpanded ? 'flex-start' : 'center' }}>
				<span className='material-icons text-primary' style={{ fontSize: 24, minWidth: 24 }}>
					shield
				</span>
				{isExpanded && (
					<>
						<span className='fw-bold ms-2 text-nowrap small flex-grow-1' style={{ color: '#1E3A5F' }}>MyApp</span>
						<button
							type='button'
							className='btn btn-link p-0 d-flex align-items-center justify-content-center'
							style={{ width: 28, height: 28, color: isPinned ? '#1E3A5F' : '#9CA3AF', flexShrink: 0 }}
							onClick={() => setIsPinned((prev) => !prev)}
							title={isPinned ? t('sidebar.unpin') : t('sidebar.pin')}>
							<span className='material-icons' style={{ fontSize: 18, transform: isPinned ? 'none' : 'rotate(45deg)', transition: 'transform 200ms ease' }}>
								push_pin
							</span>
						</button>
					</>
				)}
			</div>

			{/* Main items */}
			<div className='flex-grow-1 px-2 pt-3'>
				{mainItems.map(renderItem)}
			</div>

			{/* Bottom items */}
			<div className='px-2 pb-3 border-top pt-2'>
				{bottomItems.map(renderItem)}
			</div>
		</nav>
	);
};

export default SlimSidebar;
