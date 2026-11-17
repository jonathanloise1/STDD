import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import { Icon, IconButton } from '../ui';
import useNavItems, { INavItem } from '../hooks/useNavItems';

interface ISideNavProps {
	pinned: boolean;
	onTogglePin: () => void;
}

/**
 * The menu from `md` up: icons only, expanded on hover or keyboard focus, or kept expanded when pinned.
 * The labels show through CSS, so the accessible name of each entry is its `aria-label`.
 */
// US-AUTH-06, US-AUTH-07
const SideNav = ({ pinned, onTogglePin }: ISideNavProps) => {
	const { t } = useTranslation('menu');
	const { mainItems, bottomItems } = useNavItems();

	const renderItem = (item: INavItem) => {
		const label = t(item.labelKey);
		return (
			<Link
				key={item.id}
				id={`sidebar-${item.id}`}
				to={item.path}
				aria-label={label}
				aria-current={item.isActive ? 'page' : undefined}
				className='sidebar-item'
				title={pinned ? undefined : label}>
				<Icon icon={item.icon} size='lg' />
				<span className='slim-sidebar__label small'>{label}</span>
			</Link>
		);
	};

	return (
		<nav
			className={classNames('slim-sidebar d-none d-md-flex flex-column', {
				'is-expanded': pinned,
			})}>
			{/* Brand and pin button */}
			<div className='slim-sidebar__brand'>
				<Icon icon='Shield' size='lg' />
				<span className='slim-sidebar__label'>MyApp</span>
				<IconButton
					className='slim-sidebar__pin'
					variant='link'
					size='sm'
					icon={pinned ? 'PushPinOff' : 'PushPin'}
					label={pinned ? t('sidebar.unpin') : t('sidebar.pin')}
					onClick={onTogglePin}
				/>
			</div>

			<div className='slim-sidebar__entries'>{mainItems.map(renderItem)}</div>

			{bottomItems.length > 0 && (
				<div className='slim-sidebar__bottom border-top'>{bottomItems.map(renderItem)}</div>
			)}
		</nav>
	);
};

export default SideNav;
