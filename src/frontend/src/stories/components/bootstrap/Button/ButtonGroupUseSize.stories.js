import React from 'react';

import Button, { ButtonGroup } from '../../../../components/bootstrap/Button';
import ButtonGroupStories, { Default as DefaultStories } from './ButtonGroup.stories';

export default {
	...ButtonGroupStories,
	title: 'Components/<ButtonGroup>/PROPS/size',
	component: ButtonGroup,
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

export const Default = Template.bind({});
Default.args = {
	...DefaultStories.args,
};

export const Small = Template.bind({});
Small.args = {
	...Default.args,
	size: 'sm',
};
Small.storyName = 'sm';

export const Large = Template.bind({});
Large.args = {
	...Default.args,
	size: 'lg',
};
Large.storyName = 'lg';
