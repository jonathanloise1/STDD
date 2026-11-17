import React from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, Menu } from '../ui';
import { useOrganization } from '../contexts/organizationContext';

/**
 * The organization shown in the header.
 * - One organization: its name only.
 * - Several organizations: its name is a button that opens the list to choose the current one.
 * - None (the setup page): nothing.
 */
// US-AUTH-06
const OrganizationSwitcher = () => {
	const { t } = useTranslation('organization');
	const { selectedOrganization, organizations, setSelectedOrganization } = useOrganization();

	if (!selectedOrganization) return null;

	if (organizations.length <= 1) {
		return (
			<>
				<Icon icon='Business' size='lg' className='text-primary' />
				<span className='fw-bold text-primary text-truncate'>
					{selectedOrganization.name}
				</span>
			</>
		);
	}

	return (
		<Menu>
			<Menu.Toggle className='d-flex align-items-center gap-2 border-0 bg-transparent p-0'>
				<Icon icon='Business' size='lg' className='text-primary' />
				<span className='fw-bold text-primary text-truncate'>
					{selectedOrganization.name}
				</span>
				<Icon icon='ExpandMore' size='lg' className='text-primary' />
			</Menu.Toggle>
			<Menu.Items>
				<Menu.Header>{t('switchOrganization')}</Menu.Header>
				{organizations.map((org) => (
					<Menu.Item
						key={org.id}
						icon={org.id === selectedOrganization.id ? 'Check' : 'Business'}
						active={org.id === selectedOrganization.id}
						onSelect={() => setSelectedOrganization(org)}>
						{org.name}
					</Menu.Item>
				))}
			</Menu.Items>
		</Menu>
	);
};

export default OrganizationSwitcher;
