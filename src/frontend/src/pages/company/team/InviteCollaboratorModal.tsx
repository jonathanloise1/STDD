import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Dialog, showApiError } from '../../../ui';
import organizationService from '../../../services/api/organizationService';
import { toApiProblem } from '../../../services/api/apiProblem';
import {
	ORGANIZATION_ROLES,
	DEFAULT_ORGANIZATION_ROLE,
	OrganizationRole,
} from '../../../common/constants/organizationRoles';

interface InviteCollaboratorModalProps {
	organizationId: string;
	onClose: () => void;
	onSuccess: () => void;
}

interface InviteFormState {
	fullName: string;
	email: string;
	role: OrganizationRole;
}

/** The "Invite Team Member" dialog. */
// US-ORG-05
const InviteCollaboratorModal: React.FC<InviteCollaboratorModalProps> = ({
	organizationId,
	onClose,
	onSuccess,
}) => {
	const { t } = useTranslation(['team', 'common']);
	const [loading, setLoading] = useState(false);
	const [errors, setErrors] = useState<Record<string, string>>({});

	const [form, setForm] = useState<InviteFormState>({
		fullName: '',
		email: '',
		role: DEFAULT_ORGANIZATION_ROLE,
	});

	const handleChange = (field: keyof InviteFormState, value: string) => {
		setForm((prev) => ({ ...prev, [field]: value }));
		if (errors[field]) {
			setErrors((prev) => ({ ...prev, [field]: '' }));
		}
	};

	const validate = (): boolean => {
		const newErrors: Record<string, string> = {};

		if (!form.fullName.trim()) {
			newErrors.fullName = t('validation.fullNameRequired');
		}

		if (!form.email.trim()) {
			newErrors.email = t('validation.emailRequired');
		} else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
			newErrors.email = t('validation.emailInvalid');
		}

		setErrors(newErrors);
		return Object.keys(newErrors).length === 0;
	};

	const handleSubmit = async () => {
		if (!validate()) return;

		setLoading(true);
		try {
			await organizationService.inviteCollaborator(organizationId, {
				fullName: form.fullName.trim(),
				email: form.email.trim(),
				role: form.role,
			});
			onSuccess();
		} catch (err) {
			showApiError(toApiProblem(err));
		} finally {
			setLoading(false);
		}
	};

	return (
		<Dialog
			show
			onHide={onClose}
			size='lg'
			icon='PersonAdd'
			title={t('invite.title')}
			footer={
				<>
					<Button variant='light' onClick={onClose} disabled={loading}>
						{t('common.cancel')}
					</Button>
					<Button variant='primary' icon='Send' busy={loading} onClick={handleSubmit}>
						{loading ? t('common.sending') : t('invite.submit')}
					</Button>
				</>
			}>
			{/* Full Name */}
			<div className='mb-3'>
				<label htmlFor='invite-full-name' className='form-label'>
					{t('invite.fullName')}{' '}
					<span className='text-danger' aria-hidden='true'>
						*
					</span>
				</label>
				<input
					id='invite-full-name'
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
				<label htmlFor='invite-email' className='form-label'>
					{t('invite.email')}{' '}
					<span className='text-danger' aria-hidden='true'>
						*
					</span>
				</label>
				<input
					id='invite-email'
					type='text'
					inputMode='email'
					className={`form-control ${errors.email ? 'is-invalid' : ''}`}
					placeholder={t('invite.emailPlaceholder')}
					value={form.email}
					onChange={(e) => handleChange('email', e.target.value)}
				/>
				{errors.email && <div className='invalid-feedback'>{errors.email}</div>}
			</div>

			{/* Role Selection */}
			<fieldset className='mb-3'>
				<legend className='form-label fs-6'>{t('invite.role')}</legend>
				<div className='border rounded p-3'>
					{ORGANIZATION_ROLES.map((role) => (
						<div key={role} className='form-check mb-2'>
							<input
								className='form-check-input'
								type='radio'
								name='role'
								id={`role${role}`}
								aria-label={t(`roles.${role.toLowerCase()}`)}
								aria-describedby={`role${role}-description`}
								checked={form.role === role}
								onChange={() => handleChange('role', role)}
							/>
							<label className='form-check-label' htmlFor={`role${role}`}>
								<strong>{t(`roles.${role.toLowerCase()}`)}</strong>
								<div id={`role${role}-description`} className='text-muted small'>
									{t(`roles.${role.toLowerCase()}.description`)}
								</div>
							</label>
						</div>
					))}
				</div>
			</fieldset>
		</Dialog>
	);
};

export default InviteCollaboratorModal;
