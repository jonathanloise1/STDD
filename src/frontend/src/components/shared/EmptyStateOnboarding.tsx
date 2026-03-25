/**
 * EmptyStateOnboarding
 * A reusable empty state component with onboarding guidance.
 * Used when a list page has no items to show.
 * Also used inside the PageHelpGuide modal.
 */
import React from 'react';
import { motion } from 'framer-motion';
import Button from '../bootstrap/Button';
import useDarkMode from '../../hooks/useDarkMode';

export interface EmptyStateOnboardingProps {
	/** Unique id for the container */
	id: string;
	/** Large emoji displayed at the top */
	emoji: string;
	/** Main title message */
	title: string;
	/** Descriptive paragraph explaining the page */
	description: string;
	/** Optional CTA button label */
	ctaLabel?: string;
	/** Optional CTA navigation target (react-router path) */
	ctaTo?: string;
	/** Optional CTA click handler (alternative to ctaTo) */
	ctaOnClick?: () => void;
	/** Whether to animate the container on mount */
	animateIn?: boolean;
	/** Compact mode for use inside modals */
	compact?: boolean;
}

const EmptyStateOnboarding: React.FC<EmptyStateOnboardingProps> = ({
	id,
	emoji,
	title,
	description,
	ctaLabel,
	ctaTo,
	ctaOnClick,
	animateIn = true,
	compact = false,
}) => {
	const { darkModeStatus } = useDarkMode();

	const content = (
		<div
			id={id}
			className={`text-center ${compact ? 'py-3' : 'py-5'}`}
			style={{ maxWidth: compact ? '100%' : 520, margin: '0 auto' }}>
			<div
				id={`${id}-emoji`}
				style={{
					fontSize: compact ? '3rem' : '4rem',
					marginBottom: compact ? '0.75rem' : '1rem',
					filter: 'drop-shadow(0 4px 12px rgba(0,0,0,0.15))',
				}}>
				{emoji}
			</div>
			<h4
				id={`${id}-title`}
				className={`fw-bold mb-3 ${darkModeStatus ? 'text-light' : 'text-dark'}`}
				style={{ fontSize: compact ? '1.1rem' : '1.35rem' }}>
				{title}
			</h4>
			<p
				id={`${id}-description`}
				className={`mb-4 ${darkModeStatus ? 'text-light opacity-75' : 'text-muted'}`}
				style={{
					fontSize: compact ? '0.9rem' : '1rem',
					lineHeight: 1.6,
				}}>
				{description}
			</p>
			{ctaLabel && (ctaTo || ctaOnClick) && (
				<Button
					id={`${id}-cta-btn`}
					color='primary'
					size={compact ? 'sm' : 'lg'}
					className='fw-semibold px-4'
					icon='ArrowForward'
					{...(ctaTo ? { tag: 'a', to: ctaTo } : { onClick: ctaOnClick })}
					style={{
						boxShadow: '0 4px 15px rgba(99, 102, 241, 0.3)',
					}}>
					{ctaLabel}
				</Button>
			)}
		</div>
	);

	if (animateIn) {
		return (
			<motion.div
				initial={{ opacity: 0, y: 20 }}
				animate={{ opacity: 1, y: 0 }}
				transition={{ duration: 0.5, ease: 'easeOut' }}>
				{content}
			</motion.div>
		);
	}

	return content;
};

export default EmptyStateOnboarding;
