import React from 'react';
import Alert from '../../../../components/bootstrap/Alert';
import AlertStories, { Default as DefaultStories } from './Alert.stories';

export default {
	...AlertStories,
	title: 'Components/<Alert>/PROPS/borderWidth',
};

const TemplateAlert = (args) => (
	// eslint-disable-next-line react/jsx-props-no-spreading
	<Alert {...args} />
);

export const Default = TemplateAlert.bind({});
Default.args = {
	...DefaultStories.args,
	isOutline: true,
};

export const Zero = TemplateAlert.bind({});
Zero.args = {
	...Default.args,
	borderWidth: '0',
};
Zero.storyName = '0';

export const One = TemplateAlert.bind({});
One.args = {
	...Default.args,
	borderWidth: '1',
};
One.storyName = '1';

export const Two = TemplateAlert.bind({});
Two.args = {
	...Default.args,
	borderWidth: '2',
};
Two.storyName = '2';

export const Three = TemplateAlert.bind({});
Three.args = {
	...Default.args,
	borderWidth: '3',
};
Three.storyName = '3';

export const Four = TemplateAlert.bind({});
Four.args = {
	...Default.args,
	borderWidth: '4',
};
Four.storyName = '4';

export const Five = TemplateAlert.bind({});
Five.args = {
	...Default.args,
	borderWidth: '5',
};
Five.storyName = '5';
