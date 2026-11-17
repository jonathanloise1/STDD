import React, { useEffect } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardBody, CardHeader, Page } from '../../../ui';
import { ManualPageContent } from '../../../help/components/ManualPageContent';
import { ManualSearchForm } from '../../../help/components/ManualSearchForm';
import { SearchResults } from '../../../help/components/SearchResults';
import { useHelpSearcher, useManualBundle } from '../../../hooks/useHelpData';
import { reportClientEvent } from '../../../services/api/clientEventsService';

/** The manual page id of the address: `home`, an area, or `{area}/{story id}`. */
const pageIdOf = (area?: string, story?: string): string => {
	if (!area) return 'home';
	return story ? `${area}/${story}` : area;
};

/** The search results of `/manual/search?q=...` (manual.md 5.3). */
// US-HELP-02
const SearchView = ({ query }: { query: string }) => {
	const { t } = useTranslation('help');
	const { pathname } = useLocation();
	const resource = useHelpSearcher(query !== '');

	useEffect(() => {
		if (resource.status === 'error') reportClientEvent('help.search.unavailable', pathname);
	}, [resource.status, pathname]);

	if (query === '') return null;
	if (resource.status === 'error')
		return (
			<p role='alert' data-testid='help-unavailable'>
				{t('unavailable')}
			</p>
		);
	if (resource.status === 'loading')
		return <span className='placeholder col-6' aria-busy='true' />;
	return <SearchResults ps={resource.data.searcher} query={query} />;
};

/** A page of the manual: home, area or story (manual.md 5.2). */
// US-HELP-01
const BrowseView = ({ pageId }: { pageId: string }) => {
	const { t } = useTranslation('help');
	const { pathname } = useLocation();
	const resource = useManualBundle(true);

	useEffect(() => {
		if (resource.status === 'error') reportClientEvent('help.browse.load_failed', pathname);
	}, [resource.status, pathname]);

	if (resource.status === 'error')
		return (
			<p role='alert' data-testid='manual-load-failed'>
				{t('manualLoadFailed')}
			</p>
		);
	if (resource.status === 'loading')
		return <span className='placeholder col-6' aria-busy='true' />;
	const page = resource.data.pages.find((p) => p.page_id === pageId);
	if (!page)
		return (
			<div data-testid='manual-page-missing'>
				<p>{t('pageMissing')}</p>
				<Link to='/manual'>{t('goToManualHome')}</Link>
			</div>
		);
	return <ManualPageContent page={page} bundle={resource.data} />;
};

/** The Manual page: the manual of the application language with its search field. */
// US-HELP-01, US-HELP-02
const ManualPage = () => {
	const { t } = useTranslation('help');
	const { pathname } = useLocation();
	const { area, story } = useParams();
	const [params] = useSearchParams();
	const isSearch = pathname.replace(/\/+$/, '') === '/manual/search';
	const query = (params.get('q') ?? '').trim();

	return (
		<Page title={t('manualTitle')}>
			<div className='row'>
				<div className='col-12 col-xl-9'>
					<Card>
						<CardHeader icon='MenuBook' title={t('manualTitle')} />
						<CardBody>
							<ManualSearchForm
								initialQuery={isSearch ? (params.get('q') ?? '') : ''}
							/>
							{isSearch ? (
								<SearchView query={query} />
							) : (
								<BrowseView pageId={pageIdOf(area, story)} />
							)}
						</CardBody>
					</Card>
				</div>
			</div>
		</Page>
	);
};

export default ManualPage;
