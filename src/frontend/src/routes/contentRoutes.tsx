import React, { lazy } from 'react';
import { RouteProps } from 'react-router-dom';
import AdminProtectedRoute from '../components/auth/AdminProtectedRoute';

// Company page imports
const COMPANY = {
	DASHBOARD: lazy(() => import('../pages/company/dashboards/DashboardView')),
	// Setup page shown while the user belongs to no organization
	ONBOARDING: lazy(() => import('../pages/company/onboarding/OnboardingWizardPage')),
	// Audit Log (Admin only)
	AUDIT_LOG: lazy(() => import('../pages/company/audit/AuditLogPage')),
};

// Help page imports: the manual (every role) and the unanswered questions (Admin only)
const HELP = {
	MANUAL: lazy(() => import('../pages/company/help/ManualPage')),
	UNANSWERED: lazy(() => import('../pages/company/help/UnansweredQuestionsPage')),
};

// Settings page imports (Admin only)
const COMPANY_SETTINGS = {
	ORGANIZATION_PROFILE: lazy(
		() => import('../pages/company/settings/organization/OrganizationProfilePage'),
	),
	ORGANIZATION_EDIT: lazy(() => import('../pages/company/organizations/UpdateOrganization')),
	TEAM_MEMBERS: lazy(() => import('../pages/company/team/TeamMembersPage')),
};

// Tasks page imports
const TASKS = {
	LIST: lazy(() => import('../pages/company/tasks/TaskListPage')),
};

// Path literals stay plain strings: the manual check reads them (scripts/lib/sourcescan.ts).
const contents: RouteProps[] = [
	{ path: 'dashboard', element: <COMPANY.DASHBOARD /> },

	// Setup page: the shell sends users without an organization here
	{ path: 'onboarding', element: <COMPANY.ONBOARDING /> },

	// Audit Log (Admin only)
	{
		path: 'audit',
		element: (
			<AdminProtectedRoute>
				<COMPANY.AUDIT_LOG />
			</AdminProtectedRoute>
		),
	},

	// Manual: the home page, the search results, an area and a page of an area
	{ path: 'manual', element: <HELP.MANUAL /> },
	{ path: 'manual/search', element: <HELP.MANUAL /> },
	{ path: 'manual/:area', element: <HELP.MANUAL /> },
	{ path: 'manual/:area/:story', element: <HELP.MANUAL /> },

	// Unanswered questions (Admin only)
	{
		path: 'unanswered-questions',
		element: (
			<AdminProtectedRoute>
				<HELP.UNANSWERED />
			</AdminProtectedRoute>
		),
	},

	// Organization, Update Organization and Team (Admin only)
	{
		path: 'settings/organization',
		element: (
			<AdminProtectedRoute>
				<COMPANY_SETTINGS.ORGANIZATION_PROFILE />
			</AdminProtectedRoute>
		),
	},
	{
		path: 'settings/organization/edit',
		element: (
			<AdminProtectedRoute>
				<COMPANY_SETTINGS.ORGANIZATION_EDIT />
			</AdminProtectedRoute>
		),
	},
	{
		path: 'settings/team',
		element: (
			<AdminProtectedRoute>
				<COMPANY_SETTINGS.TEAM_MEMBERS />
			</AdminProtectedRoute>
		),
	},

	// Tasks
	{ path: 'tasks', element: <TASKS.LIST /> },
];

export default contents;
