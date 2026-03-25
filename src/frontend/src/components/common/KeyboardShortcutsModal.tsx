// US-SHORT-02: Keyboard shortcuts help modal
import React from 'react';
import { useTranslation } from 'react-i18next';
import Modal, { ModalHeader, ModalBody, ModalFooter } from '../bootstrap/Modal';
import Button from '../bootstrap/Button';
import Icon from '../icon/Icon';

/** Label map for shortcut keys → human-readable section name (i18n key) */
const SHORTCUT_LABELS: Record<string, { icon: string; labelKey: string }> = {
	d: { icon: 'Dashboard', labelKey: 'shortcuts.dashboard' },
	n: { icon: 'AccountTree', labelKey: 'shortcuts.nodes' },
	c: { icon: 'Receipt', labelKey: 'shortcuts.costEntries' },
	r: { icon: 'MergeType', labelKey: 'shortcuts.allocationRules' },
	e: { icon: 'Badge', labelKey: 'shortcuts.employees' },
	t: { icon: 'Schedule', labelKey: 'shortcuts.timesheet' },
	a: { icon: 'Inventory2', labelKey: 'shortcuts.assets' },
	k: { icon: 'Calculate', labelKey: 'shortcuts.calculations' },
	p: { icon: 'Assessment', labelKey: 'shortcuts.reports' },
	i: { icon: 'Upload', labelKey: 'shortcuts.import' },
	s: { icon: 'Settings', labelKey: 'shortcuts.settings' },
};

interface KeyboardShortcutsModalProps {
	isOpen: boolean;
	setIsOpen: (open: boolean) => void;
}

/** US-SHORT-02: Modal listing all available keyboard shortcuts */
const KeyboardShortcutsModal: React.FC<KeyboardShortcutsModalProps> = ({ isOpen, setIsOpen }) => {
	const { t } = useTranslation('common');

	return (
		<Modal
			id='keyboard-shortcuts-modal'
			isOpen={isOpen}
			setIsOpen={setIsOpen}
			isCentered
			size='lg'
			titleId='keyboard-shortcuts-title'>
			<ModalHeader setIsOpen={setIsOpen}>
				<Icon icon='Keyboard' className='me-2' />
				<span className='h5 mb-0'>{t('shortcuts.title')}</span>
			</ModalHeader>
			<ModalBody>
				{/* Navigation shortcuts */}
				<h6 className='text-uppercase text-muted mb-3'>
					{t('shortcuts.navigationSection')}
				</h6>
				<p className='text-muted small mb-3'>{t('shortcuts.navigationHint')}</p>
				<table className='table table-modern mb-4'>
					<thead>
						<tr>
							<th style={{ width: '140px' }}>{t('shortcuts.shortcut')}</th>
							<th>{t('shortcuts.action')}</th>
						</tr>
					</thead>
					<tbody>
						{Object.entries(SHORTCUT_LABELS).map(([key, { icon, labelKey }]) => (
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
				<h6 className='text-uppercase text-muted mb-3'>
					{t('shortcuts.generalSection')}
				</h6>
				<table className='table table-modern mb-0'>
					<thead>
						<tr>
							<th style={{ width: '140px' }}>{t('shortcuts.shortcut')}</th>
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
			</ModalBody>
			<ModalFooter>
				<Button color='light' onClick={() => setIsOpen(false)}>
					{t('close')}
				</Button>
			</ModalFooter>
		</Modal>
	);
};

export default KeyboardShortcutsModal;
