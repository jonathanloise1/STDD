import React from 'react';
import Alert, { AlertHeading } from '../../../../components/bootstrap/Alert';

const CommonDashboardAlert = () => {
	return (
		<Alert
			icon='Verified'
			isLight
			color='primary'
			borderWidth={0}
			className='shadow-3d-primary'
			isDismissible>
			<AlertHeading tag='h2' className='h4'>
				Congratulations! 🎉
			</AlertHeading>
			<span>You have reached your monthly sales targets.</span>
		</Alert>
	);
};

export default CommonDashboardAlert;
