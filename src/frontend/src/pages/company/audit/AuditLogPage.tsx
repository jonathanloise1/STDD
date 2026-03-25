/** @userstory US-AUD-01 */
import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import type { TColor } from '../../../type/color-type';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import SubHeader, { SubHeaderLeft } from '../../../layout/SubHeader/SubHeader';
import Card, { CardBody, CardHeader, CardTitle } from '../../../components/bootstrap/Card';
import Badge from '../../../components/bootstrap/Badge';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import Loading from '../../../components/Loading';
import Input from '../../../components/bootstrap/forms/Input';
import Select from '../../../components/bootstrap/forms/Select';
import Modal, {
	ModalHeader,
	ModalBody,
	ModalFooter,
} from '../../../components/bootstrap/Modal';

// Contexts
import { useOrganization } from '../../../contexts/organizationContext';

// Services & types
import auditService from '../../../services/api/auditService';
import type { AuditLogQueryParams } from '../../../services/api/auditService';
import type { AuditLogDto, AuditLogPagedResult } from '../../../type/audit-types';

// ── Action badge colors ──────────────────────────────────────────
const actionColors: Record<string, TColor> = {
	Create: 'success',
	Update: 'info',
	Delete: 'danger',
};

// US-AUD-01: Audit log page (ORG-scoped, Admin-only via route guard)
const AuditLogPage = () => {
	const { t } = useTranslation(['audit', 'common']);
	const { selectedOrganization } = useOrganization();
	const orgId = selectedOrganization?.id;

	// Filter state
	const [filterEntityType, setFilterEntityType] = useState('');
	const [filterDateFrom, setFilterDateFrom] = useState('');
	const [filterDateTo, setFilterDateTo] = useState('');
	const [page, setPage] = useState(1);
	const pageSize = 25;

	// Data state
	const [result, setResult] = useState<AuditLogPagedResult | null>(null);
	const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);

	// UI state
	const [isLoading, setIsLoading] = useState(false);
	const [detailModalOpen, setDetailModalOpen] = useState(false);

	// Known entity types for filter dropdown
	const entityTypes = useMemo(() => {
		if (!result?.items) return [];
		const set = new Set<string>();
		result.items.forEach((item) => set.add(item.entityType));
		return Array.from(set).sort();
	}, [result]);

	// Fetch audit logs
	const loadLogs = useCallback(async () => {
		if (!orgId) return;
		setIsLoading(true);
		try {
			const params: AuditLogQueryParams = {
				page,
				pageSize,
				...(filterEntityType && { entityType: filterEntityType }),
				...(filterDateFrom && { from: filterDateFrom }),
				...(filterDateTo && { to: filterDateTo }),
			};
			const data = await auditService.list(orgId, params);
			setResult(data);
		} catch (err) {
			console.error('Error loading audit logs:', err);
			toast.error(t('errorLoading'));
		} finally {
			setIsLoading(false);
		}
	}, [orgId, page, filterEntityType, filterDateFrom, filterDateTo, t]);

	useEffect(() => {
		loadLogs();
	}, [loadLogs]);

	const totalPages = result ? Math.ceil(result.totalCount / pageSize) : 0;

	// Open detail modal
	const handleOpenDetail = (log: AuditLogDto) => {
		setSelectedLog(log);
		setDetailModalOpen(true);
	};

	// Format JSON prettily
	const formatJson = (jsonStr: string | null | undefined): string => {
		if (!jsonStr) return '—';
		try {
			return JSON.stringify(JSON.parse(jsonStr), null, 2);
		} catch {
			return jsonStr;
		}
	};

	// Parse changed properties
	const parseChangedProps = (props: string | null | undefined): string[] => {
		if (!props) return [];
		try {
			return JSON.parse(props);
		} catch {
			return props.split(',').map((s) => s.trim());
		}
	};

	if (!orgId) return null;

	return (
		<PageWrapper title={t('title')}>
			<SubHeader>
				<SubHeaderLeft>
					<Icon icon='History' className='me-2' size='2x' />
					<span className='h4 mb-0 fw-bold'>{t('title')}</span>
				</SubHeaderLeft>
			</SubHeader>
			<Page>
				{/* Filters */}
				<Card className='mb-3'>
					<CardBody>
						<div className='row g-3 align-items-end'>
							<div className='col-auto'>
								<label className='form-label'>{t('filter.entityType')}</label>
								<Select
									ariaLabel={t('filter.entityType')}
									value={filterEntityType}
									onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
										setFilterEntityType(e.target.value);
										setPage(1);
									}}>
									<option value=''>{t('filter.allTypes')}</option>
									{entityTypes.map((et) => (
										<option key={et} value={et}>
											{et}
										</option>
									))}
								</Select>
							</div>
							<div className='col-auto'>
								<label className='form-label'>{t('filter.dateFrom')}</label>
								<Input
									type='date'
									value={filterDateFrom}
									onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
										setFilterDateFrom(e.target.value);
										setPage(1);
									}}
								/>
							</div>
							<div className='col-auto'>
								<label className='form-label'>{t('filter.dateTo')}</label>
								<Input
									type='date'
									value={filterDateTo}
									onChange={(e: React.ChangeEvent<HTMLInputElement>) => {
										setFilterDateTo(e.target.value);
										setPage(1);
									}}
								/>
							</div>
							<div className='col-auto'>
								<Button
									color='light'
									icon='FilterAltOff'
									onClick={() => {
										setFilterEntityType('');
										setFilterDateFrom('');
										setFilterDateTo('');
										setPage(1);
									}}>
									{t('common:reset')}
								</Button>
							</div>
						</div>
					</CardBody>
				</Card>

				{/* Table */}
				<Card>
					<CardHeader>
						<CardTitle>
							{t('logEntries')}{' '}
							{result && (
								<Badge color='light' className='ms-2'>
									{result.totalCount}
								</Badge>
							)}
						</CardTitle>
					</CardHeader>
					<CardBody>
						{isLoading ? (
							<Loading />
						) : (
							<>
								<div className='table-responsive'>
									<table className='table table-modern'>
										<thead>
											<tr>
												<th>{t('col.timestamp')}</th>
												<th>{t('col.action')}</th>
												<th>{t('col.entityType')}</th>
												<th>{t('col.user')}</th>
												<th>{t('col.changes')}</th>
												<th />
											</tr>
										</thead>
										<tbody>
											{result?.items.map((log) => (
												<tr key={log.id}>
													<td>
														{new Date(
															log.timestamp,
														).toLocaleString()}
													</td>
													<td>
														<Badge
															color={
																actionColors[log.action] ||
																'secondary'
															}>
															{log.action}
														</Badge>
													</td>
													<td>{log.entityType}</td>
													<td>
														{log.userDisplayName || log.userId}
													</td>
													<td>
														{parseChangedProps(
															log.changedProperties,
														).join(', ') || '—'}
													</td>
													<td>
														<Button
															color='light'
															size='sm'
															icon='Visibility'
															onClick={() =>
																handleOpenDetail(log)
															}
														/>
													</td>
												</tr>
											))}
											{(!result || result.items.length === 0) && (
												<tr>
													<td
														colSpan={6}
														className='text-center text-muted py-4'>
														{t('noEntries')}
													</td>
												</tr>
											)}
										</tbody>
									</table>
								</div>

								{/* Pagination */}
								{totalPages > 1 && (
									<div className='d-flex justify-content-center mt-3 gap-2'>
										<Button
											color='light'
											isDisable={page <= 1}
											onClick={() => setPage(page - 1)}>
											{t('common:previous')}
										</Button>
										<span className='align-self-center'>
											{page} / {totalPages}
										</span>
										<Button
											color='light'
											isDisable={page >= totalPages}
											onClick={() => setPage(page + 1)}>
											{t('common:next')}
										</Button>
									</div>
								)}
							</>
						)}
					</CardBody>
				</Card>

				{/* Detail Modal */}
				<Modal
					isOpen={detailModalOpen}
					setIsOpen={setDetailModalOpen}
					isCentered
					size='lg'>
					<ModalHeader setIsOpen={setDetailModalOpen}>
						<h5>
							<Icon icon='Info' className='me-2' />
							{t('detail.title')}
						</h5>
					</ModalHeader>
					<ModalBody>
						{selectedLog && (
							<>
								<div className='row mb-3'>
									<div className='col-4 fw-bold'>{t('col.timestamp')}</div>
									<div className='col-8'>
										{new Date(selectedLog.timestamp).toLocaleString()}
									</div>
								</div>
								<div className='row mb-3'>
									<div className='col-4 fw-bold'>{t('col.action')}</div>
									<div className='col-8'>
										<Badge
											color={
												actionColors[selectedLog.action] || 'secondary'
											}>
											{selectedLog.action}
										</Badge>
									</div>
								</div>
								<div className='row mb-3'>
									<div className='col-4 fw-bold'>{t('col.entityType')}</div>
									<div className='col-8'>{selectedLog.entityType}</div>
								</div>
								<div className='row mb-3'>
									<div className='col-4 fw-bold'>{t('col.user')}</div>
									<div className='col-8'>
										{selectedLog.userDisplayName || selectedLog.userId}
									</div>
								</div>
								{selectedLog.oldValues && (
									<div className='mb-3'>
										<div className='fw-bold mb-1'>
											{t('detail.oldValues')}
										</div>
										<pre className='bg-light p-2 rounded small'>
											{formatJson(selectedLog.oldValues)}
										</pre>
									</div>
								)}
								{selectedLog.newValues && (
									<div className='mb-3'>
										<div className='fw-bold mb-1'>
											{t('detail.newValues')}
										</div>
										<pre className='bg-light p-2 rounded small'>
											{formatJson(selectedLog.newValues)}
										</pre>
									</div>
								)}
							</>
						)}
					</ModalBody>
					<ModalFooter>
						<Button
							id='audit-detail-close-btn'
							color='light'
							onClick={() => setDetailModalOpen(false)}>
							{t('common:cancel')}
						</Button>
					</ModalFooter>
				</Modal>
			</Page>
		</PageWrapper>
	);
};

export default AuditLogPage;
