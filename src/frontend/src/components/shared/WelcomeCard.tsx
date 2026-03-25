import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import Card, { CardBody } from '../bootstrap/Card';
import Icon from '../icon/Icon';
import useDarkMode from '../../hooks/useDarkMode';

interface WelcomeCardProps {
	userName: string;
	userType: 'company' | 'freelance';
	stats?: {
		label: string;
		value: string | number;
		icon: string;
		color: string;
	}[];
}

const WelcomeCard: React.FC<WelcomeCardProps> = ({ userName, userType, stats = [] }) => {
	const { t } = useTranslation(['dashboard', 'common']);
	const { darkModeStatus: darkMode } = useDarkMode();
	const [currentTime, setCurrentTime] = useState(new Date());

	// Update time every second
	useEffect(() => {
		const timer = setInterval(() => {
			setCurrentTime(new Date());
		}, 1000);
		return () => clearInterval(timer);
	}, []);

	// Format time with seconds
	const formatTime = (date: Date) => {
		return date.toLocaleTimeString('it-IT', {
			hour: '2-digit',
			minute: '2-digit',
			second: '2-digit',
		});
	};

	// Format date
	const formatDate = (date: Date) => {
		return date.toLocaleDateString('it-IT', {
			weekday: 'long',
			day: 'numeric',
			month: 'long',
			year: 'numeric',
		});
	};

	// Get greeting based on time
	const getGreeting = () => {
		const hour = currentTime.getHours();
		if (hour < 12) return t('welcome.greetings.morning');
		if (hour < 18) return t('welcome.greetings.afternoon');
		return t('welcome.greetings.evening');
	};

	// Get motivational message based on time and day
	const getMotivationalMessage = () => {
		const day = currentTime.getDay();
		const hour = currentTime.getHours();

		if (day === 0 || day === 6) {
			return t('welcome.messages.weekend');
		}
		if (hour < 10) {
			return t('welcome.messages.earlyMorning');
		}
		if (hour < 14) {
			return t('welcome.messages.productive');
		}
		if (hour < 18) {
			return t('welcome.messages.afternoon');
		}
		return t('welcome.messages.evening');
	};

	// Gradient colors based on user type
	const gradientColors =
		userType === 'company'
			? 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 50%, #a855f7 100%)'
			: 'linear-gradient(135deg, #10b981 0%, #14b8a6 50%, #06b6d4 100%)';

	const glowColor =
		userType === 'company' ? 'rgba(99, 102, 241, 0.3)' : 'rgba(16, 185, 129, 0.3)';

	return (
		<motion.div
			initial={{ opacity: 0, y: -20 }}
			animate={{ opacity: 1, y: 0 }}
			transition={{ duration: 0.5 }}>
			<Card
				className='border-0 mb-4 overflow-hidden'
				style={{
					borderRadius: '20px',
					background: darkMode
						? 'linear-gradient(135deg, #1f1f28 0%, #16161d 100%)'
						: 'linear-gradient(135deg, #ffffff 0%, #f8fafc 100%)',
					border: `1px solid ${darkMode ? '#2a2a35' : '#e5e7eb'}`,
					boxShadow: darkMode
						? `0 8px 32px rgba(0,0,0,0.4), 0 0 0 1px ${glowColor}`
						: '0 8px 32px rgba(0,0,0,0.08)',
				}}>
				<CardBody className='p-4'>
					<div className='row align-items-center'>
						{/* Left section: Greeting */}
						<div className='col-lg-6 col-md-7'>
							<div className='d-flex align-items-start'>
								{/* Animated emoji */}
								<motion.div
									animate={{
										y: [0, -5, 0],
										rotate: [0, 5, -5, 0],
									}}
									transition={{
										duration: 2,
										repeat: Infinity,
										ease: 'easeInOut',
									}}
									className='me-3'
									style={{ fontSize: '3rem' }}>
									{currentTime.getHours() < 12
										? '☀️'
										: currentTime.getHours() < 18
											? '🌤️'
											: '🌙'}
								</motion.div>
								<div>
									<motion.h2
										initial={{ opacity: 0, x: -20 }}
										animate={{ opacity: 1, x: 0 }}
										transition={{ delay: 0.2 }}
										className={`mb-1 fw-bold ${darkMode ? 'text-light' : ''}`}
										style={{ fontSize: '1.75rem' }}>
										{getGreeting()},{' '}
										<span
											style={{
												background: gradientColors,
												WebkitBackgroundClip: 'text',
												WebkitTextFillColor: 'transparent',
												backgroundClip: 'text',
											}}>
											{userName || t('welcome.user')}
										</span>
										! 👋
									</motion.h2>
									<motion.p
										initial={{ opacity: 0 }}
										animate={{ opacity: 1 }}
										transition={{ delay: 0.4 }}
										className={`mb-2 ${darkMode ? 'text-light opacity-75' : 'text-muted'}`}
										style={{ fontSize: '1rem' }}>
										{getMotivationalMessage()}
									</motion.p>
									<motion.div
										initial={{ opacity: 0 }}
										animate={{ opacity: 1 }}
										transition={{ delay: 0.5 }}
										className='d-flex align-items-center gap-3 mt-3'>
										<div
											className='d-flex align-items-center px-3 py-2 rounded-pill'
											style={{
												background: darkMode
													? 'rgba(255,255,255,0.08)'
													: 'rgba(0,0,0,0.05)',
											}}>
											<Icon icon='CalendarToday' className='me-2 text-info' />
											<span
												className={`small ${darkMode ? 'text-light' : ''}`}
												style={{ textTransform: 'capitalize' }}>
												{formatDate(currentTime)}
											</span>
										</div>
									</motion.div>
								</div>
							</div>
						</div>

						{/* Right section: Clock and stats */}
						<div className='col-lg-6 col-md-5 mt-4 mt-md-0'>
							<div className='d-flex flex-column align-items-end'>
								{/* Live Clock */}
								<motion.div
									initial={{ scale: 0.8, opacity: 0 }}
									animate={{ scale: 1, opacity: 1 }}
									transition={{ delay: 0.3, type: 'spring' }}
									className='text-end mb-3'>
									<div
										className='fw-bold'
										style={{
											fontSize: '2.5rem',
											fontFamily: 'monospace',
											background: gradientColors,
											WebkitBackgroundClip: 'text',
											WebkitTextFillColor: 'transparent',
											backgroundClip: 'text',
											letterSpacing: '2px',
										}}>
										{formatTime(currentTime)}
									</div>
								</motion.div>

								{/* Quick Stats */}
								{stats.length > 0 && (
									<div className='d-flex gap-3'>
										{stats.map((stat, index) => (
											<motion.div
												key={stat.label}
												initial={{ opacity: 0, y: 10 }}
												animate={{ opacity: 1, y: 0 }}
												transition={{ delay: 0.5 + index * 0.1 }}
												whileHover={{ scale: 1.05, y: -2 }}
												className='d-flex align-items-center px-3 py-2 rounded-3'
												style={{
													background: darkMode
														? 'rgba(255,255,255,0.08)'
														: 'rgba(0,0,0,0.03)',
													border: `1px solid ${darkMode ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.05)'}`,
													cursor: 'default',
												}}>
												<Icon
													icon={stat.icon}
													className={`me-2 text-${stat.color}`}
												/>
												<div>
													<div
														className={`fw-bold ${darkMode ? 'text-light' : ''}`}
														style={{
															fontSize: '1.1rem',
															lineHeight: 1,
														}}>
														{stat.value}
													</div>
													<small
														className={
															darkMode
																? 'text-light opacity-50'
																: 'text-muted'
														}
														style={{ fontSize: '0.7rem' }}>
														{stat.label}
													</small>
												</div>
											</motion.div>
										))}
									</div>
								)}
							</div>
						</div>
					</div>
				</CardBody>
			</Card>
		</motion.div>
	);
};

export default WelcomeCard;
