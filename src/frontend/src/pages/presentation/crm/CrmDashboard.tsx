import React, { useEffect, useState } from 'react';
import { DateRangePicker } from 'react-date-range';
import dayjs from 'dayjs';
import { useTour } from '@reactour/tour';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Page from '../../../layout/Page/Page';
import { demoPagesMenu } from '../../../menu';
import Button from '../../../components/bootstrap/Button';
import Popovers from '../../../components/bootstrap/Popovers';
import Icon from '../../../components/icon/Icon';
import CommonSalePerformance from '../../_common/CRMDashboard/CommonSalePerformance';
import CommonTopSales from '../../_common/CRMDashboard/CommonTopSales';
import CommonLatestTransActions from '../../_common/CRMDashboard/CommonLatestTransActions';
import CommonIncome from '../../_common/CRMDashboard/CommonIncome';

const CrmDashboard = () => {
	/**
	 * For Tour
	 */
	const { currentStep, setCurrentStep } = useTour();
	useEffect(() => {
		if (currentStep === 3) setCurrentStep(4);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [currentStep]);

	const [state, setState] = useState({
		selection: {
			startDate: dayjs().startOf('week').add(-1, 'week').toDate(),
			endDate: dayjs().endOf('week').toDate(),
			key: 'selection',
		},
		selection2: {
			startDate: dayjs().startOf('week').add(-1, 'week').add(2, 'day').toDate(),
			endDate: dayjs().endOf('week').add(-4, 'day').toDate(),
			key: 'selection2',
		},
		selection3: {
			startDate: dayjs().startOf('week').add(3, 'week').add(2, 'day').toDate(),
			endDate: dayjs().startOf('week').add(3, 'week').add(2, 'day').toDate(),
			key: 'selection3',
		},
	});

	const datePicker = (
		<DateRangePicker
			onChange={(item) => setState({ ...state, ...item })}
			// showSelectionPreview
			moveRangeOnFirstSelection={false}
			retainEndDateOnFirstSelection={false}
			months={2}
			ranges={[state.selection, state.selection2, state.selection3]}
			direction='horizontal'
			rangeColors={[
				String(import.meta.env.VITE_PRIMARY_COLOR),
				String(import.meta.env.VITE_SECONDARY_COLOR),
				String(import.meta.env.VITE_SUCCESS_COLOR),
			]}
		/>
	);

	return (
		<PageWrapper title={demoPagesMenu.crm.subMenu.dashboard.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Icon icon='Info' className='me-2' size='2x' />
					<span className='text-muted'>Check out latest updates.</span>
				</SubHeaderLeft>
				<SubHeaderRight>
					<Popovers
						placement='bottom-end'
						className='mw-100 overflow-hidden'
						data-tour='date-range-menu'
						bodyClassName='p-0'
						trigger='click'
						desc={datePicker}>
						<Button color='dark' isLight data-tour='date-range'>
							{`${dayjs(state.selection.startDate).format('MMM Do YY')} - ${dayjs(
								state.selection3.endDate,
							).format('MMM Do YY')}`}
						</Button>
					</Popovers>
				</SubHeaderRight>
			</SubHeader>
			<Page>
				<div className='row'>
					<div className='col-lg-8'>
						<CommonSalePerformance />
					</div>
					<div className='col-lg-4'>
						<CommonTopSales />
					</div>
					<div className='col-lg-6'>
						<CommonLatestTransActions />
					</div>
					<div className='col-lg-6'>
						<CommonIncome />
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default CrmDashboard;
