import React, { FC, ReactNode } from 'react';
import classNames from 'classnames';

interface EmptyStateProps {
	icon?: string;
	title: string;
	description?: string;
	action?: ReactNode;
	className?: string;
}

const EmptyState: FC<EmptyStateProps> = ({
	icon = 'inbox',
	title,
	description,
	action,
	className,
}) => {
	return (
		<div
			className={classNames(
				'empty-state d-flex flex-column align-items-center justify-content-center text-center py-5',
				className,
			)}>
			<span
				className='material-icons text-muted mb-3'
				style={{ fontSize: 64, opacity: 0.4 }}>
				{icon}
			</span>
			<h5 className='mb-2'>{title}</h5>
			{description && <p className='text-muted mb-3'>{description}</p>}
			{action}
		</div>
	);
};

export default EmptyState;
