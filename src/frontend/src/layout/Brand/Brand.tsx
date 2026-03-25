import React, { FC } from 'react';
import { Link } from 'react-router-dom';
import Icon from '../../components/icon/Icon';
import Logo from '../../components/Logo';

interface IBrandProps {
	asideStatus: boolean;
	setAsideStatus(...args: unknown[]): unknown;
}
const Brand: FC<IBrandProps> = ({ asideStatus, setAsideStatus }) => {
	return (
		<div className='brand'>
			<div className='brand-logo'>
				<h1 className='brand-title '>
					<Link to='/' aria-label='Logo'>
						<Logo height={32} />
					</Link>
				</h1>
			</div>
			<button
				type='button'
				className='btn brand-aside-toggle'
				aria-label='Toggle Aside'
				onClick={() => {
					console.log('[DEBUG Brand] click! asideStatus before:', asideStatus);
					setAsideStatus(!asideStatus);
				}}>
				<Icon icon='FirstPage' className='brand-aside-toggle-close' />
				<Icon icon='LastPage' className='brand-aside-toggle-open' />
			</button>
		</div>
	);
};

export default Brand;
