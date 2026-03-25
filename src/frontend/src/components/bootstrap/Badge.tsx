import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';
import useDarkMode from '../../hooks/useDarkMode';
import { TColor } from '../../type/color-type';

interface IBadgeProps extends Record<string, any> {
	children: ReactNode;
	className?: string;
	color?: TColor;
	rounded?:
		| null
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
		| 'pill';
	shadow?: null | 'none' | 'sm' | 'default' | 'lg';
	isLight?: boolean;
}
const Badge: FC<IBadgeProps> = ({
	children,
	className,
	color = 'primary',
	shadow,
	rounded,
	isLight,
	...props
}) => {
	const { darkModeStatus } = useDarkMode();
	return (
		<span
			className={classNames(
				'badge',
				{
					[`bg-${color}`]: !isLight,
					[`bg-l${darkModeStatus ? 'o25' : '10'}-${color}`]: isLight,
					[`text-${color}`]: isLight,
					[`shadow${shadow !== 'default' ? `-${shadow}` : ''}`]: !!shadow,
					[`rounded${rounded !== 'default' ? `-${rounded}` : ''}`]: rounded,
					'rounded-0':
						rounded === 'bottom' ||
						rounded === 'top' ||
						rounded === 'end' ||
						rounded === 'start' ||
						rounded === 0,
				},
				className,
			)}
			{...props}>
			{children}
		</span>
	);
};

export default Badge;
