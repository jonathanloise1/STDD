import React, {
	Children,
	cloneElement,
	HTMLAttributes,
	ReactElement,
	ReactNode,
} from 'react';
import classNames from 'classnames';
import Validation from './Validation';

interface IChecksGroupProps extends HTMLAttributes<HTMLDivElement> {
	id?: string | undefined;
	className?: string;
	children: ReactElement<IChecksProps> | ReactElement<IChecksProps>[];
	isInline?: boolean;
	isTouched?: boolean;
	isValid?: boolean;
	invalidFeedback?: string;
	validFeedback?: string;
	isTooltipFeedback?: boolean;
	ref?: React.Ref<HTMLDivElement>;
}
export const ChecksGroup = ({
		id,
		className,
		children,
		isInline,
		isValid,
		isTouched,
		invalidFeedback,
		validFeedback,
		isTooltipFeedback,
		ref,
		...props
	}: IChecksGroupProps) => {
		return (
			<>
				<div
					ref={ref}
					id={id}
					className={classNames(
						{
							'is-invalid': !isValid && isTouched && invalidFeedback,
							'is-valid': !isValid && isTouched && !invalidFeedback,
						},
						className,
					)}
					{...props}>
					{Children.map(children, (child) =>
						cloneElement(child, {
							isInline: child.props.isInline || isInline,
							isValid,
							isTouched,
							invalidFeedback,
							validFeedback,
							isTooltipFeedback,
							isValidMessage: false,
						}),
					)}
				</div>
				<Validation
					isTouched={isTouched}
					invalidFeedback={invalidFeedback}
					validFeedback={validFeedback}
					isTooltip={isTooltipFeedback}
				/>
			</>
		);
	};
ChecksGroup.displayName = 'ChecksGroup';

export interface IChecksProps extends HTMLAttributes<HTMLInputElement> {
	id?: string | undefined;
	className?: string;
	name?: string | null;
	type?: 'checkbox' | 'radio' | 'switch';
	label?: ReactNode;
	value?: string | number;
	checked?: boolean | string;
	disabled?: boolean;
	isInline?: boolean;
	isFormCheckInput?: boolean;
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
	ariaLabel?: string;
	ref?: React.Ref<HTMLInputElement>;
}
const Checks = ({
		id,
		className,
		name,
		type = 'checkbox',
		label,
		value,
		checked,
		disabled,
		isInline,
		isFormCheckInput,
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
		ariaLabel,
		ref,
		...props
	}: IChecksProps) => {
		const INNER = (
			<input
				ref={ref}
				className={classNames(
					'form-check-input',
					{
						'mt-0': isFormCheckInput,
						'is-invalid': !isValid && isTouched && invalidFeedback,
						'is-valid': !isValid && isTouched && !invalidFeedback,
					},
					className,
				)}
				name={name === null ? id : name}
				type={type === 'radio' ? 'radio' : 'checkbox'}
				id={id}
				value={value}
				checked={type === 'radio' ? checked === value : !!checked}
				disabled={disabled}
				onBlur={onBlur}
				onChange={onChange}
				onFocus={onFocus}
				onInput={onInput}
				onInvalid={onInvalid}
				onSelect={onSelect}
				aria-label={ariaLabel}
				{...props}
			/>
		);

		if (isFormCheckInput) {
			return INNER;
		}
		return (
			<div
				className={classNames('form-check', {
					'form-switch': type === 'switch',
					'form-check-inline': isInline,
				})}>
				{INNER}
				{label && (
					<label className='form-check-label' htmlFor={id}>
						{label}
					</label>
				)}
				{isValidMessage && (
					<Validation
						isTouched={isTouched}
						invalidFeedback={invalidFeedback}
						validFeedback={validFeedback}
						isTooltip={isTooltipFeedback}
					/>
				)}
			</div>
		);
};
export default Checks;
