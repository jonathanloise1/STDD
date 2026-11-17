import React, { FormEvent, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, CardBody, CardHeader, Page } from '../../../ui';
import helpService from '../../../services/api/helpService';
import { toApiProblem } from '../../../services/api/apiProblem';
import {
	downloadJson,
	gapFileName,
	periodError,
	resolvePeriod,
} from '../../../help/runtime/gapExport';

const isoDay = (date: Date): string => date.toISOString().slice(0, 10);

/** The Unanswered questions page (Admin): a period and a download of the questions the manual could not answer. */
// US-HELP-07
const UnansweredQuestionsPage = () => {
	const { t } = useTranslation('help');
	const [from, setFrom] = useState(() => isoDay(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
	const [to, setTo] = useState(() => isoDay(new Date()));
	const [toError, setToError] = useState(false);
	const [failure, setFailure] = useState<string | null>(null);
	const [isBusy, setIsBusy] = useState(false);

	// US-HELP-07
	const handleDownload = async (event: FormEvent) => {
		event.preventDefault();
		setFailure(null);
		const period = resolvePeriod(from, to);
		if (periodError(period)) {
			setToError(true);
			return;
		}
		setToError(false);
		setIsBusy(true);
		try {
			const records = await helpService.downloadGaps(period.from, period.to);
			downloadJson(gapFileName(period), records);
		} catch (err) {
			setFailure(toApiProblem(err).traceId);
		} finally {
			setIsBusy(false);
		}
	};

	return (
		<Page title={t('unansweredTitle')}>
			<div className='row'>
				<div className='col-12 col-xl-6'>
					<Card>
						<CardHeader icon='LiveHelp' title={t('unansweredTitle')} />
						<CardBody>
							<p className='text-muted'>{t('unansweredDescription')}</p>
							<form noValidate onSubmit={handleDownload} className='row g-3'>
								<div className='col-sm-6'>
									<label htmlFor='gaps-from' className='form-label fw-bold'>
										{t('from')}
									</label>
									<input
										id='gaps-from'
										data-testid='input-gaps-from'
										type='date'
										className='form-control'
										value={from}
										onChange={(e) => {
											setFrom(e.target.value);
											setToError(false);
										}}
									/>
								</div>
								<div className='col-sm-6'>
									<label htmlFor='gaps-to' className='form-label fw-bold'>
										{t('to')}
									</label>
									<input
										id='gaps-to'
										data-testid='input-gaps-to'
										type='date'
										className={`form-control ${toError ? 'is-invalid' : ''}`}
										value={to}
										aria-invalid={toError}
										aria-describedby={toError ? 'gaps-to-error' : undefined}
										onChange={(e) => {
											setTo(e.target.value);
											setToError(false);
										}}
									/>
									{toError && (
										<div
											id='gaps-to-error'
											className='invalid-feedback'
											data-testid='error-gaps-to'>
											{t('endBeforeStart')}
										</div>
									)}
								</div>
								<div className='col-12'>
									<Button
										type='submit'
										busy={isBusy}
										data-testid='btn-gaps-download'>
										{t('download')}
									</Button>
								</div>
							</form>
							{failure && (
								<div
									role='alert'
									className='alert alert-danger mt-3 mb-0'
									data-testid='gaps-download-failed'>
									<div>{t('downloadFailed')}</div>
									<div className='small text-muted mt-1'>
										{t('common:apiErrors.reference', { id: failure })}
									</div>
								</div>
							)}
						</CardBody>
					</Card>
				</div>
			</div>
		</Page>
	);
};

export default UnansweredQuestionsPage;
