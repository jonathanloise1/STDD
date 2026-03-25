import React, { FC, HTMLAttributes } from 'react';
import classNames from 'classnames';
import Validation from './Validation';

export interface ITextareaProps extends HTMLAttributes<HTMLTextAreaElement | HTMLInputElement> {
	id?: string;
	name?: string;
	size?: 'lg' | 'sm';
	className?: string;
	rows?: number;
	placeholder?: string;
	autoComplete?: string;
	ariaDescribedby?: string;
	ariaLabelledby?: string;
	ariaLabel?: string;
	title?: string;
	disabled?: boolean;
	readOnly?: boolean | 'plaintext';
	value?: string | number | readonly string[] | undefined;
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
	required?: boolean;
}
const Textarea: FC<ITextareaProps> = ({
	id,
	name,
	className,
	rows = 3,
	placeholder,
	autoComplete,
	ariaDescribedby,
	ariaLabelledby,
	ariaLabel,
	title,
	size,
	disabled,
	readOnly,
	value,
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
	required,
	...props
}) => {
	return (
		<>
			<textarea
				id={id}
				className={classNames(
					{
						'form-control': readOnly !== 'plaintext',
						'form-control-plaintext': readOnly === 'plaintext',
						[`form-control-${size}`]: size,
						'is-invalid': !isValid && isTouched && invalidFeedback,
						'is-valid': !isValid && isTouched && !invalidFeedback,
					},
					className,
				)}
				rows={rows}
				name={name}
				title={title}
				disabled={disabled}
				readOnly={!!readOnly}
				placeholder={placeholder}
				autoComplete={autoComplete}
				aria-describedby={ariaDescribedby}
				aria-label={ariaLabel}
				aria-labelledby={ariaLabelledby}
				value={value}
				onBlur={onBlur}
				onChange={onChange}
				onFocus={onFocus}
				onInput={onInput}
				onInvalid={onInvalid}
				onSelect={onSelect}
				required={required}
				{...props}
			/>
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

export default Textarea;
