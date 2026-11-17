import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';
import type { Tone } from '../components/Badge';

export interface INoticeProps {
	tone?: Tone;
	icon?: IconName;
	className?: string;
	children?: ReactNode;
}

/** A static note on a page. It has no live role: it is not news (frontend.md 10.4). */
export const Notice: FC<INoticeProps> = ({ tone = 'info', icon, className, children }) => (
	<div className={classNames('alert d-flex gap-2', `alert-${tone}`, className)}>
		{icon && <Icon icon={icon} className='mt-1' />}
		<div>{children}</div>
	</div>
);
