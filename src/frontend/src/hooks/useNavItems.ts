import { useMemo } from 'react';
import { useLocation } from 'react-router-dom';
import { usePermissions } from '../contexts/permissionContext';
import type { IconName } from '../ui';

export interface INavItem {
	id: 'dashboard' | 'tasks' | 'manual' | 'team' | 'audit' | 'unanswered' | 'settings';
	path: string;
	icon: IconName;
	/** Key in the `menu` namespace. */
	labelKey: string;
	/** Whether the page of this entry is the one that is open. */
	isActive: boolean;
}

/**
 * The entries of the menu for the role of the user in the selected organization:
 * Dashboard, Tasks and Manual for everyone; Team, Audit Log and Unanswered questions for Admins;
 * and Settings at the bottom for Admins.
 */
// US-AUTH-06, US-AUTH-07
const useNavItems = (): { mainItems: INavItem[]; bottomItems: INavItem[] } => {
	const { isAdmin } = usePermissions();
	const { pathname } = useLocation();

	return useMemo(() => {
		const startsWith = (path: string) => pathname === path || pathname.startsWith(`${path}/`);
		const main: INavItem[] = [
			{
				id: 'dashboard',
				path: '/dashboard',
				icon: 'Dashboard',
				labelKey: 'menu.dashboard',
				isActive: startsWith('/dashboard'),
			},
			{
				id: 'tasks',
				path: '/tasks',
				icon: 'TaskAlt',
				labelKey: 'menu.tasks',
				isActive: startsWith('/tasks'),
			},
			{
				id: 'manual',
				path: '/manual',
				icon: 'MenuBook',
				labelKey: 'menu.manual',
				isActive: startsWith('/manual'),
			},
		];
		const bottom: INavItem[] = [];

		if (isAdmin) {
			main.push(
				{
					id: 'team',
					path: '/settings/team',
					icon: 'Groups',
					labelKey: 'menu.team',
					isActive: startsWith('/settings/team'),
				},
				{
					id: 'audit',
					path: '/audit',
					icon: 'History',
					labelKey: 'menu.auditLog',
					isActive: startsWith('/audit'),
				},
				{
					id: 'unanswered',
					path: '/unanswered-questions',
					icon: 'LiveHelp',
					labelKey: 'menu.unansweredQuestions',
					isActive: startsWith('/unanswered-questions'),
				},
			);
			bottom.push({
				id: 'settings',
				path: '/settings/organization',
				icon: 'Settings',
				labelKey: 'Settings',
				isActive: startsWith('/settings/organization'),
			});
		}

		return { mainItems: main, bottomItems: bottom };
	}, [isAdmin, pathname]);
};

export default useNavItems;
