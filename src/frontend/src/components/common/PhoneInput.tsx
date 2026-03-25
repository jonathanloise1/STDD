import React, { useState, useRef, useEffect } from 'react';
import {
	FlagImage,
	defaultCountries,
	parseCountry,
	guessCountryByPartialPhoneNumber,
} from 'react-international-phone';
import 'react-international-phone/style.css';
import './PhoneInput.scss';

interface PhoneInputProps {
	id?: string;
	value: string;
	onChange: (phoneNumber: string) => void;
	onBlur?: () => void;
	isInvalid?: boolean;
	disabled?: boolean;
	label?: string;
	required?: boolean;
	/** When true, render directly in editing mode (no click-to-edit). Useful for forms. */
	alwaysEditing?: boolean;
}

// Filter to show only EU countries + common European countries
const euCountryCodes = [
	'at',
	'be',
	'bg',
	'hr',
	'cy',
	'cz',
	'dk',
	'ee',
	'fi',
	'fr',
	'de',
	'gr',
	'hu',
	'ie',
	'it',
	'lv',
	'lt',
	'lu',
	'mt',
	'nl',
	'pl',
	'pt',
	'ro',
	'sk',
	'si',
	'es',
	'se', // EU members
	'gb',
	'ch',
	'no',
	'is',
	'li',
	'mc',
	'ad',
	'sm',
	'va', // Other European
];

const euCountries = defaultCountries.filter((country) => {
	const { iso2 } = parseCountry(country);
	return euCountryCodes.includes(iso2);
});

// Sort with Italy first, then alphabetically
const sortedEuCountries = euCountries.sort((a, b) => {
	const isoA = parseCountry(a).iso2;
	const isoB = parseCountry(b).iso2;
	if (isoA === 'it') return -1;
	if (isoB === 'it') return 1;
	return parseCountry(a).name.localeCompare(parseCountry(b).name);
});

// Parse countries into usable format
const parsedCountries = sortedEuCountries.map((c) => parseCountry(c));

// Helper to detect country from phone number
const detectCountryFromPhone = (phone: string): { iso2: string; dialCode: string } => {
	if (!phone) return { iso2: 'it', dialCode: '39' };
	const result = guessCountryByPartialPhoneNumber({ phone, countries: sortedEuCountries });
	if (result?.country) {
		return { iso2: result.country.iso2, dialCode: result.country.dialCode };
	}
	return { iso2: 'it', dialCode: '39' };
};

// Extract number without dial code
const extractNumber = (phone: string, dialCode: string): string => {
	if (!phone) return '';
	const withoutPlus = phone.startsWith('+') ? phone.substring(1) : phone;
	if (withoutPlus.startsWith(dialCode)) {
		return withoutPlus.substring(dialCode.length);
	}
	return withoutPlus;
};

const PhoneInput: React.FC<PhoneInputProps> = ({
	id,
	value,
	onChange,
	onBlur,
	isInvalid,
	disabled,
	alwaysEditing = false,
}) => {
	const [isEditing, setIsEditing] = useState(alwaysEditing);
	const [showDropdown, setShowDropdown] = useState(false);
	const [localNumber, setLocalNumber] = useState('');
	const [selectedCountry, setSelectedCountry] = useState<{ iso2: string; dialCode: string }>({
		iso2: 'it',
		dialCode: '39',
	});

	const wrapperRef = useRef<HTMLDivElement>(null);
	const inputRef = useRef<HTMLInputElement>(null);
	const dropdownRef = useRef<HTMLDivElement>(null);

	// Parse value on mount and when it changes externally
	useEffect(() => {
		const detected = detectCountryFromPhone(value);
		setSelectedCountry(detected);
		setLocalNumber(extractNumber(value, detected.dialCode));
	}, [value]);

	// Focus input when entering edit mode (skip for alwaysEditing — no auto-focus on form mount)
	useEffect(() => {
		if (isEditing && !alwaysEditing && inputRef.current) {
			inputRef.current.focus();
		}
	}, [isEditing, alwaysEditing]);

	// Handle click outside — close dropdown; in click-to-edit mode also save & exit
	useEffect(() => {
		if (!isEditing) return;

		const handleClickOutside = (event: MouseEvent) => {
			// Close country dropdown on outside click (both modes)
			if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
				setShowDropdown(false);
			}
			// In click-to-edit mode only: save & exit editing on outside click
			if (!alwaysEditing && wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
				handleSave();
			}
		};

		document.addEventListener('mousedown', handleClickOutside);
		return () => document.removeEventListener('mousedown', handleClickOutside);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isEditing, alwaysEditing, localNumber, selectedCountry]);

	const handleSave = () => {
		if (!alwaysEditing) {
			setIsEditing(false);
		}
		setShowDropdown(false);
		const fullNumber = localNumber ? `+${selectedCountry.dialCode}${localNumber}` : '';
		onChange(fullNumber);
		onBlur?.();
	};

	const handleKeyDown = (e: React.KeyboardEvent) => {
		if (e.key === 'Enter') {
			handleSave();
		} else if (e.key === 'Escape') {
			if (!alwaysEditing) {
				// Reset to original value
				const detected = detectCountryFromPhone(value);
				setSelectedCountry(detected);
				setLocalNumber(extractNumber(value, detected.dialCode));
				setIsEditing(false);
			}
			setShowDropdown(false);
		}
	};

	const handleCountrySelect = (iso2: string, dialCode: string) => {
		setSelectedCountry({ iso2, dialCode });
		setShowDropdown(false);
		inputRef.current?.focus();
		// In form mode, notify parent immediately with new dial code
		if (alwaysEditing) {
			const fullNumber = localNumber ? `+${dialCode}${localNumber}` : '';
			onChange(fullNumber);
		}
	};

	const handleNumberChange = (e: React.ChangeEvent<HTMLInputElement>) => {
		// Only allow digits
		const cleaned = e.target.value.replace(/\D/g, '');
		setLocalNumber(cleaned);
		// In form mode, notify parent on every keystroke for real-time validation
		if (alwaysEditing) {
			const fullNumber = cleaned ? `+${selectedCountry.dialCode}${cleaned}` : '';
			onChange(fullNumber);
		}
	};

	/** Blur handler for the inner <input>: triggers save only when focus leaves the wrapper */
	const handleInputBlur = (e: React.FocusEvent) => {
		// Only used in form mode — click-to-edit uses handleClickOutside instead
		if (!alwaysEditing) return;
		// Skip if focus moves within the same wrapper (e.g. to country dropdown button)
		if (wrapperRef.current?.contains(e.relatedTarget as Node)) return;
		handleSave();
	};

	const displayValue = value ? `+${selectedCountry.dialCode} ${localNumber}` : '+39';

	// Display mode (skip when alwaysEditing)
	if (!isEditing && !alwaysEditing) {
		return (
			<button
				type='button'
				className={`editable-field editable-phone ${disabled ? 'editable-field--disabled' : ''} ${!value ? 'editable-field--empty' : ''}`}
				onClick={() => !disabled && setIsEditing(true)}
				disabled={disabled}>
				<FlagImage
					iso2={selectedCountry.iso2}
					size='16px'
					className='editable-phone__flag'
				/>
				<span className='editable-phone__number'>{displayValue}</span>
			</button>
		);
	}

	// Edit mode
	return (
		<div ref={wrapperRef} className={`editable-field-wrapper${alwaysEditing ? ' editable-phone--form-mode' : ''}`}>
			<span
				className={`editable-field editable-field--editing editable-phone editable-phone--editing ${isInvalid ? 'is-invalid' : ''}`}>
				{/* Country selector */}
				<button
					type='button'
					className='editable-phone__country-btn'
					onClick={() => setShowDropdown(!showDropdown)}>
					<FlagImage iso2={selectedCountry.iso2} size='16px' />
					<span className='editable-phone__arrow'>▾</span>
				</button>

				{/* Dial code display */}
				<span className='editable-phone__dial-code'>+{selectedCountry.dialCode}</span>

				{/* Number input */}
				<input
					id={id ? `${id}-input` : undefined}
					ref={inputRef}
					type='tel'
					value={localNumber}
					onChange={handleNumberChange}
					onKeyDown={handleKeyDown}
					onBlur={handleInputBlur}
					className='editable-field__input editable-phone__input'
					placeholder='123 456 7890'
				/>

				{/* Country dropdown */}
				{showDropdown && (
					<div ref={dropdownRef} className='editable-phone__dropdown'>
						{parsedCountries.map((country) => (
							<button
								key={country.iso2}
								type='button'
								className={`editable-phone__dropdown-item ${country.iso2 === selectedCountry.iso2 ? 'editable-phone__dropdown-item--selected' : ''}`}
								onClick={() => handleCountrySelect(country.iso2, country.dialCode)}>
								<FlagImage iso2={country.iso2} size='18px' />
								<span className='editable-phone__dropdown-name'>
									{country.name}
								</span>
								<span className='editable-phone__dropdown-code'>
									+{country.dialCode}
								</span>
							</button>
						))}
					</div>
				)}
			</span>
		</div>
	);
};

export default PhoneInput;
