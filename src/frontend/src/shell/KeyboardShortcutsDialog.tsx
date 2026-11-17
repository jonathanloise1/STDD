import React from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Dialog, Icon } from '../ui';
import type { IShortcut } from '../hooks/useKeyboardShortcuts';

interface IKeyboardShortcutsDialogProps {
	show: boolean;
	onHide: () => void;
	/** The shortcuts the user can use; only these are listed. */
	shortcuts: IShortcut[];
}

/** Dialog listing the keyboard shortcuts the role of the user allows. */
// US-AUTH-09
const KeyboardShortcutsDialog = ({ show, onHide, shortcuts }: IKeyboardShortcutsDialogProps) => {
	const { t } = useTranslation('common');

	return (
		<Dialog
			id='keyboard-shortcuts-modal'
			show={show}
			onHide={onHide}
			size='lg'
			icon='Keyboard'
			title={t('shortcuts.title')}
			footer={
				<Button variant='light' onClick={onHide}>
					{t('close')}
				</Button>
			}>
			{/* Navigation shortcuts */}
			<h3 className='h6 text-uppercase text-muted mb-3'>
				{t('shortcuts.navigationSection')}
			</h3>
			<p className='text-muted small mb-3'>{t('shortcuts.navigationHint')}</p>
			<table className='table align-middle mb-4'>
				<thead>
					<tr>
						<th className='app-col-shortcut'>{t('shortcuts.shortcut')}</th>
						<th>{t('shortcuts.action')}</th>
					</tr>
				</thead>
				<tbody>
					{shortcuts.map(({ key, icon, labelKey }) => (
						<tr key={key}>
							<td>
								<kbd className='me-1'>G</kbd>
								<span className='text-muted mx-1'>→</span>
								<kbd>{key.toUpperCase()}</kbd>
							</td>
							<td>
								<Icon icon={icon} size='lg' className='me-2 text-muted' />
								{t(labelKey)}
							</td>
						</tr>
					))}
				</tbody>
			</table>

			{/* General shortcuts */}
			<h3 className='h6 text-uppercase text-muted mb-3'>{t('shortcuts.generalSection')}</h3>
			<table className='table align-middle mb-0'>
				<thead>
					<tr>
						<th className='app-col-shortcut'>{t('shortcuts.shortcut')}</th>
						<th>{t('shortcuts.action')}</th>
					</tr>
				</thead>
				<tbody>
					<tr>
						<td>
							<kbd>?</kbd>
						</td>
						<td>{t('shortcuts.openHelp')}</td>
					</tr>
					<tr>
						<td>
							<kbd>Esc</kbd>
						</td>
						<td>{t('shortcuts.closeModal')}</td>
					</tr>
				</tbody>
			</table>
		</Dialog>
	);
};

export default KeyboardShortcutsDialog;
