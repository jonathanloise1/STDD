import React, { FC } from 'react';
import classNames from 'classnames';
import { Spinner as RbSpinner } from 'react-bootstrap';

export interface ISpinnerProps {
	size?: 'sm' | 'md' | 'lg';
	className?: string;
}

/** A decorative spinner for inside another element (frontend.md 7). */
export const Spinner: FC<ISpinnerProps> = ({ size = 'md', className }) => (
	<RbSpinner
		animation='border'
		aria-hidden='true'
		className={classNames('app-spinner', `app-spinner--${size}`, className)}
	/>
);
