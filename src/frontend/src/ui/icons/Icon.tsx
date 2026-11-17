import React, { FC } from 'react';
import classNames from 'classnames';
import { registry, IconName } from './registry';

export type IconSize = 'sm' | 'md' | 'lg' | '2x' | '3x' | '5x';

const SIZES: Record<IconSize, string> = {
	sm: '1em',
	md: '1.25em',
	lg: '1.5em',
	'2x': '2em',
	'3x': '3em',
	'5x': '5em',
};

export interface IIconProps {
	icon: IconName;
	size?: IconSize;
	className?: string;
}

/** A decorative icon of the registry: hidden from assistive technology, coloured by the text colour (frontend.md 5). */
export const Icon: FC<IIconProps> = ({ icon, size = 'md', className }) => {
	const Glyph = registry[icon];
	return (
		<Glyph
			size={SIZES[size]}
			className={classNames('app-icon', className)}
			aria-hidden='true'
			focusable='false'
		/>
	);
};
