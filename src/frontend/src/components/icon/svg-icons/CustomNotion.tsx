import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomNotion = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		fill='currentColor'
		viewBox='0 0 24 24'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path d='M4 4a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zm2 0v16h12V4zm2 3h8v2H8zm0 4h8v2H8zm0 4h5v2H8z' />
	</svg>
);
export default SvgCustomNotion;
