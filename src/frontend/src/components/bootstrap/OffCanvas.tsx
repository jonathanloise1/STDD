import React, {
	FC,
	HTMLAttributes,
	ReactElement,
	ReactNode,
	use,
	useLayoutEffect,
	useRef,
} from 'react';
import classNames from 'classnames';
import { AnimatePresence, motion } from 'framer-motion';
import Portal from '../../layout/Portal/Portal';
import TagWrapper from '../TagWrapper';
import useEventListener from '../../hooks/useEventListener';
import ThemeContext from '../../contexts/themeContext';
import useDeviceScreen from '../../hooks/useDeviceScreen';
import { TOffCanvasPlacement } from '../../type/offCanvas-type';

interface IOffCanvasTitleProps extends HTMLAttributes<HTMLElement> {
	id: string;
	children: ReactNode;
	className?: string;
	tag?: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6' | 'div' | 'span';
	ref?: React.Ref<HTMLHeadingElement>;
}
export const OffCanvasTitle = ({ tag = 'h5', id, children, className, ref, ...props }: IOffCanvasTitleProps) => {
	return (
			<TagWrapper
				tag={tag}
				ref={ref}
				id={id}
				className={classNames('offcanvas-title', className)}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{children}
		</TagWrapper>
	);
};
OffCanvasTitle.displayName = 'OffCanvasTitle';

interface IOffCanvasHeaderProps extends HTMLAttributes<HTMLDivElement> {
	children: ReactElement<IOffCanvasTitleProps> | ReactNode;
	className?: string;
	setOpen?(...args: unknown[]): unknown | undefined;
	ref?: React.Ref<HTMLDivElement>;
}
export const OffCanvasHeader = ({ children, className, setOpen = () => {}, ref, ...props }: IOffCanvasHeaderProps) => {
	return (
			<div ref={ref} className={classNames('offcanvas-header', className)} {...props}>
				{children}
				{setOpen && (
					<button
						type='button'
						className='btn-close text-reset'
						data-bs-dismiss='offcanvas'
						aria-label='Close'
						onClick={() => setOpen(false)}
					/>
				)}
			</div>
		);
	};
OffCanvasHeader.displayName = 'OffCanvasHeader';

interface IOffCanvasBodyProps extends HTMLAttributes<HTMLElement> {
	tag?: 'div' | 'span' | 'section' | 'form';
	children: ReactNode;
	className?: string;
	onSubmit?(...args: unknown[]): unknown;
	ref?: React.Ref<HTMLDivElement>;
}
export const OffCanvasBody = ({ tag = 'div', children, className, ref, ...props }: IOffCanvasBodyProps) => {
	return (
			<TagWrapper
				tag={tag}
				ref={ref}
				className={classNames('offcanvas-body', className)}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{children}
		</TagWrapper>
	);
};
OffCanvasBody.displayName = 'OffCanvasBody';

interface IOffCanvasProps extends HTMLAttributes<HTMLElement> {
	id?: string;
	children:
		| ReactElement<IOffCanvasHeaderProps>
		| ReactElement<IOffCanvasHeaderProps>[]
		| ReactElement<IOffCanvasBodyProps>
		| ReactElement<IOffCanvasBodyProps>[]
		| ReactNode
		| ReactNode[];
	placement?: TOffCanvasPlacement;
	titleId?: string | null;
	isOpen: boolean;
	setOpen(...args: unknown[]): unknown;
	isBodyScroll?: boolean;
	isBackdrop?: boolean;
	isModalStyle?: boolean;
	isRightPanel?: boolean;
	tag?: 'div' | 'section' | 'form';
	onSubmit?(...args: unknown[]): unknown;
	noValidate?: true;
}
const OffCanvas: FC<IOffCanvasProps> = ({
	id,
	titleId,
	children,
	placement = 'end',
	isBodyScroll,
	isBackdrop = true,
	isModalStyle,
	isOpen,
	setOpen = () => {},
	isRightPanel,
	tag: Tag = 'div',
	...props
}) => {
	const { mobileDesign } = use(ThemeContext);

	const initialProps = {
		isBackdrop: isRightPanel && !mobileDesign ? false : isBackdrop,
		isBodyScroll: isRightPanel && !mobileDesign ? true : isBodyScroll,
		placement: isRightPanel ? 'end' : placement,
	};

	// @ts-ignore
	const { setRightPanel } = use(ThemeContext);
	const deviceScreen = useDeviceScreen();

	useLayoutEffect(() => {
		// @ts-ignore
		setRightPanel(isRightPanel && deviceScreen?.width > 1200 && isOpen);
	});

	const ref = useRef(null);

	// Disable Body Scroll
	useLayoutEffect(() => {
		if (!initialProps.isBodyScroll && isOpen) {
			document.body.style.overflow = 'hidden';
			document.body.style.paddingRight = '0px';
		}
		return () => {
			document.body.style.overflow = 'auto';
			document.body.style.removeProperty('padding-right');
		};
	});

	// Backdrop close function
	const closeCanvas = (event: { target: any }) => {
		if (
			ref.current &&
			// @ts-ignore
			!ref.current.contains(event.target) &&
			((!isRightPanel && isBackdrop) || (isRightPanel && mobileDesign))
		) {
			setOpen(false);
		}
	};
	useEventListener('mousedown', closeCanvas);
	useEventListener('touchstart', closeCanvas);

	const PLACEMENT_ANIMATION = (initialProps.placement === 'start' && { x: '-100%' }) ||
		(initialProps.placement === 'top' && { y: '-100%' }) ||
		(initialProps.placement === 'bottom' && { y: '100%' }) || { x: '100%' };

	// @ts-ignore
	const MotionTagWrapper = motion[Tag];

	return (
		<Portal>
			<AnimatePresence mode='wait'>
				{isOpen && (
					<>
						{/*@ts-ignore*/}
						<MotionTagWrapper
							ref={ref}
							key='offCanvas'
							initial={{ opacity: 0, ...PLACEMENT_ANIMATION }}
							animate={{ opacity: 1, x: '0%', y: '0%' }}
							exit={{ opacity: 0, ...PLACEMENT_ANIMATION }}
							transition={{ ease: 'easeInOut', duration: 0.3 }}
							id={id}
							className={classNames(
								'offcanvas',
								`offcanvas-${initialProps.placement}`,
								{
									show: isOpen,
									'offcanvas-modal-style': isModalStyle,
									'offcanvas-right-panel':
										// @ts-ignore
										isRightPanel && deviceScreen?.width > 1200,
								},
							)}
							tabIndex={-1}
							aria-labelledby={titleId}
							data-bs-scroll={initialProps.isBodyScroll}
							data-bs-backdrop={initialProps.isBackdrop}
							style={{ visibility: isOpen && 'visible' }}
							// eslint-disable-next-line react/jsx-props-no-spreading
							{...props}>
							{children}
						</MotionTagWrapper>
						{initialProps.isBackdrop && (
							<div className={classNames('offcanvas-backdrop', 'fade', 'show')} />
						)}
					</>
				)}
			</AnimatePresence>
		</Portal>
	);
};

export default OffCanvas;
