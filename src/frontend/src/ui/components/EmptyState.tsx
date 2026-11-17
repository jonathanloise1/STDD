import React, { FC, ReactNode } from 'react';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';

export interface IEmptyStateProps {
	icon: IconName;
	title: string;
	text?: string;
	action?: ReactNode;
}

/** A list with nothing to show (frontend.md 8.4). */
export const EmptyState: FC<IEmptyStateProps> = ({ icon, title, text, action }) => (
	<div className='text-center py-5 text-body-secondary'>
		<Icon icon={icon} size='3x' className='mb-3 opacity-50' />
		<p className='mb-1'>{title}</p>
		{text && <p className='mb-0'>{text}</p>}
		{action}
	</div>
);
