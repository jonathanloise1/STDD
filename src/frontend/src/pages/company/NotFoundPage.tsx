import React from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Page } from '../../ui';

/** Shown for an address that matches no page, with a link back to the Dashboard page. */
// US-AUTH-06
const NotFoundPage = () => {
	const { t } = useTranslation('common');

	return (
		<Page title={t('notFound.title')}>
			<div className='d-flex flex-column align-items-center justify-content-center text-center py-5'>
				<div className='text-primary fw-bold app-not-found-code'>404</div>
				<h1 className='fw-bold mb-4'>{t('notFound.title')}</h1>
				<Button
					className='px-5 py-3'
					variant='subtle-primary'
					icon='HolidayVillage'
					to='/dashboard'>
					{t('notFound.homepage')}
				</Button>
			</div>
		</Page>
	);
};

export default NotFoundPage;
