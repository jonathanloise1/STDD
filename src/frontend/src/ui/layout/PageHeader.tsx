import React, { FC, ReactNode } from 'react';
import { Icon } from '../icons/Icon';
import type { IconName } from '../icons/registry';
import { IconButton } from '../components/IconButton';

export interface IPageHeaderProps {
	/** One text node: tests read it as the page heading. */
	title: ReactNode;
	back?: { label: string; onClick: () => void };
	icon?: IconName;
	/** Content next to the title, for example a badge. */
	extra?: ReactNode;
	/** Page actions, on the right. */
	actions?: ReactNode;
}

/** The toolbar of a page: the `h1`, an optional back button and the page actions (frontend.md 6.5). */
export const PageHeader: FC<IPageHeaderProps> = ({ title, back, icon, extra, actions }) => (
	<div className='page-header'>
		<div className='page-header__left'>
			{back && (
				<IconButton
					variant='link'
					icon='ArrowBack'
					label={back.label}
					onClick={back.onClick}
				/>
			)}
			{icon && <Icon icon={icon} size='lg' className='text-primary' />}
			<h1 className='h4 fw-bold mb-0'>{title}</h1>
			{extra}
		</div>
		{actions && <div className='page-header__actions'>{actions}</div>}
	</div>
);
