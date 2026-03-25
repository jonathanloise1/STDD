import React, { FC } from 'react';
import Button, { IButtonProps } from '../../components/bootstrap/Button';

interface ICommonStoryBtnProps extends IButtonProps {
	to: string;
}
const CommonStoryBtn: FC<ICommonStoryBtnProps> = ({ to, ...props }) => {
	return (
		<Button
			color='storybook'
			icon='CustomStorybook'
			tag='a'
			target='_blank'
			isLight
			href={`${import.meta.env.VITE_STORYBOOK_URL}${to}`}
			// eslint-disable-next-line react/jsx-props-no-spreading
			{...props}>
			Storybook
		</Button>
	);
};

export default CommonStoryBtn;
