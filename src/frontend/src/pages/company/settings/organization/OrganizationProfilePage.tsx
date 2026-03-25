import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import PageWrapper from '../../../../layout/PageWrapper/PageWrapper';
import Page from '../../../../layout/Page/Page';
import Card, { CardBody, CardHeader } from '../../../../components/bootstrap/Card';
import Button from '../../../../components/bootstrap/Button';
import Icon from '../../../../components/icon/Icon';
import Spinner from '../../../../components/bootstrap/Spinner';
import { useOrganization } from '../../../../contexts/organizationContext';
import { Organization } from '../../../../services/api/organizationService';
import { toast } from 'react-toastify';

/**
 * Organization Profile Page
 *
 * Displays organization profile and billing information (VAT, fiscal code, SDI, billing address).
 * Accessible from Settings > Organization
 * @userstory US-ORG-03, US-ORG-05
 */
const OrganizationProfilePage: React.FC = () => {
	const { t } = useTranslation(['organization', 'billing', 'common', 'menu']);
	const navigate = useNavigate();
	const { selectedOrganization, isLoading, fetchOrganizationById } = useOrganization();

	const [organization, setOrganization] = useState<Organization | null>(null);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		const loadOrganization = async () => {
			if (!selectedOrganization?.id) {
				setLoading(false);
				return;
			}

			try {
				setLoading(true);
				const orgData = await fetchOrganizationById(selectedOrganization.id);
				setOrganization(orgData);
			} catch (err) {
				console.error('Failed to load organization:', err);
				toast.error(t('organization.errorLoading', 'Failed to load organization'));
			} finally {
				setLoading(false);
			}
		};

		loadOrganization();
	}, [selectedOrganization?.id, fetchOrganizationById, t]);

	const handleEdit = () => {
		navigate('/settings/organization/edit');
	};

	if (loading || isLoading) {
		return (
			<PageWrapper title={t('settings.organization.billing', 'Billing')}>
				<Page>
					<div
						className='d-flex justify-content-center align-items-center'
						style={{ minHeight: 300 }}>
						<Spinner color='primary' size='3rem' />
					</div>
				</Page>
			</PageWrapper>
		);
	}

	if (!organization) {
		return (
			<PageWrapper title={t('settings.organization.billing', 'Billing')}>
				<Page>
					<Card>
						<CardBody className='text-center py-5'>
							<Icon icon='Business' size='3x' className='text-muted mb-3' />
							<h5>{t('organization.noOrganization', 'No organization selected')}</h5>
							<p className='text-muted'>
								{t(
									'organization.selectOrganizationFirst',
									'Please select an organization first.',
								)}
							</p>
						</CardBody>
					</Card>
				</Page>
			</PageWrapper>
		);
	}

	return (
		<PageWrapper title={t('settings.organization.billing', 'Billing')}>
			<Page>
				<div className='row g-4'>
					{/* Billing Information Card */}
					<div className='col-12'>
						<Card>
							<CardHeader className='d-flex justify-content-between align-items-center'>
								<h4 className='mb-0'>
									<Icon icon='Receipt' className='me-2' />
									{t('organization.billingInfo', 'Billing Information')}
								</h4>
								<Button color='primary' icon='Edit' onClick={handleEdit}>
									{t('Edit', 'Edit')}
								</Button>
							</CardHeader>
							<CardBody className='p-4'>
								<div className='row'>
									<div className='col-md-3 text-center mb-4 mb-md-0'>
										{organization.logoUrl ? (
											<img
												src={organization.logoUrl}
												alt={organization.name}
												className='img-fluid mb-3 rounded'
												style={{
													maxWidth: '150px',
													maxHeight: '150px',
													objectFit: 'cover',
												}}
											/>
										) : (
											<div
												className='d-flex align-items-center justify-content-center mb-3 mx-auto rounded'
												style={{
													width: '120px',
													height: '120px',
													background:
														'linear-gradient(135deg, var(--bs-primary) 0%, var(--bs-primary-dark) 100%)',
													color: 'white',
													fontSize: '2.5rem',
													fontWeight: 600,
												}}>
												{organization.name?.substring(0, 2).toUpperCase() ||
													'CO'}
											</div>
										)}
										<h5>{organization.name}</h5>
									</div>
									<div className='col-md-9'>
										<div className='row g-4'>
											{/* Fiscal Information */}
											<div className='col-lg-6'>
												<h5 className='text-muted mb-3'>
													<Icon icon='AccountBalance' className='me-2' />
													{t(
														'organization.fiscalInfo',
														'Fiscal Information',
													)}
												</h5>
												<div className='mb-2'>
													<strong>
														{t('VAT Number', 'VAT Number')}:
													</strong>
													<span className='ms-2'>
														{organization.vatNumber || '-'}
													</span>
												</div>
												<div className='mb-2'>
													<strong>
														{t('Fiscal Code', 'Fiscal Code')}:
													</strong>
													<span className='ms-2'>
														{organization.fiscalCode || '-'}
													</span>
												</div>
												<div className='mb-2'>
													<strong>
														{t('Italian SDI Code', 'Italian SDI Code')}:
													</strong>
													<span className='ms-2'>
														{organization.italianSdiCode || '-'}
													</span>
												</div>
												<div className='mb-2'>
													<strong>
														{t(
															'Certified Email',
															'Certified Email (PEC)',
														)}
														:
													</strong>
													<span className='ms-2'>
														{organization.certifiedEmail || '-'}
													</span>
												</div>
											</div>

											{/* Billing Address */}
											<div className='col-lg-6'>
												<h5 className='text-muted mb-3'>
													<Icon icon='LocationOn' className='me-2' />
													{t(
														'organization.billingAddress',
														'Billing Address',
													)}
												</h5>
												<div className='mb-2'>
													<strong>{t('Email', 'Email')}:</strong>
													<span className='ms-2'>
														{organization.billingEmail || '-'}
													</span>
												</div>
												<div className='mb-2'>
													<strong>{t('Address', 'Address')}:</strong>
													<span className='ms-2'>
														{organization.billingAddress || '-'}
													</span>
												</div>
												<div className='mb-2'>
													<strong>{t('City', 'City')}:</strong>
													<span className='ms-2'>
														{organization.billingCity || '-'}
														{organization.billingProvince &&
															` (${organization.billingProvince})`}
													</span>
												</div>
												<div className='mb-2'>
													<strong>{t('ZIP Code', 'ZIP Code')}:</strong>
													<span className='ms-2'>
														{organization.billingZipCode || '-'}
													</span>
												</div>
												<div className='mb-2'>
													<strong>{t('Country', 'Country')}:</strong>
													<span className='ms-2'>
														{organization.billingCountryCode || '-'}
													</span>
												</div>
											</div>
										</div>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default OrganizationProfilePage;
