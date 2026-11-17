import React from 'react';
import { Link } from 'react-router-dom';
import type { BundleInline } from '../runtime/types';

interface Props {
	inlines: BundleInline[];
	/** Called when a link is followed, for the panel to close. */
	onNavigate?: () => void;
}

/** Text, bold and in-app links of a manual block. No HTML from the bundle is inserted into the page. */
export const Inlines = ({ inlines, onNavigate }: Props) => (
	<>
		{inlines.map((inline, i) => {
			const key = `${i}-${inline.type}`;
			if (inline.type === 'bold') return <strong key={key}>{inline.text}</strong>;
			if (inline.type === 'link')
				return (
					<Link key={key} to={inline.href} onClick={onNavigate}>
						{inline.text}
					</Link>
				);
			return <React.Fragment key={key}>{inline.text}</React.Fragment>;
		})}
	</>
);
