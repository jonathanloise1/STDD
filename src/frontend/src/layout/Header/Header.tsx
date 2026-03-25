import React, { FC, ReactElement, ReactNode, use, useLayoutEffect } from 'react';
import classNames from 'classnames';
import { useMeasure, useWindowSize } from 'react-use';
import ThemeContext from '../../contexts/themeContext';
import Button from '../../components/bootstrap/Button';
import Portal from '../Portal/Portal';
import useDarkMode from '../../hooks/useDarkMode';
import AuthContext from '../../contexts/authContext';
import { UserType } from '../../common/data/dummyRolesData';
import OrganizationDropdown from '../../components/Header/OrganizationDropdown';

interface IHeaderLeftProps {
	children: ReactNode;
	className?: string;
}
export const HeaderLeft: FC<IHeaderLeftProps> = ({ children, className }) => {
	return <div className={classNames('header-left', 'col-md', className)}>{children}</div>;
};

interface IHeaderRightProps {
	children: ReactNode;
	className?: string;
}
export const HeaderRight: FC<IHeaderRightProps> = ({ children, className }) => {
	const [ref, { height }] = useMeasure<HTMLDivElement>();

	const root = document.documentElement;
	root.style.setProperty('--header-right-height', `${height}px`);

	return (
		<div ref={ref} className={classNames('header-right', 'col-md-auto', className)}>
			{children}
		</div>
	);
};

interface IHeaderProps {
	children: ReactElement<IHeaderLeftProps> | ReactElement<IHeaderRightProps> | ReactNode;
	hasLeftMobileMenu?: boolean;
	hasRightMobileMenu?: boolean;
	mobileCenter?: ReactNode;
}
const Header: FC<IHeaderProps> = ({
	children,
	hasLeftMobileMenu = false,
	hasRightMobileMenu = true,
	mobileCenter,
}) => {
	const { themeStatus } = useDarkMode();

	const windowsWidth = useWindowSize().width;
	const [refMobileHeader, sizeMobileHeader] = useMeasure<HTMLDivElement>();
	const [refHeader, sizeHeader] = useMeasure<HTMLDivElement>();
	const { userData } = use(AuthContext);
	const isCompany = userData?.role === UserType.COMPANY;

	const root = document.documentElement;
	root.style.setProperty('--mobile-header-height', `${sizeMobileHeader.height}px`);
	root.style.setProperty('--header-height', `${sizeHeader.height}px`);

	const {
		asideStatus,
		setAsideStatus,
		leftMenuStatus,
		setLeftMenuStatus,
		rightMenuStatus,
		setRightMenuStatus,
	} = use(ThemeContext);

	useLayoutEffect(() => {
		if (
			(asideStatus || leftMenuStatus || rightMenuStatus) &&
			windowsWidth < Number(import.meta.env.VITE_MOBILE_BREAKPOINT_SIZE)
		)
			document.body.classList.add('overflow-hidden');
		return () => {
			document.body.classList.remove('overflow-hidden');
		};
	});

	return (
		<>
			<header ref={refMobileHeader} className='mobile-header'>
				<div className='container-fluid'>
					<div className='row'>
						<div className='col d-flex justify-content-between align-items-center'>
							<Button
								aria-label='Toggle Aside'
								className='mobile-header-toggle'
								size='lg'
								color={asideStatus ? 'primary' : themeStatus}
								isLight={asideStatus}
								icon={asideStatus ? 'FirstPage' : 'LastPage'}
								onClick={() => {
									setAsideStatus(!asideStatus);
									setLeftMenuStatus(false);
									setRightMenuStatus(false);
								}}
							/>
							{hasLeftMobileMenu && (
								<Button
									aria-label='Toggle Left Menu'
									className='mobile-header-toggle'
									size='lg'
									color={leftMenuStatus ? 'primary' : themeStatus}
									isLight={leftMenuStatus}
									icon={leftMenuStatus ? 'MoreVert' : 'MoreHoriz'}
									onClick={() => {
										setAsideStatus(false);
										setLeftMenuStatus(!leftMenuStatus);
										setRightMenuStatus(false);
									}}
								/>
							)}
							{/* Custom mobile center content OR default organization dropdown */}
							{mobileCenter ? (
								<div
									className='d-flex justify-content-center align-items-center'
									style={{ flex: '1' }}>
									{mobileCenter}
								</div>
							) : (
								isCompany && (
									<div
										className='d-flex justify-content-center align-items-center'
										style={{ flex: '1' }}>
										<div className='bg-l10-primary rounded px-3 py-1 ms-4'>
											<OrganizationDropdown />
										</div>
									</div>
								)
							)}
						</div>
						{hasRightMobileMenu && (
							<div className='col-auto'>
								<Button
									aria-label='Toggle Right Menu'
									className='mobile-header-toggle'
									size='lg'
									color={rightMenuStatus ? 'primary' : themeStatus}
									isLight={rightMenuStatus}
									icon={rightMenuStatus ? 'Menu' : 'MenuOpen'}
									onClick={() => {
										setAsideStatus(false);
										setLeftMenuStatus(false);
										setRightMenuStatus(!rightMenuStatus);
									}}
								/>
							</div>
						)}
					</div>
				</div>
			</header>
			<header
				ref={refHeader}
				className={classNames('header', {
					'header-left-open': leftMenuStatus,
					'header-right-open': rightMenuStatus,
				})}>
				<div className='container-fluid'>
					<div className='row d-flex align-items-center'>
						{children}
						{(leftMenuStatus || rightMenuStatus) && (
							<Portal>
								<div
									role='presentation'
									className={classNames('header-overlay', {
										'header-overlay-left-menu': leftMenuStatus,
										'header-overlay-right-menu': rightMenuStatus,
									})}
									onClick={() => {
										setAsideStatus(false);
										setLeftMenuStatus(false);
										setRightMenuStatus(false);
									}}
								/>
							</Portal>
						)}
					</div>
				</div>
			</header>
		</>
	);
};

export default Header;
