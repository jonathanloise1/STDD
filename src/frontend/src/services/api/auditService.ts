/**
 * Audit Log API service.
 * Fetches audit trail data for compliance and debugging.
 */
import apiClient from './apiClient';
import type { AuditLogDto, AuditLogPagedResult } from '../../type/audit-types';

export type { AuditLogDto, AuditLogPagedResult } from '../../type/audit-types';

const basePath = (orgId: string) => `/api/organizations/${orgId}/audit-logs`;

export interface AuditLogQueryParams {
	from?: string;
	to?: string;
	entityType?: string;
	userId?: string;
	page?: number;
	pageSize?: number;
}

const list = async (
	orgId: string,
	params?: AuditLogQueryParams,
): Promise<AuditLogPagedResult> => {
	const response = await apiClient.get(basePath(orgId), { params });
	return response.data;
};

const getByEntity = async (
	orgId: string,
	entityType: string,
	entityId: string,
): Promise<AuditLogDto[]> => {
	const response = await apiClient.get(
		`${basePath(orgId)}/entity/${entityType}/${entityId}`,
	);
	return response.data;
};

const getByCorrelation = async (
	orgId: string,
	correlationId: string,
): Promise<AuditLogDto[]> => {
	const response = await apiClient.get(
		`${basePath(orgId)}/correlation/${correlationId}`,
	);
	return response.data;
};

const auditService = {
	list,
	getByEntity,
	getByCorrelation,
};

export default auditService;
