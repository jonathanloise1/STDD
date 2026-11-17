/**
 * Task domain types.
 * Mirrors backend DTOs from MyApp.Application.DTOs.Tasks.TaskDtos.
 */

export type TaskItemStatus = 'Todo' | 'InProgress' | 'Done' | 'Cancelled';

export interface TaskItemDto {
	id: string;
	organizationId: string;
	title: string;
	description: string | null;
	status: TaskItemStatus;
	assignedToUserId: string | null;
	assignedToName: string | null;
	dueDate: string | null;
	createdAt: string;
	updatedAt: string;
}

export interface TaskItemPagedResult {
	items: TaskItemDto[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface CreateTaskRequest {
	title: string;
	description?: string | null;
	assignedToUserId?: string | null;
	dueDate?: string | null;
}

/** A field left out is kept; an empty description clears it; `clearDueDate` clears the due date. */
export interface UpdateTaskRequest {
	title?: string;
	description?: string | null;
	status?: TaskItemStatus | null;
	dueDate?: string | null;
	clearDueDate?: boolean;
}
