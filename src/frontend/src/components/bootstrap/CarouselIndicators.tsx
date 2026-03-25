import React, { FC } from 'react';
import classNames from 'classnames';

interface ICarouselIndicatorsProps {
	id?: string;
	items:
		| {
				src?: string;
				altText?: string;
				captionHeader?: string;
				captionText?: string;
		  }[]
		| any;
	activeIndex: number | undefined;
	onClickHandler(...args: unknown[]): unknown;
	className?: string;
}
const CarouselIndicators: FC<ICarouselIndicatorsProps> = (props) => {
	const { id, items, activeIndex, onClickHandler, className } = props;

	return (
		<div className={classNames(className, 'carousel-indicators')}>
			{items.map((item: { altText: string | undefined }, index: unknown) => {
				const indicatorClasses = classNames({ active: activeIndex === index });
				return (
					<button
						// @ts-ignore

						key={index}
						type='button'
						data-bs-target={id || 'carousel'}
						data-bs-slide-to={index}
						className={indicatorClasses}
						aria-current={activeIndex === index ? 'true' : undefined}
						aria-label={item.altText}
						onClick={(e) => {
							e.preventDefault();
							onClickHandler(index);
						}}
					/>
				);
			})}
		</div>
	);
};

export default CarouselIndicators;
