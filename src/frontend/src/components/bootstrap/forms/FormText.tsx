import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';

interface IFormTextProps {
	id?: string;
	className?: string;
	children: ReactNode;
}
const FormText: FC<IFormTextProps> = ({ id, className, children, ...props }) => {
	return (
		<div id={id} className={classNames('form-text', className)} {...props}>
			{children}
		</div>
	);
};

export default FormText;
