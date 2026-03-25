import React from 'react';
import Header, { HeaderLeft } from '../../../layout/Header/Header';
import Navigation from '../../../layout/Navigation/Navigation';
import { componentPagesMenu } from '../../../menu';
import useDeviceScreen from '../../../hooks/useDeviceScreen';
import CommonHeaderRight from './CommonHeaderRight';

const UtilitiesHeader = () => {
	const { width } = useDeviceScreen();

	return (
		<Header>
			<HeaderLeft>
				<Navigation
					menu={componentPagesMenu.utilities.subMenu}
					id={`${componentPagesMenu.utilities.id}top-menu`}
					horizontal={
						!!width && width >= Number(import.meta.env.VITE_MOBILE_BREAKPOINT_SIZE)
					}
				/>
			</HeaderLeft>
			<CommonHeaderRight />
		</Header>
	);
};

export default UtilitiesHeader;
