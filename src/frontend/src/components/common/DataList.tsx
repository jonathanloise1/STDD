/**
 * DataList — Responsive list component for list pages.
 *
 * - Desktop (≥ lg / 992 px): renders the full `<table>` via `renderTable`.
 * - Mobile  (< lg):          renders compact cards via `renderCard`.
 *
 * This component replaces the `<CardBody>` that wraps the table.
 * Place it directly inside a `<Card>` alongside `<CardHeader>`.
 *
 * @example
 * <Card>
 *   <CardHeader>…</CardHeader>
 *   <DataList
 *     items={items}
 *     keyExtractor={(i) => i.id}
 *     renderTable={() => <table className='table table-modern …'>…</table>}
 *     renderCard={(item) => <div>…</div>}
 *     onCardClick={(item) => navigate(`/${item.id}`)}
 *   />
 * </Card>
 */
import React from 'react';
import { useWindowSize } from 'react-use';
import { CardBody } from '../bootstrap/Card';

const LG_BREAKPOINT = 992;

interface DataListProps<T> {
	/** Data items to render */
	items: T[];
	/** Unique key extractor for each item */
	keyExtractor: (item: T) => string;
	/** Desktop view: returns the full <table> element */
	renderTable: () => React.ReactNode;
	/** Mobile view: returns card content for a single item */
	renderCard: (item: T) => React.ReactNode;
	/** Optional click handler for mobile card items */
	onCardClick?: (item: T) => void;
}

/**
 * Generic responsive DataList component.
 * Renders a table on desktop and card list on mobile.
 */
function DataList<T>({
	items,
	keyExtractor,
	renderTable,
	renderCard,
	onCardClick,
}: DataListProps<T>) {
	const { width } = useWindowSize();
	const isMobile = width < LG_BREAKPOINT;

	// Desktop: render table inside flush CardBody
	if (!isMobile) {
		return <CardBody className='p-0'>{renderTable()}</CardBody>;
	}

	// Mobile: render card list
	return (
		<CardBody className='pb-0'>
			{items.map((item) => (
				<div
					key={keyExtractor(item)}
					className={`card shadow-sm mb-3${onCardClick ? ' cursor-pointer' : ''}`}
					onClick={onCardClick ? () => onCardClick(item) : undefined}
					role={onCardClick ? 'button' : undefined}
					tabIndex={onCardClick ? 0 : undefined}
					onKeyDown={
						onCardClick
							? (e) => {
									if (e.key === 'Enter' || e.key === ' ') {
										e.preventDefault();
										onCardClick(item);
									}
								}
							: undefined
					}>
					<div className='card-body py-2 px-3'>{renderCard(item)}</div>
				</div>
			))}
		</CardBody>
	);
}

export default DataList;
