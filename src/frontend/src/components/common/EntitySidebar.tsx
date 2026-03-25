/**
 * EntitySidebar - Sidebar card with entity information
 * Used to display counterpart info (talent/organization) in detail pages
 */
import React, { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';

import Card, { CardBody, CardHeader, CardLabel, CardTitle } from '../bootstrap/Card';
import Button from '../bootstrap/Button';
import Icon from '../icon/Icon';
import { TColor } from '../../type/color-type';

interface EntitySidebarProps {
	/** Title for the card header */
	title: string;
	/** Icon for the card header */
	icon?: string;
	/** Icon color */
	iconColor?: TColor;
	/** Avatar URL */
	avatarUrl?: string | null;
	/** Entity name */
	name: string;
	/** Initials to show if no avatar (defaults to first letters of name) */
	initials?: string;
	/** Additional details to show (VAT, address, etc.) */
	details?: Array<{
		icon: string;
		label: string;
		value: string;
	}>;
	/** Link to view full profile */
	profileLink?: {
		path: string;
		label: string;
	};
	/** Additional content to render at the bottom */
	children?: ReactNode;
}

const EntitySidebar: React.FC<EntitySidebarProps> = ({
	title,
	icon = 'Person',
	iconColor = 'info',
	avatarUrl,
	name,
	initials,
	details,
	profileLink,
	children,
}) => {
	const navigate = useNavigate();

	const displayInitials =
		initials ||
		name
			.split(' ')
			.map((n) => n[0])
			.join('')
			.substring(0, 2)
			.toUpperCase();

	return (
		<Card className='mb-4'>
			<CardHeader>
				<CardLabel icon={icon} iconColor={iconColor}>
					<CardTitle>{title}</CardTitle>
				</CardLabel>
			</CardHeader>
			<CardBody>
				<div className='d-flex align-items-center mb-3'>
					{avatarUrl ? (
						<img
							src={avatarUrl}
							alt={name}
							className='rounded-circle me-3'
							style={{ width: 64, height: 64, objectFit: 'cover' }}
						/>
					) : (
						<div
							className={`rounded-circle bg-${iconColor} d-flex align-items-center justify-content-center me-3`}
							style={{ width: 64, height: 64 }}>
							<span className='text-white fw-bold fs-4'>{displayInitials}</span>
						</div>
					)}
					<div>
						<h5 className='mb-1'>{name}</h5>
						{profileLink && (
							<Button
								color={iconColor}
								isOutline
								size='sm'
								className='py-1 px-2'
								onClick={() => navigate(profileLink.path)}>
								<Icon icon='Visibility' className='me-1' />
								{profileLink.label}
							</Button>
						)}
					</div>
				</div>

				{details && details.length > 0 && (
					<div className='border-top pt-3 mt-2'>
						{details.map((detail, index) => (
							<div key={index} className='d-flex align-items-center mb-2'>
								<Icon icon={detail.icon} size='lg' className='text-muted me-2' />
								<div>
									<div className='small text-muted'>{detail.label}</div>
									<div className='fw-semibold'>{detail.value}</div>
								</div>
							</div>
						))}
					</div>
				)}

				{children}
			</CardBody>
		</Card>
	);
};

export default EntitySidebar;
