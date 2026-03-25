/**
 * Cost Allocation Rule domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.AllocationRules.
 */

export type DriverType = 'sqm' | 'hours' | 'percentage' | 'pieces' | 'fte' | 'value' | 'custom';

// ── DTOs ─────────────────────────────────────────

export interface CostAllocationRuleDto {
	id: string;
	sourceNodeId: string;
	sourceNodeName: string;
	destinationNodeId: string;
	destinationNodeName: string;
	driverType: string;
	driverValue: number;
	driverLabel?: string | null;
	priority?: number | null;
	notes?: string | null;
	createdAt: string;
}

// ── Requests ─────────────────────────────────────

export interface CreateCostAllocationRuleRequest {
	sourceNodeId: string;
	destinationNodeId: string;
	driverType: string;
	driverValue: number;
	driverLabel?: string | null;
	priority?: number | null;
	notes?: string | null;
}

export interface UpdateCostAllocationRuleRequest {
	driverType: string;
	driverValue: number;
	driverLabel?: string | null;
	priority?: number | null;
	notes?: string | null;
}
