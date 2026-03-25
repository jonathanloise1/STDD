import React, { FC, HTMLAttributes, ReactNode } from 'react';
import classNames from 'classnames';
import { useMeasure } from 'react-use';

interface IReactCreditCardsContainerProps extends HTMLAttributes<HTMLDivElement> {
	className?: string;
	children: ReactNode;
	is3dShadow?: boolean;
	issuer?: string;
	scale?: number;
}
const ReactCreditCardsContainer: FC<IReactCreditCardsContainerProps> = ({
	className,
	is3dShadow = true,
	issuer,
	scale = 0,
	children,
	...props
}) => {
	const [ref, { width }] = useMeasure<HTMLDivElement>();
	const zoomValue = scale ? (width / 290) * scale : width < 290 ? width / 290 : undefined;
	return (
		<div
			ref={ref}
			className={classNames(
				{
					[`rccs-shadow-3d-${
						(issuer === 'visa' && 'info') ||
						(issuer === 'mastercard' && 'warning') ||
						(issuer === 'amex' && 'success') ||
						(issuer === 'hipercard' && 'danger') ||
						'dark'
					}`]: is3dShadow,
				},
				className,
			)}
			style={zoomValue ? { '--rccs-zoom': zoomValue } as React.CSSProperties : undefined}
			{...props}>
			{children}
		</div>
	);
};

export default ReactCreditCardsContainer;
