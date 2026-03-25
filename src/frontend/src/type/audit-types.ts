/**
 * Audit domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Audit.
 */

export interface AuditLogDto {
	id: string;
	entityType: string;
	entityId: string;
	action: string;
	userId: string;
	userDisplayName?: string | null;
	oldValues?: string | null;
	newValues?: string | null;
	changedProperties?: string | null;
	timestamp: string;
	correlationId?: string | null;
}

export interface AuditLogPagedResult {
	items: AuditLogDto[];
	totalCount: number;
	page: number;
	pageSize: number;
}
