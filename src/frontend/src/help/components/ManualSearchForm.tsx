import React, { FormEvent, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../ui';

interface Props {
	/** The text the field starts with; the search page passes the query. */
	initialQuery?: string;
}

/** The "Search the manual" field and the "Search" button (manual.md 5.3). */
// US-HELP-02
export const ManualSearchForm = ({ initialQuery = '' }: Props) => {
	const { t } = useTranslation('help');
	const navigate = useNavigate();
	const [query, setQuery] = useState(initialQuery);

	useEffect(() => setQuery(initialQuery), [initialQuery]);

	const submit = (event: FormEvent) => {
		event.preventDefault();
		const text = query.trim();
		if (text) navigate(`/manual/search?q=${encodeURIComponent(text)}`);
	};

	return (
		<form className='d-flex gap-2 mb-4' role='search' onSubmit={submit} noValidate>
			<input
				id='manual-search'
				data-testid='input-manual-search'
				type='search'
				className='form-control'
				aria-label={t('searchLabel')}
				placeholder={t('searchLabel')}
				value={query}
				onChange={(e) => setQuery(e.target.value)}
			/>
			<button
				type='submit'
				className='btn btn-primary d-inline-flex align-items-center gap-1'
				data-testid='btn-manual-search'>
				<Icon icon='Search' />
				{t('searchButton')}
			</button>
		</form>
	);
};
