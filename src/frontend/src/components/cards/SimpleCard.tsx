import React from 'react';
import { motion } from 'framer-motion';
import Card, { CardBody } from '../bootstrap/Card';
import Icon from '../icon/Icon';
import useDarkMode from '../../hooks/useDarkMode';

interface SimpleCardProps {
	icon: string;
	color: 'success' | 'danger' | 'info' | 'primary' | string;
	label: string;
	value: string;
	loading?: boolean;
}

// Gamified color configurations
const colorGradients: Record<string, { gradient: string; glow: string; emoji: string }> = {
	success: {
		gradient: 'linear-gradient(135deg, #10b981 0%, #34d399 100%)',
		glow: 'rgba(16, 185, 129, 0.4)',
		emoji: '💰',
	},
	danger: {
		gradient: 'linear-gradient(135deg, #ef4444 0%, #f87171 100%)',
		glow: 'rgba(239, 68, 68, 0.4)',
		emoji: '💸',
	},
	info: {
		gradient: 'linear-gradient(135deg, #06b6d4 0%, #22d3ee 100%)',
		glow: 'rgba(6, 182, 212, 0.4)',
		emoji: '🏦',
	},
	primary: {
		gradient: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
		glow: 'rgba(99, 102, 241, 0.4)',
		emoji: '📊',
	},
	warning: {
		gradient: 'linear-gradient(135deg, #f59e0b 0%, #fbbf24 100%)',
		glow: 'rgba(245, 158, 11, 0.4)',
		emoji: '⚡',
	},
};

const SimpleCard: React.FC<SimpleCardProps> = ({
	icon,
	color,
	label,
	value = 0,
	loading = false,
}) => {
	const { darkModeStatus: darkMode } = useDarkMode();
	const colorConfig = colorGradients[color] || colorGradients.primary;

	const cardStyle = {
		borderRadius: '16px',
		backgroundColor: darkMode ? '#16161d' : '#fff',
		border: `1px solid ${darkMode ? '#2a2a35' : '#e5e5e5'}`,
	};

	return (
		<motion.div
			initial={{ opacity: 0, y: 20 }}
			animate={{ opacity: 1, y: 0 }}
			whileHover={{ scale: 1.02, y: -4 }}
			transition={{ duration: 0.3 }}
			className={`h-100 ${loading ? 'opacity-50' : ''}`}>
			<Card className='h-100 border-0' style={cardStyle}>
				<CardBody className='d-flex align-items-center gap-3 p-4'>
					{/* Gamified icon badge */}
					<motion.div
						whileHover={{ rotate: 5, scale: 1.1 }}
						className='rounded-circle d-flex align-items-center justify-content-center'
						style={{
							width: '56px',
							height: '56px',
							background: colorConfig.gradient,
							boxShadow: `0 4px 15px ${colorConfig.glow}`,
						}}>
						<Icon icon={icon} size='lg' style={{ color: '#fff' }} />
					</motion.div>
					<div>
						<h6
							className={`fw-semibold mb-1 ${darkMode ? 'text-light opacity-75' : 'text-muted'}`}>
							{label}
						</h6>
						<motion.h4
							initial={{ scale: 0.8 }}
							animate={{ scale: 1 }}
							className='mb-0 fw-bold'
							style={{
								background: colorConfig.gradient,
								WebkitBackgroundClip: 'text',
								WebkitTextFillColor: 'transparent',
								backgroundClip: 'text',
							}}>
							{value || 0}
						</motion.h4>
					</div>
				</CardBody>
			</Card>
		</motion.div>
	);
};

export default SimpleCard;
