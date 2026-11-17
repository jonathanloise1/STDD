import React, { FC, ReactNode } from 'react';
import { Button } from './Button';
import { Dialog } from './Dialog';
import type { IconName } from '../icons/registry';

export interface IConfirmDialogProps {
	show: boolean;
	title: string;
	/** The question, naming the consequence. */
	message: ReactNode;
	confirmLabel: string;
	cancelLabel: string;
	confirmVariant?: 'danger' | 'primary';
	confirmIcon?: IconName;
	/** While true the confirm button is busy and the dialog cannot be dismissed. */
	busy?: boolean;
	onConfirm: () => void;
	onCancel: () => void;
}

/** A yes or no question before an irreversible action (frontend.md 11). */
export const ConfirmDialog: FC<IConfirmDialogProps> = ({
	show,
	title,
	message,
	confirmLabel,
	cancelLabel,
	confirmVariant = 'danger',
	confirmIcon,
	busy = false,
	onConfirm,
	onCancel,
}) => (
	<Dialog
		show={show}
		onHide={() => {
			if (!busy) onCancel();
		}}
		title={title}
		footer={
			<>
				<Button variant='light' onClick={onCancel} disabled={busy}>
					{cancelLabel}
				</Button>
				<Button variant={confirmVariant} icon={confirmIcon} busy={busy} onClick={onConfirm}>
					{confirmLabel}
				</Button>
			</>
		}>
		<p className='mb-0'>{message}</p>
	</Dialog>
);
