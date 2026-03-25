import React, { HTMLAttributes, ReactNode } from 'react';
import classNames from 'classnames';
import Option, { IOptionsProps, Options } from '../Option';
import Validation from './Validation';

interface ISelectProps extends Partial<IOptionsProps>, HTMLAttributes<HTMLSelectElement> {
	id?: string;
	className?: string;
	name?: string;
	children?: ReactNode;
	ariaLabel: string;
	placeholder?: string;
	size?: 'lg' | 'sm';
	multiple?: boolean;
	disabled?: boolean;
	required?: boolean;
	ariaDescribedby?: string;
	ariaLabelledby?: string;
	title?: string;
	value?: string | string[];
	defaultValue?: string | string[];
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
	ref?: React.Ref<HTMLSelectElement>;
}
const Select = ({
		id,
		name,
		className,
		children,
		required,
		placeholder,
		ariaDescribedby,
		ariaLabelledby,
		ariaLabel,
		list,
		multiple,
		title,
		size,
		disabled,
		value,
		defaultValue,
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
		ref,
		...props
	}: ISelectProps) => {
		return (
			<>
				<select
					ref={ref}
					id={id}
					className={classNames(
						'form-select',
						{
							[`form-select-${size}`]: size,
							'text-muted': value === '' && placeholder,
							'is-invalid': !isValid && isTouched && invalidFeedback,
							'is-valid': !isValid && isTouched && !invalidFeedback,
						},
						className,
					)}
					name={name}
					aria-label={ariaLabel}
					aria-describedby={ariaDescribedby}
					aria-labelledby={ariaLabelledby}
					multiple={multiple}
					disabled={disabled}
					title={title}
					value={value}
					defaultValue={defaultValue}
					required={required}
					onBlur={onBlur}
					onChange={onChange}
					onFocus={onFocus}
					onInput={onInput}
					onInvalid={onInvalid}
					onSelect={onSelect}
					{...props}>
					{placeholder && (
						<Option value='' hidden>
							{placeholder}
						</Option>
					)}
					{children || (list && <Options list={list} />)}
				</select>
				{isValidMessage && (
					<Validation
						isTouched={isTouched}
						invalidFeedback={invalidFeedback}
						validFeedback={validFeedback}
						isTooltip={isTooltipFeedback}
					/>
				)}
			</>
		);
	};
Select.displayName = 'Select';

export default Select;
