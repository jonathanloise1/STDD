import React, { SVGProps } from 'react';

const SvgCustomPoland = (props: SVGProps<SVGSVGElement>) => (
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
				fill: '#ffffff',
			}}
			d='M0 0h512v256H0z'
		/>
		<path
			style={{
				fill: '#dc143c',
			}}
			d='M0 256h512v256H0z'
		/>
	</svg>
);

export default SvgCustomPoland;
