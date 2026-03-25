import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomMedium = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		fill='currentColor'
		viewBox='0 0 24 24'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path d='M3 3h18a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1m6.5 10.5H5V15h4.5c0 .83-.67 1.5-1.5 1.5H5V18h3c1.67 0 3-1.33 3-3s-1.33-3-3-3m0-3H5h4.5c0-.83-.67-1.5-1.5-1.5H5V6h3c1.67 0 3 1.33 3 3s-1.33 3-3 3m9-1.5c-.83 0-1.5.67-1.5 1.5h3c0-.83-.67-1.5-1.5-1.5m0-2c1.93 0 3.5 1.57 3.5 3.5v.5H15c.15 1.08 1.08 1.92 2.16 1.92.63 0 1.2-.26 1.63-.68l.85.85A3.5 3.5 0 1 1 18.5 7z' />
	</svg>
);
export default SvgCustomMedium;
