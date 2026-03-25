// External dependencies
import React, { useState, useEffect, useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';

// Components
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Card, {
	CardBody,
	CardHeader,
	CardLabel,
	CardTitle,
} from '../../../components/bootstrap/Card';
import Button from '../../../components/bootstrap/Button';
import FormGroup from '../../../components/bootstrap/forms/FormGroup';
import Input from '../../../components/bootstrap/forms/Input';
import Select from '../../../components/bootstrap/forms/Select';
import Icon from '../../../components/icon/Icon';
import Loading from '../../../components/Loading';
import Spinner from '../../../components/bootstrap/Spinner';
import Tooltips from '../../../components/bootstrap/Tooltips';
import Alert from '../../../components/bootstrap/Alert';
import CountrySelect from '../../../components/common/CountrySelect';

// Contexts
import { useOrganization } from '../../../contexts/organizationContext';
// Services
import { UpdateOrganizationRequest } from '../../../services/api/organizationService';
import provinces from '../../../helpers/provinces.json';

// Styles
import './UpdateOrganization.scss';

const UpdateOrganization = () => {
	const { t } = useTranslation(['organization', 'common']);
	const navigate = useNavigate();
	const fileInputRef = useRef<HTMLInputElement>(null);
	const {
		organizations,
		selectedOrganization,
		isLoading,
		error,
		fetchOrganizationById,
		updateOrganization: updateOrganizationContext,
		uploadLogo,
	} = useOrganization();

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
		certifiedEmail: true,
		italianSdiCode: false,
		billingAddress: false,
		fiscalCode: false,
	});
	const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

	const [formData, setFormData] = useState<UpdateOrganizationRequest>({
		name: '',
		legalName: '',
		vatNumber: '',
		billingEmail: '',
		fiscalCode: '',
		italianSdiCode: '',
		billingAddress: '',
		billingCity: '',
		billingProvince: '',
		billingZipCode: '',
		billingCountryCode: 'IT',
		certifiedEmail: '',
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
		const fetchOrganizationDetails = async () => {
			if (!organizationId) return;
			try {
				const existingOrg = organizations.find((org) => org.id === organizationId);
				const org = existingOrg || (await fetchOrganizationById(organizationId));
				const initialFormData = {
					name: org.name,
					legalName: org.legalName,
					vatNumber: org.vatNumber,
					billingEmail: org.billingEmail,
					fiscalCode: org.fiscalCode || '',
					italianSdiCode: org.italianSdiCode || '',
					billingAddress: org.billingAddress || '',
					billingCity: org.billingCity || '',
					billingProvince: org.billingProvince || '',
					billingZipCode: org.billingZipCode || '',
					billingCountryCode: org.billingCountryCode || 'IT',
					certifiedEmail: org.certifiedEmail || '',
				};
				setFormData(initialFormData);
				setOriginalFormData(initialFormData);
				setLogoUrl(org.logoUrl || null);
			} catch (err) {
				toast.error(t('Failed to load organization details'), { position: 'top-right' });
			}
		};
		fetchOrganizationDetails();
	}, [organizationId, organizations, fetchOrganizationById, t]);

	// --- Validation
	const validateField = (name: string, value: string): string => {
		switch (name) {
			case 'name':
				return value.trim() === '' ? t('Organization name is required') : '';
			case 'legalName':
				return value.trim() === '' ? t('Legal name is required') : '';
			case 'vatNumber':
				return value.trim() === '' ? t('VAT number is required') : '';
			case 'billingEmail':
				if (value.trim() === '') return t('Billing email is required');
				const emailPattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
				return !emailPattern.test(value) ? t('Invalid email format') : '';
			case 'billingCity':
				return value.trim() === '' ? t('City is required') : '';
			case 'billingProvince':
				return value.trim() === '' ? t('Province is required') : '';
			case 'billingZipCode':
				if (value.trim() === '') return t('ZIP code is required');
				return !/^\d{5}$/.test(value) ? t('ZIP code must be 5 digits') : '';
			case 'italianSdiCode':
				return value.trim() === '' ? t('SDI code is required') : '';
			case 'billingAddress':
				return value.trim() === '' ? t('Billing address is required') : '';
			case 'fiscalCode':
				return value.trim() === '' ? t('Fiscal code is required') : '';
			case 'certifiedEmail':
				if (value.trim() !== '') {
					const emailPattern2 = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
					return !emailPattern2.test(value) ? t('Invalid email format') : '';
				}
				return '';
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
			'italianSdiCode',
			'billingAddress',
			'fiscalCode',
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

	const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
		const { name, value } = e.target;
		setFormData((prev) => ({ ...prev, [name]: value }));
		if (touched[name]) validateAndUpdateField(name, value);
	};

	const handleBlur = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) => {
		const { name, value } = e.target;
		setTouched((prev) => ({ ...prev, [name]: true }));
		validateAndUpdateField(name, value);
	};

	const handleUpdateOrganization = async () => {
		if (!organizationId) return;
		if (!validateForm()) {
			toast.warning(t('Please fix validation errors before submitting'), {
				position: 'top-right',
			});
			return;
		}
		try {
			setIsSaving(true);
			await updateOrganizationContext(organizationId, formData);
			setOriginalFormData(formData);
			toast.success(t('Organization has been updated successfully'), {
				position: 'top-right',
			});
			navigate('/settings/organization');
		} catch (err: any) {
			toast.error(err.message || t('An error occurred while updating the organization'), {
				position: 'top-right',
			});
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
			toast.error(t('Only image files are allowed'));
			return;
		}

		// Validate file size (max 5MB)
		if (file.size > 5 * 1024 * 1024) {
			toast.error(t('Maximum file size is 5MB'));
			return;
		}

		try {
			setIsUploadingLogo(true);
			const result = await uploadLogo(organizationId, file);
			setLogoUrl(result.logoUrl);
			toast.success(t('Logo uploaded successfully'));
		} catch (err: any) {
			toast.error(err.message || t('Failed to upload logo'));
		} finally {
			setIsUploadingLogo(false);
			// Reset input so the same file can be selected again
			if (fileInputRef.current) {
				fileInputRef.current.value = '';
			}
		}
	};

	// Get initials for placeholder
	const getInitials = () => {
		const name = formData.name || selectedOrganization?.name || '';
		const words = name.split(' ').filter(Boolean);
		if (words.length >= 2) {
			return (words[0][0] + words[1][0]).toUpperCase();
		}
		return name.substring(0, 2).toUpperCase() || 'CO';
	};

	// --- UI
	return (
		<>
			<PageWrapper title={t('Update Organization')}>
				<SubHeader>
					<SubHeaderLeft>
						<Button
							color='link'
							className='p-0 me-3'
								onClick={() => navigate('/settings/organization')}>
							<Icon icon='ArrowBack' size='lg' />
						</Button>
						<span className='h4 mb-0 fw-bold'>
							{t('Update Organization')} - {selectedOrganization?.name}
						</span>
						{isDirty && (
							<span className='badge bg-warning text-dark ms-2'>
								<Icon icon='Edit' className='me-1' size='sm' />
								{t('common:unsavedChanges', 'Unsaved changes')}
							</span>
						)}
					</SubHeaderLeft>
					<SubHeaderRight>
						<Button
							color='primary'
							onClick={handleUpdateOrganization}
							isDisable={isSaving}>
							{isSaving ? (
								<>
									<Spinner isSmall className='me-2' />
									{t('common:saving', 'Saving...')}
								</>
							) : (
								<>
									<Icon icon='Save' className='me-1' />
									{t('common:saveChanges', 'Save Changes')}
								</>
							)}
						</Button>
					</SubHeaderRight>
				</SubHeader>
				<Page container='fluid'>
					{isLoading && <Loading />}
					{error && <div className='alert alert-danger'>{error}</div>}

					{!isLoading && (() => {
						// Helper: renders tooltip icon positioned inside the form group (top-right)
						const HintIcon = ({ id, hint }: { id: string; hint: string }) => (
							<Tooltips title={hint} placement='top'>
								<span id={id} className='hint-icon'>
									<Icon icon='Info' color='primary' size='sm' />
								</span>
							</Tooltips>
						);

						return (<>
							{/* Banner */}
							<Alert color='primary' isLight icon='Info' className='mb-4'>
								{t(
									'This information is required to automatically compile contracts for signing and to use your correct company data across the platform.',
								)}
							</Alert>

							<div className='row g-4'>
								{/* Left Column - Company Identity */}
								<div className='col-lg-4'>
									<Card className='h-100'>
										<CardHeader>
											<CardLabel icon='Business'>
												<CardTitle>{t('Company Identity')}</CardTitle>
											</CardLabel>
										</CardHeader>
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
													onClick={handleLogoClick}
													disabled={isUploadingLogo}>
													{isUploadingLogo ? (
														<div className='company-logo-loading'>
															<Spinner color='primary' />
														</div>
													) : logoUrl ? (
														<img
															src={logoUrl}
															alt={formData.name || 'Company Logo'}
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
														<span className='mt-1'>
															{t('common:change', 'Change')}
														</span>
													</div>
												</button>
												{logoUrl && (
													<div className='text-muted text-center mt-2 small'>
														{t('Click to upload logo')}
													</div>
												)}
											</div>

											{/* Organization Name */}
											<div className='w-100'>
												<div className='form-group-hint'>
													<FormGroup
														id='name'
														label={t('Organization Name')}
														isFloating>
														<Input
															required
															type='text'
															name='name'
															value={formData.name}
															onChange={handleInputChange}
															onBlur={handleBlur}
															placeholder={t('Enter organization name')}
															isTouched={touched.name}
															isValid={!validationErrors.name}
															invalidFeedback={validationErrors.name}
														/>
													</FormGroup>
													<HintIcon id='hint-orgName' hint={t('Internal reference name of your organization')} />
												</div>
											</div>
										</CardBody>
									</Card>
								</div>

								{/* Right Column - Billing & Fiscal Information */}
								<div className='col-lg-8'>
									<Card className='h-100'>
										<CardHeader>
											<CardLabel icon='Receipt'>
												<CardTitle>
													{t('Billing & Fiscal Information')}
												</CardTitle>
											</CardLabel>
										</CardHeader>
										<CardBody>
											<div className='row g-4'>
												{/* Fiscal Data Section */}
												<div className='col-md-6'>
													<div className='form-group-hint'>
														<FormGroup
															id='legalName'
															label={t('Legal Name')}
															isFloating>
															<Input
																required
																name='legalName'
																value={formData.legalName}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.legalName}
																isValid={!validationErrors.legalName}
																invalidFeedback={
																	validationErrors.legalName
																}
															/>
														</FormGroup>
														<HintIcon id='hint-legalName' hint={t('Full legal company name as registered')} />
													</div>
												</div>

												<div className='col-md-6'>
													<div className='form-group-hint'>
														<FormGroup
															id='vatNumber'
															label={t('VAT Number')}
															isFloating>
															<Input
																required
																name='vatNumber'
																value={formData.vatNumber}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.vatNumber}
																isValid={!validationErrors.vatNumber}
																invalidFeedback={
																	validationErrors.vatNumber
																}
															/>
														</FormGroup>
														<HintIcon id='hint-vatNumber' hint={t('Company VAT number for electronic invoicing')} />
													</div>
												</div>

												<div className='col-md-6'>
													<div className='form-group-hint'>
														<FormGroup
															id='fiscalCode'
															label={t('Fiscal Code')}
															isFloating>
															<Input
																required
																name='fiscalCode'
																value={formData.fiscalCode}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.fiscalCode}
																isValid={!validationErrors.fiscalCode}
																invalidFeedback={
																	validationErrors.fiscalCode
																}
															/>
														</FormGroup>
														<HintIcon id='hint-fiscalCode' hint={t('Company fiscal code or owner code for sole proprietorship')} />
													</div>
												</div>

												<div className='col-md-6'>
													<div className='form-group-hint'>
														<FormGroup
															id='italianSdiCode'
															label={t('SDI Code')}
															isFloating>
															<Input
																required
																name='italianSdiCode'
																value={formData.italianSdiCode}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.italianSdiCode}
																isValid={
																	!validationErrors.italianSdiCode
																}
																invalidFeedback={
																	validationErrors.italianSdiCode
																}
															/>
														</FormGroup>
														<HintIcon id='hint-sdiCode' hint={t('7-character recipient code for electronic invoicing')} />
													</div>
												</div>

												<div className='col-md-6'>
													<div className='form-group-hint'>
														<FormGroup
															id='billingEmail'
															label={t('Billing Email')}
															isFloating>
															<Input
																required
																type='email'
																name='billingEmail'
																value={formData.billingEmail}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.billingEmail}
																isValid={!validationErrors.billingEmail}
																invalidFeedback={
																	validationErrors.billingEmail
																}
															/>
														</FormGroup>
														<HintIcon id='hint-billingEmail' hint={t('Email address for receiving fiscal documents')} />
													</div>
												</div>

												<div className='col-md-6'>
													<div className='form-group-hint'>
														<FormGroup
															id='certifiedEmail'
															label={t('Certified Email')}
															isFloating>
															<Input
																type='email'
																name='certifiedEmail'
																value={formData.certifiedEmail}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.certifiedEmail}
																isValid={
																	!validationErrors.certifiedEmail
																}
																invalidFeedback={
																	validationErrors.certifiedEmail
																}
															/>
														</FormGroup>
														<HintIcon id='hint-certifiedEmail' hint={t('Official certified email for legal communication')} />
													</div>
												</div>

												{/* Divider */}
												<div className='col-12'>
													<hr className='my-2' />
													<h6 className='text-muted mb-3'>
														<Icon icon='LocationOn' className='me-2' />
														{t('Billing Address')}
													</h6>
												</div>

												{/* Address Fields */}
												<div className='col-12'>
													<div className='form-group-hint'>
														<FormGroup
															id='billingAddress'
															label={t('Street Address')}
															isFloating>
															<Input
																required
																name='billingAddress'
																value={formData.billingAddress}
																onChange={handleInputChange}
																onBlur={handleBlur}
																isTouched={touched.billingAddress}
																isValid={
																	!validationErrors.billingAddress
																}
																invalidFeedback={
																	validationErrors.billingAddress
																}
															/>
														</FormGroup>
														<HintIcon id='hint-billingAddress' hint={t('Official registered billing or legal address')} />
													</div>
												</div>

												<div className='col-md-3'>
													<FormGroup
														id='billingCity'
														label={t('City')}
														isFloating>
														<Input
															required
															name='billingCity'
															value={formData.billingCity}
															onChange={handleInputChange}
															onBlur={handleBlur}
															isTouched={touched.billingCity}
															isValid={!validationErrors.billingCity}
															invalidFeedback={
																validationErrors.billingCity
															}
														/>
													</FormGroup>
												</div>

												<div className='col-md-3'>
													<FormGroup
														id='billingProvince'
														label={t('Province')}
														isFloating>
														<Select
															ariaLabel={t('Province')}
															list={(
																provinces as Array<{
																	code: string;
																	label: string;
																}>
															).map((p) => ({
																value: p.code,
																text: `${p.code} - ${p.label}`,
															}))}
															onChange={(
																e: React.ChangeEvent<HTMLSelectElement>,
															) => {
																const v = e.target.value;
																setFormData((prev) => ({
																	...prev,
																	billingProvince: v,
																}));
																if (touched.billingProvince)
																	validateAndUpdateField(
																		'billingProvince',
																		v,
																	);
															}}
															value={formData.billingProvince}
														/>
													</FormGroup>
												</div>

												<div className='col-md-3'>
													<FormGroup
														id='billingZipCode'
														label={t(
															'common:billing.zipCode',
															'ZIP / Postal Code',
														)}
														isFloating>
														<Input
															required
															name='billingZipCode'
															value={formData.billingZipCode}
															onChange={handleInputChange}
															onBlur={handleBlur}
															isTouched={touched.billingZipCode}
															isValid={
																!validationErrors.billingZipCode
															}
															invalidFeedback={
																validationErrors.billingZipCode
															}
														/>
													</FormGroup>
												</div>

												<div className='col-md-3'>
													<CountrySelect
														value={formData.billingCountryCode}
														onChange={(code) =>
															setFormData((prev) => ({
																...prev,
																billingCountryCode: code,
															}))
														}
														required
														label={t(
															'common:billing.country',
															'Country',
														)}
													/>
												</div>
											</div>
										</CardBody>
									</Card>
								</div>
							</div>
						</>);
					})()}
				</Page>
			</PageWrapper>
		</>
	);
};

export default UpdateOrganization;
