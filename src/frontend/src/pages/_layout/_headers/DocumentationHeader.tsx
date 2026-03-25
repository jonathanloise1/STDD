import React from 'react';
import Header, { HeaderLeft } from '../../../layout/Header/Header';
import Navigation from '../../../layout/Navigation/Navigation';
import { gettingStartedPagesMenu } from '../../../menu';
import useDeviceScreen from '../../../hooks/useDeviceScreen';
import CommonHeaderRight from './CommonHeaderRight';

const ComponentsHeader = () => {
	const { width } = useDeviceScreen();

	return (
		<Header>
			<HeaderLeft>
				<Navigation
					menu={gettingStartedPagesMenu}
					id='docMenu-top-menu'
					horizontal={
						!!width && width >= Number(import.meta.env.VITE_MOBILE_BREAKPOINT_SIZE)
					}
				/>
			</HeaderLeft>
			<CommonHeaderRight />
		</Header>
	);
};

export default ComponentsHeader;
