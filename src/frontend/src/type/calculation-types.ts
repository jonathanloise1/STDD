/**
 * Calculation engine domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Calculation.
 */

export type CalculationStatus = 'Running' | 'Completed' | 'CompletedWithWarnings' | 'Failed';

// ── Calculation Run ──────────────────────────────

export interface CalculationRunDto {
	id: string;
	status: CalculationStatus;
	startedAt: string;
	completedAt?: string | null;
	totalInputCost: number;
	totalAllocatedCost: number;
	totalIdleCost: number;
	nodeCount: number;
	ruleCount: number;
	iterationCount: number;
	errorCount: number;
	warningCount: number;
	createdAt: string;
}

export interface CalculationRunDetailDto {
	id: string;
	status: CalculationStatus;
	startedAt: string;
	completedAt?: string | null;
	totalInputCost: number;
	totalAllocatedCost: number;
	totalIdleCost: number;
	nodeCount: number;
	ruleCount: number;
	iterationCount: number;
	errorCount: number;
	warningCount: number;
	errors?: string | null;
	warnings?: string | null;
	allocationResults: AllocationResultDto[];
	nodeResults: NodeResultDto[];
}

// ── Allocation Result ────────────────────────────

export interface AllocationResultDto {
	id: string;
	sourceNodeId: string;
	sourceNodeName: string;
	destinationNodeId: string;
	destinationNodeName: string;
	amount: number;
	driverType?: string | null;
	driverValue: number;
	totalDriverValue: number;
	proportion: number;
}

// ── Node Result ──────────────────────────────────

export interface NodeResultDto {
	id: string;
	nodeId: string;
	nodeName: string;
	directCost: number;
	allocatedCostIn: number;
	allocatedCostOut: number;
	totalCost: number;
	retainedCost: number;
	capacity?: number | null;
	usedCapacity?: number | null;
	idleCapacity?: number | null;
	unitCost?: number | null;
	idleCost?: number | null;
	volume?: number | null;
	unitCostPerVolume?: number | null;
	unitPrice?: number | null;
	margin?: number | null;
	marginPercentage?: number | null;
}
