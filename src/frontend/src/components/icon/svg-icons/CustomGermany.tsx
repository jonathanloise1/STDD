import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomGermany = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		xmlSpace='preserve'
		viewBox='0 0 512 512'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path
			d='M400.005 0h-288.01C50.141.002 0 50.146 0 112v58.667h512V112C512 50.146 461.858.002 400.005 0'
			style={{
				fill: '#464655',
			}}
		/>
		<path
			d='M0 400c0 61.856 50.144 112 112 112h288c61.856 0 112-50.144 112-112v-58.667H0z'
			style={{
				fill: '#ffe15a',
			}}
		/>
		<path
			d='M0 170.67h512v170.67H0z'
			style={{
				fill: '#ff4b55',
			}}
		/>
	</svg>
);
export default SvgCustomGermany;
