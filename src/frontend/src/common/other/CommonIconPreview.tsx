import React, { FC } from 'react';
import { useClipboard } from 'use-clipboard-copy';
import Button from '../../components/bootstrap/Button';
import showNotification from '../../components/extras/showNotification';
import Icon from '../../components/icon/Icon';

interface ICommonIconPreviewProps {
	icon: string;
	forceFamily?: 'custom' | 'material' | null;
}
const CommonIconPreview: FC<ICommonIconPreviewProps> = ({ icon, forceFamily }) => {
	const clipboard = useClipboard();
	return (
		<div className='col-xl-3 col-lg-4 col-md-6 col-12'>
			<Button
				className='w-100 py-5 px-4'
				color='light'
				isLight
				shadow='default'
				hoverShadow='none'
				onClick={() => {
					clipboard.copy(icon);
					showNotification(
						'Copy to Clipboard',
						<div className='row d-flex align-items-center'>
							<div className='col-auto'>
								<Icon icon={icon} className='h1' />
							</div>
							<div className='col-auto h5'>{icon}</div>
						</div>,
					);
				}}>
				<div className='row d-flex align-items-center'>
					<div className='col-auto'>
						<Icon
							icon={icon}
							forceFamily={forceFamily}
							style={{
								fontSize: 'calc(1vh + 1vw)',
							}}
						/>
					</div>
					<div className='col-auto'>{icon}</div>
				</div>
			</Button>
		</div>
	);
};

export default CommonIconPreview;
