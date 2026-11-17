import React, { FC, ReactNode, useLayoutEffect } from 'react';
import classNames from 'classnames';

export interface IPageProps {
	/** Sets the document title. */
	title?: string;
	fluid?: boolean;
	className?: string;
	children?: ReactNode;
}

/** The root of every page: `.page-wrapper` holding the page container (frontend.md 6.5). */
export const Page: FC<IPageProps> = ({ title, fluid = false, className, children }) => {
	useLayoutEffect(() => {
		if (title) document.title = title;
	}, [title]);

	return (
		<div className='page-wrapper'>
			<div
				className={classNames(
					fluid ? 'container-fluid' : 'container-xxl',
					'app-page',
					className,
				)}>
				{children}
			</div>
		</div>
	);
};
