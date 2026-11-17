import { useMemo } from 'react';
import { useOrganization } from '../contexts/organizationContext';

/**
 * Convenience hook that extracts the organization ID required by API services.
 *
 * Returns `null` if no organization is selected.
 */
export const useApiParams = () => {
	const { selectedOrganization } = useOrganization();

	return useMemo(
		() => ({
			orgId: selectedOrganization?.id ?? null,
			/** True when the organization ID is available */
			isReady: !!selectedOrganization?.id,
		}),
		[selectedOrganization?.id],
	);
};

export default useApiParams;
