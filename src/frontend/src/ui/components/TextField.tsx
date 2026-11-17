import React, { ChangeEvent, FC, FocusEventHandler } from 'react';
import { Form } from 'react-bootstrap';

export interface ITextFieldProps {
	id: string;
	label: string;
	value: string | number;
	onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
	onBlur?: FocusEventHandler<HTMLInputElement | HTMLTextAreaElement>;
	type?: 'text' | 'email' | 'date' | 'number' | 'tel';
	as?: 'input' | 'textarea';
	rows?: number;
	/** Field message; shown under the control, as its next element (frontend.md 9). */
	error?: string;
	/** Visible hint under the field. */
	hint?: string;
	name?: string;
	required?: boolean;
	maxLength?: number;
	autoFocus?: boolean;
	placeholder?: string;
	disabled?: boolean;
	className?: string;
}

/** Label, control, field message and hint, in that order (frontend.md 9). */
export const TextField: FC<ITextFieldProps> = ({
	id,
	label,
	error,
	hint,
	as = 'input',
	type = 'text',
	className,
	...control
}) => {
	const messageId = `${id}-message`;
	const hintId = `${id}-hint`;
	const describedBy = [error ? messageId : null, hint ? hintId : null].filter(Boolean).join(' ');

	return (
		<div className={className}>
			<Form.Label htmlFor={id}>{label}</Form.Label>
			<Form.Control
				{...control}
				id={id}
				as={as}
				type={as === 'input' ? type : undefined}
				isInvalid={!!error}
				aria-invalid={error ? true : undefined}
				aria-describedby={describedBy || undefined}
			/>
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
