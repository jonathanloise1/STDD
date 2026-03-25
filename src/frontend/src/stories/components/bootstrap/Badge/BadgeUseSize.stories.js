import React from 'react';
import Badge from '../../../../components/bootstrap/Badge';
import BadgeStories from './Badge.stories';
import TagWrapper from '../../../../components/TagWrapper';

export default {
	...BadgeStories,
	title: 'Components/<Badge>/PROPS/size',
};

// eslint-disable-next-line react/prop-types
const Template = ({ tag, ...args }) => (
	<TagWrapper tag={tag}>
		{/* eslint-disable-next-line react/jsx-props-no-spreading */}
		<Badge {...args} />
	</TagWrapper>
);

export const Default = Template.bind({});
Default.args = {
	children: 'Badge',
	tag: 'h1',
};
