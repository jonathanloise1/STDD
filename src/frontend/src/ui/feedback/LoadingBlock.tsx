import React, { FC } from 'react';
import { useTranslation } from 'react-i18next';
import { Spinner } from './Spinner';

export interface ILoadingBlockProps {
	label?: string;
}

/** A region waiting for data: a centred spinner announced as a status (frontend.md 8.5). */
export const LoadingBlock: FC<ILoadingBlockProps> = ({ label }) => {
	const { t } = useTranslation('common');
	return (
		<div className='d-flex justify-content-center py-5' role='status'>
			<Spinner size='lg' />
			<span className='visually-hidden'>{label ?? t('loading')}</span>
		</div>
	);
};
