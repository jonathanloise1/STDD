import React, { FC } from 'react';
import classNames from 'classnames';
import { Button, IButtonProps } from './Button';
import type { IconName } from '../icons/registry';

export interface IIconButtonProps
	extends Omit<IButtonProps, 'children' | 'icon' | 'aria-label' | 'title' | 'to'> {
	icon: IconName;
	/** Required: it is the accessible name and the tooltip. */
	label: string;
}

/** An action shown as an icon only. The label is its accessible name (frontend.md 7). */
export const IconButton: FC<IIconButtonProps> = ({ icon, label, className, ...rest }) => (
	<Button
		{...rest}
		icon={icon}
		aria-label={label}
		title={label}
		className={classNames('app-icon-btn', className)}
	/>
);
