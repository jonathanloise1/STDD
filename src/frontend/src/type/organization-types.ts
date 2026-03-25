import { PermissionCode, OrganizationRole } from '../common/constants/permissionCodes';

/**
 * Organization membership from /api/auth/sync response
 * @userstory US-ORG-01
 */
export interface OrganizationMembership {
	organizationId: string;
	organizationName: string;
	role: OrganizationRole;
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
}

/**
 * Organization user (team member)
 */
export interface OrganizationUser {
	id: string;
	userId: string;
	firstName: string;
	lastName: string;
	fullName: string;
	email: string;
	role: OrganizationRole;
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
	status: 'Pending' | 'Active' | 'Disabled';
	// Legacy field - deprecated, use hasSigningAuthority instead
	isLegalRepresentative?: boolean;
}

/**
 * Request to invite a new collaborator
 */
export interface InviteCollaboratorRequest {
	fullName: string;
	email: string;
	role: OrganizationRole;
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
}

/**
 * Request to update an organization user
 */
export interface UpdateOrganizationUserRequest {
	fullName?: string;
	role: OrganizationRole;
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
}

/**
 * Organization details
 * @userstory US-ORG-01, US-ORG-02, US-ORG-03, US-ORG-04
 */
export interface Organization {
	id: string;
	name: string;
	legalName: string;
	vatNumber: string;
	fiscalCode: string;
	italianSdiCode: string;
	billingAddress: string;
	billingCity: string;
	billingProvince: string;
	billingZipCode: string;
	billingCountryCode: string;
	billingEmail: string;
	certifiedEmail: string;
	logoUrl: string | null;
	users: OrganizationUser[];
}

/**
 * Request to create an organization
 * @userstory US-ORG-02
 * @userstory US-ONBOARD-CO-03
 * @userstory US-CFG-14
 * @task TASK-OBC-04
 */
export interface CreateOrganizationRequest {
	name: string;
	legalName: string;
	vatNumber: string;
	fiscalCode: string;
	italianSdiCode: string;
	billingAddress: string;
	billingCity: string;
	billingProvince: string;
	billingZipCode: string;
	billingCountryCode: string;
	billingEmail: string;
	certifiedEmail: string;
	/** Phone number collected during onboarding wizard (optional). */
	phoneNumber?: string;
}

/**
 * Request to update an organization
 * @userstory US-ORG-04
 */
export interface UpdateOrganizationRequest {
	name: string;
	legalName: string;
	vatNumber: string;
	fiscalCode: string;
	italianSdiCode: string;
	billingAddress: string;
	billingCity: string;
	billingProvince: string;
	billingZipCode: string;
	billingCountryCode: string;
	billingEmail: string;
	certifiedEmail: string;
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
	permissions: PermissionCode[];
	hasSigningAuthority: boolean;
}
