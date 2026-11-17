import React from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../ui';
import { useHelp } from '../../contexts/helpContext';

interface Props {
	/** The context key of the screen part this button belongs to (manual.md 5.4), e.g. `task-form`. */
	context?: string;
	className?: string;
	/** Whether the word "Help" is shown next to the icon; the accessible name is "Help" either way. */
	showLabel?: boolean;
}

/** A "Help" button: opens the panel for the current screen, or for the part named by `context`. */
// US-HELP-03
export const HelpButton = ({
	context,
	className = 'btn btn-sm btn-link text-decoration-none',
	showLabel = true,
}: Props) => {
	const { t } = useTranslation('help');
	const { openHelp } = useHelp();
	return (
		<button
			type='button'
			className={className}
			aria-label={t('help')}
			title={t('help')}
			data-testid={context ? `btn-help-${context}` : 'btn-help'}
			onClick={() => openHelp({ context })}>
			<Icon icon='HelpOutline' />
			{showLabel && <span className='ms-1'>{t('help')}</span>}
		</button>
	);
};
