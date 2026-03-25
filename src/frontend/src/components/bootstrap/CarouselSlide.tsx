import React, { FC, ReactNode } from 'react';

interface ICarouselSlideProps {
	children?: ReactNode;
	background?: string;
}
const CarouselSlide: FC<ICarouselSlideProps> = ({ children, background }) => {
	return (
		<div
			className='carousel-slide h-100 w-100'
			style={{ backgroundImage: `url(${background})` }}>
			{children}
		</div>
	);
};

export default CarouselSlide;
