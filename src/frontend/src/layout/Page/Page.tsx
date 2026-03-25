import React, { ReactNode } from 'react';
import classNames from 'classnames';

export interface IPageProps {
	children: ReactNode;
	container?: boolean | 'sm' | 'md' | 'lg' | 'xl' | 'xxl' | 'fluid';
	className?: string;
	ref?: React.Ref<HTMLDivElement>;
}
const Page = ({ children, className, container = 'xxl', ref, ...props }: IPageProps) => {
	return (
		<div
			ref={ref}
			className={classNames('page', className, {
				[`container${typeof container === 'string' ? `-${container}` : ''}`]: container,
			})}
			{...props}>
			{children}
		</div>
	);
};
Page.displayName = 'Page';

export default Page;
