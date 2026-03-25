import React, { FC } from 'react';
import classNames from 'classnames';

export interface FilterChip {
	id: string;
	label: string;
	active?: boolean;
}

interface FilterChipsProps {
	chips: FilterChip[];
	onToggle: (chipId: string) => void;
	onRemove?: (chipId: string) => void;
	className?: string;
}

const FilterChips: FC<FilterChipsProps> = ({ chips, onToggle, onRemove, className }) => {
	return (
		<div
			className={classNames('filter-chips d-flex gap-2 overflow-auto pb-1', className)}
			style={{ scrollbarWidth: 'none' }}>
			{chips.map((chip) => (
				<button
					key={chip.id}
					type='button'
					className={classNames(
						'btn btn-sm rounded-pill d-flex align-items-center gap-1 text-nowrap',
						chip.active ? 'btn-primary' : 'btn-outline-secondary',
					)}
					onClick={() => onToggle(chip.id)}>
					{chip.label}
					{chip.active && onRemove && (
						<span
							className='material-icons'
							style={{ fontSize: 14 }}
							onClick={(e) => {
								e.stopPropagation();
								onRemove(chip.id);
							}}
							role='button'
							aria-label={`Remove ${chip.label}`}>
							close
						</span>
					)}
				</button>
			))}
		</div>
	);
};

export default FilterChips;
