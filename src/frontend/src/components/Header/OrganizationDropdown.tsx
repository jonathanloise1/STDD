import React, { useState, useRef, useEffect } from 'react';
import { useOrganization } from '../../contexts/organizationContext';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import Icon from '../icon/Icon';
import Dropdown, { DropdownToggle, DropdownMenu, DropdownItem } from '../bootstrap/Dropdown';

/**
 * Organization Switcher Component
 *
 * Displays the currently selected organization name in the header.
 * - Single organization: displays name only (no dropdown)
 * - Multiple organizations: shows dropdown to switch between organizations
 *
 * @userstory US-ORG-MULTI-01 - Multi-organization support
 *
 * NOTE: This component does NOT trigger fetchOrganizations().
 * The OrganizationContext handles auto-fetching for company users on mount.
 * This prevents duplicate API calls.
 */
const OrganizationDropdown = () => {
	const { t } = useTranslation(['common', 'organization']);
	const { selectedOrganization, organizations, isLoading, setSelectedOrganization } =
		useOrganization();
	const [isOpen, setIsOpen] = useState(false);
	const dropdownRef = useRef<HTMLDivElement>(null);

	// Close dropdown when clicking outside
	useEffect(() => {
		const handleClickOutside = (event: MouseEvent) => {
			if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
				setIsOpen(false);
			}
		};

		if (isOpen) {
			document.addEventListener('mousedown', handleClickOutside);
		}

		return () => {
			document.removeEventListener('mousedown', handleClickOutside);
		};
	}, [isOpen]);

	// Loading state
	if (isLoading) {
		return (
			<div className='d-flex align-items-center'>
				<Icon icon='Business' className='me-2' size='lg' color='primary' />
				<span className='fw-bold text-muted'>{t('common:loading')}...</span>
			</div>
		);
	}

	// No organization selected
	if (!selectedOrganization) {
		return (
			<div className='d-flex align-items-center'>
				<Icon icon='Business' className='me-2' size='lg' color='warning' />
				<span className='fw-bold text-warning'>
					{t('organization:noOrganizationSelected')}
				</span>
			</div>
		);
	}

	// Single organization - no dropdown needed
	if (organizations.length <= 1) {
		return (
			<div className='d-flex align-items-center'>
				<Icon icon='Business' className='me-2' size='lg' color='primary' />
				<span className='fw-bold text-primary'>{selectedOrganization.name}</span>
			</div>
		);
	}

	// Multiple organizations - show dropdown switcher
	const handleOrganizationSwitch = (orgId: string) => {
		const org = organizations.find((o) => o.id === orgId);
		if (org) {
			setSelectedOrganization(org);
			setIsOpen(false);
		}
	};

	return (
		<div ref={dropdownRef} className='position-relative'>
			<Dropdown isOpen={isOpen}>
				<DropdownToggle hasIcon={false}>
					<div
						className='d-flex align-items-center cursor-pointer'
						onClick={() => setIsOpen(!isOpen)}
						role='button'
						tabIndex={0}
						onKeyDown={(e) => {
							if (e.key === 'Enter' || e.key === ' ') {
								setIsOpen(!isOpen);
							}
						}}>
						<Icon icon='Business' className='me-2' size='lg' color='primary' />
						<span className='fw-bold text-primary me-2'>
							{selectedOrganization.name}
						</span>
						<Icon
							icon={isOpen ? 'ExpandLess' : 'ExpandMore'}
							size='lg'
							color='primary'
						/>
					</div>
				</DropdownToggle>
				<DropdownMenu isAlignmentEnd={false}>
					<DropdownItem isHeader>{t('organization:switchOrganization')}</DropdownItem>
					{organizations.map((org) => (
						<DropdownItem key={org.id} onClick={() => handleOrganizationSwitch(org.id)}>
							<div
								className={classNames('d-flex align-items-center', {
									active: org.id === selectedOrganization.id,
								})}>
								<Icon
									icon='Business'
									className='me-2'
									size='lg'
									color={
										org.id === selectedOrganization.id ? 'primary' : 'secondary'
									}
								/>
								<span>{org.name}</span>
								{org.id === selectedOrganization.id && (
									<Icon icon='Check' className='ms-auto' color='success' />
								)}
							</div>
						</DropdownItem>
					))}
				</DropdownMenu>
			</Dropdown>
		</div>
	);
};

export default OrganizationDropdown;
