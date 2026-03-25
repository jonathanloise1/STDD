import React from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import Card, { CardBody } from '../bootstrap/Card';
import Icon from '../icon/Icon';
import useDarkMode from '../../hooks/useDarkMode';
import { TIcons } from '../../type/icons-type';

interface QuickStatCardProps {
	icon: TIcons;
	iconColor?: string;
	value: number | string;
	label: string;
	// Empty state props
	emptyMessage?: string;
	emptyHint?: string;
	actionLabel?: string;
	actionPath?: string;
	onAction?: () => void;
	// Gamification props
	emoji?: string;
	gradient?: string;
}

// Color mapping for gradients and glows
const colorGradients: Record<string, { gradient: string; glow: string }> = {
	primary: {
		gradient: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
		glow: 'rgba(99, 102, 241, 0.4)',
	},
	success: {
		gradient: 'linear-gradient(135deg, #10b981 0%, #34d399 100%)',
		glow: 'rgba(16, 185, 129, 0.4)',
	},
	warning: {
		gradient: 'linear-gradient(135deg, #f59e0b 0%, #fbbf24 100%)',
		glow: 'rgba(245, 158, 11, 0.4)',
	},
	info: {
		gradient: 'linear-gradient(135deg, #06b6d4 0%, #22d3ee 100%)',
		glow: 'rgba(6, 182, 212, 0.4)',
	},
	danger: {
		gradient: 'linear-gradient(135deg, #ef4444 0%, #f87171 100%)',
		glow: 'rgba(239, 68, 68, 0.4)',
	},
};

const QuickStatCard: React.FC<QuickStatCardProps> = ({
	icon,
	iconColor = 'primary',
	value,
	label,
	emptyMessage,
	emptyHint,
	actionLabel,
	actionPath,
	onAction,
	emoji,
	gradient,
}) => {
	const { t } = useTranslation(['dashboard', 'common']);
	const navigate = useNavigate();
	const { darkModeStatus: darkMode } = useDarkMode();

	const isEmpty = value === 0 || value === '0';
	const showEmptyState = isEmpty && (emptyMessage || emptyHint);
	const colors = colorGradients[iconColor] || colorGradients.primary;
	const finalGradient = gradient || colors.gradient;

	const handleAction = () => {
		if (actionPath) {
			navigate(actionPath);
		} else if (onAction) {
			onAction();
		}
	};

	// Card styling
	const cardStyle = {
		borderRadius: '16px',
		backgroundColor: darkMode ? '#16161d' : '#fff',
		border: `1px solid ${darkMode ? '#2a2a35' : '#e5e5e5'}`,
		cursor: actionPath || onAction ? 'pointer' : 'default',
		transition: 'all 0.3s ease',
	};

	return (
		<motion.div
			whileHover={actionPath || onAction ? { scale: 1.02, y: -4 } : {}}
			transition={{ type: 'spring', stiffness: 300 }}>
			<Card
				className='h-100 border-0'
				style={cardStyle}
				onClick={actionPath || onAction ? handleAction : undefined}>
				<CardBody className='p-4 text-center'>
					{/* Gamified icon badge */}
					<motion.div
						initial={{ scale: 0, rotate: -10 }}
						animate={{ scale: 1, rotate: 0 }}
						transition={{ type: 'spring', stiffness: 200 }}
						className='mx-auto mb-3 d-flex align-items-center justify-content-center rounded-circle'
						style={{
							width: 64,
							height: 64,
							background: isEmpty
								? darkMode
									? 'rgba(255,255,255,0.05)'
									: 'rgba(0,0,0,0.05)'
								: finalGradient,
							boxShadow: isEmpty ? 'none' : `0 4px 20px ${colors.glow}`,
						}}>
						{emoji ? (
							<span style={{ fontSize: '1.75rem' }}>{emoji}</span>
						) : (
							<Icon
								icon={icon}
								className={isEmpty ? 'text-muted' : 'text-white'}
								size='2x'
							/>
						)}
					</motion.div>

					{/* Value with animation */}
					<motion.h2
						initial={{ opacity: 0, y: 10 }}
						animate={{ opacity: 1, y: 0 }}
						transition={{ delay: 0.1 }}
						className={`mb-1 fw-bold ${darkMode ? 'text-light' : ''} ${isEmpty ? 'opacity-50' : ''}`}
						style={{ fontSize: '2rem' }}>
						{value}
					</motion.h2>

					{/* Label */}
					<p
						className={`mb-2 ${darkMode ? 'text-light opacity-75' : 'text-muted'}`}
						style={{ fontSize: '0.9rem' }}>
						{label}
					</p>

					{/* Empty state hint */}
					{showEmptyState && (
						<motion.div
							initial={{ opacity: 0 }}
							animate={{ opacity: 1 }}
							transition={{ delay: 0.2 }}
							className='mt-3'>
							{emptyMessage && (
								<small
									className={`d-block ${darkMode ? 'text-light opacity-50' : 'text-muted'}`}>
									{emptyMessage}
								</small>
							)}
							{emptyHint && (
								<small className='d-block text-primary mt-1'>{emptyHint}</small>
							)}
						</motion.div>
					)}

					{/* CTA for empty state */}
					{isEmpty && actionLabel && (actionPath || onAction) && (
						<motion.div
							initial={{ opacity: 0 }}
							animate={{ opacity: 1 }}
							transition={{ delay: 0.3 }}
							className='mt-3'>
							<span
								className='badge rounded-pill px-3 py-2'
								style={{
									background: finalGradient,
									color: '#fff',
									fontSize: '0.8rem',
								}}>
								{actionLabel} →
							</span>
						</motion.div>
					)}
				</CardBody>
			</Card>
		</motion.div>
	);
};

export default QuickStatCard;
