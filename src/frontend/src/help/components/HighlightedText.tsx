import React, { useMemo } from 'react';
import { highlight } from '../runtime/searcher';
import type { Analyzer } from '../index';

interface Props {
	text: string;
	stems: ReadonlySet<string>;
	analyzer: Analyzer;
}

/** A text in which the words whose stem matches a query stem are bold (manual.md 5.3). */
export const HighlightedText = ({ text, stems, analyzer }: Props) => {
	const segments = useMemo(() => highlight(text, stems, analyzer), [text, stems, analyzer]);
	return (
		<>
			{segments.map((s, i) =>
				s.bold ? (
					<strong key={`${i}-b`}>{s.text}</strong>
				) : (
					<React.Fragment key={`${i}-t`}>{s.text}</React.Fragment>
				),
			)}
		</>
	);
};
