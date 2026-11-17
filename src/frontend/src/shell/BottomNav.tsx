import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import { Icon, SidePanel } from '../ui';
import useNavItems, { INavItem } from '../hooks/useNavItems';

/** The menu below `md`: Dashboard and Tasks as tabs; the other entries of the role behind "More". */
// US-AUTH-06, US-AUTH-07
const BottomNav = () => {
	const { t } = useTranslation('menu');
	const { mainItems, bottomItems } = useNavItems();
	const [showMore, setShowMore] = useState(false);

	const tabs = mainItems.slice(0, 2);
	const moreItems = [...mainItems.slice(2), ...bottomItems];
	const isMoreActive = moreItems.some((item) => item.isActive);

	const renderTab = (item: INavItem) => (
		<Link
			key={item.id}
			to={item.path}
			aria-label={t(item.labelKey)}
			aria-current={item.isActive ? 'page' : undefined}
			className='bottom-tab-bar__item'>
			<Icon icon={item.icon} size='lg' />
			<span>{t(item.labelKey)}</span>
		</Link>
	);

	return (
		<>
			<nav className='bottom-tab-bar d-md-none'>
				{tabs.map(renderTab)}
				{moreItems.length > 0 && (
					<button
						type='button'
						className={classNames('bottom-tab-bar__item', { active: isMoreActive })}
						aria-expanded={showMore}
						onClick={() => setShowMore((prev) => !prev)}>
						<Icon icon='MoreHoriz' size='lg' />
						<span>{t('More')}</span>
					</button>
				)}
			</nav>

			{/* The other entries of the role, in a sheet from the bottom */}
			<SidePanel
				show={showMore}
				onHide={() => setShowMore(false)}
				placement='bottom'
				title={t('More')}>
				<div className='d-flex flex-column'>
					{moreItems.map((item) => (
						<Link
							key={item.id}
							to={item.path}
							aria-label={t(item.labelKey)}
							aria-current={item.isActive ? 'page' : undefined}
							className='sidebar-item'
							onClick={() => setShowMore(false)}>
							<Icon icon={item.icon} size='lg' />
							<span className='flex-grow-1'>{t(item.labelKey)}</span>
						</Link>
					))}
				</div>
			</SidePanel>
		</>
	);
};

export default BottomNav;
