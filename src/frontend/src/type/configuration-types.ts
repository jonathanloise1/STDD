/**
 * Configuration domain types — FiscalYear, Scenario, NodeType.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Configuration.
 */

// ── Fiscal Year ──────────────────────────────────

export type FiscalYearStatus = 'Draft' | 'Active' | 'Closed';

export interface FiscalYearDto {
	id: string;
	name: string;
	startDate: string;
	endDate: string;
	status: FiscalYearStatus;
	scenarioCount: number;
	createdAt: string;
}

export interface CreateFiscalYearRequest {
	name: string;
	startDate: string;
	endDate: string;
}

export interface UpdateFiscalYearRequest {
	name: string;
	startDate: string;
	endDate: string;
	status: FiscalYearStatus;
}

// ── Scenario ─────────────────────────────────────

export type ScenarioStatus = 'Draft' | 'Published' | 'Archived';

export interface ScenarioDto {
	id: string;
	name: string;
	description?: string | null;
	isBaseline: boolean;
	status: ScenarioStatus;
	createdAt: string;
}

export interface CreateScenarioRequest {
	name: string;
	description?: string | null;
	isBaseline: boolean;
}

export interface DuplicateScenarioRequest {
	name?: string | null;
}

// ── NodeType ─────────────────────────────────────

export interface NodeTypeDto {
	id: string;
	name: string;
	description?: string | null;
	color?: string | null;
	icon?: string | null;
	sortOrder: number;
	uiSchema?: unknown | null;
	createdAt: string;
}

export interface CreateNodeTypeRequest {
	name: string;
	description?: string | null;
	color?: string | null;
	icon?: string | null;
	sortOrder: number;
	uiSchema?: unknown | null;
}
