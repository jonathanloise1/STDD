import React, {
	Children,
	cloneElement,
	ElementType,
	FC,
	JSXElementConstructor,
	ReactElement,
	ReactNode,
} from 'react';
import classNames from 'classnames';
import { NavLink } from 'react-router-dom';
import TagWrapper from '../TagWrapper';
import Icon from '../icon/Icon';

interface IBreadcrumbItemProps extends Record<string, any> {
	children: ReactNode;
	ariaLabel?: string;
	className?: string;
	tag?: ElementType;
	to: string;
	isActive?: boolean;
	divider?: ReactElement<any, string | JSXElementConstructor<any>> | string;
}
export const BreadcrumbItem: FC<IBreadcrumbItemProps> = ({
	children,
	ariaLabel,
	className,
	tag = 'li',
	to,
	isActive,
	divider,
}) => {
	return (
		<TagWrapper
			tag={tag}
			className={classNames('breadcrumb-item', { active: isActive }, className)}
			aria-current={isActive ? 'page' : undefined}
			aria-label={ariaLabel || (typeof children === 'string' ? children : undefined)}>
			{divider &&
				typeof divider !== 'string' &&
				cloneElement(divider, {
					className: classNames('breadcrumb-icon', divider.props.className),
				})}
			{isActive ? (
				children
			) : (
				<NavLink to={to} aria-label={ariaLabel}>
					{children}
				</NavLink>
			)}
		</TagWrapper>
	);
};

interface IBreadcrumbProps extends Record<string, any> {
	children?: ReactElement<IBreadcrumbItemProps> | ReactElement<IBreadcrumbItemProps>[];
	list?: {
		title: string;
		to: string;
		tag?: 'div' | 'li';
	}[];
	tag?: 'nav' | 'div' | 'section';
	listTag?: 'div' | 'ol' | 'ul';
	itemTag?: 'div' | 'li';
	ariaLabel?: string;
	autoActive?: boolean;
	isToHome?: ReactNode;
	divider?: ReactElement<any, string | JSXElementConstructor<any>> | string;
}
const Breadcrumb: FC<IBreadcrumbProps> = ({
	children,
	list,
	tag = 'nav',
	listTag = 'ol',
	itemTag = 'li',
	ariaLabel = 'breadcrumb',
	autoActive = true,
	isToHome = <Icon icon='HolidayVillage' />,
	divider = <Icon icon='ChevronRight' />,
}) => {
	const DIVIDER = divider !== 'string' && divider;
	return (
		<TagWrapper
			tag={tag}
			aria-label={ariaLabel}
			style={
				divider
					? ({
							'--bs-breadcrumb-divider':
								typeof divider === 'string' ? `'${divider}'` : 'none',
						} as React.CSSProperties)
					: undefined
			}>
			<TagWrapper tag={listTag} className='breadcrumb'>
				{isToHome && (
					<BreadcrumbItem to='/' ariaLabel='Home'>
						{isToHome}
					</BreadcrumbItem>
				)}
				{list
					? list.map((item, index) => (
							<BreadcrumbItem
								key={item.title}
								tag={item.tag || itemTag}
								to={item.to}
								isActive={autoActive && list.length === index + 1}
								divider={DIVIDER as ReactElement}>
								{item.title}
							</BreadcrumbItem>
						))
					: Children.map(children, (child, index) =>
							// @ts-ignore
							cloneElement(child, {
								// @ts-ignore
								tag: child.props.tag || itemTag,
								// @ts-ignore
								isActive: autoActive && children.length === index + 1,
								// @ts-ignore
								divider: child.props.divider || DIVIDER,
							}),
						)}
			</TagWrapper>
		</TagWrapper>
	);
};

export default Breadcrumb;
