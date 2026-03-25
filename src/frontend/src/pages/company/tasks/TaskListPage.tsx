import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Page from '../../../layout/Page/Page';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Card, { CardBody, CardHeader, CardLabel, CardTitle } from '../../../components/bootstrap/Card';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import Spinner from '../../../components/bootstrap/Spinner';
import { useApiParams } from '../../../hooks/useApiParams';
import taskService from '../../../services/api/taskService';
import type { TaskItemDto, TaskItemStatus, CreateTaskRequest, UpdateTaskRequest } from '../../../type/task-types';

import type { TColor } from '../../../type/color-type';

const STATUS_COLORS: Record<TaskItemStatus, TColor> = {
	Todo: 'info',
	InProgress: 'warning',
	Done: 'success',
	Cancelled: 'danger',
};

const STATUS_ICONS: Record<TaskItemStatus, string> = {
	Todo: 'RadioButtonUnchecked',
	InProgress: 'Pending',
	Done: 'CheckCircle',
	Cancelled: 'Cancel',
};

const TaskListPage = () => {
	const { t } = useTranslation('tasks');
	const { orgId, isReady } = useApiParams();
	const [tasks, setTasks] = useState<TaskItemDto[]>([]);
	const [isLoading, setIsLoading] = useState(false);
	const [showForm, setShowForm] = useState(false);
	const [editingTask, setEditingTask] = useState<TaskItemDto | null>(null);
	const [formTitle, setFormTitle] = useState('');
	const [formDescription, setFormDescription] = useState('');
	const [formDueDate, setFormDueDate] = useState('');
	const [formStatus, setFormStatus] = useState<TaskItemStatus>('Todo');

	const fetchTasks = useCallback(async () => {
		if (!orgId) return;
		setIsLoading(true);
		try {
			const result = await taskService.list(orgId, { pageSize: 50 });
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

	const resetForm = () => {
		setFormTitle('');
		setFormDescription('');
		setFormDueDate('');
		setFormStatus('Todo');
		setEditingTask(null);
		setShowForm(false);
	};

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		if (!orgId || !formTitle.trim()) return;

		try {
			if (editingTask) {
				const data: UpdateTaskRequest = {
					title: formTitle,
					description: formDescription || null,
					status: formStatus,
					dueDate: formDueDate || null,
				};
				await taskService.update(orgId, editingTask.id, data);
			} else {
				const data: CreateTaskRequest = {
					title: formTitle,
					description: formDescription || null,
					dueDate: formDueDate || null,
				};
				await taskService.create(orgId, data);
			}
			resetForm();
			fetchTasks();
		} catch (err) {
			console.error('Error saving task:', err);
		}
	};

	const handleEdit = (task: TaskItemDto) => {
		setEditingTask(task);
		setFormTitle(task.title);
		setFormDescription(task.description || '');
		setFormDueDate(task.dueDate || '');
		setFormStatus(task.status);
		setShowForm(true);
	};

	const handleDelete = async (taskId: string) => {
		if (!orgId) return;
		try {
			await taskService.remove(orgId, taskId);
			fetchTasks();
		} catch (err) {
			console.error('Error deleting task:', err);
		}
	};

	const handleStatusToggle = async (task: TaskItemDto) => {
		if (!orgId) return;
		const nextStatus: Record<TaskItemStatus, TaskItemStatus> = {
			Todo: 'InProgress',
			InProgress: 'Done',
			Done: 'Todo',
			Cancelled: 'Todo',
		};
		try {
			await taskService.update(orgId, task.id, {
				title: task.title,
				status: nextStatus[task.status],
			});
			fetchTasks();
		} catch (err) {
			console.error('Error updating task status:', err);
		}
	};

	return (
		<PageWrapper>
			<Page>
				<div className='row'>
					<div className='col-12'>
						<Card>
							<CardHeader>
								<CardLabel icon='TaskAlt' iconColor='primary'>
									<CardTitle>{t('title')}</CardTitle>
								</CardLabel>
								<Button
									color='primary'
									icon='Add'
									onClick={() => {
										resetForm();
										setShowForm(true);
									}}>
									{t('newTask')}
								</Button>
							</CardHeader>
							<CardBody>
								{/* Inline Create/Edit Form */}
								{showForm && (
									<form onSubmit={handleSubmit} className='mb-4 p-3 border rounded bg-l10-primary'>
										<div className='row g-3'>
											<div className='col-md-6'>
												<label className='form-label fw-bold'>{t('taskTitle')}</label>
												<input
													type='text'
													className='form-control'
													value={formTitle}
													onChange={(e) => setFormTitle(e.target.value)}
													required
													autoFocus
												/>
											</div>
											<div className='col-md-3'>
												<label className='form-label fw-bold'>{t('taskDueDate')}</label>
												<input
													type='date'
													className='form-control'
													value={formDueDate}
													onChange={(e) => setFormDueDate(e.target.value)}
												/>
											</div>
											{editingTask && (
												<div className='col-md-3'>
													<label className='form-label fw-bold'>{t('taskStatus')}</label>
													<select
														className='form-select'
														value={formStatus}
														onChange={(e) => setFormStatus(e.target.value as TaskItemStatus)}>
														<option value='Todo'>{t('statusTodo')}</option>
														<option value='InProgress'>{t('statusInProgress')}</option>
														<option value='Done'>{t('statusDone')}</option>
														<option value='Cancelled'>{t('statusCancelled')}</option>
													</select>
												</div>
											)}
											<div className='col-12'>
												<label className='form-label fw-bold'>{t('taskDescription')}</label>
												<textarea
													className='form-control'
													rows={2}
													value={formDescription}
													onChange={(e) => setFormDescription(e.target.value)}
												/>
											</div>
											<div className='col-12 d-flex gap-2'>
												<Button type='submit' color='primary' icon='Save'>
													{editingTask ? t('update') : t('createTask')}
												</Button>
												<Button type='button' color='light' onClick={resetForm}>
													{t('cancel')}
												</Button>
											</div>
										</div>
									</form>
								)}

								{/* Task List */}
								{isLoading ? (
									<div className='d-flex justify-content-center py-5'>
										<Spinner color='primary' size='3rem' />
									</div>
								) : tasks.length === 0 ? (
									<div className='text-center py-5 text-muted'>
										<Icon icon='TaskAlt' size='3x' className='mb-3 opacity-50' />
										<p>{t('noTasks')}. {t('noTasksDescription')}</p>
									</div>
								) : (
									<div className='table-responsive'>
										<table className='table table-modern'>
											<thead>
												<tr>
													<th style={{ width: 50 }}></th>
													<th>{t('taskTitle')}</th>
													<th>{t('taskStatus')}</th>
													<th>{t('taskDueDate')}</th>
													<th>{t('assignedTo')}</th>
													<th style={{ width: 120 }}>{t('actions')}</th>
												</tr>
											</thead>
											<tbody>
												{tasks.map((task) => (
													<tr key={task.id}>
														<td>
															<Button
																isLight
																color={STATUS_COLORS[task.status]}
																icon={STATUS_ICONS[task.status]}
																onClick={() => handleStatusToggle(task)}
																className='p-0'
															/>
														</td>
														<td>
															<div className='fw-bold'>{task.title}</div>
															{task.description && (
																<div className='small text-muted text-truncate' style={{ maxWidth: 400 }}>
																	{task.description}
																</div>
															)}
														</td>
														<td>
															<span className={`badge bg-${STATUS_COLORS[task.status]}`}>
																{t(`status${task.status}` as any)}
															</span>
														</td>
														<td>
															{task.dueDate || '-'}
														</td>
														<td>
															{task.assignedToName || '-'}
														</td>
														<td>
															<Button
																isLight
																color='info'
																icon='Edit'
																onClick={() => handleEdit(task)}
																className='me-1'
															/>
															<Button
																isLight
																color='danger'
																icon='Delete'
																onClick={() => handleDelete(task.id)}
															/>
														</td>
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
		</PageWrapper>
	);
};

export default TaskListPage;
