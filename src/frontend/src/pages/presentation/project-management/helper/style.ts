import { CSSProperties } from 'react';
import { DraggableStyle } from '@hello-pangea/dnd';

export const getItemStyle = (
	isDragging: boolean,
	draggableStyle: DraggableStyle | undefined,
): CSSProperties => ({
	// @ts-ignore
	'--bs-border-style': isDragging ? 'dashed' : 'inherit',
	userSelect: 'none',

	// styles we need to apply on draggables
	...draggableStyle,
});

export const getListStyle = (isDraggingOver: boolean): CSSProperties => ({
	background: isDraggingOver ? 'rgba(0,0,0,0.1)' : undefined,
});
