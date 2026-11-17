import React, { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useHelp } from '../../contexts/helpContext';
import { useHelpRoute } from '../../hooks/useHelpRoute';
import { useManualBundle } from '../../hooks/useHelpData';
import { reportClientEvent } from '../../services/api/clientEventsService';
import { pageHref, pagesForPanel } from '../runtime/routes';

/** The tab "This page": the manual pages of the current screen, the page of a form first (manual.md 5.4). */
// US-HELP-03
export const ThisPageTab = () => {
	const { t } = useTranslation('help');
	const { context, closeHelp } = useHelp();
	const { pathname, patterns } = useHelpRoute();
	const resource = useManualBundle(true);

	useEffect(() => {
		if (resource.status === 'error') reportClientEvent('help.open.unavailable', pathname);
	}, [resource.status, pathname]);

	if (resource.status === 'loading')
		return (
			<div className='placeholder-glow' aria-busy='true'>
				<span className='placeholder col-8' />
			</div>
		);

	if (resource.status === 'error')
		return (
			<div data-testid='help-unavailable'>
				<p role='alert'>{t('unavailable')}</p>
				<Link to='/manual' onClick={closeHelp}>
					{t('openManual')}
				</Link>
			</div>
		);

	const pages = pagesForPanel(resource.data, { pathname, patterns, context });
	return (
		<ul className='list-unstyled' data-testid='help-page-list'>
			{pages.map((page) => (
				<li key={page.page_id} className='mb-2'>
					<Link to={pageHref(page)} onClick={closeHelp} data-testid='help-page-link'>
						{page.title}
					</Link>
				</li>
			))}
		</ul>
	);
};
