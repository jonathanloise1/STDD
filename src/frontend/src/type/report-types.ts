/**
 * Report domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Reports.
 */

// ── Cost Summary ─────────────────────────────────

export interface CostSummaryReportDto {
	scenarioId: string;
	scenarioName: string;
	totalCost: number;
	nodes: NodeCostSummaryDto[];
}

export interface NodeCostSummaryDto {
	nodeId: string;
	nodeName: string;
	nodeTypeName: string;
	directCost: number;
	allocatedCostIn: number;
	allocatedCostOut: number;
	totalCost: number;
	retainedCost: number;
	unitCost?: number | null;
	idleCost?: number | null;
}

// ── Allocation Flow ──────────────────────────────

export interface AllocationFlowReportDto {
	nodeId: string;
	nodeName: string;
	totalCost: number;
	inboundAllocations: AllocationFlowItemDto[];
	outboundAllocations: AllocationFlowItemDto[];
}

export interface AllocationFlowItemDto {
	counterpartNodeId: string;
	counterpartNodeName: string;
	amount: number;
	proportion: number;
	driverType?: string | null;
}

// ── Idle Capacity ────────────────────────────────

export interface IdleCapacityReportDto {
	scenarioId: string;
	scenarioName: string;
	totalIdleCost: number;
	nodes: NodeIdleCapacityDto[];
}

export interface NodeIdleCapacityDto {
	nodeId: string;
	nodeName: string;
	capacity?: number | null;
	usedCapacity?: number | null;
	idleCapacity?: number | null;
	utilizationPercentage?: number | null;
	unitCost?: number | null;
	idleCost?: number | null;
}

// ── Scenario Comparison ──────────────────────────

export interface ScenarioComparisonReportDto {
	baselineScenarioId: string;
	baselineScenarioName: string;
	compareScenarioId: string;
	compareScenarioName: string;
	nodes: NodeComparisonDto[];
}

export interface NodeComparisonDto {
	nodeId: string;
	nodeName: string;
	baselineTotalCost: number;
	compareTotalCost: number;
	deltaCost: number;
	deltaPercentage: number;
	baselineUnitCost?: number | null;
	compareUnitCost?: number | null;
}
