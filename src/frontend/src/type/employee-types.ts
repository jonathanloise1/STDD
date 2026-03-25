/**
 * Employee & Timesheet domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Personnel.
 */

// ── Employee ─────────────────────────────────────

export interface EmployeeDto {
	id: string;
	firstName: string;
	lastName: string;
	code?: string | null;
	role?: string | null;
	department?: string | null;
	professionalFamily?: string | null;
	isActive: boolean;
	createdAt: string;
}

export interface CreateEmployeeRequest {
	firstName: string;
	lastName: string;
	code?: string | null;
	role?: string | null;
	department?: string | null;
	professionalFamily?: string | null;
}

export interface UpdateEmployeeRequest {
	firstName: string;
	lastName: string;
	code?: string | null;
	role?: string | null;
	department?: string | null;
	professionalFamily?: string | null;
	isActive: boolean;
}

// ── Employee Cost Item ───────────────────────────

export interface EmployeeCostItemDto {
	id: string;
	employeeId: string;
	employeeName: string;
	nodeId: string;
	nodeName: string;
	label: string;
	amount: number;
	sortOrder: number;
}

export interface CreateEmployeeCostItemRequest {
	employeeId: string;
	nodeId: string;
	label: string;
	amount: number;
	sortOrder: number;
}

export interface UpdateEmployeeCostItemRequest {
	nodeId: string;
	label: string;
	amount: number;
	sortOrder: number;
}

// ── Timesheet Entry ──────────────────────────────

export type TimesheetValueType = 'percentage' | 'hours';

export interface TimesheetEntryDto {
	id: string;
	employeeNodeId: string;
	employeeNodeName: string;
	activityNodeId: string;
	activityNodeName: string;
	value: number;
	valueType: string;
}

export interface CreateTimesheetEntryRequest {
	employeeNodeId: string;
	activityNodeId: string;
	value: number;
	valueType: string;
}

export interface UpdateTimesheetEntryRequest {
	value: number;
	valueType: string;
}

// ── Timesheet Validation (US-EMP-09) ─────────────

export interface TimesheetValidationResultDto {
	isValid: boolean;
	employees: TimesheetEmployeeValidationDto[];
}

export interface TimesheetEmployeeValidationDto {
	employeeNodeId: string;
	employeeNodeName: string;
	totalPercentage: number;
	isValid: boolean;
	warning?: string | null;
}

// ── Employee Cost Breakdown (US-EMP-10) ──────────

export interface EmployeeCostBreakdownDto {
	employeeNodeId: string;
	employeeNodeName: string;
	items: EmployeeCostItemDto[];
	directCostSubtotal: number;
	fullCost?: number | null;
	allocatedCostIn?: number | null;
	allocationBreakdown?: AllocationBreakdownItemDto[] | null;
}

export interface AllocationBreakdownItemDto {
	sourceNodeId: string;
	sourceNodeName: string;
	amount: number;
}

// ── Employee Import (US-EMP-07) ──────────────────

export interface EmployeeImportResultDto {
	isPreview: boolean;
	totalRows: number;
	importedCount: number;
	skippedCount: number;
	rows: EmployeeImportRowDto[];
}

export interface EmployeeImportRowDto {
	rowNumber: number;
	firstName: string;
	lastName: string;
	code?: string | null;
	role?: string | null;
	department?: string | null;
	professionalFamily?: string | null;
	status: string;
	errorMessage?: string | null;
}
