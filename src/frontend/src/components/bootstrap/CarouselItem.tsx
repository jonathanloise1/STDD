import React, { Component, createRef, ReactNode } from 'react';
import classNames from 'classnames';
import { Transition } from 'react-transition-group';
import { TransitionStatuses, TransitionTimeouts } from './utils';

type TCarouselItemProps = {
	tag?: string;
	in?: boolean;
	children?: ReactNode;
	slide?: boolean;
	className?: string;
	isFluid?: boolean;
	onEnter(...args: unknown[]): unknown;
	onEntering(...args: unknown[]): unknown;
	onExit(...args: unknown[]): unknown;
	onExiting(...args: unknown[]): unknown;
	onExited(...args: unknown[]): unknown;
};
type TCarouselItemState = {
	startAnimation: boolean;
};
class CarouselItem extends Component<TCarouselItemProps, TCarouselItemState> {
	nodeRef = createRef<HTMLDivElement>();

	constructor(props: TCarouselItemProps | Readonly<TCarouselItemProps>) {
		super(props);

		this.state = {
			startAnimation: false,
		};

		this.onEnter = this.onEnter.bind(this);
		this.onEntering = this.onEntering.bind(this);
		this.onExit = this.onExit.bind(this);
		this.onExiting = this.onExiting.bind(this);
		this.onExited = this.onExited.bind(this);
	}

	onEnter(_node: any, isAppearing: any) {
		this.setState({ startAnimation: false });

		this.props.onEnter(this.nodeRef.current, isAppearing);
	}

	onEntering(_node: any, isAppearing: any) {
		// getting this variable triggers a reflow
		const { offsetHeight } = this.nodeRef.current!;
		this.setState({ startAnimation: true });

		this.props.onEntering(this.nodeRef.current, isAppearing);
		return offsetHeight;
	}

	onExit(_node: any) {
		this.setState({ startAnimation: false });

		this.props.onExit(this.nodeRef.current);
	}

	onExiting(_node: any) {
		this.setState({ startAnimation: true });
		this.nodeRef.current!.dispatchEvent(new CustomEvent('slide.bs.carousel'));

		this.props.onExiting(this.nodeRef.current);
	}

	onExited(_node: any) {
		this.nodeRef.current!.dispatchEvent(new CustomEvent('slid.bs.carousel'));

		this.props.onExited(this.nodeRef.current);
	}

	render() {
		const {
			in: isIn,
			children,
			slide,
			tag: Tag,
			className,
			isFluid,
			...transitionProps
		} = this.props;

		return (
			// @ts-ignore
			<Transition
				nodeRef={this.nodeRef}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...transitionProps}
				enter={slide}
				exit={slide}
				in={isIn}
				onEnter={this.onEnter}
				onEntering={this.onEntering}
				onExit={this.onExit}
				onExiting={this.onExiting}
				onExited={this.onExited}>
				{(status) => {
					// @ts-ignore
					const { direction } = this.context;
					const isActive =
						status === TransitionStatuses.ENTERED ||
						status === TransitionStatuses.EXITING;
					const directionClassName =
						(status === TransitionStatuses.ENTERING ||
							status === TransitionStatuses.EXITING) &&
						this.state.startAnimation &&
						(direction === 'end' ? 'carousel-item-start' : 'carousel-item-end');
					const orderClassName =
						status === TransitionStatuses.ENTERING &&
						(direction === 'end' ? 'carousel-item-next' : 'carousel-item-prev');
					const itemClasses = classNames(
						className,
						'carousel-item',
						isActive && 'active',
						directionClassName,
						orderClassName,
						{ 'h-100': isFluid },
					);

					// @ts-ignore
					return <Tag ref={this.nodeRef} className={itemClasses}>{children}</Tag>;
				}}
			</Transition>
		);
	}
}
// @ts-ignore
CarouselItem.defaultProps = {
	// @ts-ignore
	...Transition.defaultProps,
	tag: 'div',
	timeout: TransitionTimeouts.Carousel,
	slide: true,
	isFluid: false,
};

export default CarouselItem;
