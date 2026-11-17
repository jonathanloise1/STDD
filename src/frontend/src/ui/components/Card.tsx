import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';
import { Card as RbCard } from 'react-bootstrap';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';

/** A region of a page: `div.card`, or `form.card` with `as='form'` (frontend.md 7). */
export const Card = RbCard;
export const CardBody = RbCard.Body;
export const CardFooter = RbCard.Footer;

export interface ICardHeaderProps {
	title?: ReactNode;
	icon?: IconName;
	/** Buttons shown at the right end of the header. */
	actions?: ReactNode;
	titleAs?: 'h2' | 'h3';
	className?: string;
	children?: ReactNode;
}

/** The header of a card: icon and title on the left, actions on the right. */
export const CardHeader: FC<ICardHeaderProps> = ({
	title,
	icon,
	actions,
	titleAs: TitleTag = 'h2',
	className,
	children,
}) => (
	<div className={classNames('card-header d-flex align-items-center gap-2', className)}>
		{(title || icon) && (
			<TitleTag className='h6 mb-0 d-flex align-items-center gap-2'>
				{icon && <Icon icon={icon} size='lg' className='text-primary' />}
				{title}
			</TitleTag>
		)}
		{children}
		{actions && <div className='ms-auto d-flex align-items-center gap-2'>{actions}</div>}
	</div>
);
