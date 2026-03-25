import React, { SVGProps } from 'react';

const SvgCustomSpain = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		viewBox='0 0 512 512'
		xmlSpace='preserve'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path
			style={{
				fill: '#c60b1e',
			}}
			d='M0 0h512v128H0z'
		/>
		<path
			style={{
				fill: '#ffc400',
			}}
			d='M0 128h512v256H0z'
		/>
		<path
			style={{
				fill: '#c60b1e',
			}}
			d='M0 384h512v128H0z'
		/>
	</svg>
);

export default SvgCustomSpain;
