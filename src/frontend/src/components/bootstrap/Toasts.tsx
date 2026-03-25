import React, { Children, cloneElement, FC, ReactNode } from 'react';
import Icon from '../icon/Icon';
import { TColor } from '../../type/color-type';
import { TIcons } from '../../type/icons-type';

interface IToastHeaderProps {
	icon?: TIcons;
	iconColor?: TColor;
	title: ReactNode;
	time?: string | null;
	isDismiss?: boolean;
}
const ToastHeader: FC<IToastHeaderProps> = ({
	icon,
	iconColor,
	title,
	time,
	isDismiss,
	...props
}) => {
	// @ts-ignore
	// eslint-disable-next-line react/prop-types
	const { onDismiss } = props;
	return (
		<div className='toast-header'>
			{icon && <Icon icon={icon} size='lg' color={iconColor} className='me-2' />}
			{title && <strong className='me-auto'>{title}</strong>}
			{time && <small>{time}</small>}
			{}
			{isDismiss && onDismiss}
		</div>
	);
};

interface IToastBodyProps {
	children: ReactNode;
}
const ToastBody: FC<IToastBodyProps> = ({ children }) => {
	return <div className='toast-body'>{children}</div>;
};

interface IToastProps {
	children: ReactNode;
	onDismiss(...args: unknown[]): unknown;
}
export const Toast: FC<IToastProps> = ({ children, onDismiss }) => {
	return (
		<div className='toast show' role='alert' aria-live='assertive' aria-atomic='true'>
			{Children.map(children, (child, index) =>
				// @ts-ignore
				cloneElement(child, {
					key: index,
					onDismiss: (
						<button
							type='button'
							className='btn-close'
							aria-label='Close'
							onClick={onDismiss}
						/>
					),
				}),
			)}
		</div>
	);
};

interface IToastContainerProps {
	children: ReactNode;
}
export const ToastContainer: FC<IToastContainerProps> = ({ children }) => {
	return <div className='toast-container position-fixed top-0 end-0 p-3'>{children}</div>;
};

// @ts-ignore
// eslint-disable-next-line react/prop-types
export const ToastCloseButton = ({ closeToast }) => (
	<button type='button' className='btn-close' aria-label='Close' onClick={closeToast} />
);

interface IToastsProps {
	title: ReactNode;
	children: ReactNode;
	icon?: TIcons;
	iconColor?: TColor;
	time?: string | null;
}
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const Toasts: FC<IToastsProps> = ({ icon, iconColor, title, time, children, ...props }) => {
	return (
		<>
			<ToastHeader icon={icon} iconColor={iconColor} title={title} time={time} />
			<ToastBody>{children}</ToastBody>
		</>
	);
};

export default Toasts;
