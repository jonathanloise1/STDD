import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';
import Alert from '../../components/bootstrap/Alert';
import { TColor } from '../../type/color-type';

interface ICommonDescProps {
	children: ReactNode;
	className?: string;
	color?: TColor;
}
const CommonDesc: FC<ICommonDescProps> = ({ children, className, color = 'warning' }) => {
	return (
		<Alert
			color={color}
			isLight
			shadow='md'
			borderWidth={0}
			icon='Info'
			className={classNames('flex-nowrap', 'w-100', 'mb-0', className)}>
			{children}
		</Alert>
	);
};

export default CommonDesc;
