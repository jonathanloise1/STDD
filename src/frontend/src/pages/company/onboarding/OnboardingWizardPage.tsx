// US-ONBOARD-01: Onboarding wizard — guided first-time setup
import React, { useState, FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import Wizard, { WizardItem } from '../../../components/Wizard';
import Card, { CardBody } from '../../../components/bootstrap/Card';
import FormGroup from '../../../components/bootstrap/forms/FormGroup';
import Input from '../../../components/bootstrap/forms/Input';
import Icon from '../../../components/icon/Icon';
import Spinner from '../../../components/bootstrap/Spinner';
import { useOrganization } from '../../../contexts/organizationContext';
import organizationService from '../../../services/api/organizationService';
import userProfileService from '../../../services/api/userProfileService';

/** Onboarding wizard — create first organization */
const OnboardingWizardPage = () => {
	const { t } = useTranslation(['organization', 'common']);
	const navigate = useNavigate();
	const { fetchOrganizations, setSelectedOrganization } = useOrganization();

	// Org form
	const [orgName, setOrgName] = useState('');
	const [legalName, setLegalName] = useState('');
	const [vatNumber, setVatNumber] = useState('');
	const [billingEmail, setBillingEmail] = useState('');

	// State
	const [submitting, setSubmitting] = useState(false);

	// US-ONBOARD-01: Submit handler — create org
	const handleSubmit = async (e: FormEvent) => {
		e.preventDefault();

		// Basic validation
		if (!orgName.trim()) {
			toast.error(t('onboarding.orgNameRequired'));
			return;
		}

		try {
			setSubmitting(true);

			// Create organization
			const org = await organizationService.createOrganization({
				name: orgName.trim(),
				legalName: legalName.trim() || orgName.trim(),
				vatNumber: vatNumber.trim(),
				billingEmail: billingEmail.trim(),
				fiscalCode: '',
				italianSdiCode: '',
				billingAddress: '',
				billingCity: '',
				billingProvince: '',
				billingZipCode: '',
				billingCountryCode: 'IT',
				certifiedEmail: '',
			});

			// Refresh organization list and select the new org
			await fetchOrganizations();
			setSelectedOrganization(org);

			toast.success(t('onboarding.success'));
			navigate('/dashboard');
		} catch (err) {
			console.error('Onboarding error:', err);
			toast.error(t('onboarding.error'));
		} finally {
			setSubmitting(false);
		}
	};

	// US-ONBOARD-02: Skip handler — mark onboarding complete and go to dashboard
	const handleSkip = async () => {
		try {
			localStorage.setItem('myapp_onboarded', 'true');
			// Mark onboarding complete via API
			await userProfileService.completeOnboarding();
		} catch {
			// Proceed even if the API call fails
		}
		navigate('/dashboard', { replace: true });
	};

	return (
		<PageWrapper title={t('onboarding.title')}>
			<Page>
				<div id='onboarding-wizard' className='row justify-content-center'>
					<div className='col-12 col-lg-10 col-xl-8'>
						<div className='text-center mb-4'>
							<h2 className='fw-bold'>{t('onboarding.title')}</h2>
							<button
								id='onboarding-skip'
								type='button'
								className='btn btn-link text-muted mt-2'
								onClick={handleSkip}>
								{t('common:skipForNow', 'Skip for now')}
							</button>
						</div>

						{/* US-ONBOARD-01: Wizard container */}
						<Wizard
							isHeader='withButton'
							color='primary'
							stretch={false}
							onSubmit={handleSubmit}>
							{/* Step 1: Organization details */}
							<WizardItem
								id='step-org'
								title={t('onboarding.step2Title')}>
								<div className='py-3'>
									<p className='text-muted mb-4'>
										{t('onboarding.orgDetailsDesc')}
									</p>

									{submitting && (
										<div className='text-center py-4'>
											<Spinner color='primary' size='3rem' />
											<p className='mt-3 text-muted'>
												{t('onboarding.creating')}
											</p>
										</div>
									)}

									{!submitting && (
										<div className='row g-3'>
											<div className='col-12'>
												<FormGroup
													id='orgName'
													label={t('Organization Name')}
													isFloating>
													<Input
														value={orgName}
														onChange={(
															e: React.ChangeEvent<HTMLInputElement>,
														) =>
															setOrgName(
																e.target.value,
															)
														}
														placeholder={t(
															'Enter organization name',
														)}
														autoFocus
													/>
												</FormGroup>
											</div>
											<div className='col-12 col-md-6'>
												<FormGroup
													id='legalName'
													label={t('Legal Name')}
													isFloating>
													<Input
														value={legalName}
														onChange={(
															e: React.ChangeEvent<HTMLInputElement>,
														) =>
															setLegalName(
																e.target.value,
															)
														}
														placeholder={t(
															'Legal Name',
														)}
													/>
												</FormGroup>
											</div>
											<div className='col-12 col-md-6'>
												<FormGroup
													id='vatNumber'
													label={t('VAT Number')}
													isFloating>
													<Input
														value={vatNumber}
														onChange={(
															e: React.ChangeEvent<HTMLInputElement>,
														) =>
															setVatNumber(
																e.target.value,
															)
														}
														placeholder={t(
															'VAT Number',
														)}
													/>
												</FormGroup>
											</div>
											<div className='col-12'>
												<FormGroup
													id='billingEmail'
													label={t('Billing Email')}
													isFloating>
													<Input
														type='email'
														value={billingEmail}
														onChange={(
															e: React.ChangeEvent<HTMLInputElement>,
														) =>
															setBillingEmail(
																e.target.value,
															)
														}
														placeholder={t(
															'Billing Email',
														)}
													/>
												</FormGroup>
											</div>
										</div>
									)}
								</div>
							</WizardItem>
						</Wizard>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default OnboardingWizardPage;
