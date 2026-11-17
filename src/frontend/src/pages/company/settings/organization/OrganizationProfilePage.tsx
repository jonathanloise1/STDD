import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
	ApiErrorMessage,
	Button,
	Card,
	CardBody,
	Icon,
	LoadingBlock,
	Page,
	PageHeader,
} from '../../../../ui';
import { useOrganization } from '../../../../contexts/organizationContext';
import organizationService, { Organization } from '../../../../services/api/organizationService';
import { ApiProblem, toApiProblem } from '../../../../services/api/apiProblem';

/** One label and value of the details; a detail not filled in shows "-". */
const Detail = ({ label, value }: { label: string; value?: string | null }) => (
	<div className='mb-2 d-flex'>
		<dt className='fw-bold me-2'>{label}</dt>
		<dd className='mb-0'>{value || '-'}</dd>
	</div>
);

/**
 * Organization page: profile and billing details of the current organization (Admins only).
 */
// US-ORG-01
const OrganizationProfilePage: React.FC = () => {
	const { t } = useTranslation(['organization', 'common', 'menu']);
	const navigate = useNavigate();
	const { selectedOrganization } = useOrganization();

	const [organization, setOrganization] = useState<Organization | null>(null);
	const [loading, setLoading] = useState(true);
	const [loadError, setLoadError] = useState<ApiProblem | null>(null);

	const organizationId = selectedOrganization?.id;

	useEffect(() => {
		if (!organizationId) return;
		let cancelled = false;
		setLoading(true);
		setLoadError(null);
		organizationService
			.getOrganizationById(organizationId)
			.then((org) => {
				if (!cancelled) setOrganization(org);
			})
			.catch((err) => {
				if (!cancelled) {
					setOrganization(null);
					setLoadError(toApiProblem(err));
				}
			})
			.finally(() => {
				if (!cancelled) setLoading(false);
			});
		return () => {
			cancelled = true;
		};
	}, [organizationId]);

	const title = t('menu:settings.organization.billing');

	if (loading) {
		return (
			<Page title={title}>
				<LoadingBlock />
			</Page>
		);
	}

	if (loadError || !organization) {
		return <Page title={title}>{loadError && <ApiErrorMessage problem={loadError} />}</Page>;
	}

	const city = `${organization.billingCity || '-'}${
		organization.billingProvince ? ` (${organization.billingProvince})` : ''
	}`;

	return (
		<Page title={title}>
			<PageHeader
				icon='Receipt'
				title={t('organization.billingInfo')}
				actions={
					<Button
						variant='primary'
						icon='Edit'
						onClick={() => navigate('/settings/organization/edit')}>
						{t('Edit')}
					</Button>
				}
			/>
			<div className='row g-4'>
				<div className='col-12'>
					<Card>
						<CardBody className='p-4'>
							<div className='row'>
								<div className='col-md-3 text-center mb-4 mb-md-0'>
									{organization.logoUrl ? (
										<img
											src={organization.logoUrl}
											alt={organization.name}
											className='img-fluid mb-3 rounded app-org-logo'
										/>
									) : (
										<div
											className='d-flex align-items-center justify-content-center mb-3 mx-auto rounded app-org-logo-fallback'
											aria-hidden='true'>
											{organization.name.substring(0, 2).toUpperCase()}
										</div>
									)}
									<h2 className='h5'>{organization.name}</h2>
								</div>
								<div className='col-md-9'>
									<div className='row g-4'>
										<div className='col-lg-6'>
											<h3 className='h5 text-muted mb-3'>
												<Icon icon='AccountBalance' className='me-2' />
												{t('organization.fiscalInfo')}
											</h3>
											<dl className='mb-0'>
												<Detail
													label={t('VAT Number')}
													value={organization.vatNumber}
												/>
												<Detail
													label={t('Fiscal Code')}
													value={organization.fiscalCode}
												/>
											</dl>
										</div>

										<div className='col-lg-6'>
											<h3 className='h5 text-muted mb-3'>
												<Icon icon='LocationOn' className='me-2' />
												{t('organization.billingAddress')}
											</h3>
											<dl className='mb-0'>
												<Detail
													label={t('Email')}
													value={organization.billingEmail}
												/>
												<Detail
													label={t('Address')}
													value={organization.billingAddress}
												/>
												<Detail label={t('City')} value={city} />
												<Detail
													label={t('ZIP Code')}
													value={organization.billingZipCode}
												/>
												<Detail
													label={t('Country')}
													value={organization.billingCountryCode}
												/>
											</dl>
										</div>
									</div>
								</div>
							</div>
						</CardBody>
					</Card>
				</div>
			</div>
		</Page>
	);
};

export default OrganizationProfilePage;
