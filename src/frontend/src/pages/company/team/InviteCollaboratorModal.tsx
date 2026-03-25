/** @userstory US-TEAM-01, US-TEAM-02, US-TEAM-03, US-TEAM-04, US-TEAM-12, US-TEAM-13, US-TEAM-14, US-TEAM-15 */
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import Modal, { ModalHeader, ModalBody, ModalFooter } from '../../../components/bootstrap/Modal';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import { toast } from 'react-toastify';
import organizationService from '../../../services/api/organizationService';
import {
	PermissionCodes,
	PermissionCode,
	PermissionMetadata,
	OrganizationRole,
	getAllPermissionCodes,
} from '../../../common/constants/permissionCodes';
import useDarkMode from '../../../hooks/useDarkMode';

interface InviteCollaboratorModalProps {
	organizationId: string;
	onClose: () => void;
	onSuccess: () => void;
}

interface InviteFormState {
	fullName: string;
	email: string;
	role: OrganizationRole;
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
}

const InviteCollaboratorModal: React.FC<InviteCollaboratorModalProps> = ({
	organizationId,
	onClose,
	onSuccess,
}) => {
	const { t } = useTranslation(['team', 'common']);
	const { darkModeStatus } = useDarkMode();
	const [loading, setLoading] = useState(false);
	const [errors, setErrors] = useState<Record<string, string>>({});

	const [form, setForm] = useState<InviteFormState>({
		fullName: '',
		email: '',
		role: 'Member',
		permissions: [],
		hasSigningAuthority: false,
	});

	// Handle text field changes
	const handleChange = (field: keyof InviteFormState, value: any) => {
		setForm((prev) => ({ ...prev, [field]: value }));
		// Clear error when field changes
		if (errors[field]) {
			setErrors((prev) => ({ ...prev, [field]: '' }));
		}
	};

	// Handle role change
	const handleRoleChange = (role: OrganizationRole) => {
		setForm((prev) => ({
			...prev,
			role,
			// Clear permissions when switching to Admin (they have all)
			permissions: role === 'Admin' ? [] : prev.permissions,
		}));
		// Clear permission error when switching roles
		if (errors.permissions) {
			setErrors((prev) => ({ ...prev, permissions: '' }));
		}
	};

	// Toggle permission
	const togglePermission = (permission: PermissionCode) => {
		setForm((prev) => {
			const current = prev.permissions;
			const newPermissions = current.includes(permission)
				? current.filter((p) => p !== permission)
				: [...current, permission];
			return { ...prev, permissions: newPermissions };
		});
		// Clear error when permissions change
		if (errors.permissions) {
			setErrors((prev) => ({ ...prev, permissions: '' }));
		}
	};

	// Validate form
	const validate = (): boolean => {
		const newErrors: Record<string, string> = {};

		if (!form.fullName.trim()) {
			newErrors.fullName = t('validation.fullNameRequired');
		}

		if (!form.email.trim()) {
			newErrors.email = t('validation.emailRequired');
		} else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
			newErrors.email = t('validation.emailInvalid');
		}

		if (form.role === 'Member' && form.permissions.length === 0) {
			newErrors.permissions = t('validation.atLeastOnePermission');
		}

		setErrors(newErrors);
		return Object.keys(newErrors).length === 0;
	};

	// Handle submit
	const handleSubmit = async () => {
		if (!validate()) return;

		setLoading(true);
		try {
			await organizationService.inviteCollaborator(organizationId, {
				fullName: form.fullName,
				email: form.email,
				role: form.role,
				permissions: form.role === 'Admin' ? getAllPermissionCodes() : form.permissions,
				hasSigningAuthority: form.hasSigningAuthority,
			});
			onSuccess();
		} catch (err: any) {
			console.error('Error inviting collaborator:', err);
			const errorMessage = err.response?.data?.message || err.message || t('errorInviting');
			toast.error(errorMessage);
		} finally {
			setLoading(false);
		}
	};

	return (
		<Modal isOpen setIsOpen={onClose} isCentered size='lg'>
			<ModalHeader setIsOpen={onClose}>
				<h5>
					<Icon icon='PersonAdd' className='me-2' />
					{t('invite.title')}
				</h5>
			</ModalHeader>

			<ModalBody>
				{/* Full Name */}
				<div className='mb-3'>
					<label className='form-label'>
						{t('invite.fullName')} <span className='text-danger'>*</span>
					</label>
					<input
						type='text'
						className={`form-control ${errors.fullName ? 'is-invalid' : ''}`}
						placeholder={t('invite.fullNamePlaceholder')}
						value={form.fullName}
						onChange={(e) => handleChange('fullName', e.target.value)}
					/>
					{errors.fullName && <div className='invalid-feedback'>{errors.fullName}</div>}
				</div>

				{/* Email */}
				<div className='mb-3'>
					<label className='form-label'>
						{t('invite.email')} <span className='text-danger'>*</span>
					</label>
					<input
						type='email'
						className={`form-control ${errors.email ? 'is-invalid' : ''}`}
						placeholder={t('invite.emailPlaceholder')}
						value={form.email}
						onChange={(e) => handleChange('email', e.target.value)}
					/>
					{errors.email && <div className='invalid-feedback'>{errors.email}</div>}
				</div>

				{/* Role Selection */}
				<div className='mb-3'>
					<label className='form-label'>{t('invite.role')}</label>
					<div className='border rounded p-3'>
						<div className='form-check mb-2'>
							<input
								className='form-check-input'
								type='radio'
								name='role'
								id='roleAdmin'
								checked={form.role === 'Admin'}
								onChange={() => handleRoleChange('Admin')}
							/>
							<label className='form-check-label' htmlFor='roleAdmin'>
								<strong>🔑 {t('roles.admin')}</strong>
								<div className='text-muted small'>
									{t('roles.admin.description')}
								</div>
							</label>
						</div>
						<div className='form-check'>
							<input
								className='form-check-input'
								type='radio'
								name='role'
								id='roleMember'
								checked={form.role === 'Member'}
								onChange={() => handleRoleChange('Member')}
							/>
							<label className='form-check-label' htmlFor='roleMember'>
								<strong>👤 {t('roles.member')}</strong>
								<div className='text-muted small'>
									{t('roles.member.description')}
								</div>
							</label>
						</div>
					</div>
				</div>

				{/* Permissions (only for Member) */}
				{form.role === 'Member' && (
					<div className='mb-3'>
						<label className='form-label'>
							{t('invite.permissions')}{' '}
							<span className='text-muted'>({t('invite.permissionsHint')})</span>
						</label>
						<div
							className={`border rounded ${errors.permissions ? 'border-danger' : ''}`}>
							{Object.values(PermissionCodes).map((code) => {
								const meta = PermissionMetadata[code];
								return (
									<label
										key={code}
										className='d-flex align-items-center p-3 border-bottom cursor-pointer hover-bg-light'
										style={{ cursor: 'pointer' }}>
										<input
											type='checkbox'
											className='form-check-input me-3'
											checked={form.permissions.includes(code)}
											onChange={() => togglePermission(code)}
										/>
										<span className='me-2' style={{ fontSize: '1.2em' }}>
											{meta.icon}
										</span>
										<div>
											<div className='fw-bold'>{t(meta.labelKey)}</div>
											<small className='text-muted'>
												{t(meta.descriptionKey)}
											</small>
										</div>
									</label>
								);
							})}
						</div>
						{errors.permissions && (
							<div className='text-danger small mt-1'>{errors.permissions}</div>
						)}
					</div>
				)}

				{/* Signing Authority */}
				<div className='mb-3'>
					<div
						className={classNames('border rounded p-3', {
							'bg-l10-dark': !darkModeStatus,
							'bg-dark': darkModeStatus,
						})}>
						<div className='form-check'>
							<input
								className='form-check-input'
								type='checkbox'
								id='signingAuthority'
								checked={form.hasSigningAuthority}
								onChange={(e) =>
									handleChange('hasSigningAuthority', e.target.checked)
								}
							/>
							<label className='form-check-label' htmlFor='signingAuthority'>
								<strong>{t('invite.signingAuthority')}</strong>
								<div className='text-warning small'>
									<Icon icon='Warning' className='me-1' />
									{t('invite.signingWarning')}
								</div>
							</label>
						</div>
					</div>
				</div>
			</ModalBody>

			<ModalFooter className='d-flex justify-content-end gap-2'>
				<Button color='light' onClick={onClose} isDisable={loading}>
					{t('common.cancel')}
				</Button>
				<Button color='primary' onClick={handleSubmit} isDisable={loading}>
					{loading ? (
						<>
							<span className='spinner-border spinner-border-sm me-2' />
							{t('common.sending')}
						</>
					) : (
						<>
							<Icon icon='Send' className='me-2' />
							{t('invite.submit')}
						</>
					)}
				</Button>
			</ModalFooter>
		</Modal>
	);
};

export default InviteCollaboratorModal;
