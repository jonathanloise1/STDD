import React, { FC, ReactNode } from 'react';
import Alert from '../../components/bootstrap/Alert';
import PrismCode from '../../components/extras/PrismCode';

const prismStyle: React.CSSProperties = {
	padding: '0',
	margin: '0',
	background: 'transparent',
	borderRadius: '0',
};

interface ICommonHowToUseProps {
	children: string | ReactNode;
	isPrism?: boolean;
}
const CommonHowToUse: FC<ICommonHowToUseProps> = ({ children, isPrism }) => {
	return (
		<Alert
			color='info'
			isLight
			shadow='md'
			borderWidth={0}
			icon='CustomReact'
			className='flex-nowrap w-100'>
			{isPrism ? (
				<PrismCode code={children as string} language='jsx' style={prismStyle} className='common-how-to-use-prism' />
			) : (
				<code>{children}</code>
			)}
		</Alert>
	);
};

export default CommonHowToUse;
