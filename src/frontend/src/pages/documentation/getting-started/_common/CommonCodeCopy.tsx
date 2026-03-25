import React from 'react';
import { useClipboard } from 'use-clipboard-copy';
import { CardCodeView } from '../../../../components/bootstrap/Card';
import Button from '../../../../components/bootstrap/Button';

const CommonCodeCopy = ({ children }: { children: string }) => {
	const clipboard = useClipboard();
	return (
		<div className='row'>
			<div className='col'>
				<CardCodeView>{children}</CardCodeView>
			</div>
			<div className='col-auto'>
				<Button
					className='h-100'
					color='info'
					isLight
					icon='ContentCopy'
					onClick={() => clipboard.copy(children)}>
					Copy
				</Button>
			</div>
		</div>
	);
};

export default CommonCodeCopy;
