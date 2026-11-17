/**
 * Dashboard API service: the task figures of the Dashboard page.
 */
import apiClient from './apiClient';
import type { TaskItemDto } from '../../type/task-types';

export interface DashboardCountsDto {
	/** Every task, Cancelled included. */
	total: number;
	todo: number;
	inProgress: number;
	done: number;
	cancelled: number;
}

const basePath = (orgId: string) => `/api/organizations/${orgId}/dashboard`;

/** The counts over every task of the organization. */
// US-DASH-01
const getCounts = async (orgId: string): Promise<DashboardCountsDto> => {
	const response = await apiClient.get(`${basePath(orgId)}/counts`);
	return response.data;
};

/** The five most recently created tasks, whatever their status. */
// US-DASH-02
const getRecentTasks = async (orgId: string): Promise<TaskItemDto[]> => {
	const response = await apiClient.get(`${basePath(orgId)}/recent-tasks`);
	return response.data;
};

const dashboardService = { getCounts, getRecentTasks };

export default dashboardService;
