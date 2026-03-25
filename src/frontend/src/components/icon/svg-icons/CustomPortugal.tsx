import React, { SVGProps } from 'react';

const SvgCustomPortugal = (props: SVGProps<SVGSVGElement>) => (
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
				fill: '#006600',
			}}
			d='M0 0h170.67v512H0z'
		/>
		<path
			style={{
				fill: '#ff0000',
			}}
			d='M170.67 0H512v512H170.67z'
		/>
		<circle
			style={{
				fill: '#ffcc00',
			}}
			cx='170.67'
			cy='256'
			r='80'
		/>
	</svg>
);

export default SvgCustomPortugal;
