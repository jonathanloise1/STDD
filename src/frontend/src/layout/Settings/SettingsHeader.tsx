import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Button from '../../components/bootstrap/Button';
import Icon from '../../components/icon/Icon';

const SettingsHeader: React.FC = () => {
	const { t } = useTranslation('menu');
	const navigate = useNavigate();

	const handleBack = () => {
		navigate('/dashboard');
	};

	return (
		<header className='settings-header border-bottom bg-light px-4 py-3'>
			<Button color='link' className='p-0 text-decoration-none' onClick={handleBack}>
				<Icon icon='ArrowBack' className='me-2' />
				{t('settings.back', 'Torna')}
			</Button>
		</header>
	);
};

export default SettingsHeader;
