import React, { useEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { pageHref } from '../runtime/routes';
import type { BundlePage, ManualBundle } from '../runtime/types';
import { ManualBlocks } from './ManualBlocks';

interface Props {
	page: BundlePage;
	bundle: ManualBundle;
}

/**
 * One manual page: its blocks, and for the home page the list of areas, for an area page the list of its pages in
 * the order of the stories (manual.md 2.4, 5.2). A hash in the address scrolls to the section of that key.
 */
// US-HELP-01
export const ManualPageContent = ({ page, bundle }: Props) => {
	const { t } = useTranslation('help');
	const { hash } = useLocation();

	useEffect(() => {
		const id = decodeURIComponent(hash.replace(/^#/, ''));
		if (!id) return;
		document.getElementById(id)?.scrollIntoView({ block: 'start' });
	}, [hash, page.page_id]);

	const listed =
		page.kind === 'home'
			? bundle.pages.filter((p) => p.kind === 'area')
			: page.kind === 'area'
				? bundle.pages.filter((p) => p.kind === 'story' && p.area === page.page_id)
				: [];

	return (
		<article data-testid='manual-page' data-page-id={page.page_id}>
			<ManualBlocks blocks={page.blocks} lang={bundle.lang} />
			{listed.length > 0 && (
				<>
					<h2 className='h5 text-muted mt-4'>
						{page.kind === 'home' ? t('areas') : t('pages')}
					</h2>
					<ul className='list-unstyled' data-testid='manual-page-list'>
						{listed.map((p) => (
							<li key={p.page_id} className='mb-2'>
								<Link to={pageHref(p)}>{p.title}</Link>
							</li>
						))}
					</ul>
				</>
			)}
		</article>
	);
};
