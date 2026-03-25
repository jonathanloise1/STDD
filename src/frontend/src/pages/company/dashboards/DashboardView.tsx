import React, { useEffect, useState, useCallback, use, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Page from '../../../layout/Page/Page';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Card, { CardBody, CardHeader, CardLabel, CardTitle } from '../../../components/bootstrap/Card';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import Spinner from '../../../components/bootstrap/Spinner';
import AuthContext from '../../../contexts/authContext';
import { useApiParams } from '../../../hooks/useApiParams';
import taskService from '../../../services/api/taskService';
import type { TaskItemDto, TaskItemStatus } from '../../../type/task-types';
import type { TColor } from '../../../type/color-type';

const STATUS_COLORS: Record<TaskItemStatus, TColor> = {
	Todo: 'info',
	InProgress: 'warning',
	Done: 'success',
	Cancelled: 'danger',
};

const DashboardView = () => {
	const navigate = useNavigate();
	const { t } = useTranslation('dashboard');
	const { t: tTasks } = useTranslation('tasks');
	const { userData } = use(AuthContext);
	const { orgId, isReady } = useApiParams();
	const [tasks, setTasks] = useState<TaskItemDto[]>([]);
	const [isLoading, setIsLoading] = useState(false);

	useEffect(() => {
		const hasOnboarded = localStorage.getItem('myapp_onboarded');
		if (!hasOnboarded) {
			navigate('/onboarding', { replace: true });
		}
	}, [navigate]);

	const fetchTasks = useCallback(async () => {
		if (!orgId) return;
		setIsLoading(true);
		try {
			const result = await taskService.list(orgId, { pageSize: 10 });
			setTasks(result.items);
		} catch (err) {
			console.error('Error loading tasks:', err);
		} finally {
			setIsLoading(false);
		}
	}, [orgId]);

	useEffect(() => {
		if (isReady) fetchTasks();
	}, [isReady, fetchTasks]);

	const greeting = useMemo(() => {
		const hour = new Date().getHours();
		if (hour < 12) return t('goodMorning');
		if (hour < 18) return t('goodAfternoon');
		return t('goodEvening');
	}, [t]);

	const userName = userData?.name || '';

	const stats = useMemo(() => {
		const todo = tasks.filter((t) => t.status === 'Todo').length;
		const inProgress = tasks.filter((t) => t.status === 'InProgress').length;
		const done = tasks.filter((t) => t.status === 'Done').length;
		return { todo, inProgress, done, total: tasks.length };
	}, [tasks]);

	return (
		<PageWrapper>
			<Page>
				{/* Greeting */}
				<div className='mb-4'>
					<h3 className='fw-bold mb-1'>
						{greeting}{userName ? `, ${userName}` : ''}
					</h3>
					<p className='text-muted mb-0'>{t('taskOverview')}</p>
				</div>

				{isLoading ? (
					<div className='d-flex justify-content-center py-5'>
						<Spinner color='primary' size='3rem' />
					</div>
				) : (
					<>
						{/* KPI Cards */}
						<div className='row g-3 mb-4'>
							<div className='col-6 col-lg-3'>
								<Card className='mb-0'>
									<CardBody className='text-center'>
										<div className='fw-bold display-6 text-primary'>{stats.total}</div>
										<div className='text-muted small'>{t('totalTasks')}</div>
									</CardBody>
								</Card>
							</div>
							<div className='col-6 col-lg-3'>
								<Card className='mb-0'>
									<CardBody className='text-center'>
										<div className='fw-bold display-6 text-info'>{stats.todo}</div>
										<div className='text-muted small'>{t('todo')}</div>
									</CardBody>
								</Card>
							</div>
							<div className='col-6 col-lg-3'>
								<Card className='mb-0'>
									<CardBody className='text-center'>
										<div className='fw-bold display-6 text-warning'>{stats.inProgress}</div>
										<div className='text-muted small'>{t('inProgress')}</div>
									</CardBody>
								</Card>
							</div>
							<div className='col-6 col-lg-3'>
								<Card className='mb-0'>
									<CardBody className='text-center'>
										<div className='fw-bold display-6 text-success'>{stats.done}</div>
										<div className='text-muted small'>{t('done')}</div>
									</CardBody>
								</Card>
							</div>
						</div>

						{/* Recent Tasks */}
						<div className='row g-3'>
							<div className='col-12'>
								<Card>
									<CardHeader>
										<CardLabel icon='TaskAlt' iconColor='primary'>
											<CardTitle>{t('recentTasks')}</CardTitle>
										</CardLabel>
										<Button color='primary' isLight onClick={() => navigate('/tasks')}>
											{t('viewAll')}
										</Button>
									</CardHeader>
									<CardBody>
										{tasks.length === 0 ? (
											<div className='text-center py-4 text-muted'>
												<Icon icon='TaskAlt' size='3x' className='mb-3 opacity-50' />
												<p>{t('noTasksDashboard')}</p>
											</div>
										) : (
											<div className='table-responsive'>
												<table className='table table-modern'>
													<thead>
														<tr>
															<th>{t('taskTitle')}</th>
															<th>{t('taskStatus')}</th>
															<th>{t('taskDueDate')}</th>
														</tr>
													</thead>
													<tbody>
														{tasks.slice(0, 5).map((task) => (
															<tr key={task.id} className='cursor-pointer' onClick={() => navigate('/tasks')}>
																<td className='fw-bold'>{task.title}</td>
																<td>
																	<span className={`badge bg-${STATUS_COLORS[task.status]}`}>
																		{tTasks(`status${task.status}` as any)}
																	</span>
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
					</>
				)}
			</Page>
		</PageWrapper>
	);
};

export default DashboardView;
