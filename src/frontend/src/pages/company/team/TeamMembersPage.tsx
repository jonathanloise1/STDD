/** @userstory US-TEAM-01, US-TEAM-02, US-TEAM-04, US-TEAM-05, US-TEAM-06, US-TEAM-07 */
import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import SubHeader, { SubHeaderLeft } from '../../../layout/SubHeader/SubHeader';
import Loading from '../../../components/Loading';
import useDarkMode from '../../../hooks/useDarkMode';
import Button from '../../../components/bootstrap/Button';
import Modal, { ModalHeader, ModalBody, ModalFooter } from '../../../components/bootstrap/Modal';
import Icon from '../../../components/icon/Icon';
import Card, {
	CardBody,
	CardHeader,
	CardTitle,
	CardActions,
} from '../../../components/bootstrap/Card';
import Badge from '../../../components/bootstrap/Badge';
import { toast } from 'react-toastify';
import { usePermissions } from '../../../contexts/permissionContext';
import { useOrganization } from '../../../contexts/organizationContext';
import { AccessDenied } from '../../../components/permissions';
import organizationService from '../../../services/api/organizationService';
import {
	OrganizationUser,
	Organization,
	PendingInvitation,
} from '../../../type/organization-types';
import {
	PermissionCodes,
	PermissionCode,
	PermissionMetadata,
} from '../../../common/constants/permissionCodes';
import InviteCollaboratorModal from './InviteCollaboratorModal';
import EditMemberModal from './EditMemberModal';

const TeamMembersPage: React.FC = () => {
	const { t } = useTranslation(['team', 'common']);
	const navigate = useNavigate();
	const { selectedOrganization } = useOrganization();
	const { isAdmin } = usePermissions();
	const { darkModeStatus } = useDarkMode();

	const [users, setUsers] = useState<OrganizationUser[]>([]);
	const [filteredUsers, setFilteredUsers] = useState<OrganizationUser[]>([]);
	const [searchTerm, setSearchTerm] = useState('');
	const [loading, setLoading] = useState(true);
	const [pendingInvitations, setPendingInvitations] = useState<PendingInvitation[]>([]);
	const [resendingId, setResendingId] = useState<string | null>(null);

	const [showInviteModal, setShowInviteModal] = useState(false);
	const [editingUser, setEditingUser] = useState<OrganizationUser | null>(null);
	const [removeTargetUser, setRemoveTargetUser] = useState<OrganizationUser | null>(null);

	const organizationId = selectedOrganization?.id;

	// Load users and pending invitations for organization
	const loadData = async () => {
		if (!organizationId) return;

		try {
			setLoading(true);
			const [orgUsers, invitations] = await Promise.all([
				organizationService.getOrganizationUsers(organizationId),
				organizationService.getPendingInvitations(organizationId),
			]);
			setUsers(orgUsers);
			setFilteredUsers(orgUsers);
			setPendingInvitations(invitations);
		} catch (err) {
			console.error('Error loading team:', err);
			toast.error(t('errorLoading'));
		} finally {
			setLoading(false);
		}
	};

	useEffect(() => {
		loadData();
	}, [organizationId]);

	// Filter users by search term
	const handleSearch = (value: string) => {
		setSearchTerm(value);
		if (!value.trim()) {
			setFilteredUsers(users);
			return;
		}
		const lower = value.toLowerCase();
		setFilteredUsers(
			users.filter(
				(u) =>
					`${u.firstName} ${u.lastName}`.toLowerCase().includes(lower) ||
					u.email.toLowerCase().includes(lower),
			),
		);
	};

	// Handle invite success
	const handleInviteSuccess = () => {
		setShowInviteModal(false);
		loadData();
		toast.success(t('inviteSent'));
	};

	// Handle resend invitation
	const handleResendInvitation = async (invitation: PendingInvitation) => {
		if (!organizationId) return;
		try {
			setResendingId(invitation.id);
			// Resend with the original invitation data
			await organizationService.inviteCollaborator(organizationId, {
				fullName: invitation.fullName,
				email: invitation.email,
				role: invitation.role,
				permissions: invitation.permissions,
				hasSigningAuthority: invitation.hasSigningAuthority,
			});
			toast.success(t('inviteSent'));
		} catch (err) {
			console.error('Error resending invitation:', err);
			toast.error(t('errorResending'));
		} finally {
			setResendingId(null);
		}
	};

	// Handle edit success
	const handleEditSuccess = () => {
		setEditingUser(null);
		loadData();
		toast.success(t('updateSuccess'));
	};

	// Handle remove confirmation
	const handleRemoveConfirm = async () => {
		if (!organizationId || !removeTargetUser) return;

		try {
			await organizationService.removeCollaborator(organizationId, removeTargetUser.id);
			toast.success(t('removeSuccess'));
			setRemoveTargetUser(null);
			loadData();
		} catch (err) {
			console.error('Error removing user:', err);
			toast.error(t('errorRemoving'));
		}
	};

	// Get permission icons for a user
	const getPermissionIcons = (user: OrganizationUser): string => {
		if (user.role === 'Admin') return '—';
		if (!user.permissions || user.permissions.length === 0) return '—';

		return user.permissions
			.map((p) => PermissionMetadata[p as PermissionCode]?.icon || '')
			.filter(Boolean)
			.join(' ');
	};

	// Check if current user can remove target user
	const canRemoveUser = (user: OrganizationUser): boolean => {
		// Cannot remove Admins for now (business rule: need to check if last admin)
		// In production, add proper validation
		return user.role !== 'Admin';
	};

	// Access denied if not admin
	if (!loading && !isAdmin) {
		return <AccessDenied />;
	}

	if (loading || !selectedOrganization) {
		return <Loading />;
	}

	return (
		<>
			<PageWrapper title={t('title')}>
				<SubHeader>
					<SubHeaderLeft>
						<Button
							color='link'
							className='p-0 me-3'
							onClick={() => navigate('/settings/organization')}>
							<Icon icon='ArrowBack' size='lg' />
						</Button>
						<span className='h4 mb-0 fw-bold'>
							{t('title')} - {selectedOrganization.name}
						</span>
					</SubHeaderLeft>
				</SubHeader>

				<Page container='fluid'>
					<Card>
						<CardHeader>
							<CardTitle>
								<Icon icon='People' className='me-2' />
								{t('Members')}
							</CardTitle>
							<CardActions>
								<div className='d-flex align-items-center gap-3'>
									{/* Search */}
									<div className='position-relative'>
										<input
											type='text'
											className='form-control'
											placeholder={t('searchPlaceholder')}
											value={searchTerm}
											onChange={(e) => handleSearch(e.target.value)}
											style={{
												borderRadius: '20px',
												paddingLeft: '40px',
												minWidth: '250px',
											}}
										/>
										<Icon
											icon='Search'
											style={{
												position: 'absolute',
												left: '12px',
												top: '50%',
												transform: 'translateY(-50%)',
												color: '#6c757d',
											}}
										/>
									</div>
									{/* Invite button */}
									<Button
										color='primary'
										icon='PersonAdd'
										onClick={() => setShowInviteModal(true)}>
										{t('inviteButton')}
									</Button>
								</div>
							</CardActions>
						</CardHeader>
						<CardBody className='p-0'>
							<div className='table-responsive'>
								<table className='table table-modern mb-0'>
									<thead>
										<tr>
											<th style={{ minWidth: '200px', paddingLeft: '20px' }}>
												{t('columns.name')}
											</th>
											<th style={{ minWidth: '200px' }}>
												{t('columns.email')}
											</th>
											<th style={{ minWidth: '120px' }}>
												{t('columns.role')}
											</th>
											<th style={{ minWidth: '150px' }}>
												{t('columns.permissions')}
											</th>
											<th style={{ minWidth: '100px' }}>
												{t('columns.signing')}
											</th>
											<th
												className='text-end'
												style={{ minWidth: '150px', paddingRight: '20px' }}>
												{t('columns.actions')}
											</th>
										</tr>
									</thead>
									<tbody>
										{filteredUsers.length > 0 ? (
											filteredUsers.map((user) => (
												<tr key={user.id}>
													<td style={{ paddingLeft: '20px' }}>
														<div className='d-flex align-items-center'>
															{user.status === 'Pending' && (
																<Icon
																	icon='Mail'
																	className='me-2 text-warning'
																	title={t('pending')}
																/>
															)}
															{`${user.firstName} ${user.lastName}`}
														</div>
													</td>
													<td>{user.email}</td>
													<td>
														<Badge
															color={
																user.role === 'Admin'
																	? 'primary'
																	: 'secondary'
															}>
															{user.role === 'Admin' ? '🔑' : '👤'}{' '}
															{t(`roles.${user.role.toLowerCase()}`)}
														</Badge>
													</td>
													<td>
														<span
															style={{ fontSize: '1.2em' }}
															title={user.permissions?.join(', ')}>
															{getPermissionIcons(user)}
														</span>
													</td>
													<td>
														{user.hasSigningAuthority ? (
															<span className='text-success'>✅</span>
														) : (
															<span className='text-muted'>❌</span>
														)}
													</td>
													<td
														className='text-end'
														style={{ paddingRight: '20px' }}>
														<Button
															color='link'
															size='sm'
															onClick={() => setEditingUser(user)}
															title={t('edit')}>
															<Icon icon='Edit' />
														</Button>
														{canRemoveUser(user) && (
															<Button
																color='link'
																size='sm'
																className='text-danger'
																onClick={() =>
																	setRemoveTargetUser(user)
																}
																title={t('remove')}>
																<Icon icon='Delete' />
															</Button>
														)}
													</td>
												</tr>
											))
										) : (
											<tr>
												<td colSpan={6} className='text-center py-4'>
													{searchTerm
														? t('noMatchingMembers')
														: t('noMembers')}
												</td>
											</tr>
										)}
									</tbody>
								</table>
							</div>

							{/* Legend */}
							<div
								className='p-3 border-top'
								style={{
									backgroundColor: darkModeStatus ? '#2d3748' : '#f8f9fa',
									borderColor: darkModeStatus ? '#4a5568' : undefined,
								}}>
								<small style={{ color: darkModeStatus ? '#a0aec0' : '#6c757d' }}>
									<strong
										style={{ color: darkModeStatus ? '#e2e8f0' : undefined }}>
										{t('legendLabel')}:
									</strong>{' '}
									{Object.values(PermissionCodes).map((code) => (
										<span key={code} className='me-3'>
											{PermissionMetadata[code].icon}{' '}
											{t(PermissionMetadata[code].labelKey)}
										</span>
									))}
									<span className='ms-3'>
										✅ {t('canSign')} | ❌ {t('cannotSign')}
									</span>
								</small>
							</div>
						</CardBody>
					</Card>

					{/* Pending Invitations Section */}
					{pendingInvitations.length > 0 && (
						<Card className='mt-4'>
							<CardHeader>
								<CardTitle>
									<Icon icon='Mail' className='me-2' />
									{t('pendingInvitations')}
									<Badge color='warning' className='ms-2'>
										{pendingInvitations.length}
									</Badge>
								</CardTitle>
							</CardHeader>
							<CardBody className='p-0'>
								<div className='table-responsive'>
									<table className='table table-modern mb-0'>
										<thead>
											<tr>
												<th
													style={{
														minWidth: '200px',
														paddingLeft: '20px',
													}}>
													{t('columns.name')}
												</th>
												<th style={{ minWidth: '200px' }}>
													{t('columns.email')}
												</th>
												<th style={{ minWidth: '150px' }}>
													{t('columns.sentAt')}
												</th>
												<th
													className='text-end'
													style={{
														minWidth: '150px',
														paddingRight: '20px',
													}}>
													{t('columns.actions')}
												</th>
											</tr>
										</thead>
										<tbody>
											{pendingInvitations.map((invitation) => (
												<tr key={invitation.id}>
													<td style={{ paddingLeft: '20px' }}>
														{invitation.fullName}
													</td>
													<td>{invitation.email}</td>
													<td>
														<Badge color='warning'>
															{new Date(
																invitation.createdAt,
															).toLocaleDateString()}
														</Badge>
													</td>
													<td
														className='text-end'
														style={{ paddingRight: '20px' }}>
														<Button
															color='info'
															size='sm'
															onClick={() =>
																handleResendInvitation(invitation)
															}
															isDisable={
																resendingId === invitation.id
															}>
															<Icon icon='Refresh' className='me-1' />
															{resendingId === invitation.id
																? t('sending')
																: t('resend')}
														</Button>
													</td>
												</tr>
											))}
										</tbody>
									</table>
								</div>
							</CardBody>
						</Card>
					)}
				</Page>
			</PageWrapper>

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
				/>
			)}

			{/* Remove Confirmation Modal */}
			<Modal
				isOpen={!!removeTargetUser}
				setIsOpen={() => setRemoveTargetUser(null)}
				isCentered>
				<ModalHeader setIsOpen={() => setRemoveTargetUser(null)}>
					<h5>{t('confirmRemoveTitle')}</h5>
				</ModalHeader>
				<ModalBody>
					<div className='text-center'>
						<Icon icon='Warning' size='5x' className='mb-4 text-danger' />
						<p className='fs-5'>
							{t('team.confirmRemoveMessage', {
								name: removeTargetUser
									? `${removeTargetUser.firstName} ${removeTargetUser.lastName}`
									: '',
							})}
						</p>
					</div>
				</ModalBody>
				<ModalFooter className='d-flex justify-content-center gap-3'>
					<Button color='light' onClick={() => setRemoveTargetUser(null)}>
						{t('common.cancel')}
					</Button>
					<Button color='danger' onClick={handleRemoveConfirm}>
						<Icon icon='Delete' className='me-2' />
						{t('remove')}
					</Button>
				</ModalFooter>
			</Modal>
		</>
	);
};

export default TeamMembersPage;
