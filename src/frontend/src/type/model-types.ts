/** @userstory US-NODE-01, US-NODE-02, US-NODE-03, US-NODE-04, US-NODE-05, US-NODE-06 */

// Re-export configuration types for backward compatibility
export type { FiscalYearDto, ScenarioDto, NodeTypeDto } from './configuration-types';

// ── NodeType (summary for Node display) ──────────

export interface NodeTypeInfo {
	id: string;
	name: string;
	color?: string;
	icon?: string;
	uiSchema?: string;
}

// ── Node ─────────────────────────────────────────

export interface NodeDto {
	id: string;
	name: string;
	code: string;
	description?: string;
	nodeTypeId: string;
	nodeTypeName: string;
	nodeTypeColor?: string;
	isIntermediate?: boolean;
	capacity?: number;
	unitOfMeasure?: string;
	volume?: number;
	unitPrice?: number;
	extraProperties?: string;
	createdAt: string;
}

export interface NodeDetailDto {
	id: string;
	name: string;
	code: string;
	description?: string;
	isIntermediate?: boolean;
	capacity?: number;
	unitOfMeasure?: string;
	volume?: number;
	unitPrice?: number;
	extraProperties?: string;
	nodeType: NodeTypeInfo;
	costSummary?: {
		directCosts: number;
		allocatedCosts: number;
		totalCost: number;
		hasCalculationData: boolean;
	};
	createdAt: string;
	updatedAt: string;
}

export interface CreateNodeRequest {
	name: string;
	code: string;
	nodeTypeId: string;
	description?: string;
	extraProperties?: string;
	capacity?: number;
	unitOfMeasure?: string;
	volume?: number;
	unitPrice?: number;
}

export interface UpdateNodeRequest {
	name: string;
	code: string;
	nodeTypeId: string;
	description?: string;
	extraProperties?: string;
	capacity?: number;
	unitOfMeasure?: string;
	volume?: number;
	unitPrice?: number;
}

export interface DuplicateNodeRequest {
	name?: string;
	code?: string;
}
