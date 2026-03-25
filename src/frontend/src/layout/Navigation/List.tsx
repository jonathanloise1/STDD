import React, { HTMLAttributes, ReactNode } from 'react';
import classNames from 'classnames';

interface IListProps extends HTMLAttributes<HTMLUListElement> {
	id?: string;
	children?: ReactNode;
	className?: string;
	ariaLabelledby?: string;
	parentId?: string;
	rootId?: string;
	horizontal?: boolean;
	ref?: React.Ref<HTMLUListElement>;
}
const List = ({ id, children, className, ariaLabelledby, parentId, rootId, horizontal, ref, ...props }: IListProps) => {
	return (
		<ul
			ref={ref}
			id={id}
			className={classNames('navigation', { 'navigation-menu': horizontal }, className)}
			aria-labelledby={ariaLabelledby}
			data-bs-parent={
				parentId === `${rootId}__${rootId}`
					? `#${rootId}`
					: (parentId && `#${parentId}`) || null
			}
			{...props}>
			{children}
		</ul>
	);
};
List.displayName = 'List';

export default List;
