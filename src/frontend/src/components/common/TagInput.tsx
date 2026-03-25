import React, { useState, useRef, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import Icon from '../icon/Icon';
import './TagInput.scss';

interface TagInputProps {
	tags: string[];
	onChange: (tags: string[]) => void;
	placeholder?: string;
	maxTags?: number;
	label?: string;
	icon?: string;
	variant?: 'primary' | 'info';
}

const TagInput: React.FC<TagInputProps> = ({
	tags,
	onChange,
	placeholder,
	maxTags = 20,
	label,
	icon,
	variant = 'primary',
}) => {
	const { t } = useTranslation(['common']);
	const [inputValue, setInputValue] = useState('');
	const [isAdding, setIsAdding] = useState(false);
	const inputRef = useRef<HTMLInputElement>(null);

	useEffect(() => {
		if (isAdding && inputRef.current) {
			inputRef.current.focus();
		}
	}, [isAdding]);

	const handleAddTag = () => {
		const trimmed = inputValue.trim();
		if (trimmed && !tags.includes(trimmed) && tags.length < maxTags) {
			onChange([...tags, trimmed]);
			setInputValue('');
		}
	};

	const handleKeyDown = (e: React.KeyboardEvent) => {
		if (e.key === 'Enter') {
			e.preventDefault();
			handleAddTag();
		} else if (e.key === 'Escape') {
			setInputValue('');
			setIsAdding(false);
		} else if (e.key === 'Backspace' && inputValue === '' && tags.length > 0) {
			// Remove last tag on backspace if input is empty
			onChange(tags.slice(0, -1));
		}
	};

	const handleRemoveTag = (tagToRemove: string) => {
		onChange(tags.filter((tag) => tag !== tagToRemove));
	};

	const handleBlur = () => {
		if (inputValue.trim()) {
			handleAddTag();
		}
		setIsAdding(false);
	};

	return (
		<div className={`tag-input tag-input--${variant}`}>
			{label && (
				<div className='tag-input__label'>
					{icon && <Icon icon={icon} className='tag-input__label-icon' />}
					<span>{label}</span>
				</div>
			)}
			<div className='tag-input__container'>
				{tags.map((tag) => (
					<span key={tag} className='tag-input__tag'>
						<span className='tag-input__tag-text'>{tag}</span>
						<button
							type='button'
							className='tag-input__tag-remove'
							onClick={() => handleRemoveTag(tag)}
							aria-label={t('remove')}>
							<Icon icon='Close' />
						</button>
					</span>
				))}

				{tags.length < maxTags &&
					(isAdding ? (
						<input
							ref={inputRef}
							type='text'
							className='tag-input__input'
							value={inputValue}
							onChange={(e) => setInputValue(e.target.value)}
							onKeyDown={handleKeyDown}
							onBlur={handleBlur}
							placeholder={placeholder || t('profile.addTag', 'Add...')}
						/>
					) : (
						<button
							type='button'
							className='tag-input__add-btn'
							onClick={() => setIsAdding(true)}>
							<Icon icon='Add' />
						</button>
					))}
			</div>
		</div>
	);
};

export default TagInput;
