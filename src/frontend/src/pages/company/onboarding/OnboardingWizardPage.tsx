import React, { use, useState, FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
	Button,
	Card,
	CardBody,
	CardHeader,
	notify,
	Page,
	showApiError,
	Spinner,
	TextField,
} from '../../../ui';
import AuthContext from '../../../contexts/authContext';
import { useOrganization } from '../../../contexts/organizationContext';
import { toApiProblem } from '../../../services/api/apiProblem';

/**
 * The "Set Up Your Organization" page: one step, "Organization Details".
 * It is shown exactly while the user belongs to no organization.
 */
// US-ONBOARD-01, US-ONBOARD-02
const OnboardingWizardPage = () => {
	const { t } = useTranslation(['organization', 'common']);
	const navigate = useNavigate();
	const { createOrganization } = useOrganization();
	const { syncUserData } = use(AuthContext);

	// Org form
	const [orgName, setOrgName] = useState('');
	const [legalName, setLegalName] = useState('');
	const [vatNumber, setVatNumber] = useState('');
	const [billingEmail, setBillingEmail] = useState('');
	const [nameError, setNameError] = useState('');

	// State
	const [submitting, setSubmitting] = useState(false);

	const handleSubmit = async (e: FormEvent) => {
		e.preventDefault();

		// A name made only of spaces counts as empty
		if (!orgName.trim()) {
			setNameError(t('onboarding.orgNameRequired'));
			return;
		}

		try {
			setSubmitting(true);

			const org = await createOrganization({
				name: orgName.trim(),
				legalName: legalName.trim() || orgName.trim(),
				vatNumber: vatNumber.trim(),
				billingEmail: billingEmail.trim(),
				fiscalCode: '',
				billingAddress: '',
				billingCity: '',
				billingProvince: '',
				billingZipCode: '',
				billingCountryCode: 'IT',
			});

			// The creator is the Admin: load the role so the menu shows "Settings"
			await syncUserData?.(true, org.id);

			notify.success(t('onboarding.success'));
			navigate('/dashboard');
		} catch (err) {
			showApiError(toApiProblem(err));
		} finally {
			setSubmitting(false);
		}
	};

	return (
		<Page title={t('onboarding.title')}>
			<div id='onboarding-wizard' className='row justify-content-center'>
				<div className='col-12 col-lg-10 col-xl-8'>
					<div className='text-center mb-4'>
						<h1 className='h2 fw-bold'>{t('onboarding.title')}</h1>
					</div>

					<Card as='form' noValidate onSubmit={handleSubmit}>
						<CardHeader icon='Assignment' title={t('onboarding.step2Title')} />
						<CardBody>
							<div className='py-3'>
								<p className='text-muted mb-4'>{t('onboarding.orgDetailsDesc')}</p>

								{submitting && (
									<div className='text-center py-4'>
										<Spinner size='lg' />
										<p className='mt-3 text-muted'>
											{t('onboarding.creating')}
										</p>
									</div>
								)}

								{!submitting && (
									<div className='row g-3'>
										<TextField
											id='orgName'
											className='col-12'
											label={t('Organization Name')}
											value={orgName}
											onChange={(e) => {
												setOrgName(e.target.value);
												if (nameError) setNameError('');
											}}
											error={nameError || undefined}
											autoFocus
										/>
										<TextField
											id='legalName'
											className='col-12 col-md-6'
											label={t('Legal Name')}
											value={legalName}
											onChange={(e) => setLegalName(e.target.value)}
										/>
										<TextField
											id='vatNumber'
											className='col-12 col-md-6'
											label={t('VAT Number')}
											value={vatNumber}
											onChange={(e) => setVatNumber(e.target.value)}
										/>
										<TextField
											id='billingEmail'
											className='col-12'
											type='email'
											label={t('Billing Email')}
											value={billingEmail}
											onChange={(e) => setBillingEmail(e.target.value)}
										/>
									</div>
								)}
							</div>
							<div className='d-flex justify-content-end'>
								<Button type='submit' variant='primary' busy={submitting}>
									{t('common:submit')}
								</Button>
							</div>
						</CardBody>
					</Card>
				</div>
			</div>
		</Page>
	);
};

export default OnboardingWizardPage;
