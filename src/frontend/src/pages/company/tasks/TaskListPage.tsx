import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
	Badge,
	Button,
	Card,
	CardBody,
	CardHeader,
	ConfirmDialog,
	EmptyState,
	IconButton,
	LoadingBlock,
	Page,
	ApiErrorMessage,
	showApiError,
} from '../../../ui';
import type { IconName, Tone } from '../../../ui';
import { HelpButton } from '../../../help/components/HelpButton';
import { usePermissions } from '../../../contexts/permissionContext';
import { useApiParams } from '../../../hooks/useApiParams';
import taskService from '../../../services/api/taskService';
import { ApiProblem, toApiProblem } from '../../../services/api/apiProblem';
import type {
	TaskItemDto,
	TaskItemStatus,
	CreateTaskRequest,
	UpdateTaskRequest,
} from '../../../type/task-types';

const TITLE_MAX_LENGTH = 200;
const DESCRIPTION_MAX_LENGTH = 2000;

const STATUS_COLORS: Record<TaskItemStatus, Tone> = {
	Todo: 'info',
	InProgress: 'warning',
	Done: 'success',
	Cancelled: 'danger',
};

const STATUS_ICONS: Record<TaskItemStatus, IconName> = {
	Todo: 'RadioButtonUnchecked',
	InProgress: 'Pending',
	Done: 'CheckCircle',
	Cancelled: 'Cancel',
};

/** The status a task moves to when the "Change status" button is clicked. */
const NEXT_STATUS: Record<TaskItemStatus, TaskItemStatus> = {
	Todo: 'InProgress',
	InProgress: 'Done',
	Done: 'Todo',
	Cancelled: 'Todo',
};

/** The Tasks page: list, create, edit, change status and delete. */
// US-TASK-01, US-TASK-02, US-TASK-03, US-TASK-04, US-TASK-05
const TaskListPage = () => {
	const { t } = useTranslation('tasks');
	const { canEdit } = usePermissions();
	const { orgId, isReady } = useApiParams();
	const [tasks, setTasks] = useState<TaskItemDto[]>([]);
	const [isLoading, setIsLoading] = useState(true);
	const [loadError, setLoadError] = useState<ApiProblem | null>(null);
	const [showForm, setShowForm] = useState(false);
	const [isSaving, setIsSaving] = useState(false);
	const [editingTask, setEditingTask] = useState<TaskItemDto | null>(null);
	const [formTitle, setFormTitle] = useState('');
	const [formDescription, setFormDescription] = useState('');
	const [formDueDate, setFormDueDate] = useState('');
	const [formStatus, setFormStatus] = useState<TaskItemStatus>('Todo');
	const [titleError, setTitleError] = useState('');
	const [busyTaskId, setBusyTaskId] = useState<string | null>(null);
	const [deleteTarget, setDeleteTarget] = useState<TaskItemDto | null>(null);

	const fetchTasks = useCallback(async () => {
		if (!orgId) return;
		setIsLoading(true);
		setLoadError(null);
		try {
			const result = await taskService.list(orgId, { pageSize: 50 });
			setTasks(result.items);
		} catch (err) {
			setTasks([]);
			setLoadError(toApiProblem(err));
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
		setTitleError('');
		setEditingTask(null);
		setShowForm(false);
	};

	/** Shows the problem of a failed change; a task that no longer exists is left out of the refreshed list. */
	const handleChangeFailure = (err: unknown) => {
		const problem = toApiProblem(err);
		showApiError(problem);
		if (problem.status === 404) {
			resetForm();
			setDeleteTarget(null);
			fetchTasks();
		}
	};

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		if (!orgId) return;
		const title = formTitle.trim();
		if (!title) {
			setTitleError(t('titleRequired'));
			return;
		}

		setIsSaving(true);
		try {
			if (editingTask) {
				const data: UpdateTaskRequest = {
					title,
					description: formDescription,
					status: formStatus,
					...(formDueDate
						? { dueDate: formDueDate }
						: editingTask.dueDate
							? { clearDueDate: true }
							: {}),
				};
				await taskService.update(orgId, editingTask.id, data);
			} else {
				const data: CreateTaskRequest = {
					title,
					description: formDescription || null,
					dueDate: formDueDate || null,
				};
				await taskService.create(orgId, data);
			}
			resetForm();
			fetchTasks();
		} catch (err) {
			handleChangeFailure(err);
		} finally {
			setIsSaving(false);
		}
	};

	const handleEdit = (task: TaskItemDto) => {
		setEditingTask(task);
		setFormTitle(task.title);
		setFormDescription(task.description || '');
		setFormDueDate(task.dueDate || '');
		setFormStatus(task.status);
		setTitleError('');
		setShowForm(true);
	};

	const handleDeleteConfirm = async () => {
		if (!orgId || !deleteTarget) return;
		try {
			await taskService.remove(orgId, deleteTarget.id);
		} catch (err) {
			handleChangeFailure(err);
		}
		setDeleteTarget(null);
		fetchTasks();
	};

	const handleStatusToggle = async (task: TaskItemDto) => {
		if (!orgId) return;
		setBusyTaskId(task.id);
		try {
			// A body with only the status changes only the status
			const updated = await taskService.update(orgId, task.id, {
				status: NEXT_STATUS[task.status],
			});
			setTasks((current) => current.map((item) => (item.id === task.id ? updated : item)));
		} catch (err) {
			handleChangeFailure(err);
		} finally {
			setBusyTaskId(null);
		}
	};

	return (
		<Page title={t('title')}>
			<div className='row'>
				<div className='col-12'>
					<Card>
						<CardHeader
							icon='TaskAlt'
							title={t('title')}
							actions={
								canEdit && (
									<Button
										variant='primary'
										icon='Add'
										onClick={() => {
											resetForm();
											setShowForm(true);
										}}>
										{t('newTask')}
									</Button>
								)
							}
						/>
						<CardBody>
							{/* Inline Create/Edit Form */}
							{canEdit && showForm && (
								<form
									noValidate
									onSubmit={handleSubmit}
									className='mb-4 p-3 border rounded bg-primary-subtle'>
									<div className='row g-3'>
										<div className='col-md-6'>
											<label
												htmlFor='task-title'
												className='form-label fw-bold'>
												{t('taskTitle')}
											</label>
											<input
												id='task-title'
												type='text'
												className={`form-control ${titleError ? 'is-invalid' : ''}`}
												value={formTitle}
												maxLength={TITLE_MAX_LENGTH}
												onChange={(e) => {
													setFormTitle(e.target.value);
													if (titleError) setTitleError('');
												}}
												aria-invalid={!!titleError}
												aria-describedby={
													titleError ? 'task-title-error' : undefined
												}
												autoFocus
											/>
											{titleError && (
												<div
													id='task-title-error'
													className='invalid-feedback'>
													{titleError}
												</div>
											)}
										</div>
										<div className='col-md-3'>
											<label
												htmlFor='task-due-date'
												className='form-label fw-bold'>
												{t('taskDueDate')}
											</label>
											<input
												id='task-due-date'
												type='date'
												className='form-control'
												value={formDueDate}
												onChange={(e) => setFormDueDate(e.target.value)}
											/>
										</div>
										{editingTask && (
											<div className='col-md-3'>
												<label
													htmlFor='task-status'
													className='form-label fw-bold'>
													{t('taskStatus')}
												</label>
												<select
													id='task-status'
													className='form-select'
													value={formStatus}
													onChange={(e) =>
														setFormStatus(
															e.target.value as TaskItemStatus,
														)
													}>
													<option value='Todo'>{t('statusTodo')}</option>
													<option value='InProgress'>
														{t('statusInProgress')}
													</option>
													<option value='Done'>{t('statusDone')}</option>
													<option value='Cancelled'>
														{t('statusCancelled')}
													</option>
												</select>
											</div>
										)}
										<div className='col-12'>
											<label
												htmlFor='task-description'
												className='form-label fw-bold'>
												{t('taskDescription')}
											</label>
											<textarea
												id='task-description'
												className='form-control'
												rows={2}
												value={formDescription}
												maxLength={DESCRIPTION_MAX_LENGTH}
												onChange={(e) => setFormDescription(e.target.value)}
											/>
										</div>
										<div className='col-12 d-flex gap-2'>
											<Button
												type='submit'
												variant='primary'
												icon='Save'
												busy={isSaving}>
												{editingTask ? t('update') : t('createTask')}
											</Button>
											<Button
												type='button'
												variant='light'
												onClick={resetForm}>
												{t('cancel')}
											</Button>
											<div className='ms-auto'>
												<HelpButton context='task-form' />
											</div>
										</div>
									</div>
								</form>
							)}

							{/* Task List */}
							{isLoading ? (
								<LoadingBlock />
							) : loadError ? (
								<ApiErrorMessage problem={loadError} />
							) : tasks.length === 0 ? (
								<EmptyState
									icon='TaskAlt'
									title={t('noTasks')}
									text={canEdit ? t('noTasksDescription') : undefined}
								/>
							) : (
								<div className='table-responsive'>
									<table className='table align-middle'>
										<thead>
											<tr>
												<th>{t('taskTitle')}</th>
												<th>{t('taskStatus')}</th>
												<th>{t('taskDueDate')}</th>
												{canEdit && (
													<th className='app-col-actions'>
														{t('actions')}
													</th>
												)}
											</tr>
										</thead>
										<tbody>
											{tasks.map((task) => (
												<tr key={task.id}>
													<td>
														<div className='d-flex align-items-start gap-2'>
															{canEdit && (
																<IconButton
																	variant={`subtle-${STATUS_COLORS[task.status]}`}
																	icon={STATUS_ICONS[task.status]}
																	label={t('changeStatus')}
																	busy={busyTaskId === task.id}
																	onClick={() =>
																		handleStatusToggle(task)
																	}
																	className='p-0 flex-shrink-0'
																/>
															)}
															<div>
																<div className='fw-bold'>
																	{task.title}
																</div>
																{task.description && (
																	<div className='small text-muted text-truncate app-truncate-wide'>
																		{task.description}
																	</div>
																)}
															</div>
														</div>
													</td>
													<td>
														<Badge tone={STATUS_COLORS[task.status]}>
															{t(`status${task.status}`)}
														</Badge>
													</td>
													<td>{task.dueDate || '-'}</td>
													{canEdit && (
														<td>
															<IconButton
																variant='subtle-info'
																icon='Edit'
																label={t('edit')}
																onClick={() => handleEdit(task)}
																className='me-1'
															/>
															<IconButton
																variant='subtle-danger'
																icon='Delete'
																label={t('delete')}
																onClick={() =>
																	setDeleteTarget(task)
																}
															/>
														</td>
													)}
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

			{/* Delete confirmation */}
			<ConfirmDialog
				show={!!deleteTarget}
				title={t('deleteTask')}
				message={t('confirmDelete')}
				cancelLabel={t('cancel')}
				confirmLabel={t('delete')}
				onCancel={() => setDeleteTarget(null)}
				onConfirm={handleDeleteConfirm}
			/>
		</Page>
	);
};

export default TaskListPage;
