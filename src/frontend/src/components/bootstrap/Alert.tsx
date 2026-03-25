import React, { ElementType, FC, ReactNode, useState } from 'react';
import classNames from 'classnames';
import { NavLink } from 'react-router-dom';
import TagWrapper from '../TagWrapper';
import Icon from '../icon/Icon';
import { TColor } from '../../type/color-type';
import { TIcons } from '../../type/icons-type';

interface IAlertHeadingProps extends Record<string, any> {
	children: ReactNode;
	className?: string;
	tag?: ElementType;
}
export const AlertHeading: FC<IAlertHeadingProps> = ({
	className,
	children,
	tag = 'h4',
	...props
}) => {
	return (
		// eslint-disable-next-line react/jsx-props-no-spreading
		<TagWrapper tag={tag} className={classNames('alert-heading', className)} {...props}>
			{children}
		</TagWrapper>
	);
};

interface IAlertLinkProps extends Record<string, any> {
	className?: string;
	children: ReactNode;
	href?: string | undefined;
	to?: string;
}
export const AlertLink: FC<IAlertLinkProps> = ({ className, children, href, to, ...props }) => {
	const LINK_CLASSES = classNames('alert-link', className);
	if (to) {
		return (
			// eslint-disable-next-line react/jsx-props-no-spreading
			<NavLink to={to} className={LINK_CLASSES} {...props}>
				{children}
			</NavLink>
		);
	}
	return (
		<a href={href} className={LINK_CLASSES} {...props}>
			{children}
		</a>
	);
};

interface IAlertProps extends Record<string, any> {
	className?: string;
	children: ReactNode;
	color?: TColor;
	isDismissible?: boolean;
	isOutline?: boolean;
	isLight?: boolean;
	shadow?: null | 'sm' | 'md' | 'lg' | '3d';
	icon?: TIcons;
	rounded?: null | 'default' | 0 | 1 | 2 | 3 | 'pill';
	borderWidth?: null | 0 | 1 | 2 | 3 | 4 | 5;
}
const Alert: FC<IAlertProps> = ({
	children,
	className,
	color = 'primary',
	isDismissible,
	isOutline,
	isLight,
	shadow,
	icon,
	rounded,
	borderWidth,
	...props
}) => {
	const [status, setStatus] = useState<boolean>(true);
	if (status) {
		return (
			<div
				className={classNames(
					'alert',
					{
						[`alert-${color}`]: color && !(isLight || isOutline),
						'alert-dismissible': isDismissible,
						fade: isDismissible,
						show: isDismissible,
						[`alert-light-${color}`]: isLight,
						[`alert-outline-${color}`]: isOutline,
						[`shadow${shadow !== 'md' ? `-${shadow}` : ''}`]:
							!!shadow && shadow !== '3d',
						[`border-${borderWidth}`]: borderWidth || borderWidth === 0,
						[`rounded${rounded !== 'default' ? `-${rounded}` : ''}`]:
							rounded || rounded === 0,
						[`shadow-3d-${color}`]: shadow === '3d',
					},
					className,
				)}
				{...props}
				role='alert'>
				{icon ? (
					<>
						<div className='alert-icon'>
							<Icon icon={icon} />
						</div>
						<div className='alert-text'>{children}</div>
					</>
				) : (
					children
				)}
				{isDismissible && (
					<button
						type='button'
						className='btn-close'
						aria-label='Close'
						onClick={() => setStatus(false)}
					/>
				)}
			</div>
		);
	}
	return null;
};

export default Alert;
