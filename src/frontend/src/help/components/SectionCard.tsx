import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { SectionView } from '../runtime/searcher';

interface Props {
	view: SectionView;
	onNavigate: () => void;
}

/** A section of the manual shown as a card: page title, heading, the first 160 characters and a link (manual.md 7.2). */
export const SectionCard = ({ view, onNavigate }: Props) => {
	const { t } = useTranslation('help');
	return (
		<div className='card mb-2' data-testid='help-card'>
			<div className='card-body p-3'>
				<div className='fw-bold' data-testid='help-card-page'>
					{view.pageTitle}
				</div>
				<div className='small fw-semibold' data-testid='help-card-heading'>
					{view.heading ?? t('overview')}
				</div>
				<p className='small text-muted my-2' data-testid='help-card-text'>
					{view.snippet}
				</p>
				<Link
					to={view.url}
					className='small'
					onClick={onNavigate}
					data-testid='help-card-link'>
					{t('openInManual')}
				</Link>
			</div>
		</div>
	);
};
