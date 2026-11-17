import React from 'react';
import { useTranslation } from 'react-i18next';
import type { Rating } from '../runtime/askState';

interface Props {
	rating: Rating;
	onRate: (rating: 'yes' | 'no') => void;
}

/** "Did this help?" with "Yes" and "No" under a result; the vote cannot be changed. */
// US-HELP-06
export const FeedbackRow = ({ rating, onRate }: Props) => {
	const { t } = useTranslation('help');
	const voted = rating !== 'none';
	return (
		<div className='mt-3' data-testid='help-feedback'>
			<div className='d-flex align-items-center gap-2'>
				<span className='small fw-semibold'>{t('didThisHelp')}</span>
				<button
					type='button'
					className='btn btn-sm btn-outline-secondary'
					disabled={voted}
					onClick={() => onRate('yes')}
					data-testid='btn-help-yes'>
					{t('yes')}
				</button>
				<button
					type='button'
					className='btn btn-sm btn-outline-secondary'
					disabled={voted}
					onClick={() => onRate('no')}
					data-testid='btn-help-no'>
					{t('no')}
				</button>
			</div>
			{voted && (
				<div
					className='small text-success mt-2'
					role='status'
					data-testid='help-feedback-thanks'>
					{t('thanks')}
				</div>
			)}
		</div>
	);
};
