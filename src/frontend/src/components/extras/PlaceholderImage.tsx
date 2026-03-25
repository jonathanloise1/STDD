import React, { FC, HTMLAttributes } from 'react';
import classNames from 'classnames';

interface IPlaceholderImageProps extends HTMLAttributes<HTMLOrSVGElement> {
	width?: string | number;
	height?: string | number;
	text?: string;
	className?: string;
	ariaLabel?: string;
}
const PlaceholderImage: FC<IPlaceholderImageProps> = ({
	width = 75,
	height = 75,
	text,
	className,
	ariaLabel,
}) => {
	return (
		<svg
			className={classNames('placeholder-img', className)}
			width={width}
			height={height}
			xmlns='http://www.w3.org/2000/svg'
			role='img'
			aria-label={ariaLabel || `Example placeholder image: ${width}x${height}`}
			preserveAspectRatio='xMidYMid slice'>
			<title>Example placeholder image</title>
			<rect width='100%' height='100%' fill='var(--bs-gray)' />
			<text
				x='50%'
				y='50%'
				dominantBaseline='middle'
				textAnchor='middle'
				fill='#dee2e6'
				fontSize='1.25rem'
				dy='.1rem'>
				{text || `${width}x${height}`}
			</text>
		</svg>
	);
};

export default PlaceholderImage;
