import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomHtml5 = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		width='1em'
		height='1em'
		viewBox='0 0 512 512'
		className='svg-icon'
		{...props}>
		<path
			fill='currentColor'
			fillRule='evenodd'
			d='m32 0 40.8 460.8L256 512l183.1-51.2L480 0zm359.5 150.7H176.8l5.1 57.9h204.5L371 382.1l-115 32.1h-.1L141 382.1l-8.1-90.8h56.3l4.2 47.1 62.5 17 .1-.1 62.6-17 6.5-73.3H130.6L115.5 94.2h281z'
		/>
	</svg>
);
export default SvgCustomHtml5;
