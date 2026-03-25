import React from 'react';
import classNames from 'classnames';

interface LoadingProps {
	size?: number | string;
	color?: string;
	className?: string;
}

const Loading: React.FC<LoadingProps> = ({ size = 180, className }) => {
	const finalSize = typeof size === 'number' ? `${size}px` : size;

	return (
		<div
			className={classNames('d-flex flex-column align-items-center h-100 w-100', className)}
			style={{ paddingTop: '15vh' }}>
			<div
				className='loading-breathe'
				style={{
					width: finalSize,
					height: 'auto',
				}}>
				<img
					src='/myapp-shield.svg'
					alt='Loading...'
					style={{
						width: '100%',
						height: 'auto',
						display: 'block',
					}}
				/>
			</div>
			<style>{`
				.loading-breathe {
					animation: breathe 1.5s ease-in-out infinite;
				}

				@keyframes breathe {
					0%, 100% {
						transform: scale(0.95);
						opacity: 0.6;
					}
					50% {
						transform: scale(1.05);
						opacity: 1;
					}
				}

				@media (prefers-reduced-motion: reduce) {
					.loading-breathe {
						animation: none;
						opacity: 0.8;
					}
				}
			`}</style>
		</div>
	);
};

export default Loading;
