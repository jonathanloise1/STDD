import React from 'react';
import { useTranslation } from 'react-i18next';

/**
 * Status options for projects
 */
export const PROJECT_STATUS_OPTIONS = ['Active', 'Completed', 'Archived', 'Suspended'] as const;
export type ProjectStatusType = (typeof PROJECT_STATUS_OPTIONS)[number];

interface ProjectStatusBadgeProps {
	/**
	 * Status value to display
	 */
	statusName: string;
}

/**
 * Badge component to consistently display project status with appropriate styling
 */
const ProjectStatusBadge: React.FC<ProjectStatusBadgeProps> = ({ statusName }) => {
	const { t } = useTranslation(['projects', 'common']);

	let bgColor = 'rgba(255, 184, 0, 0.2)';
	let textColor = '#FF9500';

	switch (statusName?.toLowerCase()) {
		case 'active':
			bgColor = 'rgba(40, 199, 111, 0.2)';
			textColor = '#28c76f';
			break;
		case 'completed':
			bgColor = 'rgba(0, 207, 232, 0.2)';
			textColor = '#00cfe8';
			break;
		case 'archived':
			bgColor = 'rgba(105, 108, 255, 0.2)';
			textColor = '#696cff';
			break;
		case 'suspended':
			bgColor = 'rgba(234, 84, 85, 0.2)';
			textColor = '#ea5455';
			break;
		// Default is yellow for unknown status
	}

	return (
		<span
			className='badge px-2 py-1 fs-6'
			style={{
				background: bgColor,
				color: textColor,
				borderRadius: '30px',
			}}>
			{t(`projectStatus.${statusName}`)}
		</span>
	);
};

export default ProjectStatusBadge;
