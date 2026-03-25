import React, { cloneElement, FC, HTMLAttributes, ReactElement, ReactNode, useState } from 'react';
import { usePopper } from 'react-popper';
import classNames from 'classnames';
import Portal from '../../layout/Portal/Portal';

type Direction =
	| 'auto'
	| 'auto-start'
	| 'auto-end'
	| 'top'
	| 'top-start'
	| 'top-end'
	| 'bottom'
	| 'bottom-start'
	| 'bottom-end'
	| 'right'
	| 'right-start'
	| 'right-end'
	| 'left'
	| 'left-start'
	| 'left-end';

interface IPopoversProps extends HTMLAttributes<HTMLDivElement> {
	children: ReactElement | string;
	className?: string;
	bodyClassName?: string | null;
	title?: string;
	desc?: ReactNode;
	placement?: Direction;
	flip?: Direction[];
	trigger?: 'click' | 'hover';
	delay?: number;
	isDisplayInline?: boolean;
	modifiers?: object;
}
const Popovers: FC<IPopoversProps> = ({
	children,
	className,
	bodyClassName,
	title,
	desc,
	placement = 'top',
	flip = ['top', 'bottom'],
	trigger = 'click',
	delay = 0,
	isDisplayInline,
	modifiers,
	...props
}) => {
	const [referenceElement, setReferenceElement] = useState<HTMLSpanElement | null>(null);
	const [popperElement, setPopperElement] = useState<HTMLDivElement | null>(null);
	const [arrowElement, setArrowElement] = useState<HTMLDivElement | null>(null);
	const { styles, attributes } = usePopper(referenceElement, popperElement, {
		placement,
		modifiers: [
			{
				name: 'offset',
				options: {
					offset: [0, 6.5],
				},
			},
			{
				name: 'flip',
				enabled: true,
				options: {
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

	const [popoverOpen, setPopoverOpen] = useState<boolean>(false);

	const ON_CLICK = () => {
		if (trigger === 'click') setPopoverOpen(!popoverOpen);
		if (typeof children !== 'string' && (children?.props as any)?.onClick) (children.props as any).onClick();
	};

	const ON_MOUSE_OVER = () => {
		if (trigger === 'hover') setPopoverOpen(true);
		if (typeof children !== 'string' && (children?.props as any)?.onMouseOver)
			(children.props as any).onMouseOver();
	};

	const ON_MOUSE_LEAVE = () => {
		if (trigger === 'hover') setTimeout(() => setPopoverOpen(false), delay);
		if (typeof children !== 'string' && (children?.props as any)?.onMouseLeave)
			(children.props as any).onMouseLeave();
	};

	const PROPS = {
		className: classNames(
			{ 'd-inline-block': isDisplayInline, 'popover-string': typeof children === 'string' },
			typeof children !== 'string' && (children?.props as any)?.className,
		),
		onClick: ON_CLICK,
		onMouseOver: ON_MOUSE_OVER,
		onMouseLeave: ON_MOUSE_LEAVE,
	};

	return (
		<>
			{cloneElement(
				typeof children === 'string' ? (
					<span ref={setReferenceElement} {...PROPS}>
						{children}
					</span>
				) : (
					children
				),
				{
					ref: setReferenceElement,
					...PROPS,
				},
			)}
			{popoverOpen && (
				<Portal>
					<div
						ref={setPopperElement}
						role='tooltip'
						className={classNames('popover', 'bs-popover-auto', className)}
						style={styles.popper}
						{...props}
						{...attributes.popper}>
						<div ref={setArrowElement} className='popover-arrow' style={styles.arrow} />
						{title && <h3 className='popover-header'>{title}</h3>}
						{desc && (
							<div className={classNames('popover-body', bodyClassName)}>{desc}</div>
						)}
					</div>
				</Portal>
			)}
		</>
	);
};

export default Popovers;
