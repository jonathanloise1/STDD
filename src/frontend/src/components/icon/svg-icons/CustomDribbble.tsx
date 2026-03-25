import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomDribbble = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		fill='currentColor'
		viewBox='0 0 24 24'
		width='1em'
		height='1em'
		className='svg-icon'
		{...props}>
		<path d='M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10c.85 0 1.68-.1 2.47-.31l-.95-3.66a6.3 6.3 0 0 1-1.52.37A6.4 6.4 0 0 1 7.35 7.57a6.4 6.4 0 0 1 10.83 4.65 6.3 6.3 0 0 1-.37 2.13l3.67.96c.2-.79.32-1.62.32-2.47A9.98 9.98 0 0 0 12 2.2m2.93 7.23a3.33 3.33 0 1 0 0 6.66c.33 0 .66-.05.96-.14l-.95-3.66.94-3.72c-.3-.09-.62-.14-.95-.14' />
	</svg>
);
export default SvgCustomDribbble;
