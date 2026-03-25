/** @userstory US-TEAM-02, US-TEAM-03, US-TEAM-04, US-TEAM-05 */
import React, { useState, useEffect, use } from 'react';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import Modal, { ModalHeader, ModalBody, ModalFooter } from '../../../components/bootstrap/Modal';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import { toast } from 'react-toastify';
import organizationService from '../../../services/api/organizationService';
import { OrganizationUser } from '../../../type/organization-types';
import {
	PermissionCodes,
	PermissionCode,
	PermissionMetadata,
	OrganizationRole,
	getAllPermissionCodes,
} from '../../../common/constants/permissionCodes';
import { usePermissions } from '../../../contexts/permissionContext';
import AuthContext from '../../../contexts/authContext';
import useDarkMode from '../../../hooks/useDarkMode';

interface EditMemberModalProps {
	organizationId: string;
	member: OrganizationUser;
	onClose: () => void;
	onSuccess: () => void;
}

interface EditFormState {
	fullName: string;
	role: OrganizationRole;
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
}

const EditMemberModal: React.FC<EditMemberModalProps> = ({
	organizationId,
	member,
	onClose,
	onSuccess,
}) => {
	const { t } = useTranslation(['team', 'common']);
	const { darkModeStatus } = useDarkMode();
	const { isAdmin: currentUserIsAdmin } = usePermissions();
	const { userData } = use(AuthContext);
	const [loading, setLoading] = useState(false);
	const [errors, setErrors] = useState<Record<string, string>>({});
	const [confirmingDemotion, setConfirmingDemotion] = useState(false);

	const isEditingSelf = userData?.id === member.userId;

	const [form, setForm] = useState<EditFormState>({
		fullName: member.fullName || `${member.firstName} ${member.lastName}`.trim(),
		role: member.role,
		permissions: member.permissions || [],
		hasSigningAuthority: member.hasSigningAuthority || false,
	});

	const [originalForm, setOriginalForm] = useState<EditFormState>({
		fullName: member.fullName || `${member.firstName} ${member.lastName}`.trim(),
		role: member.role,
		permissions: member.permissions || [],
		hasSigningAuthority: member.hasSigningAuthority || false,
	});

	useEffect(() => {
		setOriginalForm({
			fullName: member.fullName || `${member.firstName} ${member.lastName}`.trim(),
			role: member.role,
			permissions: member.permissions || [],
			hasSigningAuthority: member.hasSigningAuthority || false,
		});
		setForm({
			fullName: member.fullName || `${member.firstName} ${member.lastName}`.trim(),
			role: member.role,
			permissions: member.permissions || [],
			hasSigningAuthority: member.hasSigningAuthority || false,
		});
	}, [member]);

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

		if (form.role === 'Member' && form.permissions.length === 0) {
			newErrors.permissions = t('validation.atLeastOnePermission');
		}

		setErrors(newErrors);
		return Object.keys(newErrors).length === 0;
	};

	// Check if form has changed
	const hasChanges = (): boolean => {
		return (
			form.fullName.trim() !== originalForm.fullName.trim() ||
			form.role !== originalForm.role ||
			form.hasSigningAuthority !== originalForm.hasSigningAuthority ||
			JSON.stringify([...form.permissions].sort()) !==
				JSON.stringify([...originalForm.permissions].sort())
		);
	};

	// Check if demoting self from Admin to Member
	const isDemotingSelf = (): boolean => {
		return isEditingSelf && originalForm.role === 'Admin' && form.role === 'Member';
	};

	// Handle submit
	const handleSubmit = async () => {
		if (!validate()) return;

		// If demoting self, require confirmation
		if (isDemotingSelf() && !confirmingDemotion) {
			setConfirmingDemotion(true);
			return;
		}

		setLoading(true);
		try {
			await organizationService.updateOrganizationUser(organizationId, member.userId, {
				fullName: form.fullName.trim(),
				role: form.role,
				permissions: form.role === 'Admin' ? getAllPermissionCodes() : form.permissions,
				hasSigningAuthority: form.hasSigningAuthority,
			});
			// Success toast shown by parent component
			onSuccess();
		} catch (err: any) {
			console.error('Error updating member:', err);
			const errorMessage = err.response?.data?.message || err.message || t('errorUpdating');
			toast.error(errorMessage);
		} finally {
			setLoading(false);
			setConfirmingDemotion(false);
		}
	};

	return (
		<Modal isOpen setIsOpen={onClose} isCentered size='lg'>
			<ModalHeader setIsOpen={onClose}>
				<h5>
					<Icon icon='Edit' className='me-2' />
					{t('edit.title')}
				</h5>
			</ModalHeader>

			<ModalBody>
				{/* Member Info */}
				<div
					className={classNames('mb-3 p-3 rounded', {
						'bg-l10-dark': !darkModeStatus,
						'bg-dark': darkModeStatus,
					})}>
					<div className='row'>
						<div className='col-md-6'>
							<strong>{t('edit.memberName')}</strong>
							<input
								type='text'
								className={`form-control mt-1 ${errors.fullName ? 'is-invalid' : ''}`}
								value={form.fullName}
								onChange={(e) => {
									setForm((prev) => ({ ...prev, fullName: e.target.value }));
									if (errors.fullName) {
										setErrors((prev) => ({ ...prev, fullName: '' }));
									}
								}}
								placeholder={t('invite.fullNamePlaceholder')}
							/>
							{errors.fullName && (
								<div className='invalid-feedback'>{errors.fullName}</div>
							)}
						</div>
						<div className='col-md-6'>
							<strong>{t('edit.memberEmail')}</strong>
							<div className='mt-1 text-muted'>{member.email}</div>
						</div>
					</div>
					{member.status === 'Pending' && (
						<div className='mt-2'>
							<span className='badge bg-warning text-dark'>
								<Icon icon='Schedule' className='me-1' />
								{t('status.pending')}
							</span>
						</div>
					)}
				</div>

				{/* Demotion Warning */}
				{isDemotingSelf() && (
					<div className='alert alert-warning'>
						<Icon icon='Warning' className='me-2' />
						{t('edit.demotionWarning')}
					</div>
				)}

				{/* Role Selection */}
				<div className='mb-3'>
					<label className='form-label'>{t('edit.role')}</label>
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
							{t('edit.permissions')}{' '}
							<span className='text-muted'>({t('invite.permissionsHint')})</span>
						</label>
						<div
							className={`border rounded ${errors.permissions ? 'border-danger' : ''}`}>
							{Object.values(PermissionCodes).map((code) => {
								const meta = PermissionMetadata[code];
								return (
									<label
										key={code}
										className='d-flex align-items-center p-3 border-bottom'
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
									setForm((prev) => ({
										...prev,
										hasSigningAuthority: e.target.checked,
									}))
								}
							/>
							<label className='form-check-label' htmlFor='signingAuthority'>
								<strong>{t('edit.signingAuthority')}</strong>
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

				{confirmingDemotion ? (
					<>
						<Button
							color='warning'
							onClick={() => setConfirmingDemotion(false)}
							isDisable={loading}>
							{t('common.goBack')}
						</Button>
						<Button color='danger' onClick={handleSubmit} isDisable={loading}>
							{loading ? (
								<>
									<span className='spinner-border spinner-border-sm me-2' />
									{t('common.saving')}
								</>
							) : (
								<>
									<Icon icon='Warning' className='me-2' />
									{t('edit.confirmDemotion')}
								</>
							)}
						</Button>
					</>
				) : (
					<Button
						color='info'
						onClick={handleSubmit}
						isDisable={loading || !hasChanges()}>
						{loading ? (
							<>
								<span className='spinner-border spinner-border-sm me-2' />
								{t('common.saving')}
							</>
						) : (
							<>
								<Icon icon='Save' className='me-2' />
								{t('common.save')}
							</>
						)}
					</Button>
				)}
			</ModalFooter>
		</Modal>
	);
};

export default EditMemberModal;
