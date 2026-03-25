/**
 * Reusable banner prompting the user to complete missing fiscal data
 * on the organization. Displayed on contracts and invoicing pages when
 * fiscal fields are incomplete.
 *
 * @userstory US-ONBOARD-CO-07
 * @task TASK-OBC-06
 */
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import type { FiscalCompletenessResult } from '../../hooks/useFiscalCompleteness';

interface FiscalDataPromptProps {
	/** Result from useFiscalCompleteness hook. */
	completeness: FiscalCompletenessResult;
	/** Organization ID to navigate to settings page. */
	organizationId?: string;
}

/**
 * Renders a dismissible alert banner when fiscal data is incomplete.
 * Hidden when all fiscal fields are filled.
 */
const FiscalDataPrompt: React.FC<FiscalDataPromptProps> = ({ completeness, organizationId }) => {
	const { t } = useTranslation('organization');
	const navigate = useNavigate();

	if (completeness.isComplete) return null;

	return (
		<div className='alert alert-warning d-flex align-items-center mb-3' role='alert'>
			<div className='flex-grow-1'>
				<strong>{t('Fiscal Data Incomplete')}</strong>
				<span className='ms-2'>
					{t('Complete your organization billing details to enable contracts and invoicing.')}
				</span>
				<span className='ms-1 text-muted'>({completeness.percentage}%)</span>
			</div>
			{organizationId && (
				<button
					type='button'
					className='btn btn-warning btn-sm ms-3'
					onClick={() => navigate(`/company/settings/organization`)}
				>
					{t('Complete Now')}
				</button>
			)}
		</div>
	);
};

export default FiscalDataPrompt;
