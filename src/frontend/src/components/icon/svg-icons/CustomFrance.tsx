import * as React from 'react';
import type { SVGProps } from 'react';
const SvgCustomFrance = (props: SVGProps<SVGSVGElement>) => (
	<svg
		xmlns='http://www.w3.org/2000/svg'
		width='1em'
		height='1em'
		fill='none'
		viewBox='0 0 512 512'
		className='svg-icon'
		{...props}>
		<mask
			id='custom-france_svg__a'
			width={512}
			height={512}
			x={0}
			y={0}
			maskUnits='userSpaceOnUse'
			style={{
				maskType: 'alpha',
			}}>
			<path
				fill='#036738'
				d='M400 0H112C50.144 0 0 50.144 0 112v288c0 61.856 50.144 112 112 112h288c61.856 0 112-50.144 112-112V112C512 50.144 461.856 0 400 0'
			/>
		</mask>
		<g mask='url(#custom-france_svg__a)'>
			<path
				fill='#41479B'
				d='M38.42 0C17.2 0 0 26.202 0 58.527v394.947C0 485.796 17.2 512 38.42 512H171V0z'
			/>
			<path fill='#F5F5F5' d='M341 0H171v512h170z' />
			<path
				fill='#FF4B55'
				d='M473.58 0H341v512h132.58c21.219 0 38.42-26.202 38.42-58.527V58.527C512 26.202 494.8 0 473.58 0'
			/>
		</g>
	</svg>
);
export default SvgCustomFrance;
