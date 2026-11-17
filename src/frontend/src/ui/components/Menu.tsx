import React, { ComponentPropsWithoutRef, FC, ReactNode, forwardRef } from 'react';
import classNames from 'classnames';
import { Dropdown } from 'react-bootstrap';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';

// The toggle is a plain button: no `.btn`, no caret (the theme hides it), so the caller styles it.
const ToggleButton = forwardRef<HTMLButtonElement, ComponentPropsWithoutRef<'button'>>(
	(props, ref) => <button ref={ref} type='button' {...props} />,
);
ToggleButton.displayName = 'MenuToggleButton';

export interface IMenuProps {
	align?: 'start' | 'end';
	children?: ReactNode;
}

const MenuRoot: FC<IMenuProps> = ({ align = 'start', children }) => (
	<Dropdown align={align}>{children}</Dropdown>
);

export interface IMenuToggleProps {
	id?: string;
	className?: string;
	'aria-label'?: string;
	'data-testid'?: string;
	children?: ReactNode;
}

const MenuToggle: FC<IMenuToggleProps> = ({ className, children, ...rest }) => (
	<Dropdown.Toggle
		{...rest}
		as={ToggleButton}
		className={classNames('app-menu-toggle', className)}>
		{children}
	</Dropdown.Toggle>
);

const MenuItems: FC<{ children?: ReactNode }> = ({ children }) => (
	<Dropdown.Menu as='ul'>{children}</Dropdown.Menu>
);

export interface IMenuItemProps {
	onSelect: () => void;
	icon?: IconName;
	active?: boolean;
	id?: string;
	'data-testid'?: string;
	children?: ReactNode;
}

const MenuItem: FC<IMenuItemProps> = ({ onSelect, icon, active, children, ...rest }) => (
	<li>
		<Dropdown.Item {...rest} as='button' type='button' onClick={onSelect} active={active}>
			{icon && <Icon icon={icon} />}
			{children}
		</Dropdown.Item>
	</li>
);

const MenuHeader: FC<{ children?: ReactNode }> = ({ children }) => (
	<li>
		<Dropdown.Header>{children}</Dropdown.Header>
	</li>
);

/** A button that opens a short list of actions; choosing one closes it (frontend.md 7). */
export const Menu = Object.assign(MenuRoot, {
	Toggle: MenuToggle,
	Items: MenuItems,
	Item: MenuItem,
	Header: MenuHeader,
});
