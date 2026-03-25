import React, { FC, HTMLAttributes, ReactNode, useState } from 'react';
import classNames from 'classnames';
import { useTranslation } from 'react-i18next';
import { TIcons } from '../../type/icons-type';
import Item from './Item';
import List from './List';

/** @userstory US-MENU-01, US-MENU-07 */

interface INavigationLineProps {
	className?: string;
}
export const NavigationLine: FC<INavigationLineProps> = ({ className }) => {
	return <hr className={classNames('navigation-line', className)} />;
};

interface INavigationTitleProps extends HTMLAttributes<HTMLSpanElement> {
	className?: string;
	children: ReactNode;
}
export const NavigationTitle: FC<INavigationTitleProps> = ({ className, children, ...props }) => {
	return (
		<li className='navigation-item'>
			<span className={classNames('navigation-title', className)} {...props}>
				{children}
			</span>
		</li>
	);
};

interface INavigationProps {
	horizontal?: boolean;
	menu: {
		[key: string]: {
			id?: string | number;
			text?: string;
			path?: string;
			icon?: TIcons;
			isDisable?: boolean;
			iconColorClass?: string;
			subMenu?: {
				[key: string]: {
					id?: string | number;
					text?: string;
					path?: string;
					icon?: TIcons;
					isDisable?: boolean;
				};
			} | null;
		};
	};
	id: string;
	className?: string;
	ref?: React.Ref<HTMLElement>;
}
const Navigation = ({ menu, horizontal, id, className, ref, ...props }: INavigationProps) => {
		const [activeItem, setActiveItem] = useState(undefined);

		const { t } = useTranslation('menu');

		function fillMenu(
			data:
				| {
						id?: string | number;
						text?: string;
						path?: string;
						icon?: TIcons;
						isDisable?: boolean;
						subMenu?:
							| {
									id?: string | number;
									text?: string;
									path?: string;
									icon?: TIcons;
									isDisable?: boolean;
							  }[]
							| undefined;
				  }[]
				| any,
			parentId: string,
			rootId: string,
			isHorizontal: boolean | undefined,
			isMore: boolean | undefined,
		) {
			return Object.keys(data).map((item) => {
				return data[item].path ? (
					<Item
						key={data[item].id}
						rootId={rootId}
						id={data[item].id}
						title={data[item].text}
						icon={data[item].icon}
						iconColorClass={data[item].iconColorClass}
						to={`${data[item].path}`}
						parentId={parentId}
						isHorizontal={isHorizontal}
						setActiveItem={setActiveItem}
						activeItem={activeItem}
						notification={data[item].notification}
						hide={data[item].hide}
						navigateOnClick={!!data[item].navigableParent}>
						{!!data[item].subMenu &&
							fillMenu(
								data[item].subMenu,
								data[item].id,
								rootId,
								isHorizontal,
								undefined,
							)}
					</Item>
				) : (
					!isMore && !isHorizontal && (
						<NavigationTitle key={data[item].id}>
							{t(data[item].text) as ReactNode}
						</NavigationTitle>
					)
				);
			});
		}

		return (
			// @ts-ignore

			<nav ref={ref} aria-label={id} className={className} {...props}>
				<List id={id} horizontal={horizontal}>
					{fillMenu(menu, id, id, horizontal, undefined)}
					{horizontal && (
						<Item
							rootId={`other-${id}`}
							title={t('More') as string}
							icon='MoreHoriz'
							isHorizontal
							isMore>
							{fillMenu(menu, `other-${id}`, `other-${id}`, false, true)}
						</Item>
					)}
				</List>
			</nav>
		);
	};
Navigation.displayName = 'Navigation';

export default Navigation;
