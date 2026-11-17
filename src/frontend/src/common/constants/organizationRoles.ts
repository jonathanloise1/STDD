/**
 * Organization roles, as defined by the backend
 * (MyApp.Domain.Entities.OrganizationRole).
 *
 * - Admin: full access, including organization settings, team and audit log.
 * - Editor: can create, edit and delete data (for example tasks).
 * - Viewer: read-only access.
 */
export const ORGANIZATION_ROLES = ['Admin', 'Editor', 'Viewer'] as const;

export type OrganizationRole = (typeof ORGANIZATION_ROLES)[number];

/** Role assigned by default when inviting a collaborator. */
export const DEFAULT_ORGANIZATION_ROLE: OrganizationRole = 'Viewer';
