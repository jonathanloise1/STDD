import React, { cloneElement, HTMLAttributes, ReactElement, ReactNode } from 'react';
import classNames from 'classnames';

interface INavLinkDropdownProps extends HTMLAttributes<HTMLSpanElement> {
	children: ReactElement<INavItemProps>[] | ReactNode[] | string;
	className?: string;
	ref?: React.Ref<HTMLSpanElement>;
}
export const NavLinkDropdown = ({ children, className, ref, ...props }: INavLinkDropdownProps) => {
	return (
		<span
			ref={ref}
			className={classNames('nav-link', 'cursor-pointer', className)}
			aria-current='page'
			{...props}>
			{children}
		</span>
	);
};
NavLinkDropdown.displayName = 'NavLinkDropdown';

interface INavItemProps extends HTMLAttributes<HTMLLIElement> {
	children: ReactNode;
	className?: string;
	isActive?: boolean;
	isDisable?: boolean;
	ref?: React.Ref<HTMLLIElement>;
}
export const NavItem = ({ children, className, isActive, isDisable, ref, ...props }: INavItemProps) => {
	// @ts-ignore

	if (children.type.displayName === 'Dropdown') {
			// @ts-ignore
			return cloneElement(children, {
				tag: 'li',
				// @ts-ignore

				className: classNames(children.props.className, 'nav-item'),
			});
		}
		return (
			<li ref={ref} className={classNames('nav-item', className)} {...props}>
				{
					// @ts-ignore
					cloneElement(children, {
						className: classNames(
							// @ts-ignore

							children.props.className,
							{ active: isActive, disabled: isDisable },
							'nav-link',
						),
					})
				}
			</li>
		);
	};
NavItem.displayName = 'NavItem';

interface INavProps extends HTMLAttributes<HTMLElement> {
	children: ReactElement<INavItemProps>[] | ReactNode[];
	className?: string;
	tag?: 'ul' | 'nav';
	design?: 'tabs' | 'pills';
	isFill?: boolean;
	isJustified?: boolean;
	isVertical?: boolean;
	verticalBreakpoint?: 'sm' | 'md' | 'lg' | 'xl' | 'xxl' | null;
	ref?: React.Ref<HTMLUListElement>;
}
const Nav = ({
		tag: Tag = 'ul',
		children,
		className,
		design = 'pills',
		isFill,
		isJustified,
		isVertical,
		verticalBreakpoint,
		ref,
		...props
	}: INavProps) => {
	return (
		// @ts-ignore
		<Tag
				ref={ref}
				className={classNames(
					'nav',
					{
						[`nav-${design}`]: design,
						'nav-fill': isFill,
						'nav-justified': isJustified,
					},
					{
						[`flex${verticalBreakpoint ? `-${verticalBreakpoint}` : ''}-column`]:
							isVertical || !!verticalBreakpoint,
					},
					className,
				)}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{children}
		</Tag>
	);
};
Nav.displayName = 'Nav';

export default Nav;
