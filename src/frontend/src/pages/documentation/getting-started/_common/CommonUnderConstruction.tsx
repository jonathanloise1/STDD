import React from 'react';
import Pose from '../../../../assets/he_multitasking.png';

const CommonUnderConstruction = () => {
	return (
		<div className='row'>
			<div className='col-12'>
				<img className='w-100' src={Pose} alt='Pose' />
			</div>
			<div className='col-12'>
				<div className='display-4 fw-bold py-3 d-flex justify-content-center'>
					Under Construction
				</div>
			</div>
		</div>
	);
};

export default CommonUnderConstruction;
