import React, { useEffect, useState, useRef, useMemo } from 'react';
import { FlagImage } from 'react-international-phone';
import { useTranslation } from 'react-i18next';
import countries from 'i18n-iso-countries';
import countryService, { Country } from '../../services/api/countryService';
import './CountrySelect.scss';

// Register languages for country name translations
import enLocale from 'i18n-iso-countries/langs/en.json';
import itLocale from 'i18n-iso-countries/langs/it.json';
import frLocale from 'i18n-iso-countries/langs/fr.json';
import deLocale from 'i18n-iso-countries/langs/de.json';
import esLocale from 'i18n-iso-countries/langs/es.json';
import ptLocale from 'i18n-iso-countries/langs/pt.json';
import plLocale from 'i18n-iso-countries/langs/pl.json';

countries.registerLocale(enLocale);
countries.registerLocale(itLocale);
countries.registerLocale(frLocale);
countries.registerLocale(deLocale);
countries.registerLocale(esLocale);
countries.registerLocale(ptLocale);
countries.registerLocale(plLocale);

interface CountrySelectProps {
	value: string;
	onChange: (countryCode: string) => void;
	onBlur?: () => void;
	isInvalid?: boolean;
	disabled?: boolean;
	label?: string;
	showFlag?: boolean;
	required?: boolean;
	compact?: boolean; // New prop for compact inline mode
}

const CountrySelect: React.FC<CountrySelectProps> = ({
	value,
	onChange,
	onBlur,
	isInvalid,
	disabled,
	label,
	required = false,
	compact = false,
}) => {
	const { i18n } = useTranslation();
	const [countryList, setCountryList] = useState<Country[]>([]);
	const [loading, setLoading] = useState(true);
	const [isOpen, setIsOpen] = useState(false);
	const [dropdownPosition, setDropdownPosition] = useState<'below' | 'above'>('below');
	const wrapperRef = useRef<HTMLDivElement>(null);

	// Get current language for country name translation
	const currentLang = i18n.language?.split('-')[0] || 'en';

	// Helper function to get translated country name
	const getCountryName = (isoCode: string, fallbackName: string): string => {
		const translatedName = countries.getName(isoCode, currentLang);
		return translatedName || fallbackName;
	};

	useEffect(() => {
		const loadCountries = async () => {
			try {
				const data = await countryService.getCountries();
				setCountryList(data);
				// Set Italy as default if no value is set
				if (!value && data.length > 0) {
					onChange('IT');
				}
			} catch (error) {
				console.error('Failed to load countries:', error);
			} finally {
				setLoading(false);
			}
		};

		loadCountries();
	}, []);

	// Calculate dropdown position based on available space
	const calculateDropdownPosition = () => {
		if (!wrapperRef.current) return;

		const rect = wrapperRef.current.getBoundingClientRect();
		const dropdownHeight = 250; // max-height of dropdown
		const spaceBelow = window.innerHeight - rect.bottom;
		const spaceAbove = rect.top;

		// Open upward if not enough space below and more space above
		if (spaceBelow < dropdownHeight && spaceAbove > spaceBelow) {
			setDropdownPosition('above');
		} else {
			setDropdownPosition('below');
		}
	};

	// Handle click outside to close
	useEffect(() => {
		const handleClickOutside = (event: MouseEvent) => {
			if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
				setIsOpen(false);
				onBlur?.();
			}
		};

		if (isOpen) {
			calculateDropdownPosition();
			document.addEventListener('mousedown', handleClickOutside);
			window.addEventListener('scroll', calculateDropdownPosition, true);
		}
		return () => {
			document.removeEventListener('mousedown', handleClickOutside);
			window.removeEventListener('scroll', calculateDropdownPosition, true);
		};
	}, [isOpen, onBlur]);

	const selectedCountry = countryList.find((c) => c.isoCode === value);
	const iso2 = value?.toLowerCase() || 'it';
	const selectedCountryName = selectedCountry
		? getCountryName(selectedCountry.isoCode, selectedCountry.name)
		: getCountryName('IT', 'Italia');

	const handleSelect = (countryCode: string) => {
		onChange(countryCode);
		setIsOpen(false);
		onBlur?.();
	};

	// Compact mode - inline style like other fields
	if (compact) {
		return (
			<div
				ref={wrapperRef}
				className={`country-select country-select--compact ${isInvalid ? 'is-invalid' : ''}`}>
				{label && <label className='country-select__label'>{label}</label>}
				<button
					type='button'
					className={`country-select__trigger ${disabled ? 'country-select__trigger--disabled' : ''}`}
					onClick={() => !disabled && !loading && setIsOpen(!isOpen)}
					disabled={disabled || loading}>
					<FlagImage iso2={iso2} size='18px' className='country-select__flag' />
					<span className='country-select__name'>{selectedCountryName}</span>
					<span className='country-select__arrow'>▾</span>
				</button>

				{isOpen && (
					<div className={`country-select__dropdown country-select__dropdown--${dropdownPosition}`}>
						{countryList.map((country) => (
							<button
								key={country.isoCode}
								type='button'
								className={`country-select__option ${country.isoCode === value ? 'country-select__option--selected' : ''}`}
								onClick={() => handleSelect(country.isoCode)}>
								<FlagImage iso2={country.isoCode.toLowerCase()} size='18px' />
								<span className='country-select__option-name'>
									{getCountryName(country.isoCode, country.name)}
								</span>
								<span className='country-select__option-code'>
									{country.isoCode}
								</span>
							</button>
						))}
					</div>
				)}
			</div>
		);
	}

	// Standard mode - full form field with floating label
	return (
		<div
			ref={wrapperRef}
			className={`country-select country-select--standard ${isInvalid ? 'is-invalid' : ''}`}>
			<div className='country-select__field'>
				<button
					type='button'
					className={`country-select__input ${disabled ? 'country-select__input--disabled' : ''}`}
					onClick={() => !disabled && !loading && setIsOpen(!isOpen)}
					disabled={disabled || loading}>
					<FlagImage iso2={iso2} size='20px' className='country-select__flag' />
					<span className='country-select__value'>{selectedCountryName}</span>
					<span className='country-select__arrow'>▾</span>
				</button>
				{label && <label className='country-select__floating-label'>{label}</label>}
			</div>

			{isOpen && (
				<div className={`country-select__dropdown country-select__dropdown--full country-select__dropdown--${dropdownPosition}`}>
					{countryList.map((country) => (
						<button
							key={country.isoCode}
							type='button'
							className={`country-select__option ${country.isoCode === value ? 'country-select__option--selected' : ''}`}
							onClick={() => handleSelect(country.isoCode)}>
							<FlagImage iso2={country.isoCode.toLowerCase()} size='20px' />
							<span className='country-select__option-name'>
								{getCountryName(country.isoCode, country.name)}
							</span>
							<span className='country-select__option-code'>{country.isoCode}</span>
						</button>
					))}
				</div>
			)}
		</div>
	);
};

export default CountrySelect;
