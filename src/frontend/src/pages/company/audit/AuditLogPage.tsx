import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
	ApiErrorMessage,
	Badge,
	Button,
	Card,
	CardBody,
	CardHeader,
	Dialog,
	IconButton,
	LoadingBlock,
	Page,
	PageHeader,
} from '../../../ui';
import type { Tone } from '../../../ui';
import { useOrganization } from '../../../contexts/organizationContext';
import auditService from '../../../services/api/auditService';
import type { AuditLogQueryParams } from '../../../services/api/auditService';
import { ApiProblem, toApiProblem } from '../../../services/api/apiProblem';
import type { AuditLogDto, AuditLogPagedResult } from '../../../type/audit-types';
import {
	AUDIT_ENTITY_TYPES,
	fieldLabel,
	formatValue,
	parseChangedFields,
	parseValues,
} from './auditFormat';

const actionColors: Record<string, Tone> = {
	Create: 'success',
	Update: 'info',
	Delete: 'danger',
};

const PAGE_SIZE = 25;

/** Start of a day of the device, as an instant. */
const startOfDay = (isoDate: string): string => {
	const [y, m, d] = isoDate.split('-').map(Number);
	return new Date(y, m - 1, d, 0, 0, 0, 0).toISOString();
};

/** End of a day of the device, as an instant. */
const endOfDay = (isoDate: string): string => {
	const [y, m, d] = isoDate.split('-').map(Number);
	return new Date(y, m - 1, d, 23, 59, 59, 999).toISOString();
};

/** The Audit Log page: changes of the organization with filters and a details dialog (Admins only). */
// US-AUD-01, US-AUD-02, US-AUD-03
const AuditLogPage = () => {
	const { t, i18n } = useTranslation(['audit', 'common', 'tasks']);
	const { selectedOrganization } = useOrganization();
	const orgId = selectedOrganization?.id;

	// Filter state
	const [filterEntityType, setFilterEntityType] = useState('');
	const [filterDateFrom, setFilterDateFrom] = useState('');
	const [filterDateTo, setFilterDateTo] = useState('');
	const [page, setPage] = useState(1);

	// Data state
	const [result, setResult] = useState<AuditLogPagedResult | null>(null);
	const [loadError, setLoadError] = useState<ApiProblem | null>(null);
	const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);
	const [isLoading, setIsLoading] = useState(true);
	const requestRef = useRef(0);

	const loadLogs = useCallback(async () => {
		if (!orgId) return;
		const request = ++requestRef.current;
		setIsLoading(true);
		setLoadError(null);
		try {
			const params: AuditLogQueryParams = {
				page,
				pageSize: PAGE_SIZE,
				...(filterEntityType && { entityType: filterEntityType }),
				...(filterDateFrom && { from: startOfDay(filterDateFrom) }),
				...(filterDateTo && { to: endOfDay(filterDateTo) }),
			};
			const data = await auditService.list(orgId, params);
			if (request === requestRef.current) setResult(data);
		} catch (err) {
			if (request === requestRef.current) {
				setResult(null);
				setLoadError(toApiProblem(err));
			}
		} finally {
			if (request === requestRef.current) setIsLoading(false);
		}
	}, [orgId, page, filterEntityType, filterDateFrom, filterDateTo]);

	useEffect(() => {
		loadLogs();
	}, [loadLogs]);

	const totalPages = result ? Math.ceil(result.totalCount / PAGE_SIZE) : 0;

	const typeName = (log: AuditLogDto) =>
		t(`types.${log.entityType}`, { defaultValue: log.entityTypeName || log.entityType });
	const actionName = (log: AuditLogDto) =>
		t(`actions.${log.action}`, { defaultValue: log.action });
	const when = (log: AuditLogDto) => new Date(log.timestamp).toLocaleString(i18n.language);
	const changedFields = (log: AuditLogDto) =>
		log.action === 'Update'
			? parseChangedFields(log.changedProperties).map((field) =>
					fieldLabel(t, log.entityType, field),
				)
			: [];

	/** "Old Values" or "New Values" of a change: one line per field, by its screen label. */
	const renderValues = (log: AuditLogDto, id: string, heading: string, raw?: string | null) => {
		const entries = parseValues(raw);
		if (!raw) return null;
		return (
			<section className='mb-3' aria-labelledby={id}>
				<h6 id={id} className='fw-bold mb-2'>
					{heading}
				</h6>
				{entries.length === 0 ? (
					<div className='text-muted'>-</div>
				) : (
					<dl className='row mb-0'>
						{entries.map(([field, value]) => (
							<React.Fragment key={field}>
								<dt className='col-5 fw-normal text-muted'>
									{fieldLabel(t, log.entityType, field)}
								</dt>
								<dd className='col-7'>
									{formatValue(t, log.entityType, field, value, i18n.language)}
								</dd>
							</React.Fragment>
						))}
					</dl>
				)}
			</section>
		);
	};

	if (!orgId) return null;

	return (
		<Page title={t('title')}>
			<PageHeader icon='History' title={t('title')} />
			{/* Filters */}
			<Card className='mb-3'>
				<CardBody>
					<div className='row g-3 align-items-end'>
						<div className='col-auto'>
							<label htmlFor='audit-filter-type' className='form-label'>
								{t('filterType')}
							</label>
							<select
								id='audit-filter-type'
								className='form-select'
								value={filterEntityType}
								onChange={(e) => {
									setFilterEntityType(e.target.value);
									setPage(1);
								}}>
								<option value=''>{t('allTypes')}</option>
								{AUDIT_ENTITY_TYPES.map((type) => (
									<option key={type} value={type}>
										{t(`types.${type}`)}
									</option>
								))}
							</select>
						</div>
						<div className='col-auto'>
							<label htmlFor='audit-filter-from' className='form-label'>
								{t('dateFrom')}
							</label>
							<input
								id='audit-filter-from'
								type='date'
								className='form-control'
								value={filterDateFrom}
								onChange={(e) => {
									setFilterDateFrom(e.target.value);
									setPage(1);
								}}
							/>
						</div>
						<div className='col-auto'>
							<label htmlFor='audit-filter-to' className='form-label'>
								{t('dateTo')}
							</label>
							<input
								id='audit-filter-to'
								type='date'
								className='form-control'
								value={filterDateTo}
								onChange={(e) => {
									setFilterDateTo(e.target.value);
									setPage(1);
								}}
							/>
						</div>
						<div className='col-auto'>
							<Button
								variant='light'
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
					{result && (
						<Badge tone='light' data-testid='audit-total'>
							{t('totalChanges', { count: result.totalCount })}
						</Badge>
					)}
				</CardHeader>
				<CardBody>
					{isLoading ? (
						<LoadingBlock />
					) : loadError ? (
						<ApiErrorMessage problem={loadError} />
					) : (
						<>
							<div className='table-responsive'>
								<table className='table align-middle'>
									<thead>
										<tr>
											<th>{t('timestamp')}</th>
											<th>{t('action')}</th>
											<th>{t('entityType')}</th>
											<th>{t('user')}</th>
											<th>{t('changes')}</th>
										</tr>
									</thead>
									<tbody>
										{result?.items.map((log) => {
											const fields = changedFields(log);
											return (
												<tr key={log.id}>
													<td>{when(log)}</td>
													<td>
														<Badge
															tone={
																actionColors[log.action] ||
																'secondary'
															}>
															{actionName(log)}
														</Badge>
													</td>
													<td>{typeName(log)}</td>
													<td>{log.userDisplayName || '-'}</td>
													<td>
														<div className='d-flex align-items-center justify-content-between gap-2'>
															<span>
																{fields.length > 0
																	? fields.join(', ')
																	: '-'}
															</span>
															<IconButton
																variant='light'
																size='sm'
																icon='Visibility'
																label={t('details')}
																onClick={() => setSelectedLog(log)}
															/>
														</div>
													</td>
												</tr>
											);
										})}
										{(!result || result.items.length === 0) && (
											<tr>
												<td
													colSpan={5}
													className='text-center text-muted py-4'>
													{t('empty')}
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
										variant='light'
										disabled={page <= 1}
										onClick={() => setPage(page - 1)}>
										{t('common:previous')}
									</Button>
									<span className='align-self-center'>
										{page} / {totalPages}
									</span>
									<Button
										variant='light'
										disabled={page >= totalPages}
										onClick={() => setPage(page + 1)}>
										{t('common:next')}
									</Button>
								</div>
							)}
						</>
					)}
				</CardBody>
			</Card>

			{/* Details dialog */}
			<Dialog
				show={!!selectedLog}
				onHide={() => setSelectedLog(null)}
				size='lg'
				icon='Info'
				title={t('details')}
				footer={
					<Button
						id='audit-detail-close-btn'
						variant='light'
						onClick={() => setSelectedLog(null)}>
						{t('common:cancel')}
					</Button>
				}>
				{selectedLog && (
					<>
						<dl className='row mb-3'>
							<dt className='col-4'>{t('timestamp')}</dt>
							<dd className='col-8'>{when(selectedLog)}</dd>
							<dt className='col-4'>{t('action')}</dt>
							<dd className='col-8'>
								<Badge tone={actionColors[selectedLog.action] || 'secondary'}>
									{actionName(selectedLog)}
								</Badge>
							</dd>
							<dt className='col-4'>{t('entityType')}</dt>
							<dd className='col-8'>{typeName(selectedLog)}</dd>
							<dt className='col-4'>{t('user')}</dt>
							<dd className='col-8'>{selectedLog.userDisplayName || '-'}</dd>
						</dl>
						{renderValues(
							selectedLog,
							'audit-old-values',
							t('oldValues'),
							selectedLog.oldValues,
						)}
						{renderValues(
							selectedLog,
							'audit-new-values',
							t('newValues'),
							selectedLog.newValues,
						)}
					</>
				)}
			</Dialog>
		</Page>
	);
};

export default AuditLogPage;
