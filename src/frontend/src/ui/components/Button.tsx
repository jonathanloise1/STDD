import React, { FC } from 'react';
import classNames from 'classnames';
import { Button as RbButton, ButtonProps as RbButtonProps } from 'react-bootstrap';
import { Link, LinkProps } from 'react-router-dom';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';
import { Spinner } from '../feedback/Spinner';

type Colour =
	| 'primary'
	| 'secondary'
	| 'success'
	| 'info'
	| 'warning'
	| 'danger'
	| 'light'
	| 'dark';

export type ButtonVariant = Colour | 'link' | `outline-${Colour}` | `subtle-${Colour}`;

export interface IButtonProps extends Omit<RbButtonProps, 'variant'> {
	variant?: ButtonVariant;
	/** Icon shown before the label. */
	icon?: IconName;
	/** While true the button is disabled and shows a spinner in place of the icon. */
	busy?: boolean;
	/** Renders a router link (role link) instead of a button. */
	to?: string;
}

/** Every action with a visible label (frontend.md 7). `type` defaults to `button`. */
export const Button: FC<IButtonProps> = ({
	variant = 'primary',
	icon,
	busy = false,
	to,
	type,
	disabled,
	children,
	...rest
}) => {
	const content = (
		<>
			{busy && <Spinner size='sm' />}
			{!busy && icon && <Icon icon={icon} />}
			{children}
		</>
	);

	if (to) {
		// A router link styled as a button. react-bootstrap's own `as` would give it role="button".
		const { className, size, active, ...linkRest } = rest;
		delete linkRest.as;
		delete linkRest.bsPrefix;
		return (
			<Link
				{...(linkRest as unknown as Omit<LinkProps, 'to'>)}
				to={to}
				aria-disabled={disabled || busy ? true : undefined}
				className={classNames(
					'btn',
					`btn-${variant}`,
					size && `btn-${size}`,
					active && 'active',
					(disabled || busy) && 'disabled',
					className,
				)}>
				{content}
			</Link>
		);
	}

	return (
		<RbButton {...rest} type={type ?? 'button'} variant={variant} disabled={disabled || busy}>
			{content}
		</RbButton>
	);
};
