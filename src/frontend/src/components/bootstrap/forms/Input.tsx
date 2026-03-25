import React, { HTMLAttributes } from 'react';
import { NumericFormat } from 'react-number-format';
import { IMaskInput } from 'react-imask';
import classNames from 'classnames';
import Portal from '../../../layout/Portal/Portal';
import Validation from './Validation';
import { TInputTypes } from '../../../type/input-type';

export interface IInputProps extends HTMLAttributes<HTMLInputElement> {
	component?: 'NumberFormat' | 'InputMask';
	type?: TInputTypes;
	id?: string;
	name?: string;
	size?: 'lg' | 'sm' | null;
	className?: string;
	required?: boolean;
	placeholder?: string;
	title?: string;
	list?: string[];
	autoComplete?: string;
	disabled?: boolean;
	multiple?: boolean;
	readOnly?: boolean | 'plaintext';
	ariaDescribedby?: string;
	ariaLabelledby?: string;
	ariaLabel?: string;
	value?: string | number | readonly string[] | undefined;
	min?: number;
	max?: number;
	step?: number;
	isTouched?: boolean;
	isValid?: boolean;
	invalidFeedback?: string;
	validFeedback?: string;
	isValidMessage?: boolean;
	isTooltipFeedback?: boolean;
	onBlur?(...args: unknown[]): unknown;
	onChange?(...args: unknown[]): unknown;
	onFocus?(...args: unknown[]): unknown;
	onInput?(...args: unknown[]): unknown;
	onInvalid?(...args: unknown[]): unknown;
	onSelect?(...args: unknown[]): unknown;
	/**
	 * Mask string. Format characters are:
	 * * `9`: `0-9`
	 * * `a`: `A-Z, a-z`
	 * * `\*`: `A-Z, a-z, 0-9`
	 *
	 * Any character can be escaped with backslash, which usually will appear as double backslash in JS strings.
	 * For example, German phone mask with unremoveable prefix +49 will look like `mask="+4\\9 99 999 99"` or `mask={"+4\\\\9 99 999 99"}`
	 */
	mask?: string;
	format?: string;
	accept?: string;
	maxLength?: number;
	ref?: React.Ref<HTMLInputElement>;
}
const Input = ({
		type = 'text',
		id,
		name,
		className,
		required,
		placeholder,
		autoComplete,
		ariaDescribedby,
		ariaLabelledby,
		ariaLabel,
		list,
		title,
		size,
		disabled,
		readOnly,
		multiple,
		value,
		min,
		max,
		step,
		isValid,
		isTouched,
		invalidFeedback,
		validFeedback,
		isValidMessage = true,
		isTooltipFeedback,
		onBlur,
		onChange,
		onFocus,
		onInput,
		onInvalid,
		onSelect,
		component,
		// InputMask & NumberFormat props
		mask,
		// NumberFormat props
		format,
		ref,
		...props
	}: IInputProps) => {
		const PROPS = {
			id,
			name: name === null ? id : name,
			type: !list ? type : undefined,
			className: classNames(
				{
					'form-control': readOnly !== 'plaintext' && type !== 'range',
					'form-range': type === 'range',
					'form-control-plaintext': readOnly === 'plaintext',
					'form-control-color': type === 'color',
					[`form-control-${size}`]: size,
					'is-invalid': !isValid && isTouched && invalidFeedback,
					'is-valid': !isValid && isTouched && !invalidFeedback,
				},
				className,
			),
			required,
			placeholder,
			title,
			list: list ? `${id}-list` : undefined,
			disabled,
			readOnly: !!readOnly,
			multiple,
			autoComplete,
			'aria-describedby': ariaDescribedby,
			'aria-label': ariaLabel,
			'aria-labelledby': ariaLabelledby,
			value: typeof value === 'undefined' && type !== 'file' ? '' : value,
			min,
			max,
			step,
			onBlur,
			onChange: readOnly ? undefined : onChange,
			onFocus,
			onInput,
			onInvalid,
			onSelect,
			...props,
		};
		const NUMBER_FORMAT_PROPS = {
			mask,
			format,
			onBlur: () => onBlur,
			onChange: readOnly ? null : () => onChange,
			onFocus: () => onFocus,
			onInput: () => onInput,
			onInvalid: () => onInvalid,
			onSelect: () => onSelect,
		};
		const MASK_PROPS = { mask };

		const LIST = list && (
			<Portal>
				<datalist id={`${id}-list`}>
					{list.map((option) => (
						<option key={option} aria-labelledby={option} value={option} />
					))}
				</datalist>
			</Portal>
		);

		const VALIDATION = isValidMessage && (
			<Validation
				isTouched={isTouched}
				invalidFeedback={invalidFeedback}
				validFeedback={validFeedback}
				isTooltip={isTooltipFeedback}
			/>
		);

		if (component === 'NumberFormat' || format) {
			return (
				<>
					{/* eslint-disable-next-line react/jsx-props-no-spreading */} {/* @ts-ignore */}
					<NumericFormat ref={ref} {...PROPS} {...NUMBER_FORMAT_PROPS} />
					{LIST}
					{VALIDATION}
				</>
			);
		}
		if (component === 'InputMask' || mask) {
			return (
				<>
					{/* eslint-disable-next-line react/jsx-props-no-spreading */} {/* @ts-ignore */}
					<IMaskInput mask={mask} {...PROPS} />
					{LIST}
					{VALIDATION}
				</>
			);
		}
		return (
			<>
				{}
				<input ref={ref} {...PROPS} />
				{LIST}
				{VALIDATION}
			</>
		);
};Input.displayName = 'Input';

export default Input;