import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageWrapper from '../../layout/PageWrapper/PageWrapper';
import Page from '../../layout/Page/Page';
import Card, { CardBody } from '../bootstrap/Card';
import Button from '../bootstrap/Button';
import Icon from '../icon/Icon';

interface AccessDeniedProps {
	/** Custom message to display */
	message?: string;
	/** Path to navigate to when clicking the button */
	returnPath?: string;
	/** Label for the return button */
	returnLabel?: string;
}

/**
 * Access Denied page component.
 * Displayed when a user tries to access a route they don't have permission for.
 * @userstory US-TEAM-08
 */
const AccessDenied: React.FC<AccessDeniedProps> = ({
	message,
	returnPath = '/dashboard',
	returnLabel,
}) => {
	const { t } = useTranslation(['common']);
	const navigate = useNavigate();

	const displayMessage = message || t('accessDenied.message');
	const buttonLabel = returnLabel || t('accessDenied.backToDashboard');

	return (
		<PageWrapper title={t('accessDenied.title')}>
			<Page>
				<div className='row h-100 align-items-center justify-content-center'>
					<div className='col-12 col-lg-6 col-lg-4'>
						<Card>
							<CardBody className='text-center py-5'>
								<div className='mb-4'>
									<Icon icon='Block' size='5x' className='text-danger' />
								</div>
								<h3 className='mb-3'>{t('accessDenied.title')}</h3>
								<p className='text-muted mb-4'>{displayMessage}</p>
								<Button
									color='primary'
									size='lg'
									onClick={() => navigate(returnPath)}>
									<Icon icon='ArrowBack' className='me-2' />
									{buttonLabel}
								</Button>
							</CardBody>
						</Card>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default AccessDenied;
