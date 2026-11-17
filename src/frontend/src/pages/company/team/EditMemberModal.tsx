import React, { useState, use } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Dialog, Notice, showApiError } from '../../../ui';
import organizationService from '../../../services/api/organizationService';
import { toApiProblem } from '../../../services/api/apiProblem';
import { OrganizationUser } from '../../../type/organization-types';
import { ORGANIZATION_ROLES, OrganizationRole } from '../../../common/constants/organizationRoles';
import AuthContext from '../../../contexts/authContext';

interface EditMemberModalProps {
	organizationId: string;
	member: OrganizationUser;
	onClose: () => void;
	/** Called after saving; the flag is true when the user changed their own role. */
	onSuccess: (changedOwnRole: boolean) => void;
	/** Called when the member is no longer in the team. */
	onMemberGone: () => void;
}

/** The "Edit Team Member" dialog: the name and email are shown, only the role changes. */
// US-ORG-06
const EditMemberModal: React.FC<EditMemberModalProps> = ({
	organizationId,
	member,
	onClose,
	onSuccess,
	onMemberGone,
}) => {
	const { t } = useTranslation(['team', 'common']);
	const { userData } = use(AuthContext);
	const [loading, setLoading] = useState(false);
	const [confirmingDemotion, setConfirmingDemotion] = useState(false);
	const [role, setRole] = useState<OrganizationRole>(member.role);

	const isEditingSelf = userData?.id === member.userId;
	const hasChanges = role !== member.role;
	// An Admin who lowers their own role must confirm
	const isDemotingSelf = isEditingSelf && member.role === 'Admin' && role !== 'Admin';

	const handleSubmit = async () => {
		if (isDemotingSelf && !confirmingDemotion) {
			setConfirmingDemotion(true);
			return;
		}

		setLoading(true);
		try {
			await organizationService.updateOrganizationUser(organizationId, member.userId, {
				role,
			});
			onSuccess(isEditingSelf);
		} catch (err) {
			const problem = toApiProblem(err);
			showApiError(problem);
			if (problem.status === 404) onMemberGone();
		} finally {
			setLoading(false);
			setConfirmingDemotion(false);
		}
	};

	return (
		<Dialog
			show
			onHide={onClose}
			size='lg'
			icon='Edit'
			title={t('edit.title')}
			footer={
				<>
					<Button variant='light' onClick={onClose} disabled={loading}>
						{t('common.cancel')}
					</Button>

					{confirmingDemotion ? (
						<>
							<Button
								variant='warning'
								onClick={() => setConfirmingDemotion(false)}
								disabled={loading}>
								{t('common.goBack')}
							</Button>
							<Button
								variant='danger'
								icon='Warning'
								busy={loading}
								onClick={handleSubmit}>
								{t('edit.confirmDemotion')}
							</Button>
						</>
					) : (
						<Button
							variant='info'
							icon='Save'
							busy={loading}
							disabled={!hasChanges}
							onClick={handleSubmit}>
							{t('common.save')}
						</Button>
					)}
				</>
			}>
			{/* Member Info: shown, not editable */}
			<div className='mb-3 p-3 rounded bg-body-tertiary'>
				<div className='row'>
					<div className='col-md-6'>
						<strong>{t('edit.memberName')}</strong>
						<div className='mt-1'>
							{`${member.firstName} ${member.lastName}`.trim()}
						</div>
					</div>
					<div className='col-md-6'>
						<strong>{t('edit.memberEmail')}</strong>
						<div className='mt-1 text-muted'>{member.email}</div>
					</div>
				</div>
			</div>

			{/* Demotion Warning */}
			{isDemotingSelf && (
				<Notice tone='warning' icon='Warning' className='mb-3'>
					{t('edit.demotionWarning')}
				</Notice>
			)}

			{/* Role Selection */}
			<fieldset className='mb-3'>
				<legend className='form-label fs-6'>{t('edit.role')}</legend>
				<div className='border rounded p-3'>
					{ORGANIZATION_ROLES.map((r) => (
						<div key={r} className='form-check mb-2'>
							<input
								className='form-check-input'
								type='radio'
								name='role'
								id={`role${r}`}
								aria-label={t(`roles.${r.toLowerCase()}`)}
								aria-describedby={`role${r}-description`}
								checked={role === r}
								onChange={() => {
									setRole(r);
									setConfirmingDemotion(false);
								}}
							/>
							<label className='form-check-label' htmlFor={`role${r}`}>
								<strong>{t(`roles.${r.toLowerCase()}`)}</strong>
								<div id={`role${r}-description`} className='text-muted small'>
									{t(`roles.${r.toLowerCase()}.description`)}
								</div>
							</label>
						</div>
					))}
				</div>
			</fieldset>
		</Dialog>
	);
};

export default EditMemberModal;
