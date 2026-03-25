/**
 * Asset & Depreciation domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Assets.
 */

// ── Asset ────────────────────────────────────────

export type DepreciationMethod = 'straight-line' | 'reducing-balance';

export interface AssetDto {
	id: string;
	code?: string | null;
	name: string;
	description?: string | null;
	category?: string | null;
	purchaseDate: string;
	purchaseValue: number;
	usefulLifeYears: number;
	residualValue: number;
	depreciationMethod: string;
	annualDepreciation: number;
	destinationNodeTypeId?: string | null;
	destinationNodeTypeName?: string | null;
	isActive: boolean;
	createdAt: string;
}

export interface CreateAssetRequest {
	code?: string | null;
	name: string;
	description?: string | null;
	category?: string | null;
	purchaseDate: string;
	purchaseValue: number;
	usefulLifeYears: number;
	residualValue: number;
	depreciationMethod: string;
	destinationNodeTypeId?: string | null;
}

export interface UpdateAssetRequest {
	code?: string | null;
	name: string;
	description?: string | null;
	category?: string | null;
	purchaseDate: string;
	purchaseValue: number;
	usefulLifeYears: number;
	residualValue: number;
	depreciationMethod: string;
	destinationNodeTypeId?: string | null;
	isActive: boolean;
}

// ── Asset Depreciation ───────────────────────────

export interface AssetDepreciationDto {
	id: string;
	assetId: string;
	assetName: string;
	destinationNodeId: string;
	destinationNodeName: string;
	annualAmount: number;
	yearNumber: number;
	isGenerated: boolean;
	costEntryId?: string | null;
}
