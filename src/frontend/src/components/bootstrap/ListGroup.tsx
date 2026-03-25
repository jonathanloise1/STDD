import React, { HTMLAttributes, ReactElement, ReactNode } from 'react';
import classNames from 'classnames';
import TagWrapper from '../TagWrapper';
import { TColor } from '../../type/color-type';

interface IListGroupItemProps extends HTMLAttributes<HTMLElement> {
	tag?: 'section' | 'div' | 'li' | 'a' | 'button' | 'label';
	children: ReactNode;
	className?: string;
	color?: TColor;
	isActive?: boolean;
	isDisable?: boolean;
	ref?: React.Ref<HTMLDivElement>;
}
export const ListGroupItem = ({ tag = 'li', children, className, color, isActive, isDisable, ref, ...props }: IListGroupItemProps) => {
	return (
			<TagWrapper
				ref={ref}
				tag={tag}
				className={classNames(
					'list-group-item',
					{
						'list-group-item-action': tag === 'a' || tag === 'button',
						[`list-group-item-${color}`]: color,
						active: isActive,
						disabled: isDisable,
					},
					className,
				)}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{children}
		</TagWrapper>
	);
};
ListGroupItem.displayName = 'ListGroupItem';

interface IListGroupProps extends HTMLAttributes<HTMLElement> {
	children: ReactElement<IListGroupItemProps> | ReactElement<IListGroupItemProps>[];
	className?: string;
	tag?: 'section' | 'div' | 'ol' | 'ul';
	isFlush?: boolean;
	isHorizontal?: boolean | 'sm' | 'md' | 'lg' | 'xl' | 'xxl';
	isNumbered?: boolean;
	ref?: React.Ref<HTMLDivElement>;
}
const ListGroup = ({ children, className, tag = 'ul', isHorizontal, isFlush, isNumbered, ref, ...props }: IListGroupProps) => {
	return (
			<TagWrapper
				ref={ref}
				tag={tag}
				className={classNames(
					'list-group',
					{
						'list-group-flush': isFlush,
					},
					{ 'list-group-numbered': isNumbered },
					{
						[`list-group-horizontal${
							typeof isHorizontal === 'string' ? `-${isHorizontal}` : ''
						}`]: isHorizontal,
					},
					className,
				)}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
			{children}
		</TagWrapper>
	);
};
ListGroup.displayName = 'ListGroup';

export default ListGroup;
