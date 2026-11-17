import React, { useEffect, useState, useCallback, use, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
	ApiErrorMessage,
	Badge,
	Button,
	Card,
	CardBody,
	CardHeader,
	EmptyState,
	LoadingBlock,
	Page,
} from '../../../ui';
import type { Tone } from '../../../ui';
import AuthContext from '../../../contexts/authContext';
import { usePermissions } from '../../../contexts/permissionContext';
import { useApiParams } from '../../../hooks/useApiParams';
import dashboardService, { DashboardCountsDto } from '../../../services/api/dashboardService';
import { ApiProblem, toApiProblem } from '../../../services/api/apiProblem';
import type { TaskItemDto, TaskItemStatus } from '../../../type/task-types';

const STATUS_COLORS: Record<TaskItemStatus, Tone> = {
	Todo: 'info',
	InProgress: 'warning',
	Done: 'success',
	Cancelled: 'danger',
};

/** One of the four count cards. */
const CountCard = ({
	label,
	value,
	colorClass,
}: {
	label: string;
	value: number;
	colorClass: string;
}) => (
	<div className='col-6 col-lg-3'>
		<Card className='mb-0'>
			<CardBody className='text-center'>
				<div role='group' aria-label={label}>
					<div className={`fw-bold display-6 ${colorClass}`}>{value}</div>
					<div className='text-muted small'>{label}</div>
				</div>
			</CardBody>
		</Card>
	</div>
);

/** The Dashboard page: greeting, task counts and the five latest tasks. */
// US-DASH-01, US-DASH-02, US-DASH-03, US-DASH-04
const DashboardView = () => {
	const navigate = useNavigate();
	const location = useLocation();
	const { t } = useTranslation('dashboard');
	const { t: tTasks } = useTranslation('tasks');
	const { t: tCommon } = useTranslation('common');
	const { userData } = use(AuthContext);
	const { canEdit } = usePermissions();
	const { orgId, isReady } = useApiParams();

	const [counts, setCounts] = useState<DashboardCountsDto | null>(null);
	const [countsError, setCountsError] = useState<ApiProblem | null>(null);
	const [isLoadingCounts, setIsLoadingCounts] = useState(true);
	const [recent, setRecent] = useState<TaskItemDto[] | null>(null);
	const [recentError, setRecentError] = useState<ApiProblem | null>(null);
	const [isLoadingRecent, setIsLoadingRecent] = useState(true);

	// A guarded page sent the user here: read the message once and clear it from the history entry
	const [accessDenied, setAccessDenied] = useState(
		() => (location.state as { accessDenied?: boolean } | null)?.accessDenied === true,
	);
	useEffect(() => {
		if ((location.state as { accessDenied?: boolean } | null)?.accessDenied) {
			setAccessDenied(true);
			navigate(location.pathname, { replace: true, state: null });
		}
	}, [location.state, location.pathname, navigate]);

	const loadData = useCallback(async () => {
		if (!orgId) return;
		setIsLoadingCounts(true);
		setIsLoadingRecent(true);
		setCountsError(null);
		setRecentError(null);

		const loadCounts = async () => {
			try {
				setCounts(await dashboardService.getCounts(orgId));
			} catch (err) {
				setCounts(null);
				setCountsError(toApiProblem(err));
			} finally {
				setIsLoadingCounts(false);
			}
		};
		const loadRecent = async () => {
			try {
				setRecent(await dashboardService.getRecentTasks(orgId));
			} catch (err) {
				setRecent(null);
				setRecentError(toApiProblem(err));
			} finally {
				setIsLoadingRecent(false);
			}
		};
		await Promise.all([loadCounts(), loadRecent()]);
	}, [orgId]);

	useEffect(() => {
		if (isReady) loadData();
	}, [isReady, loadData]);

	// The greeting follows the time on the device of the user
	const greeting = useMemo(() => {
		const hour = new Date().getHours();
		if (hour < 12) return t('goodMorning');
		if (hour < 18) return t('goodAfternoon');
		return t('goodEvening');
	}, [t]);

	const userName = `${userData?.name || ''} ${userData?.surname || ''}`.trim();

	return (
		<Page title={t('title')}>
			{accessDenied && (
				<div
					role='alert'
					className='alert alert-warning d-flex align-items-center justify-content-between'>
					<span>{tCommon('noAccess')}</span>
					<button
						type='button'
						className='btn-close'
						aria-label={tCommon('close')}
						onClick={() => setAccessDenied(false)}
					/>
				</div>
			)}

			{/* Greeting */}
			<div className='mb-4'>
				<h1 className='h3 fw-bold mb-1'>
					{greeting}
					{userName ? `, ${userName}` : ''}
				</h1>
				<p className='text-muted mb-0'>{t('taskOverview')}</p>
			</div>

			{/* Task counts */}
			<div className='mb-4'>
				{isLoadingCounts ? (
					<LoadingBlock />
				) : countsError ? (
					<ApiErrorMessage problem={countsError} />
				) : (
					counts && (
						<div className='row g-3'>
							<CountCard
								label={t('totalTasks')}
								value={counts.total}
								colorClass='text-primary'
							/>
							<CountCard
								label={t('todo')}
								value={counts.todo}
								colorClass='text-info'
							/>
							<CountCard
								label={t('inProgress')}
								value={counts.inProgress}
								colorClass='text-warning'
							/>
							<CountCard
								label={t('done')}
								value={counts.done}
								colorClass='text-success'
							/>
						</div>
					)
				)}
			</div>

			{/* Recent Tasks */}
			<div className='row g-3'>
				<div className='col-12'>
					<Card>
						<CardHeader
							icon='TaskAlt'
							title={t('recentTasks')}
							actions={
								<Button variant='subtle-primary' onClick={() => navigate('/tasks')}>
									{t('viewAll')}
								</Button>
							}
						/>
						<CardBody>
							{isLoadingRecent ? (
								<LoadingBlock />
							) : recentError ? (
								<ApiErrorMessage problem={recentError} />
							) : recent && recent.length === 0 ? (
								<EmptyState
									icon='TaskAlt'
									title={canEdit ? t('noTasksDashboard') : tTasks('noTasks')}
								/>
							) : (
								<div className='table-responsive'>
									<table className='table align-middle'>
										<thead>
											<tr>
												<th>{t('taskTitle')}</th>
												<th>{t('taskStatus')}</th>
												<th>{t('taskDueDate')}</th>
											</tr>
										</thead>
										<tbody>
											{(recent ?? []).slice(0, 5).map((task) => (
												<tr
													key={task.id}
													className='app-clickable'
													onClick={() => navigate('/tasks')}>
													<td className='fw-bold'>{task.title}</td>
													<td>
														<Badge tone={STATUS_COLORS[task.status]}>
															{tTasks(`status${task.status}`)}
														</Badge>
													</td>
													<td>{task.dueDate || '-'}</td>
												</tr>
											))}
										</tbody>
									</table>
								</div>
							)}
						</CardBody>
					</Card>
				</div>
			</div>
		</Page>
	);
};

export default DashboardView;
