import React, { ChangeEvent, FC, FocusEventHandler } from 'react';
import { Form } from 'react-bootstrap';

export interface ISelectOption {
	value: string;
	label: string;
}

export interface ISelectFieldProps {
	id: string;
	label: string;
	value: string;
	onChange: (event: ChangeEvent<HTMLSelectElement>) => void;
	onBlur?: FocusEventHandler<HTMLSelectElement>;
	options: ISelectOption[];
	/** Text of the empty first option; it cannot be chosen again once a value is set. */
	placeholder?: string;
	error?: string;
	hint?: string;
	name?: string;
	required?: boolean;
	autoFocus?: boolean;
	disabled?: boolean;
	className?: string;
}

/** A native select with the same structure as {@link TextField}: label, control, message, hint. */
export const SelectField: FC<ISelectFieldProps> = ({
	id,
	label,
	error,
	hint,
	options,
	placeholder,
	className,
	...control
}) => {
	const messageId = `${id}-message`;
	const hintId = `${id}-hint`;
	const describedBy = [error ? messageId : null, hint ? hintId : null].filter(Boolean).join(' ');

	return (
		<div className={className}>
			<Form.Label htmlFor={id}>{label}</Form.Label>
			<Form.Select
				{...control}
				id={id}
				isInvalid={!!error}
				aria-invalid={error ? true : undefined}
				aria-describedby={describedBy || undefined}>
				{placeholder !== undefined && (
					<option value='' disabled hidden>
						{placeholder}
					</option>
				)}
				{options.map((option) => (
					<option key={option.value} value={option.value}>
						{option.label}
					</option>
				))}
			</Form.Select>
			{error && (
				<Form.Control.Feedback type='invalid' id={messageId}>
					{error}
				</Form.Control.Feedback>
			)}
			{hint && (
				<Form.Text as='div' id={hintId} className='text-body-secondary'>
					{hint}
				</Form.Text>
			)}
		</div>
	);
};
