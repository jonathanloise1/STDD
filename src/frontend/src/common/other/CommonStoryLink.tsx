import React, { FC, HTMLAttributes, ReactNode } from 'react';

interface ICommonStoryLinkProps extends HTMLAttributes<HTMLAnchorElement> {
	children: ReactNode;
	to: string;
}
const CommonStoryLink: FC<ICommonStoryLinkProps> = ({ to, children, ...props }) => {
	return (
		<a
			href={`${import.meta.env.VITE_STORYBOOK_URL}${to}`}
			target='_blank'
			rel='noreferrer'
			{...props}>
			{children}
		</a>
	);
};

export default CommonStoryLink;
