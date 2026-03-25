import React, { use, useEffect, useState } from 'react';
import { useTour } from '@reactour/tour';
import useDarkMode from '../../../hooks/useDarkMode';
import { demoPagesMenu } from '../../../menu';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import SubHeader, {
	SubHeaderLeft,
	SubHeaderRight,
	SubheaderSeparator,
} from '../../../layout/SubHeader/SubHeader';
import Page from '../../../layout/Page/Page';
import { TABS, TTabs } from './common/helper';
import Button, { ButtonGroup } from '../../../components/bootstrap/Button';

import CommonAvatarTeam from '../../../common/other/CommonAvatarTeam';

import CommonDashboardAlert from './common/CommonDashboardAlert';
import CommonDashboardUserCard from './common/CommonDashboardUserCard';
import CommonDashboardMarketingTeam from './common/CommonDashboardMarketingTeam';
import CommonDashboardDesignTeam from './common/CommonDashboardDesignTeam';
import CommonDashboardIncome from './common/CommonDashboardIncome';
import CommonDashboardRecentActivities from './common/CommonDashboardRecentActivities';
import CommonDashboardUserIssue from './common/CommonDashboardUserIssue';
import CommonDashboardSalesByStore from './common/CommonDashboardSalesByStore';
import CommonDashboardWaitingAnswer from './common/CommonDashboardWaitingAnswer';
import CommonMyWallet from '../../_common/CommonMyWallet';
import CommonDashboardTopSeller from './common/CommonDashboardTopSeller';
import ThemeContext from '../../../contexts/themeContext';

const DashboardPage = () => {
	const { mobileDesign } = use(ThemeContext);
	/**
	 * Tour Start
	 */
	const { setIsOpen } = useTour();
	useEffect(() => {
		if (localStorage.getItem('tourModalStarted') !== 'shown' && !mobileDesign) {
			setTimeout(() => {
				setIsOpen(true);
				localStorage.setItem('tourModalStarted', 'shown');
			}, 7000);
		}
		return () => {};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	const { themeStatus } = useDarkMode();

	const [activeTab, setActiveTab] = useState<TTabs>(TABS.YEARLY);

	return (
		<PageWrapper title={demoPagesMenu.sales.subMenu.dashboard.text}>
			<SubHeader>
				<SubHeaderLeft>
					<span className='h4 mb-0 fw-bold'>Overview</span>
					<SubheaderSeparator />
					<ButtonGroup>
						{Object.keys(TABS).map((key) => (
							<Button
								key={key}
								color={activeTab === TABS[key] ? 'success' : themeStatus}
								onClick={() => setActiveTab(TABS[key])}>
								{TABS[key]}
							</Button>
						))}
					</ButtonGroup>
				</SubHeaderLeft>
				<SubHeaderRight>
					<CommonAvatarTeam>
						<strong>Marketing</strong> Team
					</CommonAvatarTeam>
				</SubHeaderRight>
			</SubHeader>
			<Page container='fluid'>
				<div className='row'>
					<div className='col-12'>
						<CommonDashboardAlert />
					</div>

					<div className='col-xl-4'>
						<CommonDashboardUserCard />
					</div>
					<div className='col-xl-4'>
						<CommonDashboardMarketingTeam />
					</div>
					<div className='col-xl-4'>
						<CommonDashboardDesignTeam />
					</div>

					<div className='col-xxl-6'>
						<CommonDashboardIncome activeTab={activeTab} />
					</div>
					<div className='col-xxl-3'>
						<CommonDashboardRecentActivities />
					</div>
					<div className='col-xxl-3'>
						<CommonDashboardUserIssue />
					</div>

					<div className='col-xxl-8'>
						<CommonDashboardSalesByStore />
					</div>

					<div className='col-xxl-4 col-xl-6'>
						<CommonDashboardWaitingAnswer />
					</div>

					<div className='col-xxl-4 col-xl-6'>
						<CommonMyWallet />
					</div>
					<div className='col-xxl-8'>
						<CommonDashboardTopSeller />
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default DashboardPage;
