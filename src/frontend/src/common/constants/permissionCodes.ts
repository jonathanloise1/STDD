/**
 * Permission codes for the roles & permissions system.
 * These codes must match the backend OrganizationPermissions constants.
 *
 * @see docs/areas/organization/userstories.md
 *
 * @userstory US-MENU-02, US-MENU-03, US-TEAM-03, US-TEAM-08
 */
export const PermissionCodes: Record<string, string> = {
	// Domain-specific permission codes will be added here
};

export type PermissionCode = string;

/**
 * Organization roles
 */
export type OrganizationRole = 'Admin' | 'Member';

/**
 * Permission metadata for UI display
 */
export interface PermissionMeta {
	label: string;
	labelKey: string;
	description: string;
	descriptionKey: string;
	icon: string;
}

/**
 * Permission metadata lookup
 */
export const PermissionMetadata: Record<string, PermissionMeta> = {
};

/**
 * Get all permission codes as an array
 */
export const getAllPermissionCodes = (): PermissionCode[] => {
	return Object.values(PermissionCodes);
};

/**
 * Check if a string is a valid permission code
 */
export const isValidPermissionCode = (code: string): code is PermissionCode => {
	return Object.values(PermissionCodes).includes(code as PermissionCode);
};
