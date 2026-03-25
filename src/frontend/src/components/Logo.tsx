import React, { FC } from 'react';

interface ILogoProps {
	width?: number;
	height?: number;
}
const Logo: FC<ILogoProps> = ({ width = 520, height = 120 }) => {
	return (
		<img
			src='/myapp-logo.svg'
			alt='MyApp'
			width={width}
			height={height}
			style={{ maxWidth: '100%', height: 'auto' }}
		/>
	);
};

export default Logo;
