import React, { CSSProperties, FC, useEffect, useRef } from 'react';
import Prism from 'prismjs';
import classNames from 'classnames';
import 'prismjs/components/prism-scss.min';
import 'prismjs/components/prism-jsx.min';
import 'prismjs/plugins/line-numbers/prism-line-numbers.css';
import 'prismjs/plugins/line-numbers/prism-line-numbers.min';

interface IPrismCodeProps {
	code: string;
	plugins?: string | null;
	language: string | null | undefined;
	className?: string;
	style?: CSSProperties | undefined;
	rounded?: 'default' | 0 | 1 | 2 | 3 | 'bottom' | 'top' | 'circle' | 'end' | 'start' | 'pill';
}
const PrismCode: FC<IPrismCodeProps> = (props) => {
	const ref = useRef<HTMLElement>(null);

	useEffect(() => {
		if (ref && ref.current) {
			Prism.highlightElement(ref.current);
		}
		return () => {};
	}, [ref]);

	const { code, plugins, language, className, style, rounded = 2 } = props;
	return (
		<pre
			className={classNames('prismjs-code', plugins, className, {
				[`rounded${rounded !== 'default' ? `-${rounded}` : ''}`]: rounded,
			})}
			style={style}>
			<code ref={ref} className={`language-${language}`}>
				{code.trim()}
			</code>
		</pre>
	);
};

export default PrismCode;
