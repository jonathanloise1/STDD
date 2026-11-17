import React, { FC, ReactNode, useId } from 'react';
import { Modal } from 'react-bootstrap';
import { useTranslation } from 'react-i18next';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';

export interface IDialogProps {
	show: boolean;
	onHide: () => void;
	/** Its text is the accessible name of the dialog. */
	title: ReactNode;
	icon?: IconName;
	size?: 'sm' | 'lg' | 'xl';
	/** The actions, the primary or destructive one last. */
	footer?: ReactNode;
	id?: string;
	children?: ReactNode;
}

/** A task or a question that needs the user's attention: centred, no fade (frontend.md 11). */
export const Dialog: FC<IDialogProps> = ({
	show,
	onHide,
	title,
	icon,
	size,
	footer,
	id,
	children,
}) => {
	const { t } = useTranslation('common');
	const titleId = useId();

	return (
		<Modal
			show={show}
			onHide={onHide}
			size={size}
			id={id}
			centered
			animation={false}
			aria-labelledby={titleId}>
			<Modal.Header closeButton closeLabel={t('close')}>
				<Modal.Title as='h2' id={titleId} className='h5 d-flex align-items-center gap-2'>
					{icon && <Icon icon={icon} size='lg' className='text-primary' />}
					{title}
				</Modal.Title>
			</Modal.Header>
			<Modal.Body>{children}</Modal.Body>
			{footer && <Modal.Footer className='gap-2'>{footer}</Modal.Footer>}
		</Modal>
	);
};
