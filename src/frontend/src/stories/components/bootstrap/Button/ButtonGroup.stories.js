import React from 'react';

import Button, { ButtonGroup } from '../../../../components/bootstrap/Button';
import TagWrapper from '../../../../components/TagWrapper';

import * as ButtonStories from './Button.stories';

export default {
	title: 'Components/<ButtonGroup>',
	component: ButtonGroup,
	subcomponents: { Button, TagWrapper },
	argTypes: {
		isToolbar: { control: { type: 'boolean' } },
		isVertical: { control: { type: 'boolean' } },
		size: { control: { type: 'inline-radio' } },
		ariaLabel: { control: { type: 'text' } },
	},
	args: { ...ButtonGroup.defaultProps },
	parameters: {
		docs: {
			description: {
				component: 'Button component',
			},
		},
	},
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
	children: [
		{ ...ButtonStories.Default.args },
		{ ...ButtonStories.Default.args, children: 'Button 2' },
		{ ...ButtonStories.Default.args, children: 'Button 3' },
	],
};
