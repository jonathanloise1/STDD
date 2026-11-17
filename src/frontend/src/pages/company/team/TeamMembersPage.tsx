import React, { use, useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
	ApiErrorMessage,
	Badge,
	Button,
	Card,
	CardBody,
	CardHeader,
	ConfirmDialog,
	Icon,
	IconButton,
	LoadingBlock,
	notify,
	Page,
	PageHeader,
	showApiError,
	type Tone,
} from '../../../ui';
import AuthContext from '../../../contexts/authContext';
import { useOrganization } from '../../../contexts/organizationContext';
import organizationService from '../../../services/api/organizationService';
import { ApiProblem, toApiProblem } from '../../../services/api/apiProblem';
import { OrganizationUser, PendingInvitation } from '../../../type/organization-types';
import InviteCollaboratorModal from './InviteCollaboratorModal';
import EditMemberModal from './EditMemberModal';

const roleBadgeColor: Record<string, Tone> = {
	Admin: 'primary',
	Editor: 'info',
	Viewer: 'secondary',
};

/** The Team page: members, pending invitations, invite, change role and remove (Admins only). */
// US-ORG-04, US-ORG-05, US-ORG-06, US-ORG-07
const TeamMembersPage: React.FC = () => {
	const { t } = useTranslation(['team', 'common']);
	const navigate = useNavigate();
	const { selectedOrganization } = useOrganization();
	const { syncUserData } = use(AuthContext);

	const [users, setUsers] = useState<OrganizationUser[]>([]);
	const [searchTerm, setSearchTerm] = useState('');
	const [loading, setLoading] = useState(true);
	const [loadError, setLoadError] = useState<ApiProblem | null>(null);
	const [pendingInvitations, setPendingInvitations] = useState<PendingInvitation[]>([]);

	const [showInviteModal, setShowInviteModal] = useState(false);
	const [editingUser, setEditingUser] = useState<OrganizationUser | null>(null);
	const [removeTargetUser, setRemoveTargetUser] = useState<OrganizationUser | null>(null);

	const organizationId = selectedOrganization?.id;

	// Load users and pending invitations for organization
	const loadData = useCallback(async () => {
		if (!organizationId) return;

		try {
			setLoading(true);
			setLoadError(null);
			const [orgUsers, invitations] = await Promise.all([
				organizationService.getOrganizationUsers(organizationId),
				organizationService.getPendingInvitations(organizationId),
			]);
			// Invited people are listed under "Pending Invitations" only; removed members are not listed
			setUsers(orgUsers.filter((user) => user.status === 'Active'));
			setPendingInvitations(invitations);
		} catch (err) {
			setLoadError(toApiProblem(err));
		} finally {
			setLoading(false);
		}
	}, [organizationId]);

	useEffect(() => {
		loadData();
	}, [loadData]);

	// Members whose name or email contains the search text, ignoring case
	const filteredUsers = useMemo(() => {
		const lower = searchTerm.trim().toLowerCase();
		if (!lower) return users;
		return users.filter(
			(u) =>
				`${u.firstName} ${u.lastName}`.toLowerCase().includes(lower) ||
				u.email.toLowerCase().includes(lower),
		);
	}, [users, searchTerm]);

	const handleInviteSuccess = () => {
		setShowInviteModal(false);
		loadData();
		notify.success(t('inviteSent'));
	};

	const handleEditSuccess = (changedOwnRole: boolean) => {
		setEditingUser(null);
		loadData();
		notify.success(t('updateSuccess'));
		// The menu and the pages follow the new role at once
		if (changedOwnRole) syncUserData?.(true, organizationId);
	};

	// A member who is no longer in the team is left out of the refreshed table
	const handleMemberGone = () => {
		setEditingUser(null);
		loadData();
	};

	const handleRemoveConfirm = async () => {
		if (!organizationId || !removeTargetUser) return;

		try {
			await organizationService.removeCollaborator(organizationId, removeTargetUser.userId);
			notify.success(t('removeSuccess'));
		} catch (err) {
			showApiError(toApiProblem(err));
		}
		setRemoveTargetUser(null);
		loadData();
	};

	// Admins cannot be removed from the Team page; their role must be lowered first
	const canRemoveUser = (user: OrganizationUser): boolean => user.role !== 'Admin';

	if (!selectedOrganization) {
		return null;
	}

	return (
		<Page title={t('title')} fluid>
			<PageHeader
				back={{
					label: t('common:back'),
					onClick: () => navigate('/settings/organization'),
				}}
				title={`${t('title')} - ${selectedOrganization.name}`}
			/>

			<Card>
				<CardHeader
					icon='People'
					title={t('Members')}
					actions={
						<div className='d-flex align-items-center gap-3'>
							{/* Search */}
							<div className='position-relative'>
								<input
									type='text'
									className='form-control app-search-input'
									placeholder={t('searchPlaceholder')}
									aria-label={t('searchPlaceholder')}
									value={searchTerm}
									onChange={(e) => setSearchTerm(e.target.value)}
								/>
								<span className='position-absolute text-body-secondary app-search-icon'>
									<Icon icon='Search' />
								</span>
							</div>
							{/* Invite button */}
							<Button
								variant='primary'
								icon='PersonAdd'
								onClick={() => setShowInviteModal(true)}>
								{t('inviteButton')}
							</Button>
						</div>
					}
				/>
				<CardBody className='p-0'>
					{loading ? (
						<LoadingBlock />
					) : loadError ? (
						<div className='p-3'>
							<ApiErrorMessage problem={loadError} />
						</div>
					) : (
						<div className='table-responsive'>
							<table className='table align-middle mb-0' aria-label={t('Members')}>
								<thead>
									<tr>
										<th className='app-col-lg app-cell-start'>
											{t('columns.name')}
										</th>
										<th className='app-col-lg'>{t('columns.email')}</th>
										<th className='app-col-sm'>{t('columns.role')}</th>
										<th className='text-end app-col-md app-cell-end'>
											{t('columns.actions')}
										</th>
									</tr>
								</thead>
								<tbody>
									{filteredUsers.length > 0 ? (
										filteredUsers.map((user) => (
											<tr key={user.id}>
												<td className='app-cell-start'>
													{`${user.firstName} ${user.lastName}`}
												</td>
												<td>{user.email}</td>
												<td>
													<Badge
														tone={
															roleBadgeColor[user.role] ?? 'secondary'
														}>
														{t(`roles.${user.role.toLowerCase()}`)}
													</Badge>
												</td>
												<td className='text-end app-cell-end'>
													<IconButton
														variant='subtle-info'
														size='sm'
														icon='Edit'
														label={t('common:edit')}
														onClick={() => setEditingUser(user)}
													/>
													{canRemoveUser(user) && (
														<IconButton
															variant='subtle-danger'
															size='sm'
															className='ms-1'
															icon='Delete'
															label={t('remove')}
															onClick={() =>
																setRemoveTargetUser(user)
															}
														/>
													)}
												</td>
											</tr>
										))
									) : (
										<tr>
											<td colSpan={4} className='text-center py-4'>
												{searchTerm
													? t('noMatchingMembers')
													: t('noMembers')}
											</td>
										</tr>
									)}
								</tbody>
							</table>
						</div>
					)}
				</CardBody>
			</Card>

			{/* Pending Invitations Section */}
			{!loading && !loadError && pendingInvitations.length > 0 && (
				<Card className='mt-4'>
					<CardHeader icon='Mail' title={t('pendingInvitations')}>
						<Badge tone='warning'>{pendingInvitations.length}</Badge>
					</CardHeader>
					<CardBody className='p-0'>
						<div className='table-responsive'>
							<table
								className='table align-middle mb-0'
								aria-label={t('pendingInvitations')}>
								<thead>
									<tr>
										<th className='app-col-lg app-cell-start'>
											{t('columns.name')}
										</th>
										<th className='app-col-lg'>{t('columns.email')}</th>
										<th className='app-col-md'>{t('columns.sentAt')}</th>
									</tr>
								</thead>
								<tbody>
									{pendingInvitations.map((invitation) => (
										<tr key={invitation.id}>
											<td className='app-cell-start'>
												{invitation.fullName}
											</td>
											<td>{invitation.email}</td>
											<td>
												<Badge tone='warning'>
													{new Date(
														invitation.createdAt,
													).toLocaleDateString()}
												</Badge>
											</td>
										</tr>
									))}
								</tbody>
							</table>
						</div>
					</CardBody>
				</Card>
			)}

			{/* Invite Modal */}
			{showInviteModal && organizationId && (
				<InviteCollaboratorModal
					organizationId={organizationId}
					onClose={() => setShowInviteModal(false)}
					onSuccess={handleInviteSuccess}
				/>
			)}

			{/* Edit Modal */}
			{editingUser && organizationId && (
				<EditMemberModal
					organizationId={organizationId}
					member={editingUser}
					onClose={() => setEditingUser(null)}
					onSuccess={handleEditSuccess}
					onMemberGone={handleMemberGone}
				/>
			)}

			{/* Remove Confirmation Modal */}
			<ConfirmDialog
				show={!!removeTargetUser}
				title={t('confirmRemoveTitle')}
				message={
					<>
						<Icon icon='Warning' size='lg' className='me-2 text-danger' />
						{t('team.confirmRemoveMessage', {
							name: removeTargetUser
								? `${removeTargetUser.firstName} ${removeTargetUser.lastName}`
								: '',
						})}
					</>
				}
				cancelLabel={t('common.cancel')}
				confirmLabel={t('remove')}
				confirmIcon='Delete'
				onCancel={() => setRemoveTargetUser(null)}
				onConfirm={handleRemoveConfirm}
			/>
		</Page>
	);
};

export default TeamMembersPage;
