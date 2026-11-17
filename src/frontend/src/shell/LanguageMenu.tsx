import React from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, Menu, notify, showApiError } from '../ui';
import userProfileService from '../services/api/userProfileService';
import { toApiProblem } from '../services/api/apiProblem';
import { LANGUAGES, LanguageCode } from './languages';

/**
 * The language button of the header: the globe and the code of the current language, with the menu of the
 * languages. The accessible name follows the language of the application.
 */
// US-AUTH-08
const LanguageMenu = () => {
	const { i18n, t } = useTranslation(['menu', 'settings']);

	/**
	 * Switches the language at once, confirms it, then saves the choice;
	 * when saving fails the app keeps the language until the user signs out.
	 */
	const changeLanguage = async (code: LanguageCode, name: string) => {
		await i18n.changeLanguage(code);
		notify.success(t('settings:languageChanged', { language: name }));
		try {
			await userProfileService.updateLanguage(code);
		} catch (err) {
			showApiError(toApiProblem(err));
		}
	};

	return (
		<Menu align='end'>
			<Menu.Toggle
				className='app-header__icon-btn'
				id='btn-language-selector'
				data-testid='btn-language-selector'
				aria-label={t('menu:menu.language')}>
				<Icon icon='Language' size='lg' />
				<span className='app-header__lang-code'>
					{i18n.language.substring(0, 2).toUpperCase()}
				</span>
			</Menu.Toggle>
			<Menu.Items>
				{LANGUAGES.map((language) => (
					<Menu.Item
						key={language.code}
						id={`btn-lang-${language.code}`}
						onSelect={() => changeLanguage(language.code, language.name)}>
						{language.name}
					</Menu.Item>
				))}
			</Menu.Items>
		</Menu>
	);
};

export default LanguageMenu;
