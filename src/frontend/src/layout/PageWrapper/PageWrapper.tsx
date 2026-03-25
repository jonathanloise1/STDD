import React, { ReactElement, useContext, useEffect, useLayoutEffect } from 'react';
import classNames from 'classnames';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ISubHeaderProps } from '../SubHeader/SubHeader';
import { IPageProps } from '../Page/Page';
import AuthContext from '../../contexts/authContext';
import { demoPagesMenu } from '../../menu';

// Apple-style page transition variants
const pageTransitionVariants = {
	initial: {
		opacity: 0,
		y: 8,
		scale: 0.99,
	},
	animate: {
		opacity: 1,
		y: 0,
		scale: 1,
		transition: {
			duration: 0.25,
			ease: [0.25, 0.1, 0.25, 1] as [number, number, number, number],
		},
	},
	exit: {
		opacity: 0,
		transition: {
			duration: 0.15,
			ease: 'easeOut' as const,
		},
	},
};

interface IPageWrapperProps {
	isProtected?: boolean;
	title?: string;
	description?: string;
	children:
		| ReactElement<ISubHeaderProps>[]
		| ReactElement<IPageProps>
		| ReactElement<IPageProps>[];
	className?: string;
	ref?: React.Ref<HTMLDivElement>;
}
const PageWrapper = ({ isProtected = true, title, description, className, children, ref }: IPageWrapperProps) => {
		useLayoutEffect(() => {
			// @ts-ignore
			document.getElementsByTagName('TITLE')[0].text = `${title ? `${title}` : ''}`;
			// @ts-ignore
			document
				?.querySelector('meta[name="description"]')
				.setAttribute('content', description || import.meta.env.VITE_META_DESC || '');
		});

		// const { userToken } = useContext(AuthContext);

		// const navigate = useNavigate();
		// useEffect(() => {
		// 	if (isProtected && userToken === '') {
		// 		navigate(`../${demoPagesMenu.login.path}`);
		// 	}
		// 	return () => {};
		// 	// eslint-disable-next-line react-hooks/exhaustive-deps
		// }, []);

		return (
			<motion.div
				ref={ref}
				className={classNames('page-wrapper', 'container-fluid', className)}
				variants={pageTransitionVariants}
				initial='initial'
				animate='animate'
				exit='exit'>
				{children}
			</motion.div>
		);
};PageWrapper.displayName = 'PageWrapper';

export default PageWrapper;