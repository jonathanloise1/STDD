import React, { FC } from 'react';
import classNames from 'classnames';

interface ICarouselControlProps {
	id?: string;
	direction: 'prev' | 'next';
	onClickHandler(...args: unknown[]): unknown;
	directionText?: string;
	className?: string;
}
const CarouselControl: FC<ICarouselControlProps> = (props) => {
	const { id, direction, onClickHandler, directionText, className } = props;

	return (
		<button
			className={classNames(className, `carousel-control-${direction}`)}
			type='button'
			data-bs-target={id || 'carousel'}
			data-bs-slide={direction}
			onClick={(e) => {
				e.preventDefault();
				onClickHandler();
			}}>
			<span className={classNames(`carousel-control-${direction}-icon`)} aria-hidden='true' />
			<span className={classNames('visually-hidden')}>{directionText || direction}</span>
		</button>
	);
};

export default CarouselControl;
