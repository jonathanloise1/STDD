import React, { useState, useRef, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import Input, { IInputProps } from './Input';

/**
 * Get locale-aware separators from Intl.NumberFormat.
 * e.g. it → { thousands: '.', decimal: ',' }
 *      en → { thousands: ',', decimal: '.' }
 */
const getSeparators = (locale: string) => {
	const parts = new Intl.NumberFormat(locale).formatToParts(1234567.89);
	const thousands = parts.find((p) => p.type === 'group')?.value || '.';
	const decimal = parts.find((p) => p.type === 'decimal')?.value || ',';
	return { thousands, decimal };
};

/** Format an integer with locale-aware thousands separator + optional decimals */
const formatAmount = (num: number, withDecimals: boolean, locale: string): string => {
	if (!num) return '';
	const { thousands, decimal } = getSeparators(locale);
	const str = Math.round(Math.abs(num)).toString();
	const withSep = str.replace(/\B(?=(\d{3})+(?!\d))/g, thousands);
	return withDecimals ? `${withSep}${decimal}00` : withSep;
};

export interface CurrencyInputProps
	extends Omit<IInputProps, 'value' | 'onChange' | 'onBlur' | 'onFocus' | 'type'> {
	/** The numeric value (integer, e.g. 8000) */
	value: number;
	/** Called with the new numeric value whenever the user types */
	onValueChange: (value: number) => void;
	/** Called on blur, after formatting – use for validation / touched */
	onBlurValue?: (value: number) => void;
	/** Symbol shown in the input-group prefix */
	currencySymbol?: string;
}

const CurrencyInput: React.FC<CurrencyInputProps> = ({
	value,
	onValueChange,
	onBlurValue,
	currencySymbol = '€',
	...inputProps
}) => {
	const { i18n } = useTranslation();
	const locale = i18n.language || 'it';

	const numRef = useRef(value);
	const [display, setDisplay] = useState(() => formatAmount(value, false, locale));

	// Sync when value prop changes from outside (e.g. form reset) or language changes
	useEffect(() => {
		numRef.current = value;
		setDisplay(formatAmount(value, false, locale));
	}, [value, locale]);

	const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
		const digits = e.target.value.replace(/\D/g, '');
		const num = digits ? parseInt(digits, 10) : 0;
		numRef.current = num;
		onValueChange(num);
		setDisplay(formatAmount(num, false, locale));
	};

	const handleFocus = () => {
		setDisplay(formatAmount(numRef.current, false, locale));
	};

	const handleBlur = () => {
		setDisplay(formatAmount(numRef.current, true, locale));
		onBlurValue?.(numRef.current);
	};

	return (
		<div className='input-group'>
			<span className='input-group-text'>{currencySymbol}</span>
			<Input
				type='text'
				inputMode='numeric'
				value={display}
				onChange={handleChange}
				onFocus={handleFocus}
				onBlur={handleBlur}
				{...inputProps}
			/>
		</div>
	);
};

CurrencyInput.displayName = 'CurrencyInput';

export default CurrencyInput;
