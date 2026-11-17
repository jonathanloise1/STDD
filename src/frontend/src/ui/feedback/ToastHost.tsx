import React, { FC, MouseEvent, ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Slide, ToastContainer } from 'react-toastify';

interface ICloseButtonProps {
	closeToast: (event: MouseEvent<HTMLElement>) => void;
}

const CloseButton = ({ closeToast }: ICloseButtonProps): ReactElement => {
	const { t } = useTranslation('common');
	return (
		<button type='button' className='btn-close' aria-label={t('close')} onClick={closeToast} />
	);
};

/** The one toast container of the application, bottom right (frontend.md 10.1). Rendered next to `.app`. */
export const ToastHost: FC = () => (
	<ToastContainer
		position='bottom-right'
		transition={Slide}
		closeButton={CloseButton}
		hideProgressBar
		theme='light'
	/>
);
