import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import Card, { CardBody } from '../bootstrap/Card';
import Icon from '../icon/Icon';
import Button from '../bootstrap/Button';
import Modal, { ModalHeader, ModalTitle, ModalBody, ModalFooter } from '../bootstrap/Modal';
import FormGroup from '../bootstrap/forms/FormGroup';
import Input from '../bootstrap/forms/Input';
import useDarkMode from '../../hooks/useDarkMode';
import { formatCurrency } from '../../helpers/helpers';

interface YearlyGoalCardProps {
	// Data
	currentYear: number;
	earnedAmount: number; // Fatturato già incassato (fatture pagate)
	confirmedAmount: number; // Fatturato confermato (da offerte firmate)
	goalAmount: number | null; // Obiettivo impostato dall'utente
	// Callbacks
	onSetGoal: (amount: number) => void;
	// Optional customization
	title?: string;
	isLoading?: boolean;
}

const YearlyGoalCard: React.FC<YearlyGoalCardProps> = ({
	currentYear,
	earnedAmount,
	confirmedAmount,
	goalAmount,
	onSetGoal,
	title,
	isLoading = false,
}) => {
	const { t } = useTranslation(['dashboard', 'common']);
	const { darkModeStatus: darkMode } = useDarkMode();
	const [showModal, setShowModal] = useState(false);
	const [newGoal, setNewGoal] = useState<string>(goalAmount?.toString() || '');

	const hasGoal = goalAmount !== null && goalAmount > 0;

	// Total amount is earned (paid) + confirmed (from signed quotes, not yet paid)
	const totalAmount = earnedAmount + confirmedAmount;

	const progressPercentage = hasGoal
		? Math.min(100, Math.round((totalAmount / goalAmount) * 100))
		: 0;
	const remaining = hasGoal ? Math.max(0, goalAmount - totalAmount) : 0;

	// Calculate projection: at current pace, when will goal be reached?
	const getProjection = () => {
		if (!hasGoal || totalAmount === 0) return null;

		const today = new Date();
		const startOfYear = new Date(currentYear, 0, 1);
		const daysPassed = Math.floor(
			(today.getTime() - startOfYear.getTime()) / (1000 * 60 * 60 * 24),
		);
		const dailyRate = totalAmount / daysPassed;

		if (dailyRate <= 0) return null;

		const daysToGoal = remaining / dailyRate;
		const projectedDate = new Date(today.getTime() + daysToGoal * 24 * 60 * 60 * 1000);

		const monthNames = [
			'Gennaio',
			'Febbraio',
			'Marzo',
			'Aprile',
			'Maggio',
			'Giugno',
			'Luglio',
			'Agosto',
			'Settembre',
			'Ottobre',
			'Novembre',
			'Dicembre',
		];

		if (projectedDate.getFullYear() > currentYear) {
			return t('yearlyGoal.projectionNextYear');
		}

		return t('yearlyGoal.projectionMonth', { month: monthNames[projectedDate.getMonth()] });
	};

	const projection = getProjection();

	// Get progress color
	const getProgressColor = () => {
		if (progressPercentage >= 100) return '#10b981'; // emerald
		if (progressPercentage >= 75) return '#8b7cf7'; // violet
		if (progressPercentage >= 50) return '#6366f1'; // indigo
		if (progressPercentage >= 25) return '#f59e0b'; // amber
		return '#71717a'; // gray
	};

	const handleSaveGoal = () => {
		const amount = parseFloat(newGoal);
		if (!isNaN(amount) && amount > 0) {
			onSetGoal(amount);
			setShowModal(false);
		}
	};

	return (
		<>
			<Card
				className='border-0 mb-4'
				style={{
					borderRadius: '16px',
					backgroundColor: darkMode ? '#16161d' : '#fff',
					border: `1px solid ${darkMode ? '#2a2a35' : '#e5e5e5'}`,
				}}>
				<CardBody className='p-4'>
					{isLoading ? (
						<div className='text-center py-4'>
							<div className='spinner-border text-primary' role='status'>
								<span className='visually-hidden'>Loading...</span>
							</div>
						</div>
					) : hasGoal ? (
						/* Goal is set - show progress */
						<>
							<div className='d-flex flex-wrap align-items-center justify-content-between mb-3'>
								<div className='d-flex align-items-center'>
									<Icon icon='Flag' className='me-2 text-primary' size='lg' />
									<h5 className={`mb-0 fw-bold ${darkMode ? 'text-light' : ''}`}>
										{t('yearlyGoal.title', { year: currentYear })}
									</h5>
								</div>
								<Button
									color='light'
									isLight
									size='sm'
									onClick={() => setShowModal(true)}>
									<Icon icon='Edit' className='me-1' size='sm' />
									{t('yearlyGoal.edit')}
								</Button>
							</div>

							{/* Progress bar - horizontal, prominent */}
							<div className='mb-3'>
								<div
									className='position-relative rounded-pill overflow-hidden'
									style={{
										height: '24px',
										backgroundColor: darkMode ? '#1f1f28' : '#e5e5e5',
									}}>
									<div
										className='h-100 rounded-pill'
										style={{
											width: `${progressPercentage}%`,
											backgroundColor: getProgressColor(),
											transition: 'width 1s ease-out',
										}}
									/>
									<div
										className='position-absolute top-50 start-50 translate-middle fw-bold'
										style={{
											fontSize: '0.75rem',
											color:
												progressPercentage > 50
													? '#fff'
													: darkMode
														? '#fafafa'
														: '#333',
										}}>
										{progressPercentage}%
									</div>
								</div>
							</div>

							{/* Stats row */}
							<div className='row text-center g-2 mb-3'>
								<div className='col-4'>
									<small
										className={
											darkMode ? 'text-light opacity-50' : 'text-muted'
										}>
										{t('yearlyGoal.earned')}
									</small>
									<div
										className={`h5 mb-0 fw-bold ${darkMode ? 'text-light' : ''}`}>
										{formatCurrency(totalAmount)}
									</div>
								</div>
								<div className='col-4'>
									<small
										className={
											darkMode ? 'text-light opacity-50' : 'text-muted'
										}>
										{t('yearlyGoal.goal')}
									</small>
									<div
										className={`h5 mb-0 fw-bold ${darkMode ? 'text-light' : ''}`}>
										{formatCurrency(goalAmount)}
									</div>
								</div>
								<div className='col-4'>
									<small
										className={
											darkMode ? 'text-light opacity-50' : 'text-muted'
										}>
										{t('yearlyGoal.remaining')}
									</small>
									<div
										className={`h5 mb-0 fw-bold ${remaining > 0 ? 'text-warning' : 'text-success'}`}>
										{formatCurrency(remaining)}
									</div>
								</div>
							</div>

							{/* Projection */}
							{projection && remaining > 0 && (
								<div
									className='text-center p-2 rounded-2'
									style={{
										backgroundColor: darkMode ? '#1f1f28' : '#f0f9ff',
										border: `1px solid ${darkMode ? '#2a2a35' : '#bae6fd'}`,
									}}>
									<Icon icon='Timeline' className='me-2 text-info' size='sm' />
									<small
										className={
											darkMode ? 'text-light opacity-75' : 'text-info'
										}>
										{projection}
									</small>
								</div>
							)}

							{/* Goal reached celebration */}
							{remaining === 0 && (
								<div
									className='text-center p-3 rounded-2'
									style={{
										backgroundColor: darkMode
											? 'rgba(16,185,129,0.15)'
											: 'rgba(16,185,129,0.1)',
										border: '1px solid rgba(16,185,129,0.3)',
									}}>
									<span className='display-6 me-2'>🏆</span>
									<span className='text-success fw-bold'>
										{t('yearlyGoal.goalReached')}
									</span>
								</div>
							)}
						</>
					) : (
						/* No goal set - empty state */
						<div className='text-center py-4'>
							<div
								className='d-inline-flex align-items-center justify-content-center rounded-circle mb-3'
								style={{
									width: 80,
									height: 80,
									backgroundColor: darkMode ? '#1f1f28' : '#f3f4f6',
								}}>
								<Icon icon='Flag' size='2x' className='text-primary' />
							</div>
							<h5 className={`mb-2 ${darkMode ? 'text-light' : ''}`}>
								{t('yearlyGoal.setGoalTitle', { year: currentYear })}
							</h5>
							<p
								className={`mb-4 ${darkMode ? 'text-light opacity-50' : 'text-muted'}`}>
								{t('yearlyGoal.setGoalDescription')}
							</p>
							<Button color='primary' size='lg' onClick={() => setShowModal(true)}>
								{t('yearlyGoal.setGoalButton')} →
							</Button>
						</div>
					)}
				</CardBody>
			</Card>

			{/* Modal for setting goal */}
			<Modal isOpen={showModal} setIsOpen={setShowModal} size='sm' isCentered>
				<ModalHeader setIsOpen={setShowModal}>
					<ModalTitle id='yearlyGoalModalTitle'>
						{hasGoal ? t('yearlyGoal.updateGoal') : t('yearlyGoal.setGoal')}
					</ModalTitle>
				</ModalHeader>
				<ModalBody>
					<p className={darkMode ? 'text-light opacity-75' : 'text-muted'}>
						{t('yearlyGoal.setGoalTitle', { year: currentYear })}
					</p>
					<FormGroup id='yearlyGoal' label={t('yearlyGoal.goal')}>
						<Input
							type='number'
							placeholder='50000'
							value={newGoal}
							onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
								setNewGoal(e.target.value)
							}
							autoFocus
						/>
					</FormGroup>
					{confirmedAmount > 0 && (
						<small className={darkMode ? 'text-light opacity-50' : 'text-muted'}>
							{t('yearlyGoal.confirmedAmount', {
								amount: formatCurrency(confirmedAmount),
							})}
						</small>
					)}
				</ModalBody>
				<ModalFooter>
					<Button color='light' onClick={() => setShowModal(false)}>
						{t('common:Cancel')}
					</Button>
					<Button color='primary' onClick={handleSaveGoal}>
						{t('common:Save')}
					</Button>
				</ModalFooter>
			</Modal>
		</>
	);
};

export default YearlyGoalCard;
