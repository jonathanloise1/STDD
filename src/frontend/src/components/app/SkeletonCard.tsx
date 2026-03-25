import React, { FC } from 'react';
import classNames from 'classnames';
import Card, { CardBody } from '../bootstrap/Card';

interface SkeletonCardProps {
	lines?: number;
	className?: string;
}

const SkeletonCard: FC<SkeletonCardProps> = ({ lines = 3, className }) => {
	return (
		<Card className={classNames('skeleton-card shadow-sm', className)}>
			<CardBody className='d-flex align-items-center gap-3 py-3'>
				<div
					className='skeleton-shimmer rounded-3'
					style={{ width: 48, height: 48, minWidth: 48 }}
				/>
				<div className='flex-grow-1'>
					{Array.from({ length: lines }, (_, i) => (
						<div
							key={i}
							className='skeleton-shimmer rounded mb-2'
							style={{
								height: i === 0 ? 16 : 12,
								width: i === 0 ? '60%' : i === 1 ? '80%' : '40%',
							}}
						/>
					))}
				</div>
			</CardBody>
		</Card>
	);
};

export default SkeletonCard;
