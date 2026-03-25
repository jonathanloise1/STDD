import React from 'react';
import classNames from 'classnames';
import dayjs from 'dayjs';
import Checks from '../bootstrap/forms/Checks';
import Badge from '../bootstrap/Badge';
import Button from '../bootstrap/Button';
import Dropdown, { DropdownItem, DropdownMenu, DropdownToggle } from '../bootstrap/Dropdown';
import useDarkMode from '../../hooks/useDarkMode';
import { TColor } from '../../type/color-type';

export interface ITodoListItem {
	id?: string | number;
	status?: boolean;
	title?: string | number;
	date?: dayjs.ConfigType;
	badge?: {
		text?: string;
		color?: TColor;
	};
}

interface ITodoItemProps {
	list: ITodoListItem[];
	setList(...args: unknown[]): unknown;
	index: number;
	ref?: React.Ref<HTMLDivElement>;
}
export const TodoItem = ({ index, list, setList, ref, ...props }: ITodoItemProps) => {
		const itemData = list[index];

		const handleChange = (_index: number) => {
			const newTodos = [...list];
			newTodos[_index].status = !newTodos[_index].status;
			setList(newTodos);
		};

		const removeTodo = (_index: number) => {
			const newTodos = [...list];
			newTodos.splice(_index, 1);
			setList(newTodos);
		};

		const { themeStatus } = useDarkMode();

		return (
			<div ref={ref} className={classNames('todo-item')} {...props}>
				<div className='todo-bar'>
					<div
						className={classNames('h-100 w-100', 'rounded', {
							[`bg-${itemData?.badge?.color}`]: itemData?.badge,
						})}
					/>
				</div>
				<div className='todo-check'>
					<Checks
						checked={list[index].status}
						onChange={() => handleChange(index)}
						ariaLabel={itemData.title as string}
					/>
				</div>
				<div className='todo-content'>
					<div
						className={classNames('todo-title', {
							'text-decoration-line-through': list[index].status,
						})}>
						{itemData.title}
					</div>
					{itemData.date && (
						<div className='todo-subtitle text-muted small'>
							{dayjs(itemData.date).fromNow()}
						</div>
					)}
				</div>
				<div className='todo-extras'>
					{itemData?.badge && (
						<span className='me-2'>
							<Badge isLight color={itemData.badge.color}>
								{itemData.badge.text}
							</Badge>
						</span>
					)}
					<span>
						<Dropdown>
							<DropdownToggle hasIcon={false}>
								<Button
									color={themeStatus}
									icon='MoreHoriz'
									aria-label='More options'
								/>
							</DropdownToggle>
							<DropdownMenu isAlignmentEnd>
								<DropdownItem>
									<Button onClick={() => removeTodo(index)} icon='Delete'>
										Delete
									</Button>
								</DropdownItem>
							</DropdownMenu>
						</Dropdown>
					</span>
				</div>
			</div>
		);
};
TodoItem.displayName = 'TodoItem';

export interface ITodoProps {
	list: ITodoListItem[];
	className?: string;
	setList(...args: unknown[]): unknown;
	ref?: React.Ref<HTMLDivElement>;
}
const Todo = ({ className, list, setList, ref, ...props }: ITodoProps) => {
	return (
		<div ref={ref} className={classNames('todo', className)} {...props}>
				{}
				{list.map((i, index) => (
					<TodoItem key={i.id} index={index} list={list} setList={setList} />
				))}
		</div>
	);
};
Todo.displayName = 'Todo';

export default Todo;
