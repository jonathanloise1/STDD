import React, { Children, cloneElement, FC, HTMLAttributes, ReactNode } from 'react';
import classNames from 'classnames';
import { randomColor } from '../helpers/helpers';
import Popovers from './bootstrap/Popovers';
import useDarkMode from '../hooks/useDarkMode';
import { TColor } from '../type/color-type';

interface IAvatarGroupProps {
	className?: string;
	children: ReactNode[];
	size?: number;
}
export const AvatarGroup: FC<IAvatarGroupProps> = ({ className, children, size = 32 }) => {
	const { darkModeStatus } = useDarkMode();

	return (
		<div className={classNames('avatar-group', className)}>
			<div className='avatar-container'>
				{Children.map(children, (child, index) =>
					index < 3
						? // @ts-ignore
							cloneElement(child, {
								borderColor: darkModeStatus ? 'dark' : 'white',
								border: 2,
								// @ts-ignore
								color: child.props.color || randomColor(),
								size,
							})
						: null,
				)}
			</div>
			{children?.length > 3 && (
				<Popovers
					desc={Children.map(children, (child, index) =>
						index >= 3 ? (
							<>
								{/* @ts-ignore */}
								{child.props.userName}
								<br />
							</>
						) : null,
					)}
					trigger='hover'>
					<div className='avatar-more' style={{ width: size, height: size }}>
						{}+{children?.length - 3}
					</div>
				</Popovers>
			)}
		</div>
	);
};

interface IAvatarProps extends HTMLAttributes<HTMLImageElement> {
	border?: null | 0 | 1 | 2 | 3 | 4 | 5;
	borderColor?: null | TColor | 'link' | 'brand' | 'brand-two' | 'storybook' | 'white';
	className?: string;
	color?: TColor | 'link' | 'brand' | 'brand-two' | 'storybook';
	isOnline?: boolean;
	isReply?: boolean;
	rounded?: 'default' | 0 | 1 | 2 | 3 | 'bottom' | 'top' | 'circle' | 'end' | 'start' | 'pill';
	shadow?: 'none' | 'sm' | 'default' | 'lg' | null;
	size?: number;
	src: string;
	srcSet?: string | undefined;
	userName?: string | null;
	ref?: React.Ref<HTMLImageElement>;
}
const Avatar = ({
		srcSet,
		src,
		className,
		size = 128,
		rounded = 'circle',
		shadow,
		color = 'primary',
		border,
		borderColor,
		userName,
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		isOnline, // Not used
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		isReply, // Not used
		ref,
		...props
	}: IAvatarProps) => {
		const { darkModeStatus } = useDarkMode();

		const INNER = (
			<img
				ref={ref}
				className={classNames(
					'avatar',
					{
						[`rounded${rounded !== 'default' ? `-${rounded}` : ''}`]: rounded,
						'rounded-0': rounded === 0,
						[`shadow${shadow !== 'default' ? `-${shadow}` : ''}`]: !!shadow,
						border: !!border,
						[`border-${border}`]: !!border,
						[`border-${borderColor}`]: borderColor,
					},
					`bg-l${darkModeStatus ? 'o' : ''}25-${color}`,
					className,
				)}
				srcSet={srcSet}
				src={src}
				alt='Avatar'
				width={size}
				height={size}
				{...props}
			/>
		);

		if (userName) {
			return (
				<Popovers desc={userName} trigger='hover'>
					{INNER}
				</Popovers>
			);
		}
		return INNER;
	};
Avatar.displayName = 'Avatar';

export default Avatar;
