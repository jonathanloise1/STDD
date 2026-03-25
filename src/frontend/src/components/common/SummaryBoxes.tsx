/**
 * SummaryBoxes - Colored KPI boxes for summary information
 * Provides consistent styling for summary metrics in detail pages
 */
import React, { ReactNode } from 'react';

export type SummaryBoxColor =
	| 'primary'
	| 'secondary'
	| 'success'
	| 'info'
	| 'warning'
	| 'danger'
	| 'dark';

export interface SummaryBoxItem {
	/** Label displayed above the value */
	label: string;
	/** The value to display */
	value: ReactNode;
	/** Color theme for the box */
	color?: SummaryBoxColor;
	/** If true, uses solid color instead of light variant */
	solid?: boolean;
	/** Optional icon to display */
	icon?: string;
}

interface SummaryBoxesProps {
	/** Array of summary items to display */
	items: SummaryBoxItem[];
	/** Number of columns per row (default: 4 on md+) */
	columns?: 2 | 3 | 4 | 6;
	/** Additional class name for the container */
	className?: string;
}

const SummaryBoxes: React.FC<SummaryBoxesProps> = ({ items, columns = 4, className = '' }) => {
	const colClass = `col-md-${12 / columns}`;

	return (
		<div className={`row g-3 ${className}`}>
			{items.map((item, index) => {
				const bgClass = item.solid
					? `bg-${item.color || 'primary'} text-white`
					: `bg-l10-${item.color || 'primary'}`;

				return (
					<div key={index} className={colClass}>
						<div className={`${bgClass} rounded p-3`}>
							<small className={`d-block ${item.solid ? '' : 'text-muted'} mb-1`}>
								{item.label}
							</small>
							<strong className={item.solid ? 'h4 mb-0' : ''}>{item.value}</strong>
						</div>
					</div>
				);
			})}
		</div>
	);
};

export default SummaryBoxes;
