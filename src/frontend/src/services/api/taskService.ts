/**
 * Task API service.
 * CRUD operations for tasks within an organization.
 */
import apiClient from './apiClient';
import type {
	TaskItemDto,
	TaskItemPagedResult,
	CreateTaskRequest,
	UpdateTaskRequest,
} from '../../type/task-types';

const basePath = (orgId: string) => `/api/organizations/${orgId}/tasks`;

export interface TaskQueryParams {
	status?: string;
	assignedToUserId?: string;
	page?: number;
	pageSize?: number;
}

const list = async (orgId: string, params?: TaskQueryParams): Promise<TaskItemPagedResult> => {
	const response = await apiClient.get(basePath(orgId), { params });
	return response.data;
};

const getById = async (orgId: string, taskId: string): Promise<TaskItemDto> => {
	const response = await apiClient.get(`${basePath(orgId)}/${taskId}`);
	return response.data;
};

const create = async (orgId: string, data: CreateTaskRequest): Promise<TaskItemDto> => {
	const response = await apiClient.post(basePath(orgId), data);
	return response.data;
};

const update = async (
	orgId: string,
	taskId: string,
	data: UpdateTaskRequest,
): Promise<TaskItemDto> => {
	const response = await apiClient.put(`${basePath(orgId)}/${taskId}`, data);
	return response.data;
};

const remove = async (orgId: string, taskId: string): Promise<void> => {
	await apiClient.delete(`${basePath(orgId)}/${taskId}`);
};

const taskService = { list, getById, create, update, remove };

export default taskService;
