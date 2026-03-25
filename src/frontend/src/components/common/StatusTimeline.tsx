/**
 * StatusTimeline - Visual timeline with progress bar and steps
 * Shows workflow progression for entities (milestones, timesheets, quotes)
 */
import React from 'react';
import { useTranslation } from 'react-i18next';

import Card, { CardBody, CardHeader, CardLabel, CardTitle } from '../bootstrap/Card';
import Icon from '../icon/Icon';

export interface TimelineStep {
	/** Unique key for the step */
	key: string;
	/** Display label */
	label: string;
	/** Icon to show when step is pending */
	icon: string;
	/** Color for the step when active/complete */
	color: 'primary' | 'secondary' | 'success' | 'info' | 'warning' | 'danger';
	/** Date when this step was completed (null if not completed) */
	completedAt?: string | null;
	/** If true, this is the current active step */
	isCurrent?: boolean;
	/** If true, this is a terminal/final state (cancelled, rejected, etc.) */
	isTerminal?: boolean;
}

interface StatusTimelineProps {
	/** Title for the card */
	title?: string;
	/** Array of timeline steps */
	steps: TimelineStep[];
	/** Current step index (0-based) */
	currentStepIndex: number;
	/** Date formatter function */
	formatDate?: (date: string) => string;
	/** Label for the progress indicator */
	progressLabel?: string;
}

const StatusTimeline: React.FC<StatusTimelineProps> = ({
	title,
	steps,
	currentStepIndex,
	formatDate = (d) => d,
	progressLabel,
}) => {
	const { t } = useTranslation('common');

	// Calculate progress percentage
	const hasTerminal = steps.some((s) => s.isTerminal);
	const progressPercent = hasTerminal
		? 100
		: Math.min(((currentStepIndex + 1) / steps.length) * 100, 100);

	const progressBarColor = hasTerminal ? 'secondary' : 'success';

	return (
		<Card className='mb-4'>
			<CardHeader>
				<CardLabel icon='Timeline' iconColor='secondary'>
					<CardTitle>{title || t('Timeline')}</CardTitle>
				</CardLabel>
			</CardHeader>
			<CardBody>
				{/* Progress Bar */}
				<div className='d-flex justify-content-between align-items-center mb-2'>
					<span className='text-muted small'>{progressLabel || t('Progress')}</span>
					<span className='fw-bold'>
						{hasTerminal ? '-' : `${Math.round(progressPercent)}%`}
					</span>
				</div>
				<div className='progress mb-4' style={{ height: '8px' }}>
					<div
						className={`progress-bar bg-${progressBarColor}`}
						role='progressbar'
						style={{ width: `${progressPercent}%` }}
					/>
				</div>

				{/* Timeline Steps */}
				<div className='d-flex flex-column gap-3'>
					{steps.map((step, index) => {
						const isCompleted = index < currentStepIndex || step.completedAt;
						const isCurrent = index === currentStepIndex;

						// Determine circle styling
						let circleClass = 'bg-light text-muted';
						let iconName = step.icon;

						if (step.isTerminal) {
							circleClass =
								step.color === 'danger'
									? 'bg-danger text-white'
									: 'bg-secondary text-white';
						} else if (isCompleted) {
							circleClass = `bg-${step.color} text-white`;
							iconName = 'Check';
						} else if (isCurrent) {
							circleClass = `border border-${step.color} border-2 text-${step.color}`;
						}

						return (
							<div
								key={step.key}
								className={`d-flex align-items-center ${!isCompleted && !isCurrent && !step.isTerminal ? 'opacity-50' : ''}`}>
								<div
									className={`rounded-circle d-flex align-items-center justify-content-center me-3 ${circleClass}`}
									style={{ width: 36, height: 36 }}>
									<Icon icon={iconName} />
								</div>
								<div className='flex-grow-1'>
									<div
										className={`fw-semibold ${step.isTerminal ? `text-${step.color}` : isCurrent ? `text-${step.color}` : ''}`}>
										{step.label}
									</div>
									{step.completedAt && (
										<small className='text-muted'>
											{formatDate(step.completedAt)}
										</small>
									)}
								</div>
								{isCurrent && !step.isTerminal && (
									<span className='badge bg-primary'>{t('Current')}</span>
								)}
							</div>
						);
					})}
				</div>
			</CardBody>
		</Card>
	);
};

export default StatusTimeline;
