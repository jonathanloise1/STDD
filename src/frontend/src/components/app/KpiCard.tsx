import React, { FC } from 'react';
import classNames from 'classnames';
import Card, { CardBody } from '../bootstrap/Card';

interface KpiCardProps {
	value: number | string;
	label: string;
	icon?: string;
	color?: 'primary' | 'success' | 'warning' | 'danger' | 'info';
	onClick?: () => void;
	className?: string;
}

const KpiCard: FC<KpiCardProps> = ({
	value,
	label,
	icon,
	color = 'primary',
	onClick,
	className,
}) => {
	return (
		<Card
			className={classNames(
				'kpi-card shadow-sm h-100',
				onClick && 'cursor-pointer',
				className,
			)}
			onClick={onClick}
			style={{ borderTop: `3px solid var(--bs-${color})` }}>
			<CardBody className='text-center py-3'>
				{icon && (
					<span className={`material-icons text-${color} mb-1`} style={{ fontSize: 28 }}>
						{icon}
					</span>
				)}
				<div className='display-6 fw-bold'>{value}</div>
				<div className='text-muted small'>{label}</div>
			</CardBody>
		</Card>
	);
};

export default KpiCard;
