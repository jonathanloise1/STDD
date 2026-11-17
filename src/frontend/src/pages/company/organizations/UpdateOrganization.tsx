// External dependencies
import React, { useState, useEffect, useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

// Components
import {
	ApiErrorMessage,
	Badge,
	Button,
	Card,
	CardBody,
	CardHeader,
	Icon,
	LoadingBlock,
	Notice,
	notify,
	Page,
	PageHeader,
	SelectField,
	showApiError,
	Spinner,
	TextField,
} from '../../../ui';

// Contexts
import { useOrganization } from '../../../contexts/organizationContext';
// Services
import organizationService, {
	UpdateOrganizationRequest,
} from '../../../services/api/organizationService';
import { ApiProblem, toApiProblem } from '../../../services/api/apiProblem';
import provinces from './provinces.json';

// Styles
import './UpdateOrganization.scss';

/** The Update Organization page: name, billing details and logo (Admins only). */
// US-ORG-02, US-ORG-03
const UpdateOrganization = () => {
	const { t } = useTranslation(['organization', 'common']);
	const navigate = useNavigate();
	const fileInputRef = useRef<HTMLInputElement>(null);
	const {
		selectedOrganization,
		updateOrganization: updateOrganizationContext,
		uploadLogo,
	} = useOrganization();
	const [isLoading, setIsLoading] = useState(true);
	const [loadError, setLoadError] = useState<ApiProblem | null>(null);

	// Use selectedOrganization.id instead of URL param
	const organizationId = selectedOrganization?.id;

	const [isSaving, setIsSaving] = useState<boolean>(false);
	const [isUploadingLogo, setIsUploadingLogo] = useState<boolean>(false);
	const [logoUrl, setLogoUrl] = useState<string | null>(null);

	const [touched, setTouched] = useState<Record<string, boolean>>({
		name: false,
		legalName: false,
		vatNumber: false,
		billingEmail: false,
		billingCity: false,
		billingProvince: false,
		billingZipCode: false,
		billingAddress: false,
		fiscalCode: false,
		billingCountryCode: false,
	});
	const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

	const [formData, setFormData] = useState<UpdateOrganizationRequest>({
		name: '',
		legalName: '',
		vatNumber: '',
		billingEmail: '',
		fiscalCode: '',
		billingAddress: '',
		billingCity: '',
		billingProvince: '',
		billingZipCode: '',
		billingCountryCode: 'IT',
	});

	const [originalFormData, setOriginalFormData] = useState<UpdateOrganizationRequest | null>(
		null,
	);

	// Dirty tracking
	const isDirty = useMemo(() => {
		if (!originalFormData) return false;
		return JSON.stringify(formData) !== JSON.stringify(originalFormData);
	}, [formData, originalFormData]);

	// Warn on unsaved changes
	useEffect(() => {
		const handleBeforeUnload = (e: BeforeUnloadEvent) => {
			if (isDirty) {
				e.preventDefault();
				e.returnValue = '';
			}
		};
		window.addEventListener('beforeunload', handleBeforeUnload);
		return () => window.removeEventListener('beforeunload', handleBeforeUnload);
	}, [isDirty]);

	useEffect(() => {
		if (!organizationId) return;
		let cancelled = false;
		setIsLoading(true);
		setLoadError(null);
		organizationService
			.getOrganizationById(organizationId)
			.then((org) => {
				if (cancelled) return;
				const initialFormData = {
					name: org.name,
					legalName: org.legalName || '',
					vatNumber: org.vatNumber || '',
					billingEmail: org.billingEmail || '',
					fiscalCode: org.fiscalCode || '',
					billingAddress: org.billingAddress || '',
					billingCity: org.billingCity || '',
					billingProvince: org.billingProvince || '',
					billingZipCode: org.billingZipCode || '',
					billingCountryCode: org.billingCountryCode || 'IT',
				};
				setFormData(initialFormData);
				setOriginalFormData(initialFormData);
				setLogoUrl(org.logoUrl || null);
			})
			.catch((err) => {
				if (!cancelled) setLoadError(toApiProblem(err));
			})
			.finally(() => {
				if (!cancelled) setIsLoading(false);
			});
		return () => {
			cancelled = true;
		};
	}, [organizationId]);

	// --- Validation
	const validateField = (name: string, value: string): string => {
		switch (name) {
			case 'name':
				return value.trim() === '' ? t('Organization name is required') : '';
			case 'legalName':
				return value.trim() === '' ? t('Legal name is required') : '';
			case 'vatNumber':
				return value.trim() === '' ? t('VAT number is required') : '';
			case 'billingEmail': {
				if (value.trim() === '') return t('Billing email is required');
				const emailPattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
				return !emailPattern.test(value) ? t('Invalid email format') : '';
			}
			case 'billingCity':
				return value.trim() === '' ? t('City is required') : '';
			case 'billingProvince':
				return value.trim() === '' ? t('Province is required') : '';
			case 'billingZipCode':
				if (value.trim() === '') return t('ZIP code is required');
				return !/^\d{5}$/.test(value) ? t('ZIP code must be 5 digits') : '';
			case 'billingAddress':
				return value.trim() === '' ? t('Billing address is required') : '';
			case 'fiscalCode':
				return value.trim() === '' ? t('Fiscal code is required') : '';
			case 'billingCountryCode':
				return value.trim() === '' ? t('Country is required') : '';
			default:
				return '';
		}
	};

	const validateAndUpdateField = (name: string, value: string) => {
		const error = validateField(name, value);
		setValidationErrors((prev) => ({ ...prev, [name]: error }));
		return error === '';
	};

	const validateForm = (): boolean => {
		const required = [
			'name',
			'legalName',
			'vatNumber',
			'billingEmail',
			'billingCity',
			'billingProvince',
			'billingZipCode',
			'billingAddress',
			'fiscalCode',
			'billingCountryCode',
		];
		let isValid = true;
		const newErrors: Record<string, string> = {};
		const newTouched: Record<string, boolean> = { ...touched };
		required.forEach((field) => {
			newTouched[field] = true;
			const error = validateField(
				field,
				formData[field as keyof typeof formData]?.toString() || '',
			);
			if (error) {
				newErrors[field] = error;
				isValid = false;
			}
		});
		setTouched(newTouched);
		setValidationErrors(newErrors);
		return isValid;
	};

	const handleInputChange = (
		e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>,
	) => {
		const { name, value } = e.target;
		setFormData((prev) => ({ ...prev, [name]: value }));
		if (touched[name]) validateAndUpdateField(name, value);
	};

	const handleBlur = (
		e: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>,
	) => {
		const { name, value } = e.target;
		setTouched((prev) => ({ ...prev, [name]: true }));
		validateAndUpdateField(name, value);
	};

	const handleUpdateOrganization = async () => {
		if (!organizationId) return;
		if (!validateForm()) {
			notify.warning(t('Please fix validation errors before submitting'));
			return;
		}
		try {
			setIsSaving(true);
			await updateOrganizationContext(organizationId, formData);
			setOriginalFormData(formData);
			notify.success(t('Organization has been updated successfully'));
			navigate('/settings/organization');
		} catch (err) {
			showApiError(toApiProblem(err));
		} finally {
			setIsSaving(false);
		}
	};

	// Logo upload handlers
	const handleLogoClick = () => {
		fileInputRef.current?.click();
	};

	const handleLogoChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
		const file = e.target.files?.[0];
		if (!file || !organizationId) return;

		// Validate file type
		if (!file.type.startsWith('image/')) {
			notify.error(t('Only image files are allowed'));
			return;
		}

		// Validate file size (max 5MB)
		if (file.size > 5 * 1024 * 1024) {
			notify.error(t('Maximum file size is 5MB'));
			return;
		}

		try {
			setIsUploadingLogo(true);
			const result = await uploadLogo(organizationId, file);
			setLogoUrl(result.logoUrl);
			notify.success(t('Logo uploaded successfully'));
		} catch (err) {
			showApiError(toApiProblem(err));
		} finally {
			setIsUploadingLogo(false);
			// Reset input so the same file can be selected again
			if (fileInputRef.current) {
				fileInputRef.current.value = '';
			}
		}
	};

	// A required text field of the form, wired to the shared handlers
	const renderField = (
		name: keyof UpdateOrganizationRequest,
		label: string,
		className: string,
		extra: { type?: 'email'; hint?: string; placeholder?: string; maxLength?: number } = {},
		onChange = handleInputChange,
	) => (
		<TextField
			id={name}
			name={name}
			className={className}
			label={label}
			required
			value={formData[name] ?? ''}
			onChange={onChange}
			onBlur={handleBlur}
			error={validationErrors[name] || undefined}
			type={extra.type}
			hint={extra.hint}
			placeholder={extra.placeholder}
			maxLength={extra.maxLength}
		/>
	);

	// --- UI
	return (
		<Page title={t('Update Organization')} fluid>
			<PageHeader
				back={{
					label: t('common:back'),
					onClick: () => navigate('/settings/organization'),
				}}
				title={`${t('Update Organization')} - ${selectedOrganization?.name}`}
				extra={
					isDirty && (
						<Badge tone='warning'>
							<Icon icon='Edit' size='sm' className='me-1' />
							{t('common:unsavedChanges')}
						</Badge>
					)
				}
				actions={
					<Button
						variant='primary'
						icon='Save'
						busy={isSaving}
						onClick={handleUpdateOrganization}>
						{isSaving ? t('common:saving') : t('common:saveChanges')}
					</Button>
				}
			/>
			{isLoading && <LoadingBlock />}
			{loadError && <ApiErrorMessage problem={loadError} />}

			{!isLoading && !loadError && (
				<>
					{/* Banner */}
					<Notice tone='primary' icon='Info' className='mb-4'>
						{t(
							'This information identifies your organization and appears in its billing details.',
						)}
					</Notice>

					<div className='row g-4'>
						{/* Left Column - Company Identity */}
						<div className='col-lg-4'>
							<Card className='h-100'>
								<CardHeader icon='Business' title={t('Company Identity')} />
								<CardBody className='d-flex flex-column align-items-center'>
									{/* Logo Upload */}
									<div className='company-logo-container mb-3'>
										<input
											type='file'
											ref={fileInputRef}
											onChange={handleLogoChange}
											accept='image/*'
											style={{ display: 'none' }}
										/>
										<button
											type='button'
											className={`company-logo-upload ${logoUrl ? 'has-logo' : ''}`}
											aria-label={t('Click to upload logo')}
											onClick={handleLogoClick}
											disabled={isUploadingLogo}>
											{isUploadingLogo ? (
												<div className='company-logo-loading'>
													<Spinner />
												</div>
											) : logoUrl ? (
												<img
													src={logoUrl}
													alt={formData.name || t('Company Logo')}
													className='company-logo-image'
												/>
											) : (
												<div className='company-logo-placeholder'>
													<Icon
														icon='CloudUpload'
														size='3x'
														className='mb-2'
													/>
													<span className='company-logo-text'>
														{t('Click to upload logo')}
													</span>
													<span className='company-logo-hint'>
														{t('Max 5MB, JPG/PNG')}
													</span>
												</div>
											)}
											<div className='company-logo-overlay'>
												<Icon icon='CameraAlt' size='2x' />
												<span className='mt-1'>{t('common:change')}</span>
											</div>
										</button>
										{logoUrl && (
											<div className='text-muted text-center mt-2 small'>
												{t('Click to upload logo')}
											</div>
										)}
									</div>

									{/* Organization Name */}
									{renderField('name', t('Organization Name'), 'w-100', {
										hint: t('Internal reference name of your organization'),
										placeholder: t('Enter organization name'),
									})}
								</CardBody>
							</Card>
						</div>

						{/* Right Column - Billing & Fiscal Information */}
						<div className='col-lg-8'>
							<Card className='h-100'>
								<CardHeader
									icon='Receipt'
									title={t('Billing & Fiscal Information')}
								/>
								<CardBody>
									<div className='row g-4'>
										{/* Fiscal Data Section */}
										{renderField('legalName', t('Legal Name'), 'col-md-6', {
											hint: t('Full legal company name as registered'),
										})}
										{renderField('vatNumber', t('VAT Number'), 'col-md-6', {
											hint: t('Company VAT number for electronic invoicing'),
										})}
										{renderField('fiscalCode', t('Fiscal Code'), 'col-md-6', {
											hint: t(
												'Company fiscal code or owner code for sole proprietorship',
											),
										})}
										{renderField(
											'billingEmail',
											t('Billing Email'),
											'col-md-6',
											{
												type: 'email',
												hint: t(
													'Email address for receiving fiscal documents',
												),
											},
										)}

										{/* Divider */}
										<div className='col-12'>
											<hr className='my-2' />
											<h2 className='h6 text-muted mb-0'>
												<Icon icon='LocationOn' className='me-2' />
												{t('Billing Address')}
											</h2>
										</div>

										{/* Address Fields */}
										{renderField(
											'billingAddress',
											t('Street Address'),
											'col-12',
											{
												hint: t(
													'Official registered billing or legal address',
												),
											},
										)}
										{renderField('billingCity', t('City'), 'col-md-3')}
										<SelectField
											id='billingProvince'
											name='billingProvince'
											className='col-md-3'
											label={t('Province')}
											placeholder={t('Province')}
											options={(
												provinces as Array<{ code: string; label: string }>
											).map((p) => ({
												value: p.code,
												label: `${p.code} - ${p.label}`,
											}))}
											onChange={(e) => {
												const v = e.target.value;
												setFormData((prev) => ({
													...prev,
													billingProvince: v,
												}));
												if (touched.billingProvince)
													validateAndUpdateField('billingProvince', v);
											}}
											value={formData.billingProvince ?? ''}
											error={validationErrors.billingProvince || undefined}
										/>
										{renderField(
											'billingZipCode',
											t('common:billing.zipCode'),
											'col-md-3',
										)}
										{renderField(
											'billingCountryCode',
											t('Country'),
											'col-md-3',
											{ maxLength: 2 },
											(e) => {
												e.target.value = e.target.value.toUpperCase();
												handleInputChange(e);
											},
										)}
									</div>
								</CardBody>
							</Card>
						</div>
					</div>
				</>
			)}
		</Page>
	);
};

export default UpdateOrganization;
