/**
 * DetailPageHeader - Standardized SubHeader for detail pages
 * Provides consistent navigation, title, status badge, and actions
 */
import React, { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../layout/SubHeader/SubHeader';
import Button from '../bootstrap/Button';
import Icon from '../icon/Icon';

interface DetailPageHeaderProps {
	/** Title displayed in the header */
	title: string;
	/** Optional subtitle (e.g., code, reference number) */
	subtitle?: string;
	/** Status badge component to display */
	statusBadge?: ReactNode;
	/** Action buttons to display on the right */
	actions?: ReactNode;
	/** Custom back navigation path. If not provided, uses navigate(-1) */
	backPath?: string;
	/** Custom back button label */
	backLabel?: string;
}

const DetailPageHeader: React.FC<DetailPageHeaderProps> = ({
	title,
	subtitle,
	statusBadge,
	actions,
	backPath,
	backLabel,
}) => {
	const navigate = useNavigate();
	const { t } = useTranslation('common');

	const handleBack = () => {
		if (backPath) {
			navigate(backPath);
		} else {
			navigate(-1);
		}
	};

	return (
		<SubHeader>
			<SubHeaderLeft className='d-flex align-items-center gap-2'>
				<Button color='link' className='p-0' onClick={handleBack}>
					<Icon icon='ArrowBack' size='lg' />
					{backLabel && <span className='ms-1'>{backLabel}</span>}
				</Button>
				<span className='h4 mb-0 ms-2'>{title}</span>
				{subtitle && <span className='text-muted'>({subtitle})</span>}
				{statusBadge}
			</SubHeaderLeft>
			{actions && (
				<SubHeaderRight className='d-flex align-items-center gap-2'>
					{actions}
				</SubHeaderRight>
			)}
		</SubHeader>
	);
};

export default DetailPageHeader;
