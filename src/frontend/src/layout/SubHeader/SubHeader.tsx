import React, { FC, ReactNode, useLayoutEffect } from 'react';
import classNames from 'classnames';
import { useMeasure } from 'react-use';

interface ISubHeaderLeftProps {
	children: ReactNode;
	className?: string;
}
export const SubHeaderLeft: FC<ISubHeaderLeftProps> = ({ children, className }) => {
	return <div className={classNames('subheader-left', 'col-sm', className)}>{children}</div>;
};

interface ISubHeaderRightProps {
	children: ReactNode;
	className?: string;
}
export const SubHeaderRight: FC<ISubHeaderRightProps> = ({ children, className }) => {
	return (
		<div className={classNames('subheader-right', 'col-sm-auto', className)}>{children}</div>
	);
};

interface ISubheaderSeparatorProps {
	className?: string;
}
export const SubheaderSeparator: FC<ISubheaderSeparatorProps> = ({ className }) => {
	return <div className={classNames('subheader-separator', className)} />;
};

export interface ISubHeaderProps {
	children: ReactNode;
	className?: string;
	id?: string;
}
const SubHeader: FC<ISubHeaderProps> = ({ children, className, id }) => {
	const [ref, { height }] = useMeasure<HTMLDivElement>();

	const root = document.documentElement;
	root.style.setProperty('--subheader-height', `${height}px`);

	useLayoutEffect(() => {
		document.body.classList.add('subheader-enabled');
		return () => {
			document.body.classList.remove('subheader-enabled');
		};
	});

	return (
		<div ref={ref} id={id} className={classNames('subheader', 'row', className)}>
			{children}
		</div>
	);
};

export default SubHeader;
