/**
 * Cost Entry domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.CostEntries.
 */

export type CostEntryType = 'Import' | 'Manual' | 'Adjustment' | 'Depreciation';

// ── DTOs ─────────────────────────────────────────

export interface CostEntryDto {
	id: string;
	accountCode?: string | null;
	description: string;
	amount: number;
	entryType: CostEntryType;
	notes?: string | null;
	sourceFileName?: string | null;
	sourceAssetId?: string | null;
	destinations: CostEntryDestinationDto[];
	createdAt: string;
}

export interface CostEntryDestinationDto {
	id: string;
	nodeId: string;
	nodeName: string;
	weight: number;
	amount: number;
}

// ── Requests ─────────────────────────────────────

export interface CostEntryDestinationRequest {
	nodeId: string;
	weight: number;
}

export interface CreateCostEntryRequest {
	accountCode?: string | null;
	description: string;
	amount: number;
	notes?: string | null;
	destinations: CostEntryDestinationRequest[];
}

export interface UpdateCostEntryRequest {
	accountCode?: string | null;
	description: string;
	amount: number;
	notes?: string | null;
	destinations: CostEntryDestinationRequest[];
}
