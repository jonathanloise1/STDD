import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomFigma = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		fill='currentColor'
		viewBox='0 0 24 24'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path d='M5 5.5A3.5 3.5 0 0 1 8.5 2H12v7.5H8.5a3.5 3.5 0 0 1-3.5-4M12 2h3.5a3.5 3.5 0 1 1 0 7H12zm0 12.5v-5h3.5a3.5 3.5 0 1 1 0 7H12a3.5 3.5 0 0 1 0-2.5m-7 0a3.5 3.5 0 0 1 3.5-5H12v7.5H8.5a3.5 3.5 0 0 1-3.5-4m0 7a3.5 3.5 0 1 0 7 0V16H8.5A3.5 3.5 0 0 0 5 19.5m14.5-4a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5' />
	</svg>
);
export default SvgCustomFigma;
