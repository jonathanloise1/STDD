import React, { cloneElement, FC, ReactNode, useState } from 'react';
import { usePopper } from 'react-popper';
import classNames from 'classnames';
import Portal from '../../layout/Portal/Portal';

interface ITooltipsProps {
	children: ReactNode;
	title: ReactNode;
	placement?: 'auto' | 'top' | 'bottom' | 'right' | 'left';
	flip?: ('auto' | 'top' | 'bottom' | 'right' | 'left')[];
	delay?: number;
	isDisplayInline?: boolean;
	className?: string;
	modifiers?: object;
	isDisableElements?: boolean;
}
const Tooltips: FC<ITooltipsProps> = ({
	children,
	className,
	title,
	placement = 'top',
	flip = ['top', 'bottom'],
	delay = 0,
	isDisplayInline,
	isDisableElements,
	modifiers,
}) => {
	const [referenceElement, setReferenceElement] = useState(null);
	const [popperElement, setPopperElement] = useState(null);
	const [arrowElement, setArrowElement] = useState(null);
	const { styles, attributes } = usePopper(referenceElement, popperElement, {
		placement,
		modifiers: [
			{
				name: 'offset',
				options: {
					offset: [0, -3],
				},
			},
			{
				name: 'flip',
				enabled: true,
				options: {
					// @ts-ignore
					fallbackPlacements: flip,
				},
			},
			{
				name: 'arrow',
				options: {
					element: arrowElement,
				},
			},
			{ ...modifiers },
		],
	});

	const [tooltipOpen, setTooltipOpen] = useState(false);

	const ON_MOUSE_OVER = () => {
		setTooltipOpen(true);
		// @ts-ignore
		if (children?.props?.onMouseOver) children.props.onMouseOver();
	};

	const ON_MOUSE_LEAVE = () => {
		setTimeout(() => setTooltipOpen(false), delay);
		// @ts-ignore
		if (children?.props?.onMouseLeave) children.props.onMouseLeave();
	};

	const PROPS = {
		className: classNames(
			{ 'd-inline-block': isDisplayInline, 'tooltip-string': typeof children === 'string' },
			// @ts-ignore
			children?.props?.className,
		),
		onMouseOver: ON_MOUSE_OVER,
		onMouseLeave: ON_MOUSE_LEAVE,
	};

	return (
		<>
			{cloneElement(
				// @ts-ignore
				typeof children === 'string' ? (
					// @ts-ignore

					<span ref={setReferenceElement} {...PROPS}>
						{children}
					</span>
				) : (
					(isDisableElements && (
						<span className='d-inline-block' tabIndex={0}>
							{children}
						</span>
					)) ||
						children
				),
				{
					ref: setReferenceElement,
					...PROPS,
				},
			)}
			{tooltipOpen && (
				<Portal>
					<div
						// @ts-ignore
						ref={setPopperElement}
						role='tooltip'
						className={classNames('tooltip bs-tooltip-auto show', className)}
						style={styles.popper}
						{...attributes.popper}>
						{/* @ts-ignore */}
						<div ref={setArrowElement} className='tooltip-arrow' style={styles.arrow} />
						<div className='tooltip-inner'>{title}</div>
					</div>
				</Portal>
			)}
		</>
	);
};

export default Tooltips;
