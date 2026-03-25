/**
 * Hook to check fiscal data completeness for the selected organization.
 * Returns whether key fiscal fields (VAT, fiscal code, SDI/PEC, billing address) are filled.
 * Used by the FiscalDataPrompt component for progressive profiling.
 *
 * @userstory US-ONBOARD-CO-07
 * @task TASK-OBC-06
 */
import { useMemo } from 'react';
import type { Organization } from '../type/organization-types';

export interface FiscalCompletenessResult {
	/** True if all key fiscal fields are populated. */
	isComplete: boolean;
	/** List of missing field keys (for display / i18n). */
	missingFields: string[];
	/** Completion percentage (0–100). */
	percentage: number;
}

/** Fields considered for fiscal completeness. */
const FISCAL_FIELDS: (keyof Organization)[] = [
	'vatNumber',
	'fiscalCode',
	'italianSdiCode',
	'billingAddress',
	'billingCity',
	'billingProvince',
	'billingZipCode',
];

/**
 * Evaluates fiscal data completeness for the given organization.
 * @param organization The currently selected organization, or null/undefined.
 */
export function useFiscalCompleteness(
	organization: Organization | null | undefined,
): FiscalCompletenessResult {
	return useMemo(() => {
		if (!organization) {
			return { isComplete: false, missingFields: [...FISCAL_FIELDS], percentage: 0 };
		}

		const missingFields = FISCAL_FIELDS.filter((field) => {
			const value = organization[field];
			return !value || (typeof value === 'string' && !value.trim());
		});

		const total = FISCAL_FIELDS.length;
		const filled = total - missingFields.length;
		const percentage = Math.round((filled / total) * 100);

		return {
			isComplete: missingFields.length === 0,
			missingFields: missingFields as string[],
			percentage,
		};
	}, [organization]);
}
