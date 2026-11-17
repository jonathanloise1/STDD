import React, { ComponentPropsWithoutRef, FC } from 'react';
import classNames from 'classnames';

export type Tone =
	| 'primary'
	| 'secondary'
	| 'success'
	| 'info'
	| 'warning'
	| 'danger'
	| 'light'
	| 'dark';

export interface IBadgeProps extends ComponentPropsWithoutRef<'span'> {
	tone: Tone;
}

/** Status, role or count as a pill. It always carries text (frontend.md 4.2). */
export const Badge: FC<IBadgeProps> = ({ tone, className, children, ...rest }) => (
	<span {...rest} className={classNames('badge rounded-pill', `text-bg-${tone}`, className)}>
		{children}
	</span>
);
