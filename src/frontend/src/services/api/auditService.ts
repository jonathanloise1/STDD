/**
 * Audit Log API service.
 * Fetches audit trail data for compliance and debugging.
 */
import apiClient from './apiClient';
import type { AuditLogPagedResult } from '../../type/audit-types';

export type { AuditLogDto, AuditLogPagedResult } from '../../type/audit-types';

const basePath = (orgId: string) => `/api/organizations/${orgId}/audit-logs`;

export interface AuditLogQueryParams {
	/** Start of the period (ISO instant). */
	from?: string;
	/** End of the period (ISO instant). */
	to?: string;
	/** Stored type of the entity: TaskItem, OrganizationUser, Organization or User. */
	entityType?: string;
	page?: number;
	pageSize?: number;
}

/** One page of the changes of the organization, newest first. */
// US-AUD-01, US-AUD-02
const list = async (orgId: string, params?: AuditLogQueryParams): Promise<AuditLogPagedResult> => {
	const response = await apiClient.get(basePath(orgId), { params });
	return response.data;
};

const auditService = {
	list,
};

export default auditService;
