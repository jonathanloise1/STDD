/**
 * Mapping between menu item IDs and required permissions.
 * Items not listed here are visible to all users.
 * Admin users bypass all permission checks.
 *
 * @see docs/areas/organization/userstories.md
 *
 * @userstory US-MENU-01, US-MENU-02, US-MENU-05, US-MENU-06
 */
import { PermissionCodes, PermissionCode } from './permissionCodes';

export interface MenuPermissionConfig {
	/** Required permissions (user needs at least one) */
	permissions?: PermissionCode[];
	/** If true, only Admin users can see this item */
	adminOnly?: boolean;
}

/**
 * Menu ID to permission mapping
 */
export const MenuPermissionMapping: Record<string, MenuPermissionConfig> = {
	// Dashboard - always visible
	'company-dashboard': {},

	// Organizations - always visible (but moving to settings)
	organizations: {},

	// ========== SETTINGS AREA (Admin only) ==========
	// US-MENU-05: All settings items are flat direct links, admin-only
	// Main settings entry point
	'company-settings': { adminOnly: true },

	// Organization settings
	'company-settings-organization': { adminOnly: true },

	// Team management
	'company-settings-team': { adminOnly: true },
};

/**
 * Check if a menu item should be visible based on permissions
 * @param menuId The menu item ID
 * @param isAdmin Whether the user is an Admin
 * @param userPermissions The user's permissions array
 * @returns Whether the menu item should be visible
 */
export const canShowMenuItem = (
	menuId: string,
	isAdmin: boolean,
	userPermissions: PermissionCode[],
): boolean => {
	// Admin sees everything
	if (isAdmin) return true;

	const config = MenuPermissionMapping[menuId];

	// No config means always visible
	if (!config) return true;

	// Admin-only items hidden for non-admins
	if (config.adminOnly) return false;

	// No permission requirement means visible
	if (!config.permissions || config.permissions.length === 0) return true;

	// Check if user has at least one required permission
	return config.permissions.some((p) => userPermissions.includes(p));
};
