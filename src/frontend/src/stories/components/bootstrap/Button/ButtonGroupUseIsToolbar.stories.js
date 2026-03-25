import React from 'react';

import Button, { ButtonGroup } from '../../../../components/bootstrap/Button';
import ButtonGroupStories from './ButtonGroup.stories';

export default {
	...ButtonGroupStories,
	title: 'Components/<ButtonGroup>/PROPS/isToolbar',
	argTypes: {
		...ButtonGroupStories.argTypes,
		size: { if: { arg: 'parent', exists: true } },
		children: { if: { arg: 'parent', exists: true } },
	},
};

const Template = (args) => {
	return (
		// eslint-disable-next-line react/jsx-props-no-spreading
		<ButtonGroup {...args}>
			<ButtonGroup>
				<Button color='primary'>Button</Button>
				<Button color='primary'>Button</Button>
				<Button color='primary'>Button</Button>
			</ButtonGroup>
			<ButtonGroup>
				<Button color='info'>Button</Button>
				<Button color='info'>Button</Button>
			</ButtonGroup>
		</ButtonGroup>
	);
};

export const True = Template.bind({});
True.args = {
	isToolbar: true,
};
True.storyName = 'true';
