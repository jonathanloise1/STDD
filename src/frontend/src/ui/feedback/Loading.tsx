import React, { FC } from 'react';
import classNames from 'classnames';
import { useTranslation } from 'react-i18next';

export interface ILoadingProps {
	size?: number | string;
	className?: string;
}

/** The loading screen of the application start: the shield logo, breathing slowly (frontend.md 6.8). */
export const Loading: FC<ILoadingProps> = ({ size = 180, className }) => {
	const { t } = useTranslation('common');
	const width = typeof size === 'number' ? `${size}px` : size;

	return (
		<div className={classNames('app-loading', className)}>
			<div className='app-loading__logo' style={{ width }}>
				<img src='/myapp-shield.svg' alt={t('loading')} />
			</div>
		</div>
	);
};
