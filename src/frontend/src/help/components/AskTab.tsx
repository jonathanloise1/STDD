import React, { FormEvent, useEffect, useReducer, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useHelp } from '../../contexts/helpContext';
import { useHelpLang } from '../../hooks/useHelpData';
import { useHelpRoute } from '../../hooks/useHelpRoute';
import helpService from '../../services/api/helpService';
import { reportClientEvent } from '../../services/api/clientEventsService';
import { askQuestion, rateNo } from '../runtime/askController';
import type { AskDeps, AskSession } from '../runtime/askController';
import { askReducer, initialAskState } from '../runtime/askState';
import type { AnswerEngine, EngineAvailability } from '../runtime/engine';
import { engineRegistry } from '../runtime/engines';
import { helpLoader } from '../runtime/loader';
import { MAX_QUESTION_CHARS } from '../runtime/mask';
import type { HelpPack } from '../index';
import type { LoadedPack } from '../runtime/types';
import { AnswerCard } from './AnswerCard';
import { FeedbackRow } from './FeedbackRow';
import { SectionCard } from './SectionCard';

/** The tab "Ask": a question box and the sections, answer or fixed reply it leads to (manual.md 7.2, 7.3). */
// US-HELP-04, US-HELP-05, US-HELP-06
export const AskTab = () => {
	const { t } = useTranslation('help');
	const { closeHelp } = useHelp();
	const lang = useHelpLang();
	const { pathname, pattern } = useHelpRoute();
	const route = pattern ?? pathname;

	const [question, setQuestion] = useState('');
	const [state, dispatch] = useReducer(askReducer, initialAskState);
	const [active, setActive] = useState<{
		session: AskSession;
		loaded: LoadedPack<HelpPack>;
	} | null>(null);
	const seqRef = useRef(0);
	const [noticeEngine, setNoticeEngine] = useState<AnswerEngine | null>(null);

	const deps: AskDeps = {
		lang,
		route,
		loadPack: () => helpLoader.pack(lang),
		loadStatus: () => helpService.getStatus(),
		registry: engineRegistry,
		recordGap: (record) => helpService.recordGap(record),
		now: () => new Date(),
	};

	useEffect(() => {
		if (state.phase === 'unavailable') reportClientEvent('help.ask.unavailable', route);
	}, [state.phase, state.seq, route]);

	// A tier that prepares in the background (download progress) keeps the notice up to date
	useEffect(() => {
		if (!noticeEngine?.subscribe) return undefined;
		return noticeEngine.subscribe((availability: EngineAvailability) =>
			dispatch({
				type: 'notice',
				seq: state.seq,
				notice:
					availability.state === 'consent'
						? { kind: 'consent', sizeMb: availability.sizeMb }
						: availability.state === 'preparing'
							? {
									kind: 'preparing',
									doneMb: availability.doneMb,
									totalMb: availability.totalMb,
								}
							: null,
			}),
		);
	}, [noticeEngine, state.seq]);

	const submit = (event: FormEvent) => {
		event.preventDefault();
		const text = question.trim();
		if (!text) return;
		seqRef.current += 1;
		setNoticeEngine(null);
		setActive(null);
		askQuestion(deps, text, seqRef.current, {
			dispatch,
			onSession: (session, loaded) => setActive({ session, loaded }),
			onNoticeEngine: setNoticeEngine,
		}).catch(() => dispatch({ type: 'unavailable', seq: seqRef.current }));
	};

	const rate = (rating: 'yes' | 'no') => {
		dispatch({ type: 'rated', seq: state.seq, rating });
		if (rating === 'no' && active)
			rateNo(deps, active.loaded, active.session).catch(() => undefined);
	};

	const declineDownload = () => {
		noticeEngine?.declineDownload?.();
		dispatch({ type: 'notice', seq: state.seq, notice: null });
	};

	const acceptDownload = () => {
		noticeEngine?.acceptDownload?.();
		if (!noticeEngine?.subscribe) dispatch({ type: 'notice', seq: state.seq, notice: null });
	};

	return (
		<div>
			<form onSubmit={submit} noValidate className='mb-3'>
				<label htmlFor='help-question' className='form-label fw-semibold'>
					{t('yourQuestion')}
				</label>
				<input
					id='help-question'
					data-testid='input-help-question'
					type='text'
					className='form-control mb-2'
					maxLength={MAX_QUESTION_CHARS}
					value={question}
					autoComplete='off'
					onChange={(e) =>
						setQuestion([...e.target.value].slice(0, MAX_QUESTION_CHARS).join(''))
					}
				/>
				<button
					type='submit'
					className='btn btn-primary'
					disabled={question.trim() === ''}
					data-testid='btn-help-ask'>
					{t('ask')}
				</button>
			</form>

			{state.phase === 'unavailable' && (
				<p role='alert' data-testid='help-unavailable'>
					{t('unavailable')}
				</p>
			)}

			{state.phase === 'outOfScope' && (
				<div data-testid='help-out-of-scope'>
					<p className='fw-semibold'>{t('nothingYet')}</p>
					<ul className='list-unstyled'>
						<li>
							<Link to='/manual' onClick={closeHelp}>
								{t('goToManualHome')}
							</Link>
						</li>
						<li>
							<Link
								to={`/manual/search?q=${encodeURIComponent(state.question)}`}
								onClick={closeHelp}>
								{t('searchForThis')}
							</Link>
						</li>
					</ul>
					<FeedbackRow rating={state.rating} onRate={rate} />
				</div>
			)}

			{state.phase === 'results' && (
				<div data-testid='help-results'>
					{state.notice?.kind === 'consent' && (
						<div className='alert alert-info' data-testid='help-consent'>
							<p className='mb-2'>{t('consent', { size: state.notice.sizeMb })}</p>
							<div className='d-flex gap-2'>
								<button
									type='button'
									className='btn btn-sm btn-primary'
									onClick={acceptDownload}>
									{t('download')}
								</button>
								<button
									type='button'
									className='btn btn-sm btn-outline-secondary'
									onClick={declineDownload}>
									{t('notNow')}
								</button>
							</div>
						</div>
					)}
					{state.notice?.kind === 'preparing' && (
						<div
							className='alert alert-info'
							role='status'
							data-testid='help-preparing'>
							{t('preparing', {
								done: state.notice.doneMb,
								total: state.notice.totalMb,
							})}
						</div>
					)}
					{state.notice?.kind === 'device' && (
						<p
							className='small text-muted'
							role='status'
							data-testid='help-device-reason'>
							{t('deviceUnavailable', {
								reason: t(`deviceReason.${state.notice.reason}`, {
									defaultValue: state.notice.reason,
								}),
							})}
						</p>
					)}
					{state.answer && <AnswerCard answer={state.answer} onNavigate={closeHelp} />}
					{state.header === 'best' && (
						<h3 className='h6 fw-bold' data-testid='help-cards-header'>
							{t('bestMatches')}
						</h3>
					)}
					{state.header === 'declined' && (
						<p className='fw-semibold' data-testid='help-declined'>
							{t('declined')}
						</p>
					)}
					{state.cards.map((view) => (
						<SectionCard key={view.id} view={view} onNavigate={closeHelp} />
					))}
					{state.done && <FeedbackRow rating={state.rating} onRate={rate} />}
				</div>
			)}
		</div>
	);
};
