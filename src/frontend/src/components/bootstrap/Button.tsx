import React, {
	HTMLAttributeAnchorTarget,
	HTMLAttributes,
	ReactElement,
	ReactNode,
} from 'react';
import classNames from 'classnames';
import { Link } from 'react-router-dom';
import { HashLink } from 'react-router-hash-link';
import TagWrapper from '../TagWrapper';
import Icon from '../icon/Icon';
import { TColor } from '../../type/color-type';

import { IDropdownProps } from './Dropdown';
import { TIcons } from '../../type/icons-type';

interface IButtonGroupProps extends HTMLAttributes<HTMLDivElement> {
	children:
		| ReactElement<IButtonProps>[]
		| ReactElement<IDropdownProps>[]
		| React.JSX.Element
		| React.JSX.Element[];
	className?: string;
	isToolbar?: boolean;
	isVertical?: boolean;
	size?: 'sm' | 'lg' | null;
	ariaLabel?: string;
	ref?: React.Ref<HTMLDivElement>;
}
export const ButtonGroup = ({ children, className, isToolbar, isVertical, size, ariaLabel, ref, ...props }: IButtonGroupProps) => {
	const PREFIX = isToolbar ? 'toolbar' : 'group';
		return (
			<div
				ref={ref}
				className={classNames(
					{
						[`btn-${PREFIX}`]: !isVertical,
						'btn-group-vertical': isVertical && PREFIX === 'group',
						[`btn-group-${size}`]: size,
					},
					className,
				)}
				role={PREFIX}
				aria-label={ariaLabel}
				{...props}>
			{children}
		</div>
	);
};
ButtonGroup.displayName = 'ButtonGroup';

export interface IButtonProps
	extends HTMLAttributes<
		HTMLButtonElement | HTMLAnchorElement | HTMLInputElement | HTMLLinkElement
	> {
	children?: ReactNode;
	tag?: 'button' | 'a' | 'input' | 'link';
	type?: 'button' | 'submit' | 'reset';
	to?: string | undefined;
	href?: string | undefined;
	isActive?: boolean;
	color?: TColor | 'link' | 'brand' | 'brand-two' | 'storybook';
	isOutline?: boolean;
	isLight?: boolean;
	isLink?: boolean;
	className?: string;
	icon?: TIcons;
	rounded?:
		| 'default'
		| 0
		| 1
		| 2
		| 3
		| 'bottom'
		| 'top'
		| 'circle'
		| 'end'
		| 'start'
		| 'pill'
		| null;
	size?: 'sm' | null | 'lg';
	isDisable?: boolean;
	shadow?: null | 'none' | 'sm' | 'default' | 'lg';
	hoverShadow?: null | 'none' | 'sm' | 'default' | 'lg';
	target?: HTMLAttributeAnchorTarget;
	isVisuallyHidden?: boolean;
	onClick?(...args: unknown[]): unknown;
	download?: true;
	ref?: React.Ref<HTMLAnchorElement>;
}
const Button = ({
		children,
		tag = 'button',
		type = 'button',
		to,
		href,
		isActive,
		color,
		isOutline,
		isLight,
		isLink,
		className,
		icon,
		rounded,
		size,
		isDisable,
		shadow,
		hoverShadow,
		target,
		isVisuallyHidden,
		ref,
		...props
	}: IButtonProps) => {
		const BTN_CLASS = classNames(
			'btn',
			{
				[`btn-${isOutline || isLink ? `outline-${color}` : color}`]:
					(color && !isLight) || (color && isLink),
				'border-transparent': isLink,
				[`btn-${size}`]: size,
				[`btn-hover-shadow${hoverShadow !== 'default' ? `-${hoverShadow}` : ''}`]:
					hoverShadow,
				[`btn-light-${color}`]: isLight,
				[`shadow${shadow !== 'default' ? `-${shadow}` : ''}`]: !!shadow,
				[`rounded${rounded !== 'default' ? `-${rounded}` : ''}`]: rounded,
				'rounded-0':
					rounded === 'bottom' ||
					rounded === 'top' ||
					rounded === 'end' ||
					rounded === 'start' ||
					rounded === 0,
				'btn-only-icon': !children || isVisuallyHidden,
				disabled: isDisable,
				active: isActive,
			},
			className,
		);

		const INNER = (
			<>
				{icon && <Icon icon={icon} className='btn-icon' />}
				{isVisuallyHidden ? (
					<span className='visually-hidden'>Toggle Dropdown</span>
				) : (
					children
				)}
			</>
		);

		const ANCHOR_LINK_PATTERN = /^#/i;

		const disableProps = isDisable && {
			tabIndex: -1,
			'aria-disabled': true,
			disabled: true,
		};

		if (tag === 'a') {
			if (typeof to === 'string' && ANCHOR_LINK_PATTERN.test(to)) {
				return (
					// eslint-disable-next-line react/jsx-props-no-spreading
					<HashLink ref={ref} className={BTN_CLASS} to={to} {...disableProps} {...props}>
						{INNER}
					</HashLink>
				);
			}
			if (to) {
				return (
					<Link
						ref={ref}
						className={BTN_CLASS}
						to={to}
						rel='noopener'
						target={target}
						// eslint-disable-next-line react/jsx-props-no-spreading
						{...disableProps}
						// eslint-disable-next-line react/jsx-props-no-spreading
						{...props}>
						{INNER}
					</Link>
				);
			}
			return (
				<a
					ref={ref}
					className={BTN_CLASS}
					href={href}
					role='button'
					rel='noopener'
					target={target}
					{...disableProps}
					{...props}>
					{INNER}
				</a>
			);
		}
		return (
			<TagWrapper
				ref={ref}
				tag={tag}
				type={type}
				className={BTN_CLASS}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...disableProps}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{INNER}
		</TagWrapper>
	);
};
Button.displayName = 'Button';

export default Button;
