import React, { FC } from 'react';
import Icon from '../../components/icon/Icon';
import useDarkMode from '../../hooks/useDarkMode';

interface ICommonFilterTagProps {
	title: string;
	text: string;
}
const CommonFilterTag: FC<ICommonFilterTagProps> = ({ title, text }) => {
	const { darkModeStatus } = useDarkMode();

	return (
		<small
			className={`bg-l${
				darkModeStatus ? 'o25' : '10'
			}-info text-info px-3 py-2 rounded-pill`}>
			<strong>{title}:</strong> {text}
			<Icon icon='ErrorCircle' color='danger' size='lg' className='ms-2 cursor-pointer' />
		</small>
	);
};

export default CommonFilterTag;
