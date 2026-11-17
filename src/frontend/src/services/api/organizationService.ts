import apiClient from './apiClient';

// Re-export types from organization-types for backward compatibility
export type {
	OrganizationUser,
	Organization,
	CreateOrganizationRequest,
	UpdateOrganizationRequest,
	InviteCollaboratorRequest,
	UpdateOrganizationUserRequest,
	PendingInvitation,
} from '../../type/organization-types';

import type {
	OrganizationUser,
	Organization,
	CreateOrganizationRequest,
	UpdateOrganizationRequest,
	InviteCollaboratorRequest,
	UpdateOrganizationUserRequest,
	PendingInvitation,
} from '../../type/organization-types';

/** The organizations the user belongs to. */
// US-AUTH-06
const getOrganizations = async (): Promise<Organization[]> => {
	const response = await apiClient.get('/api/organizations');
	return response.data;
};

/** The profile and billing details of one organization. */
// US-ORG-01
const getOrganizationById = async (id: string): Promise<Organization> => {
	const response = await apiClient.get(`/api/organizations/${id}`);
	return response.data;
};

/** Creates an organization; the creator becomes its Admin. */
// US-ONBOARD-02
const createOrganization = async (data: CreateOrganizationRequest): Promise<Organization> => {
	const response = await apiClient.post('/api/organizations', data);
	return response.data;
};

/** Saves the name and billing details of an organization. */
// US-ORG-02
const updateOrganization = async (
	id: string,
	data: UpdateOrganizationRequest,
): Promise<Organization> => {
	const response = await apiClient.put(`/api/organizations/${id}`, data);
	return response.data;
};

/** Invites a person by email with a role. */
// US-ORG-05
const inviteCollaborator = async (
	orgId: string,
	data: InviteCollaboratorRequest,
): Promise<void> => {
	await apiClient.post(`/api/organizations/${orgId}/collaborators/invite`, data);
};

/** The people who were invited and have not signed in yet. */
// US-ORG-04
const getPendingInvitations = async (orgId: string): Promise<PendingInvitation[]> => {
	const response = await apiClient.get(`/api/organizations/${orgId}/invitations/pending`);
	return response.data;
};

/** Takes a member out of the organization. */
// US-ORG-07
const removeCollaborator = async (orgId: string, userId: string): Promise<void> => {
	await apiClient.delete(`/api/organizations/${orgId}/collaborators/${userId}`);
};

/** Changes the role of a member. */
// US-ORG-06
const updateOrganizationUser = async (
	orgId: string,
	userId: string,
	data: UpdateOrganizationUserRequest,
): Promise<void> => {
	await apiClient.patch(`/api/organizations/${orgId}/collaborators/${userId}`, data);
};

/** The members of an organization. */
// US-ORG-04
const getOrganizationUsers = async (orgId: string): Promise<OrganizationUser[]> => {
	const response = await apiClient.get(`/api/organizations/${orgId}/users`);
	return response.data;
};

/**
 * Uploads the organization logo.
 * @param orgId - Organization ID
 * @param file - Image file (max 5MB, image/*)
 * @returns Object containing the new logo URL
 */
// US-ORG-03
const uploadLogo = async (orgId: string, file: File): Promise<{ logoUrl: string }> => {
	const formData = new FormData();
	formData.append('file', file);
	const response = await apiClient.post(`/api/organizations/${orgId}/logo`, formData, {
		headers: {
			'Content-Type': 'multipart/form-data',
		},
	});
	return response.data;
};

export const organizationService = {
	getOrganizations,
	getOrganizationById,
	createOrganization,
	updateOrganization,
	inviteCollaborator,
	removeCollaborator,
	getPendingInvitations,
	updateOrganizationUser,
	getOrganizationUsers,
	uploadLogo,
};

export default organizationService;
