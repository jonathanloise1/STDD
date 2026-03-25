/** @userstory US-ORG-01, US-ORG-02, US-ORG-03, US-ORG-04, US-ORG-05, US-ORG-07, US-ORG-08, US-ORG-09, US-ORG-10, US-ORG-11, US-ORG-12, US-ORG-13, US-TEAM-01, US-TEAM-02, US-TEAM-05, US-TEAM-06, US-TEAM-07, US-TEAM-11 */
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

/** @userstory US-ORG-01 */
const getOrganizations = async (): Promise<Organization[]> => {
	try {
		const response = await apiClient.get('/api/organizations');
		return response.data;
	} catch (error) {
		console.error('Error fetching organizations:', error);
		throw error;
	}
};

/** @userstory US-ORG-03 */
const getOrganizationById = async (id: string): Promise<Organization> => {
	try {
		const response = await apiClient.get(`/api/organizations/${id}`);
		return response.data;
	} catch (error) {
		console.error(`Error fetching organization with ID ${id}:`, error);
		throw error;
	}
};

/** @userstory US-ORG-02 */
const createOrganization = async (data: CreateOrganizationRequest): Promise<Organization> => {
	try {
		const response = await apiClient.post('/api/organizations', data);
		return response.data;
	} catch (error) {
		console.error('Error creating organization:', error);
		throw error;
	}
};

/** @userstory US-ORG-04 */
const updateOrganization = async (
	id: string,
	data: UpdateOrganizationRequest,
): Promise<Organization> => {
	try {
		const response = await apiClient.put(`/api/organizations/${id}`, data);
		return response.data;
	} catch (error) {
		console.error(`Error updating organization with ID ${id}:`, error);
		throw error;
	}
};

/** @userstory US-ORG-08 */
const inviteCollaborator = async (
	orgId: string,
	data: InviteCollaboratorRequest,
): Promise<void> => {
	await apiClient.post(`/api/organizations/${orgId}/collaborators/invite`, data);
};

/** @userstory US-ORG-09 */
const getPendingInvitations = async (orgId: string): Promise<PendingInvitation[]> => {
	const response = await apiClient.get(`/api/organizations/${orgId}/invitations/pending`);
	return response.data;
};

/** @userstory US-ORG-12 */
const removeCollaborator = async (orgId: string, userId: string): Promise<void> => {
	await apiClient.delete(`/api/organizations/${orgId}/collaborators/${userId}`);
};

/**
 * Update an organization user's role and permissions
 * @userstory US-ORG-11, US-ORG-13
 */
const updateOrganizationUser = async (
	orgId: string,
	userId: string,
	data: UpdateOrganizationUserRequest,
): Promise<void> => {
	await apiClient.patch(`/api/organizations/${orgId}/collaborators/${userId}`, data);
};

/**
 * Resend invitation to a pending user
 * @userstory US-ORG-09
 */
const resendInvitation = async (orgId: string, userId: string): Promise<void> => {
	await apiClient.post(`/api/organizations/${orgId}/users/${userId}/resend-invite`);
};

/**
 * Get all users (team members) for an organization
 * @userstory US-ORG-07
 */
const getOrganizationUsers = async (orgId: string): Promise<OrganizationUser[]> => {
	const response = await apiClient.get(`/api/organizations/${orgId}/users`);
	return response.data;
};

/**
 * Upload organization logo
 * @param orgId - Organization ID
 * @param file - Image file (max 5MB, image/*)
 * @returns Object containing the new logo URL
 * @userstory US-ORG-05
 */
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
	resendInvitation,
	getOrganizationUsers,
	uploadLogo,
};

export default organizationService;
