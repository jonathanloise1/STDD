import React, { SVGProps } from 'react';

const SvgCustomItaly = (props: SVGProps<SVGSVGElement>) => (
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
				fill: '#009246',
			}}
			d='M0 0h170.67v512H0z'
		/>
		<path
			style={{
				fill: '#f5f5f5',
			}}
			d='M170.67 0h170.67v512H170.67z'
		/>
		<path
			style={{
				fill: '#ce2b37',
			}}
			d='M341.33 0H512v512H341.33z'
		/>
	</svg>
);

export default SvgCustomItaly;