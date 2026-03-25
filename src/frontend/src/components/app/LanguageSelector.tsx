import React, { FC } from 'react';
import classNames from 'classnames';

interface LanguageSelectorProps {
	currentLanguage: string;
	onChange: (lang: string) => void;
	variant?: 'dropdown' | 'list';
	className?: string;
}

const LANGUAGES = [
	{ code: 'de', label: 'Deutsch', flag: '🇩🇪' },
	{ code: 'fr', label: 'Français', flag: '🇫🇷' },
	{ code: 'it', label: 'Italiano', flag: '🇮🇹' },
] as const;

const LanguageSelector: FC<LanguageSelectorProps> = ({
	currentLanguage,
	onChange,
	variant = 'dropdown',
	className,
}) => {
	if (variant === 'list') {
		return (
			<div className={classNames('language-selector-list', className)}>
				{LANGUAGES.map((lang) => (
					<div
						key={lang.code}
						className={classNames(
							'd-flex align-items-center gap-3 p-3 border rounded-2 mb-2 cursor-pointer',
							currentLanguage === lang.code && 'border-primary bg-l10-primary',
						)}
						role='button'
						tabIndex={0}
						onClick={() => onChange(lang.code)}
						onKeyDown={(e) => e.key === 'Enter' && onChange(lang.code)}>
						<span style={{ fontSize: 20 }}>{lang.flag}</span>
						<span className='flex-grow-1'>{lang.label}</span>
						{currentLanguage === lang.code && (
							<span className='material-icons text-primary' style={{ fontSize: 20 }}>
								check
							</span>
						)}
					</div>
				))}
			</div>
		);
	}

	return (
		<select
			className={classNames('form-select form-select-sm', className)}
			value={currentLanguage}
			onChange={(e) => onChange(e.target.value)}
			aria-label='Language'
			style={{ width: 'auto' }}>
			{LANGUAGES.map((lang) => (
				<option key={lang.code} value={lang.code}>
					{lang.flag} {lang.code.toUpperCase()}
				</option>
			))}
		</select>
	);
};

export default LanguageSelector;
