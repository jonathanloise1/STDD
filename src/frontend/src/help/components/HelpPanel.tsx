import React from 'react';
import { useTranslation } from 'react-i18next';
import { SidePanel } from '../../ui';
import { useHelp } from '../../contexts/helpContext';
import { AskTab } from './AskTab';
import { ThisPageTab } from './ThisPageTab';

/**
 * The side panel of contextual help (manual.md 5.4): the tabs "This page" and "Ask". It opens on "This page" and
 * closes with the Escape key or its close button; the screen behind it is never touched.
 */
// US-HELP-03
export const HelpPanel = () => {
	const { t } = useTranslation('help');
	const { isOpen, tab, setTab, closeHelp } = useHelp();

	return (
		<SidePanel
			id='help-panel'
			data-testid='help-panel'
			show={isOpen}
			onHide={closeHelp}
			placement='end'
			modal={false}
			title={t('help')}>
			<ul className='nav nav-tabs mb-3' role='tablist'>
				<li className='nav-item' role='presentation'>
					<button
						type='button'
						role='tab'
						id='help-tab-page'
						data-testid='tab-help-page'
						aria-selected={tab === 'page'}
						className={`nav-link ${tab === 'page' ? 'active' : ''}`}
						onClick={() => setTab('page')}>
						{t('tabThisPage')}
					</button>
				</li>
				<li className='nav-item' role='presentation'>
					<button
						type='button'
						role='tab'
						id='help-tab-ask'
						data-testid='tab-help-ask'
						aria-selected={tab === 'ask'}
						className={`nav-link ${tab === 'ask' ? 'active' : ''}`}
						onClick={() => setTab('ask')}>
						{t('tabAsk')}
					</button>
				</li>
			</ul>
			{/* Both tabs stay mounted, so a question and its result survive a visit to the other tab */}
			<div
				role='tabpanel'
				aria-labelledby='help-tab-page'
				className={tab === 'page' ? '' : 'd-none'}>
				<ThisPageTab />
			</div>
			<div
				role='tabpanel'
				aria-labelledby='help-tab-ask'
				className={tab === 'ask' ? '' : 'd-none'}>
				<AskTab />
			</div>
		</SidePanel>
	);
};
