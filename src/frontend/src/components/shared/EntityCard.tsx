/**
 * EntityCard
 * A reusable, gamification-styled card component for list items.
 * Replaces flat row-based list items with rich interactive cards
 * that include hover effects, status-colored accents, and responsive layout.
 */
import React from 'react';
import { motion } from 'framer-motion';
import Card, { CardBody } from '../bootstrap/Card';
import Icon from '../icon/Icon';
import useDarkMode from '../../hooks/useDarkMode';

export interface EntityCardStat {
	/** Unique id for the stat element */
	id: string;
	/** Label text */
	label: string;
	/** Display value */
	value: string | React.ReactNode;
	/** Optional text color class (e.g. 'text-success') */
	color?: string;
	/** Optional icon name */
	icon?: string;
}

export interface EntityCardAction {
	/** Unique id for the action button */
	id: string;
	/** Button icon */
	icon: string;
	/** Tooltip text */
	tooltip: string;
	/** Button color */
	color?: string;
	/** Click handler */
	onClick: (e: React.MouseEvent) => void;
	/** Whether to render the action */
	show?: boolean;
}

export interface EntityCardProps {
	/** Unique id for the card */
	id: string;
	/** Avatar/icon section: image URL or fallback icon */
	avatarUrl?: string | null;
	/** Fallback icon when no avatar URL */
	avatarIcon?: string;
	/** Whether avatar should be circular (for people) vs rounded (for orgs) */
	avatarCircle?: boolean;
	/** Primary title text */
	title: string;
	/** Subtitle text */
	subtitle?: string;
	/** Status badge component */
	badge?: React.ReactNode;
	/** Additional badges/icons next to main badge */
	extraBadges?: React.ReactNode;
	/** Optional description/content rendered between header and stats */
	description?: React.ReactNode;
	/** Array of stats to display */
	stats?: EntityCardStat[];
	/** Array of action buttons */
	actions?: EntityCardAction[];
	/** Accent color for the left border */
	accentColor?: string;
	/** Click handler for the whole card */
	onClick?: () => void;
	/** Animation delay for staggered rendering */
	animationDelay?: number;
}

const EntityCard: React.FC<EntityCardProps> = ({
	id,
	avatarUrl,
	avatarIcon = 'Business',
	avatarCircle = false,
	title,
	subtitle,
	badge,
	extraBadges,
	description,
	stats = [],
	actions = [],
	accentColor,
	onClick,
	animationDelay = 0,
}) => {
	const { darkModeStatus } = useDarkMode();
	const [imageError, setImageError] = React.useState(false);

	// Reset error state when avatarUrl changes
	React.useEffect(() => {
		setImageError(false);
	}, [avatarUrl]);

	const visibleActions = actions.filter((a) => a.show !== false);

	return (
		<motion.div
			id={id}
			initial={{ opacity: 0, y: 12 }}
			animate={{ opacity: 1, y: 0 }}
			transition={{ duration: 0.3, delay: animationDelay, ease: 'easeOut' }}
			whileHover={{ y: -3, transition: { duration: 0.2 } }}
			className='h-100'>
			<Card
				id={`${id}-card`}
				className='h-100 mb-0 border-0'
				style={{
					cursor: onClick ? 'pointer' : 'default',
					borderLeft: accentColor ? `4px solid ${accentColor}` : undefined,
					boxShadow: darkModeStatus
						? '0 2px 12px rgba(0,0,0,0.3)'
						: '0 2px 12px rgba(0,0,0,0.08)',
					transition: 'box-shadow 0.3s ease, transform 0.2s ease',
					background: darkModeStatus ? '#1f2128' : '#fff',
				}}
				onClick={onClick}>
				<CardBody id={`${id}-card-body`} className='p-3'>
					{/* Header: Avatar + Title + Badge */}
					<div
						id={`${id}-header`}
						className='d-flex align-items-start justify-content-between flex-wrap mb-3'>
						<div className='d-flex align-items-center flex-grow-1 me-2 mb-3'>
							{/* Avatar */}
							<div id={`${id}-avatar`} className='flex-shrink-0 me-3'>
								{avatarUrl && !imageError ? (
									<img
										src={avatarUrl}
										alt=''
										className={avatarCircle ? 'rounded-circle' : 'rounded'}
										style={{
											width: 44,
											height: 44,
											objectFit: 'cover',
											border: accentColor
												? `2px solid ${accentColor}`
												: '2px solid transparent',
										}}
										onError={() => setImageError(true)}
									/>
								) : (
									<div
										className={`${avatarCircle ? 'rounded-circle' : 'rounded'} d-flex align-items-center justify-content-center`}
										style={{
											width: 44,
											height: 44,
											background:
												'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
											boxShadow:
												'0 3px 10px rgba(99, 102, 241, 0.3)',
										}}>
										<Icon icon={avatarIcon} className='text-white' />
									</div>
								)}
							</div>
							{/* Title & Subtitle */}
							<div id={`${id}-title-section`} className='min-w-0'>
								<h6
									id={`${id}-title`}
									className='mb-0 fw-bold text-truncate text-wrap'
									style={{ fontSize: '0.95rem' }}>
									{title}
								</h6>
								{subtitle && (
									<small
										id={`${id}-subtitle`}
										className='text-muted text-truncate d-block'
										style={{ fontSize: '0.8rem' }}>
										{subtitle}
									</small>
								)}
							</div>
						</div>
						{/* Badge */}
						{(badge || extraBadges) && (
							<div
								id={`${id}-badge-section`}
								className='d-flex align-items-center gap-1 flex-shrink-0'>
								{badge}
								{extraBadges}
							</div>
						)}
					</div>

					{/* Description */}
					{description && (
						<div id={`${id}-description`} className='mb-3'>
							{description}
						</div>
					)}

					{/* Stats Row */}
					{stats.length > 0 && (
						<div id={`${id}-stats`} className='row g-2 mb-3'>
							{stats.map((stat) => (
								<div key={stat.id} className={`col-${Math.max(3, Math.floor(12 / Math.min(stats.length, 4)))}`}>
									<div
										id={stat.id}
										className='rounded-2 p-2 text-center'
										style={{
											background: darkModeStatus
												? 'rgba(255,255,255,0.05)'
												: 'rgba(0,0,0,0.03)',
										}}>
										<small
											className='text-muted d-block mb-1'
											style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '0.02em' }}>
											{stat.label}
										</small>
										<div
											className={`fw-bold ${stat.color || ''}`}
											style={{ fontSize: '0.9rem' }}>
											{stat.icon && (
												<Icon icon={stat.icon} size='sm' className='me-1' />
											)}
											{stat.value}
										</div>
									</div>
								</div>
							))}
						</div>
					)}

					{/* Actions Row */}
					{visibleActions.length > 0 && (
						<div
							id={`${id}-actions`}
							className='d-flex gap-2 justify-content-end pt-2'
							style={{
								borderTop: darkModeStatus
									? '1px solid rgba(255,255,255,0.08)'
									: '1px solid rgba(0,0,0,0.06)',
							}}>
							{visibleActions.map((action) => (
								<button
									key={action.id}
									id={action.id}
									title={action.tooltip}
								className={`btn btn-sm btn-light-${action.color || 'light'}`}
								style={{ borderRadius: 8, padding: '4px 10px' }}
								onClick={(e) => {
									e.stopPropagation();
									action.onClick(e);
								}}>
								<Icon icon={action.icon} size='lg' />
								</button>
							))}
						</div>
					)}
				</CardBody>
			</Card>
		</motion.div>
	);
};

export default EntityCard;
