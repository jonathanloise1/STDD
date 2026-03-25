import React, { FC, ReactNode, use, useLayoutEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
// import classNames from 'classnames';
// import { useTour } from '@reactour/tour';
import Button, { IButtonProps } from '../../../components/bootstrap/Button';
import { HeaderRight } from '../../../layout/Header/Header';
import OffCanvas, {
	OffCanvasBody,
	OffCanvasHeader,
	OffCanvasTitle,
} from '../../../components/bootstrap/OffCanvas';
import Alert from '../../../components/bootstrap/Alert';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../components/bootstrap/Dropdown';
import Icon from '../../../components/icon/Icon';
import ThemeContext from '../../../contexts/themeContext';
import LANG, { getLangWithKey, ILang } from '../../../lang';
import showNotification from '../../../components/extras/showNotification';
import useDarkMode from '../../../hooks/useDarkMode';
import Popovers from '../../../components/bootstrap/Popovers';
import Spinner from '../../../components/bootstrap/Spinner';

interface ICommonHeaderRightProps {
	beforeChildren?: ReactNode;
	afterChildren?: ReactNode;
}
const CommonHeaderRight: FC<ICommonHeaderRightProps> = ({ beforeChildren, afterChildren }) => {
	const { darkModeStatus, setDarkModeStatus } = useDarkMode();

	const { fullScreenStatus, setFullScreenStatus } = use(ThemeContext);
	const styledBtn: IButtonProps = {
		color: darkModeStatus ? 'dark' : 'light',
		hoverShadow: 'default',
		isLight: !darkModeStatus,
		size: 'lg',
	};

	const [offcanvasStatus, setOffcanvasStatus] = useState(false);

	const { i18n, t } = useTranslation('settings');

	/**
	 * Switches the application language and shows a confirmation notification.
	 * Delegates to `i18n.changeLanguage()` which triggers a re-render of all
	 * components using `useTranslation()` and persists the choice via the
	 * `i18next-browser-languagedetector` (localStorage).
	 *
	 * @userstory US-I18N-02 — Switch language at any time without logging out
	 * @userstory US-I18N-03 — Preference is automatically saved by LanguageDetector
	 */
	const changeLanguage = (lng: ILang['key']['lng']) => {
		i18n.changeLanguage(lng).then(() => {
			const lang = getLangWithKey(lng);
			showNotification(
				t('languageChanged', { language: lang?.text }),
				t('languageUpdated'),
				'success',
				<Icon icon={lang?.icon} size='lg' />,
			);
		});
	};

	/**
	 * Keeps the HTML `lang` attribute in sync with the current i18n language.
	 * This ensures assistive technologies and search engines correctly
	 * interpret the page language.
	 *
	 * @userstory US-I18N-01 — Platform reflects the user's preferred language
	 */
	useLayoutEffect(() => {
		document.documentElement.setAttribute('lang', i18n.language.substring(0, 2));
	});

	// const { setIsOpen } = useTour();

	return (
		<HeaderRight>
			<div className='row g-3'>
				{beforeChildren}
				{/* Tour Modal */}
				{/* {localStorage.getItem('tourModalStarted') === 'shown' && (
					<div className='col-auto position-relative'>
						<Popovers trigger='hover' desc='Start the "Facit" tour'>
							<Button
								// eslint-disable-next-line react/jsx-props-no-spreading
								{...styledBtn}
								icon='Tour'
								onClick={() => setIsOpen(true)}
								aria-label='Start the "Facit" tour'
							/>
						</Popovers>
						<Icon
							icon='Circle'
							className={classNames(
								'position-absolute start-65',
								'text-danger',
								'animate__animated animate__heartBeat animate__infinite animate__slower',
							)}
						/>
					</div>
				)} */}

				{/* Dark Mode */}
				<div className='col-auto'>
					<Popovers trigger='hover' desc='Dark / Light mode'>
						<Button
							// eslint-disable-next-line react/jsx-props-no-spreading
							{...styledBtn}
							onClick={() => setDarkModeStatus(!darkModeStatus)}
							className='btn-only-icon'
							data-tour='dark-mode'
							aria-label='Toggle dark mode'>
							<Icon
								icon={darkModeStatus ? 'DarkMode' : 'LightMode'}
								color={darkModeStatus ? 'info' : 'warning'}
								className='btn-icon'
							/>
						</Button>
					</Popovers>
				</div>

				{/* Lang Selector */}
				<div className='col-auto'>
					<Dropdown>
						<DropdownToggle hasIcon={false}>
							{typeof getLangWithKey(i18n.language as ILang['key']['lng'])?.icon ===
							'undefined' ? (
								<Button
									// eslint-disable-next-line react/jsx-props-no-spreading
									{...styledBtn}
									className='btn-only-icon'
									aria-label='Change language'
									data-tour='lang-selector'>
									<Spinner isSmall inButton='onlyIcon' isGrow />
								</Button>
							) : (
								<Button
									// eslint-disable-next-line react/jsx-props-no-spreading
									{...styledBtn}
									icon={
										getLangWithKey(i18n.language as ILang['key']['lng'])?.icon
									}
									aria-label='Change language'
									data-tour='lang-selector'
								/>
							)}
						</DropdownToggle>
						<DropdownMenu isAlignmentEnd data-tour='lang-selector-menu'>
							{Object.keys(LANG).map((i) => (
								<DropdownItem key={LANG[i].lng}>
									<Button
										icon={LANG[i].icon}
										onClick={() => changeLanguage(LANG[i].lng)}>
										{LANG[i].text}
									</Button>
								</DropdownItem>
							))}
						</DropdownMenu>
					</Dropdown>
				</div>

				{/*	Full Screen */}
				<div className='col-auto d-none d-md-block'>
					<Popovers trigger='hover' desc='Fullscreen'>
						<Button
							// eslint-disable-next-line react/jsx-props-no-spreading
							{...styledBtn}
							icon={fullScreenStatus ? 'FullscreenExit' : 'Fullscreen'}
							onClick={() => setFullScreenStatus(!fullScreenStatus)}
							aria-label='Toggle fullscreen'
						/>
					</Popovers>
				</div>

				{/* Quick Panel */}
				{/* <div className='col-auto'>
					<Dropdown>
						<DropdownToggle hasIcon={false}> */}
				{}
				{/* <Button {...styledBtn} icon='Tune' aria-label='Quick menu' />
						</DropdownToggle>
						<DropdownMenu isAlignmentEnd size='lg' className='py-0 overflow-hidden'>
							<div className='row g-0'>
								<div
									className={classNames(
										'col-12',
										'p-4',
										'd-flex justify-content-center',
										'fw-bold fs-5',
										'text-info',
										'border-bottom border-info',
										{
											'bg-l25-info': !darkModeStatus,
											'bg-lo25-info': darkModeStatus,
										},
									)}>
									Quick Panel
								</div>
								<div
									className={classNames(
										'col-6 p-4 transition-base cursor-pointer bg-light-hover',
										'border-end border-bottom',
										{ 'border-dark': darkModeStatus },
									)}>
									<div className='d-flex flex-column align-items-center justify-content-center'>
										<Icon icon='Public' size='3x' color='info' />
										<span>Dealers</span>
										<small className='text-muted'>Options</small>
									</div>
								</div>
								<div
									className={classNames(
										'col-6 p-4 transition-base cursor-pointer bg-light-hover',
										'border-bottom',
										{ 'border-dark': darkModeStatus },
									)}>
									<div className='d-flex flex-column align-items-center justify-content-center'>
										<Icon icon='Upcoming' size='3x' color='success' />
										<span>Inbox</span>
										<small className='text-muted'>Configuration</small>
									</div>
								</div>
								<div
									className={classNames(
										'col-6 p-4 transition-base cursor-pointer bg-light-hover',
										'border-end',
										{ 'border-dark': darkModeStatus },
									)}>
									<div className='d-flex flex-column align-items-center justify-content-center'>
										<Icon icon='Print' size='3x' color='danger' />
										<span>Print</span>
										<small className='text-muted'>Settings</small>
									</div>
								</div>
								<div className='col-6 p-4 transition-base cursor-pointer bg-light-hover'>
									<div className='d-flex flex-column align-items-center justify-content-center'>
										<Icon icon='ElectricalServices' size='3x' color='warning' />
										<span>Power</span>
										<small className='text-muted'>Mode</small>
									</div>
								</div>
							</div>
						</DropdownMenu>
					</Dropdown>
				</div> */}

				{/*	Notifications */}
				{/* <div className='col-auto'>
					<Button
						// eslint-disable-next-line react/jsx-props-no-spreading
						{...styledBtn}
						icon='Notifications'
						onClick={() => setOffcanvasStatus(true)}
						aria-label='Notifications'
					/>
				</div> */}
				{afterChildren}
			</div>

			<OffCanvas
				id='notificationCanvas'
				titleId='offcanvasExampleLabel'
				placement='end'
				isOpen={offcanvasStatus}
				setOpen={setOffcanvasStatus}>
				<OffCanvasHeader setOpen={setOffcanvasStatus}>
					<OffCanvasTitle id='offcanvasExampleLabel'>Notifications</OffCanvasTitle>
				</OffCanvasHeader>
				<OffCanvasBody>
					<Alert icon='ViewInAr' isLight color='info' className='flex-nowrap'>
						4 new components added.
					</Alert>
					<Alert icon='ThumbUp' isLight color='warning' className='flex-nowrap'>
						New products added to stock.
					</Alert>
					<Alert icon='Inventory2' isLight color='danger' className='flex-nowrap'>
						There are products that need to be packaged.
					</Alert>
					<Alert icon='BakeryDining' isLight color='success' className='flex-nowrap'>
						Your food order is waiting for you at the consultation.
					</Alert>
					<Alert icon='Escalator' isLight color='primary' className='flex-nowrap'>
						Escalator will turn off at 6:00 pm.
					</Alert>
				</OffCanvasBody>
			</OffCanvas>
		</HeaderRight>
	);
};

export default CommonHeaderRight;
