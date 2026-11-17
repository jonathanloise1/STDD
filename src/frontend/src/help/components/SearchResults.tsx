import React, { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { searchManual } from '../runtime/searcher';
import type { PackSearcher } from '../runtime/searcher';
import { HighlightedText } from './HighlightedText';

interface Props {
	ps: PackSearcher;
	query: string;
}

/**
 * Search results (manual.md 5.3): up to 10 sections grouped by page; each shows the page title, the section
 * heading and the first 160 characters, with the words of the query in bold.
 */
// US-HELP-02
export const SearchResults = ({ ps, query }: Props) => {
	const { t } = useTranslation('help');
	const groups = useMemo(() => searchManual(ps, query), [ps, query]);
	const stems = useMemo(() => new Set(ps.searcher.stems(query)), [ps, query]);

	if (groups.length === 0)
		return (
			<div data-testid='search-empty'>
				<p>{t('nothingFound')}</p>
				<Link to='/manual'>{t('goToManualHome')}</Link>
			</div>
		);

	return (
		<div data-testid='search-results'>
			{groups.map((group) => (
				<ul
					key={group.pageId}
					className='list-unstyled mb-4'
					data-testid='search-group'
					data-page-id={group.pageId}>
					{group.results.map((r) => (
						<li key={r.id} className='mb-2'>
							<Link
								to={r.url}
								className='d-block p-3 border rounded text-decoration-none'
								data-testid='search-result'>
								<span className='d-block fw-bold'>
									<HighlightedText
										text={r.pageTitle}
										stems={stems}
										analyzer={ps.analyzer}
									/>
								</span>
								<span className='d-block fw-semibold small'>
									<HighlightedText
										text={r.heading ?? t('overview')}
										stems={stems}
										analyzer={ps.analyzer}
									/>
								</span>
								<span className='d-block text-muted'>
									<HighlightedText
										text={r.snippet}
										stems={stems}
										analyzer={ps.analyzer}
									/>
								</span>
							</Link>
						</li>
					))}
				</ul>
			))}
		</div>
	);
};
