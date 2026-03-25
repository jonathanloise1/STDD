/**
 * Import domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Import.
 */

export type ImportStatus = 'pending' | 'mapping' | 'previewing' | 'importing' | 'completed' | 'failed';

export interface ImportSessionDto {
	id: string;
	fileName: string;
	status: string;
	rowCount: number;
	importedCount: number;
	skippedCount: number;
	totalAmount: number;
	createdByUserName: string;
	createdAt: string;
	completedAt?: string | null;
}

export interface ImportSessionDetailDto {
	id: string;
	fileName: string;
	status: string;
	rowCount: number;
	importedCount: number;
	skippedCount: number;
	totalAmount: number;
	createdByUserName: string;
	createdAt: string;
	completedAt?: string | null;
	columnMapping?: Record<string, string> | null;
	previewRows: ImportPreviewRowDto[];
}

export interface ImportPreviewRowDto {
	rowNumber: number;
	values: Record<string, string>;
	isValid: boolean;
	validationErrors: string[];
}

export interface SetColumnMappingRequest {
	columnMapping: Record<string, string>;
}
