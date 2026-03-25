import React, {
	FC,
	HTMLAttributes,
	ReactElement,
	ReactNode,
	useLayoutEffect,
	useRef,
} from 'react';
import classNames from 'classnames';
import { AnimatePresence, motion } from 'framer-motion';
import Portal from '../../layout/Portal/Portal';
import TagWrapper from '../TagWrapper';
import useEventListener from '../../hooks/useEventListener';
import { TModalFullScreen, TModalSize } from '../../type/modal-type';

interface IModalTitleProps extends HTMLAttributes<HTMLElement> {
	id: string;
	children: ReactNode;
	className?: string;
	tag?: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6' | 'div' | 'span';
	ref?: React.Ref<HTMLHeadingElement>;
}
export const ModalTitle = ({ tag = 'h5', id, children, className, ref, ...props }: IModalTitleProps) => {
	return (
			<TagWrapper
				tag={tag}
				ref={ref}
				id={id}
				className={classNames('modal-title', className)}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{children}
		</TagWrapper>
	);
};
ModalTitle.displayName = 'ModalTitle';

interface IModalHeaderProps extends HTMLAttributes<HTMLDivElement> {
	children: ReactElement<IModalTitleProps> | ReactNode;
	className?: string;
	setIsOpen?(...args: unknown[]): unknown | undefined;
	ref?: React.Ref<HTMLDivElement>;
}
export const ModalHeader = ({ children, className, setIsOpen, ref, ...props }: IModalHeaderProps) => {
	return (
			<div ref={ref} className={classNames('modal-header', className)} {...props}>
				{children}
				{setIsOpen && (
					<button
						type='button'
						className='btn-close'
						data-bs-dismiss='modal'
						aria-label='Close'
						onClick={() => setIsOpen(false)}
					/>
				)}
			</div>
		);
	};
ModalHeader.displayName = 'ModalHeader';

interface IModalBodyProps extends HTMLAttributes<HTMLDivElement> {
	children: ReactNode;
	className?: string;
	ref?: React.Ref<HTMLDivElement>;
}
export const ModalBody = ({ children, className, ref, ...props }: IModalBodyProps) => {
	return (
		<div ref={ref} className={classNames('modal-body', className)} {...props}>
			{children}
		</div>
	);
};
ModalBody.displayName = 'ModalBody';

interface IModalFooterProps extends HTMLAttributes<HTMLDivElement> {
	children: ReactNode;
	className?: string;
	ref?: React.Ref<HTMLDivElement>;
}
export const ModalFooter = ({ children, className, ref, ...props }: IModalFooterProps) => {
	return (
		<div ref={ref} className={classNames('modal-footer', className)} {...props}>
			{children}
		</div>
	);
};
ModalFooter.displayName = 'ModalFooter';

interface IModalProps extends Record<string, any> {
	children:
		| ReactElement<IModalHeaderProps>[]
		| ReactElement<IModalBodyProps>[]
		| ReactElement<IModalFooterProps>[];
	isOpen: boolean;
	setIsOpen(...args: unknown[]): unknown;
	id?: string | undefined;
	titleId?: string;
	isStaticBackdrop?: boolean;
	isScrollable?: boolean;
	isCentered?: boolean;
	size?: TModalSize;
	fullScreen?: TModalFullScreen;
	isAnimation?: boolean;
}
const Modal: FC<IModalProps> = ({
	children,
	isOpen,
	setIsOpen,
	id,
	titleId,
	isStaticBackdrop,
	isScrollable,
	isCentered,
	size,
	fullScreen,
	isAnimation = true,
	...props
}) => {
	const refModal = useRef(null);
	const ref = useRef(null);

	// <body> modal-open class (presentation)
	useLayoutEffect(() => {
		if (isOpen) {
			document.body.classList.add('modal-open');
		}
		return () => {
			document.body.classList.remove('modal-open');
		};
	});

	// Backdrop close function
	const closeModal = (event: { target: any }) => {
		// @ts-ignore
		if (ref.current && !ref.current.contains(event.target) && !isStaticBackdrop) {
			setIsOpen(false);
		}
	};
	useEventListener('mousedown', closeModal);
	useEventListener('touchstart', closeModal); // Touchscreen

	// Backdrop static function
	const modalStatic = (event: { target: any }) => {
		// @ts-ignore
		if (ref.current && !ref.current.contains(event.target) && isStaticBackdrop) {
			// @ts-ignore
			refModal.current.classList.add('modal-static');
			// @ts-ignore
			setTimeout(() => refModal.current.classList.remove('modal-static'), 300);
		}
	};
	useEventListener('mousedown', modalStatic);
	useEventListener('touchstart', modalStatic); // Touchscreen

	// Keypress close function
	const escFunction = (event: { key: string }) => {
		if (event.key === 'Escape') {
			setIsOpen(false);
		}
	};
	useEventListener('keydown', escFunction);

	const ANIMATION_PROPS = isAnimation
		? {
				initial: { opacity: 0, y: '-50%' },
				animate: { opacity: 1, x: '0%', y: '0%' },
				exit: { opacity: 0, y: '-50%' },
				transition: { ease: 'easeInOut' as const, duration: 0.3 },
			}
		: null;

	return (
		<Portal>
			<AnimatePresence mode='wait'>
				{isOpen && (
					<>
						<motion.div
							ref={refModal}
							key='modal'
							className={classNames('modal', { fade: isAnimation }, 'show')}
							role='dialog'
							style={{ display: 'block' }}
							id={id}
							tabIndex={-1}
							aria-labelledby={titleId}
							aria-hidden='true'
							data-bs-backdrop={isStaticBackdrop ? 'static' : null}
							data-bs-keyboard={isStaticBackdrop ? 'false' : null}
							{...ANIMATION_PROPS}
							{...props}>
							<div
								ref={ref}
								className={classNames('modal-dialog', {
									'modal-dialog-scrollable': isScrollable,
									'modal-dialog-centered': isCentered,
									[`modal-${size}`]: size,
									[`modal-fullscreen${
										typeof fullScreen === 'string' ? `-${fullScreen}-down` : ''
									}`]: fullScreen,
								})}>
								<div className='modal-content'>{children}</div>
							</div>
						</motion.div>
						<div
							className={classNames('modal-backdrop', { fade: isAnimation }, 'show')}
						/>
					</>
				)}
			</AnimatePresence>
		</Portal>
	);
};

export default Modal;
