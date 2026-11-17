import React, { FC, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import classNames from 'classnames';
import { IconButton } from '../components/IconButton';
import { ApiProblem, apiProblemMessage } from '../../services/api/apiProblem';

interface IApiErrorBodyProps {
	problem: ApiProblem;
}

/** The message of a failed request with its "Reference: {traceId}" line and a copy action (operations.md 4.3). */
export const ApiErrorBody: FC<IApiErrorBodyProps> = ({ problem }) => {
	const { t } = useTranslation('common');
	const [copied, setCopied] = useState(false);

	const copy = () => {
		navigator.clipboard
			?.writeText(problem.traceId)
			.then(() => {
				setCopied(true);
				setTimeout(() => setCopied(false), 2000);
			})
			.catch(() => setCopied(false));
	};

	return (
		<div>
			<div>{apiProblemMessage(problem)}</div>
			<div className='small text-muted d-flex align-items-center gap-2 mt-1'>
				<span>{t('apiErrors.reference', { id: problem.traceId })}</span>
				<IconButton
					variant='link'
					size='sm'
					className='app-icon-btn--compact'
					icon={copied ? 'Check' : 'ContentCopy'}
					label={t('apiErrors.copyReference')}
					onClick={copy}
				/>
			</div>
		</div>
	);
};

interface IApiErrorMessageProps extends IApiErrorBodyProps {
	className?: string;
}

/** Inline error block for a region of a page that could not be loaded or saved. */
export const ApiErrorMessage: FC<IApiErrorMessageProps> = ({ problem, className }) => (
	<div role='alert' className={classNames('alert alert-danger mb-0', className)}>
		<ApiErrorBody problem={problem} />
	</div>
);

/** Shows the problem of a failed request as a toast that stays until it is dismissed (frontend.md 10.1). */
export const showApiError = (problem: ApiProblem) => {
	toast.error(<ApiErrorBody problem={problem} />, { autoClose: false, closeOnClick: false });
};
