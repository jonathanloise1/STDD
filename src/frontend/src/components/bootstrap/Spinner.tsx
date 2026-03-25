import React, { ReactNode } from 'react';
import classNames from 'classnames';
import TagWrapper from '../TagWrapper';
import { TColor } from '../../type/color-type';

interface ISpinnerProps {
	children?: ReactNode;
	tag?: 'div' | 'span';
	color?: TColor | null;
	isGrow?: boolean;
	isSmall?: boolean;
	size?: string | number | null;
	inButton?: boolean | 'onlyIcon';
	className?: string;
	ref?: React.Ref<HTMLDivElement>;
}
const Spinner = ({
	tag = 'div',
	color,
	isGrow,
	isSmall,
	size,
	children = 'Loading...',
	inButton,
	className,
	ref,
	...props
}: ISpinnerProps) => {
		const HIDDEN_TEXT = <span className='visually-hidden'>{children}</span>;
		return (
			<>
				<TagWrapper
					ref={ref}
					tag={inButton ? 'span' : tag}
					className={classNames(
						{ 'spinner-border': !isGrow, 'spinner-grow': isGrow },
						{
							'spinner-border-sm': !isGrow && isSmall,
							'spinner-grow-sm': isGrow && isSmall,
						},
						{ [`text-${color}`]: color },
						{ 'me-2': inButton !== 'onlyIcon' && !!inButton },
						className,
					)}
					style={size ? { height: size, width: size } : undefined}
					role='status'
					aria-hidden={inButton ? 'true' : undefined}
					// eslint-disable-next-line react/jsx-props-no-spreading
					{...props}>
					{inButton !== 'onlyIcon' && !!inButton ? HIDDEN_TEXT : ''}
				</TagWrapper>
				{inButton === 'onlyIcon' ? HIDDEN_TEXT : null}
			</>
	);
};
Spinner.displayName = 'Spinner';

export default Spinner;
