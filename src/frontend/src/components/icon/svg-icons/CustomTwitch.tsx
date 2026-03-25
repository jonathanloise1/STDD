import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomTwitch = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		fill='currentColor'
		viewBox='0 0 24 24'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path d='M11.64 5.93h1.43v4.28h-1.43m3.93-4.28H17v4.28h-1.43M7 2 3.43 5.57v12.86h4.28V22l3.58-3.57h2.85L20.57 12V2m-1.43 9.29-2.85 2.85h-2.86l-2.5 2.5v-2.5H7.36V3.43h11.78z' />
	</svg>
);
export default SvgCustomTwitch;
