import React, { ReactNode, SetStateAction, useState } from 'react';
import CarouselIndicators from './CarouselIndicators';
import CarouselControl from './CarouselControl';
import CarouselContainer from './CarouselContainer';
import CarouselItem from './CarouselItem';
import CarouselCaption from './CarouselCaption';
import { TCarouselRounded } from '../../type/carousel-type';

interface ICarouselProps {
	id?: string;
	items?: {
		src?: string;
		altText?: string;
		captionHeader?: string;
		captionText?: string;
	}[];
	children?: ReactNode;
	activeItemIndex?: number;
	isKeyboardControl?: boolean;
	isHoverPause?: boolean;
	isRide?: boolean;
	interval?: number | string | boolean;
	mouseEnter?(...args: unknown[]): unknown;
	mouseLeave?(...args: unknown[]): unknown;
	isSlide?: boolean;
	isDark?: boolean;
	className?: string;
	isEnableTouch?: boolean;
	isFade?: boolean;
	isIndicators?: boolean;
	isControl?: boolean;
	rounded?: TCarouselRounded;
	isFluid?: boolean;
	height?: number;
	ref?: React.Ref<HTMLDivElement>;
}
const Carousel = ({
		id,
		items,
		children,
		activeItemIndex = 0,
		className,
		isKeyboardControl,
		isHoverPause,
		isRide = true,
		interval = 5000,
		mouseEnter,
		mouseLeave,
		isSlide = true,
		isDark,
		isEnableTouch = true,
		isFade,
		isIndicators = true,
		isControl = true,
		rounded,
		isFluid,
		height,
		ref,
	}: ICarouselProps) => {
		const ITEMS = items || children;
		const [activeIndex, setActiveIndex] = useState<number | undefined>(activeItemIndex);
		const [animating, setAnimating] = useState(false);

		const next = () => {
			if (animating) return;
			// @ts-ignore

			const nextIndex = activeIndex === ITEMS?.length - 1 ? 0 : activeIndex + 1;
			setActiveIndex(nextIndex);
		};

		const previous = () => {
			if (animating) return;
			// @ts-ignore
			const nextIndex = activeIndex === 0 ? ITEMS.length - 1 : activeIndex - 1;
			setActiveIndex(nextIndex);
		};

		const goToIndex = (newIndex: SetStateAction<number | undefined>) => {
			if (animating) return;
			setActiveIndex(newIndex);
		};

		const getSlideContent = (_item: {
			src?: string;
			altText?: string;
			captionHeader?: string;
			captionText?: string;
		}) => {
			if (items) {
				if (isFluid) {
					return (
						<>
							<div
								className='carousel-slide h-100'
								style={{
									backgroundImage: `url(${_item.src})`,
								}}
							/>
							<CarouselCaption
								captionText={_item.captionText}
								captionHeader={_item.captionHeader}
							/>
						</>
					);
				}
				return (
					<>
						<img src={_item.src} alt={_item.altText} className='d-block w-100' />
						<CarouselCaption
							captionText={_item.captionText}
							captionHeader={_item.captionHeader}
						/>
					</>
				);
			}
			return _item;
		};

		return (
			<CarouselContainer
				ref={ref}
				id={id}
				activeIndex={activeIndex}
				next={next}
				previous={previous}
				keyboard={isKeyboardControl}
				pause={isHoverPause ? 'hover' : false}
				ride={isRide ? 'carousel' : undefined}
				interval={interval}
				mouseEnter={mouseEnter}
				mouseLeave={mouseLeave}
				slide={isSlide || isFade}
				dark={isDark}
				className={className}
				isFluid={isFluid}
				hasChildren={!!children}
				height={height}
				enableTouch={isEnableTouch}
				fade={isFade}
				rounded={rounded}>
				{isIndicators && (
					<CarouselIndicators
						id={id}
						items={ITEMS}
						activeIndex={activeIndex}
						onClickHandler={goToIndex}
					/>
				)}
				{Array.isArray(ITEMS) &&
					ITEMS?.map((item, index) => {
						return (
							<CarouselItem
								onExiting={() => setAnimating(true)}
								onExited={() => setAnimating(false)}
								key={index}
								isFluid={isFluid || !!children}>
								{/* @ts-ignore */}
								{getSlideContent(item)}
							</CarouselItem>
						);
					})}
				{isControl && (
					<>
						<CarouselControl
							id={id}
							direction='prev'
							directionText='Previous'
							onClickHandler={previous}
						/>
						<CarouselControl
							id={id}
							direction='next'
							directionText='Next'
							onClickHandler={next}
						/>
					</>
				)}
			</CarouselContainer>
		);
	};
Carousel.displayName = 'Carousels';

export default Carousel;
