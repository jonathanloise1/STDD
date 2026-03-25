import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';

interface ICarouselCaptionProps {
	captionHeader?: ReactNode;
	captionText: ReactNode;
	className?: string;
}
const CarouselCaption: FC<ICarouselCaptionProps> = (props) => {
	const { captionHeader, captionText, className } = props;
	return (
		<div className={classNames(className, 'carousel-caption', 'd-none', 'd-md-block')}>
			<h5>{captionHeader}</h5>
			<p>{captionText}</p>
		</div>
	);
};

export default CarouselCaption;
