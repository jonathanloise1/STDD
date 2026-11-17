import React from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { AnswerView } from '../runtime/askState';

interface Props {
	answer: AnswerView;
	onNavigate: () => void;
}

/**
 * The written answer (manual.md 7.2 step 3): the text, source chips that link to the cited sections, and the line
 * that says where it was written. The source line itself is never shown as text.
 */
export const AnswerCard = ({ answer, onNavigate }: Props) => {
	const { t } = useTranslation('help');
	return (
		<div className='card border-primary mb-3' data-testid='help-answer'>
			<div className='card-body p-3'>
				<div className='small fw-bold text-primary mb-1' data-testid='help-answer-label'>
					{t('answerLabel')}
				</div>
				<p
					className='mb-2'
					style={{ whiteSpace: 'pre-line' }}
					data-testid='help-answer-text'>
					{answer.text}
				</p>
				{answer.sources.length > 0 && (
					<div className='d-flex gap-2 mb-2'>
						{answer.sources.map((s) => (
							<Link
								key={s.n}
								to={s.url}
								className='badge bg-primary text-decoration-none'
								onClick={onNavigate}
								data-testid='help-answer-source'>
								{`[${s.n}]`}
							</Link>
						))}
					</div>
				)}
				{answer.complete && (
					<div className='small text-muted' data-testid='help-answer-footer'>
						{answer.tier === 'browser' ? t('answeredOnDevice') : t('answeredByServer')}
					</div>
				)}
			</div>
		</div>
	);
};
