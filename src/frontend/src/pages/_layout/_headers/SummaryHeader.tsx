import React from 'react';
import Header, { HeaderLeft } from '../../../layout/Header/Header';
import Navigation from '../../../layout/Navigation/Navigation';
import { summaryPageTopMenu } from '../../../menu';
import useDeviceScreen from '../../../hooks/useDeviceScreen';
import CommonHeaderRight from './CommonHeaderRight';

const SummaryHeader = () => {
	const { width } = useDeviceScreen();

	return (
		<Header>
			<HeaderLeft>
				<Navigation
					menu={summaryPageTopMenu}
					id='summary-page-top-menu'
					horizontal={
						!!width && width >= Number(import.meta.env.VITE_MOBILE_BREAKPOINT_SIZE)
					}
				/>
			</HeaderLeft>
			<CommonHeaderRight />
		</Header>
	);
};

export default SummaryHeader;
