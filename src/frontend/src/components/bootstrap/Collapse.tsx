import React, { cloneElement, ElementType, FC, ReactNode, useRef, useState } from 'react';
import classNames from 'classnames';
import { Transition } from 'react-transition-group';
import {
	omit,
	pick,
	TransitionPropTypeKeys,
	TransitionStatuses,
	TransitionTimeouts,
} from './utils';

const transitionStatusToClassHash = {
	[TransitionStatuses.ENTERING]: 'collapsing',
	[TransitionStatuses.ENTERED]: 'collapse show',
	[TransitionStatuses.EXITING]: 'collapsing',
	[TransitionStatuses.EXITED]: 'collapse',
};

const getTransitionClass = (status: string) => {
	return transitionStatusToClassHash[status] || 'collapse';
};

const getHeight = (node: { scrollHeight: any } | null) => {
	// @ts-ignore
	return node.scrollHeight;
};

interface ICollapseProps extends Record<string, any> {
	tag?: ElementType | any;
	isOpen?: boolean;
	className?: string;
	isNavbar?: boolean;
	children: ReactNode;
	isChildClone?: boolean;
}
const Collapse: FC<ICollapseProps> = ({
	tag: Tag = 'div',
	isOpen,
	className,
	isNavbar,
	children,
	isChildClone,
	...restProps
}) => {
	const props = {
		...restProps,
		// @ts-ignore
		...Transition.defaultProps,
		timeout: restProps.timeout ?? TransitionTimeouts.Collapse,
	};
	const ref = useRef(null);
	const NODE = ref.current;

	const [height, setHeight] = useState<number | null>(null);

	const onEntering = (isAppearing: any) => {
		setHeight(getHeight(NODE));
		// @ts-ignore
		props.onEntering(NODE, isAppearing);
	};

	const onEntered = (isAppearing: any) => {
		setHeight(null);
		// @ts-ignore
		props.onEntered(NODE, isAppearing);
	};

	const onExit = () => {
		setHeight(getHeight(NODE));
		// @ts-ignore
		props.onExit(NODE);
	};

	const onExiting = () => {
		// getting this variable triggers a reflow
		// @ts-ignore
		const UNUSED = NODE.offsetHeight; // eslint-disable-line @typescript-eslint/no-unused-vars
		setHeight(0);

		// @ts-ignore
		props.onExiting(NODE);
	};

	const onExited = () => {
		setHeight(null);
		// @ts-ignore
		props.onExited(NODE);
	};

	const transitionProps = pick(props, TransitionPropTypeKeys);
	const childProps = omit(props, TransitionPropTypeKeys);

	return (
		// @ts-ignore
		<Transition
			nodeRef={ref}
			// eslint-disable-next-line react/jsx-props-no-spreading
			{...transitionProps}
			in={isOpen}
			onEntering={onEntering}
			onEntered={onEntered}
			onExit={onExit}
			onExiting={onExiting}
			onExited={onExited}>
			{(status) => {
				const collapseClass = getTransitionClass(status);
				const classes = classNames(className, collapseClass, isNavbar && 'navbar-collapse');
				const style = height === null ? null : { height };
				if (isChildClone) {
					// @ts-ignore
					return cloneElement(children, {
						ref,
						// @ts-ignore
						style: { ...childProps.style, ...style },
						// @ts-ignore
						className: classNames(classes, children.props.className),
						...childProps,
					});
				}
				return (
					<Tag
						// eslint-disable-next-line react/jsx-props-no-spreading
						{...childProps}
						// @ts-ignore
						style={{ ...childProps.style, ...style }}
						className={classes}
						ref={ref}>
						{children}
					</Tag>
				);
			}}
		</Transition>
	);
};

export default Collapse;
