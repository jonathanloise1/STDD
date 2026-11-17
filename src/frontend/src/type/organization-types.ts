import type { OrganizationRole } from '../common/constants/organizationRoles';

/**
 * Organization membership from /api/auth/sync response
 */
export interface OrganizationMembership {
	organizationId: string;
	organizationName: string;
	role: OrganizationRole;
}

/**
 * Organization user (team member)
 */
export interface OrganizationUser {
	id: string;
	userId: string;
	firstName: string;
	lastName: string;
	email: string;
	role: OrganizationRole;
	status: 'Pending' | 'Active' | 'Disabled';
}

/**
 * Request to invite a new collaborator
 */
export interface InviteCollaboratorRequest {
	fullName: string;
	email: string;
	role: OrganizationRole;
}

/**
 * Request to update an organization user
 */
export interface UpdateOrganizationUserRequest {
	role: OrganizationRole;
}

/**
 * Organization details
 */
export interface Organization {
	id: string;
	name: string;
	legalName: string;
	vatNumber: string;
	fiscalCode: string;
	billingAddress: string;
	billingCity: string;
	billingProvince: string;
	billingZipCode: string;
	billingCountryCode: string;
	billingEmail: string;
	logoUrl: string | null;
}

/**
 * Request to create an organization
 */
export interface CreateOrganizationRequest {
	name: string;
	legalName: string;
	vatNumber: string;
	fiscalCode: string;
	billingAddress: string;
	billingCity: string;
	billingProvince: string;
	billingZipCode: string;
	billingCountryCode: string;
	billingEmail: string;
}

/**
 * Request to update an organization
 */
export interface UpdateOrganizationRequest {
	name: string;
	legalName: string;
	vatNumber: string;
	fiscalCode: string;
	billingAddress: string;
	billingCity: string;
	billingProvince: string;
	billingZipCode: string;
	billingCountryCode: string;
	billingEmail: string;
}

/**
 * Pending invitation
 */
export interface PendingInvitation {
	id: string;
	fullName: string;
	email: string;
	createdAt: string;
	role: OrganizationRole;
}
