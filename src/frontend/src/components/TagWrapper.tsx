import React, { ElementType, HTMLAttributes, ReactNode } from 'react';

interface ITagWrapper extends Record<string, any>, HTMLAttributes<HTMLElement> {
	children: ReactNode;
	tag: ElementType;
	ref?: React.Ref<HTMLDivElement | HTMLAnchorElement | HTMLFormElement>;
}
const TagWrapper = ({ tag: Tag = 'div', children, ref, ...props }: ITagWrapper) => {
	return (
		// eslint-disable-next-line react/jsx-props-no-spreading
		<Tag ref={ref} {...props}>
			{children}
		</Tag>
	);
};
TagWrapper.displayName = 'TagWrapper';

export default TagWrapper;
