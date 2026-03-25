import React from 'react';

import Button, { ButtonGroup } from '../../../../components/bootstrap/Button';
import ButtonGroupStories, { Default } from './ButtonGroup.stories';

export default {
	...ButtonGroupStories,
	title: 'Components/<ButtonGroup>/PROPS/isVertical',
};

const Template = (args) => {
	return (
		// eslint-disable-next-line react/jsx-props-no-spreading
		<ButtonGroup {...args}>
			{}
			{args.children.map((item) => (
				// eslint-disable-next-line react/jsx-props-no-spreading
				<Button key={item.children} {...item} />
			))}
		</ButtonGroup>
	);
};

export const True = Template.bind({});
True.args = {
	...Default.args,
	isVertical: true,
};
True.storyName = 'true';

export const False = Template.bind({});
False.args = {
	...True.args,
	isVertical: false,
};
False.storyName = 'false';
