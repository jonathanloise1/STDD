import React, {
	cloneElement,
	ElementType,
	FC,
	HTMLAttributes,
	JSXElementConstructor,
	ReactElement,
	ReactNode,
	useCallback,
	useMemo,
	useRef,
	useState,
} from 'react';
import { usePopper } from 'react-popper';
import type { Placement } from '@popperjs/core';
import classNames from 'classnames';
// @ts-ignore
import useEventOutside from '@omtanke/react-use-event-outside';
import useDarkMode from '../../hooks/useDarkMode';

import { IButtonProps } from './Button';
import { TDropdownDirection } from '../../type/dropdown-type';

interface IDropdownToggleProps {
	children: ReactElement<IButtonProps> | ReactNode;
	isOpen?: boolean;
	setIsOpen?: (value: ((prevState: boolean) => boolean) | boolean | null) => void | null;
	hasIcon?: boolean;
	setReferenceElement?: (node: HTMLElement | null) => void;
}
export const DropdownToggle: FC<IDropdownToggleProps> = ({
	children,
	isOpen,
	setIsOpen = () => {},
	hasIcon = true,
	setReferenceElement,
}) => {
	return cloneElement(
		// @ts-ignore
		children.props.isButtonGroup ? (
			<span className='visually-hidden'>Toggle Dropdown</span>
		) : (
			children
		),
		{
			ref: (node: HTMLElement | null) => {
				if (setReferenceElement) setReferenceElement(node);
			},
			onClick: () => {
				// @ts-ignore
				// eslint-disable-next-line @typescript-eslint/no-unused-expressions
				children?.props?.onClick ? children.props.onClick() : null;
				if (setIsOpen) {
					setIsOpen(!isOpen);
				}
			},
			className: classNames(
				{
					'dropdown-toggle': hasIcon,
					// @ts-ignore
					'dropdown-toggle-split': children.props.isButtonGroup,
					// Only presentation
					show: isOpen,
				},
				// @ts-ignore
				children?.props?.className,
			),
			'aria-expanded': isOpen,
		},
	);
};
DropdownToggle.displayName = 'DropdownToggle';

interface IDropdownMenuProps extends HTMLAttributes<HTMLUListElement> {
	isOpen?: boolean;
	setIsOpen?: (value: ((prevState: boolean) => boolean) | boolean | null) => void | null;
	children:
		| ReactElement<IDropdownItemProps>
		| ReactElement<IDropdownItemProps>[]
		| ReactNode
		| ReactNode[];
	className?: string;
	isAlignmentEnd?: boolean;
	breakpoint?: 'sm' | 'md' | 'lg' | 'xl' | 'xxl' | null;
	size?: 'sm' | 'md' | 'lg' | null;
	direction?: string | null;
	isCloseAfterLeave?: boolean;
	popperStyles?: React.CSSProperties;
	popperAttributes?: Record<string, string | undefined>;
	setPopperElement?: (node: HTMLUListElement | null) => void;
	popperPlacement?: string;
}
export const DropdownMenu: FC<IDropdownMenuProps> = ({
	isOpen,
	setIsOpen = () => {},
	children,
	className,
	isAlignmentEnd,
	breakpoint,
	size,
	direction,
	isCloseAfterLeave = true,
	popperStyles,
	popperAttributes,
	setPopperElement,
	popperPlacement,
	...props
}) => {
	const { darkModeStatus } = useDarkMode();

	if (isOpen) {
		return (
			<ul
				role='presentation'
				ref={setPopperElement}
				// dynamic positioning must be disabled for responsive alignment
				style={!breakpoint ? popperStyles : undefined}
				data-placement={popperPlacement}
				className={classNames(
					'dropdown-menu',
					// For Bootstrap
					'show',
					{ 'dropdown-menu-dark': darkModeStatus },
					{
						[`dropdown-menu-${size}`]: size,
						'dropdown-menu-end': !isAlignmentEnd && breakpoint,
						[`dropdown-menu${breakpoint ? `-${breakpoint}` : ''}-${
							isAlignmentEnd ? 'end' : 'start'
						}`]: isAlignmentEnd || breakpoint,
					},
					className,
				)}
				data-bs-popper={breakpoint ? 'static' : null}
				onMouseLeave={
					isCloseAfterLeave && setIsOpen ? () => setIsOpen(false) : undefined
				}
				{...popperAttributes}
				{...props}>
				{children}
			</ul>
		);
	}
	return null;
};
DropdownMenu.displayName = 'DropdownMenu';

interface IItemWrapperProps {
	children: ReactNode;
	className?: string;
	ref?: React.Ref<HTMLLIElement>;
}
const ItemWrapper = ({ children, className, ref, ...props }: IItemWrapperProps) => {
	return (
		<li ref={ref} className={classNames('dropdown-item-wrapper', className)} {...props}>
			{children}
		</li>
	);
};
ItemWrapper.displayName = 'ItemWrapper';

interface IDropdownItemProps extends HTMLAttributes<HTMLLIElement> {
	children?: ReactElement<any, string | JSXElementConstructor<any>> | string;
	isHeader?: boolean;
	isDivider?: boolean;
	isText?: boolean;
	ref?: React.Ref<HTMLLIElement>;
}
export const DropdownItem = ({ children, isHeader, isDivider, isText, ref, ...props }: IDropdownItemProps) => {
		if (isHeader) {
			return (
				// eslint-disable-next-line react/jsx-props-no-spreading
				<ItemWrapper ref={ref} {...props}>
					{cloneElement(
						// @ts-ignore
						typeof children === 'string' ? <h6>{children}</h6> : children,
						{
							// @ts-ignore

							className: classNames('dropdown-header', children?.props?.className),
						},
					)}
				</ItemWrapper>
			);
		}
		if (isDivider) {
			return (
				// eslint-disable-next-line react/jsx-props-no-spreading
				<ItemWrapper ref={ref} {...props}>
					{} {/* @ts-ignore */}
					<hr className={classNames('dropdown-divider', children?.props?.className)} />
				</ItemWrapper>
			);
		}
		if (isText) {
			return (
				// eslint-disable-next-line react/jsx-props-no-spreading
				<ItemWrapper ref={ref} {...props}>
					{cloneElement(
						// @ts-ignore
						typeof children === 'string' ? <div>{children}</div> : children,
						{
							className: classNames(
								'dropdown-item-text',
								'dropdown-item',
								'disabled',
								// @ts-ignore

								children?.props?.className,
							),
						},
					)}
				</ItemWrapper>
			);
		}
		return (
			// eslint-disable-next-line react/jsx-props-no-spreading
			<ItemWrapper ref={ref} {...props}>
				{cloneElement(
					// @ts-ignore
					typeof children === 'string' ? <span>{children}</span> : children,
					{
						// @ts-ignore

						className: classNames('dropdown-item', children?.props?.className),
					},
				)}
			</ItemWrapper>
		);
	};
DropdownItem.displayName = 'DropdownItem';

export interface IDropdownProps {
	tag?: ElementType;
	children: ReactElement<IDropdownToggleProps>[] | ReactElement<IDropdownMenuProps>[];
	isOpen?: boolean | null;
	setIsOpen?(...args: unknown[]): unknown;
	direction?: TDropdownDirection;
	isButtonGroup?: boolean;
	className?: string;
}
const Dropdown: FC<IDropdownProps> = ({
	tag: Tag = 'div',
	children,
	isOpen,
	setIsOpen,
	direction = 'down',
	isButtonGroup,
	className,
}) => {
	const [state, setState] = useState(isOpen !== null && !!setIsOpen ? isOpen : false);

	const dropdownRef = useRef(null);

	// usePopper: React 19-compatible positioning via state-based refs
	const [referenceElement, setReferenceElement] = useState<HTMLElement | null>(null);
	const [popperElement, setPopperElement] = useState<HTMLUListElement | null>(null);

	const currentIsOpen = isOpen !== null && !!setIsOpen ? isOpen : state;
	const currentSetIsOpen = isOpen !== null && !!setIsOpen ? setIsOpen : setState;

	// Compute placement from direction + child props
	const placementInfo = useMemo(() => {
		// Find the DropdownMenu child to read isAlignmentEnd / breakpoint
		let isAlignmentEnd = false;
		let breakpoint: string | null = null;
		React.Children.forEach(children, (child: any) => {
			if (child?.type?.displayName === 'DropdownMenu') {
				if (child.props.isAlignmentEnd) isAlignmentEnd = true;
				if (child.props.breakpoint) breakpoint = child.props.breakpoint;
			}
		});

		const yAxis =
			(direction === 'up' && 'top') ||
			(direction === 'end' && 'right') ||
			(direction === 'start' && 'left') ||
			'bottom';
		const xAxis = isAlignmentEnd ? 'end' : 'start';
		const placement: Placement = breakpoint ? 'bottom-start' : `${yAxis}-${xAxis}` as Placement;
		return { placement, xAxis, breakpoint };
	}, [direction, children]);

	const { styles: popperStyles, attributes: popperAttributes, state: popperState } = usePopper(
		referenceElement,
		currentIsOpen ? popperElement : null,
		{
			placement: placementInfo.placement,
			modifiers: [
				{
					name: 'flip',
					options: {
						fallbackPlacements: [`top-${placementInfo.xAxis}` as Placement, `bottom-${placementInfo.xAxis}` as Placement],
					},
				},
			],
		},
	);

	// Clicking outside to close
	const closeMenu = useCallback(() => {
		if (isOpen !== null && !!setIsOpen) {
			setIsOpen(false);
		} else {
			setState(false);
		}
	}, [isOpen, setIsOpen]);

	useEventOutside(dropdownRef, 'mousedown', closeMenu);
	useEventOutside(dropdownRef, 'touchstart', closeMenu);

	return (
		<Tag
			ref={dropdownRef}
			className={classNames(
				{
					[`drop${direction}`]: direction && !isButtonGroup,
					'btn-group': isButtonGroup,
				},
				className,
			)}>
			{/* @ts-ignore */}
			{children.map((child: ReactElement, index: any) => {
				// @ts-ignore
				if (child?.type?.displayName === 'DropdownToggle') {
					return cloneElement(child as any, {
						isOpen: currentIsOpen,
						setIsOpen: currentSetIsOpen,
						setReferenceElement,
						direction,
						key: index,
					} as any);
				}
				// @ts-ignore
				if (child?.type?.displayName === 'DropdownMenu') {
					return cloneElement(child as any, {
						isOpen: currentIsOpen,
						setIsOpen: currentSetIsOpen,
						direction,
						setPopperElement,
						popperStyles: popperStyles.popper,
						popperAttributes: popperAttributes.popper,
						popperPlacement: popperState?.placement,
						key: index,
					} as any);
				}
				return child;
			})}
		</Tag>
	);
};
Dropdown.displayName = 'Dropdown';

export default Dropdown;
