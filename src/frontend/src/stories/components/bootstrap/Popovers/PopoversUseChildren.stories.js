import React from 'react';
import Popovers from '../../../../components/bootstrap/Popovers';
import PopoversStories, { Default } from './Popovers.stories';
import Button from '../../../../components/bootstrap/Button';

export default {
	...PopoversStories,
	title: 'Components/<Popovers>/PROPS/children',
};

const Template = (args) => {
	// eslint-disable-next-line react/jsx-props-no-spreading
	return <Popovers {...args} />;
};

export const String = Template.bind({});
String.args = {
	...Default.args,
};
String.storyName = 'string';

export const Html = Template.bind({});
Html.args = {
	...Default.args,
	children: <strong>strong</strong>,
};
Html.storyName = 'HTML';

export const ReactComponent = Template.bind({});
ReactComponent.args = {
	...Default.args,
	children: (
		<Button color='primary' isLight icon='Send' size='lg'>
			Button
		</Button>
	),
	trigger: 'hover',
};
ReactComponent.storyName = 'React Component';
