import React, {
	Children,
	cloneElement,
	HTMLAttributes,
	isValidElement,
	ReactElement,
} from 'react';
import classNames from 'classnames';
import TagWrapper from '../../TagWrapper';
import Validation from './Validation';
import { IInputProps } from './Input';
import { IButtonProps } from '../Button';
import { ITextareaProps } from './Textarea';
import { IChecksProps } from './Checks';

interface IInputGroupTextProps extends HTMLAttributes<HTMLElement> {
	tag?: 'span' | 'div' | 'label';
	children: ReactElement<IChecksProps> | string;
	id?: string;
	className?: string;
	htmlFor?: string;
	ref?: React.Ref<HTMLDivElement>;
}
export const InputGroupText = ({ tag = 'span', id, className, children, htmlFor, ref, ...props }: IInputGroupTextProps) => {
	return (
			<TagWrapper
				tag={tag}
				ref={ref}
				id={id}
				className={classNames('input-group-text', className)}
				htmlFor={htmlFor}
				// eslint-disable-next-line react/jsx-props-no-spreading
				{...props}>
				{}
				{isValidElement(children) && children?.props?.type
					? cloneElement(children, { isFormCheckInput: true })
				: children}
		</TagWrapper>
	);
};
InputGroupText.displayName = 'InputGroupText';

type TInputGroupChildren =
	| ReactElement<IInputGroupTextProps>[]
	| ReactElement<IInputProps>[]
	| ReactElement<ITextareaProps>[]
	| ReactElement<IButtonProps>[];
interface IInputGroupProps extends HTMLAttributes<HTMLDivElement> {
	children: TInputGroupChildren;
	id?: string;
	className?: string;
	isWrap?: boolean;
	size?: 'sm' | 'lg';
	ref?: React.Ref<HTMLDivElement>;
}
const InputGroup = ({ id, className, children, isWrap = true, size, ref, ...props }: IInputGroupProps) => {
		let IS_VALID = false;
		let IS_TOUCHED = false;
		let INVALID_FEEDBACK;
		let VALID_FEEDBACK;
		let IS_TOOLTIP_FEEDBACK = false;

		const validClass = (child: TInputGroupChildren) => {
			for (let i = 0; i < child?.length; i += 1) {
				// @ts-ignore
				if (child[i].props.isValid) {
					IS_VALID = true;
				}
				// @ts-ignore
				if (child[i].props.isTouched) {
					IS_TOUCHED = true;
				}
				// @ts-ignore
				if (child[i].props.invalidFeedback) {
					// @ts-ignore
					INVALID_FEEDBACK = child[i].props.invalidFeedback;
				}
				// @ts-ignore
				if (child[i].props.validFeedback) {
					// @ts-ignore
					VALID_FEEDBACK = child[i].props.validFeedback;
				}
				// @ts-ignore
				if (child[i].props.isTooltipFeedback) {
					IS_TOOLTIP_FEEDBACK = true;
					break;
				}
			}
		};
		validClass(children);

		return (
			<div
				ref={ref}
				id={id}
				className={classNames(
					'input-group',
					{
						'flex-nowrap': !isWrap,
						[`input-group-${size}`]: size,
						'has-validation':
							(!IS_VALID && IS_TOUCHED && (INVALID_FEEDBACK || VALID_FEEDBACK)) ||
							(IS_VALID && VALID_FEEDBACK),
					},
					className,
				)}
				{...props}>
				{
					// @ts-ignore
					Children.map(children, (item, index) =>
						// @ts-ignore
						item?.props?.isValidMessage
							? // @ts-ignore

								cloneElement(item, { key: index, isValidMessage: false })
							: // @ts-ignore

								cloneElement(item, { key: index }),
					)
				}
				<Validation
					isTouched={IS_TOUCHED}
					validFeedback={VALID_FEEDBACK}
					invalidFeedback={INVALID_FEEDBACK}
					isTooltip={IS_TOOLTIP_FEEDBACK}
				/>
			</div>
		);
	};
InputGroup.displayName = 'InputGroup';

export default InputGroup;
