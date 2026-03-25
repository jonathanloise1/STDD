import React from 'react';
import Header, { HeaderLeft } from '../../../layout/Header/Header';
import Navigation from '../../../layout/Navigation/Navigation';
import { pageLayoutTypesPagesMenu } from '../../../menu';
import useDeviceScreen from '../../../hooks/useDeviceScreen';
import CommonHeaderRight from './CommonHeaderRight';

const PageLayoutHeader = () => {
	const { width } = useDeviceScreen();
	return (
		<Header>
			<HeaderLeft>
				<Navigation
					menu={{
						...pageLayoutTypesPagesMenu.pageLayout.subMenu,
						...pageLayoutTypesPagesMenu.asideTypes.subMenu,
					}}
					id='header-top-menu'
					horizontal={
						!!width && width >= Number(import.meta.env.VITE_MOBILE_BREAKPOINT_SIZE)
					}
				/>
			</HeaderLeft>
			<CommonHeaderRight />
		</Header>
	);
};

export default PageLayoutHeader;
