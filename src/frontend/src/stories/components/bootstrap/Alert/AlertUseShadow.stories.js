import React from 'react';
import Alert from '../../../../components/bootstrap/Alert';
import AlertStories, { Default as DefaultStories } from './Alert.stories';

export default {
	...AlertStories,
	title: 'Components/<Alert>/PROPS/shadow',
};

const TemplateAlert = (args) => (
	// eslint-disable-next-line react/jsx-props-no-spreading
	<Alert {...args} />
);

export const Default = TemplateAlert.bind({});
Default.args = {
	...DefaultStories.args,
	isLight: true,
};

export const Small = TemplateAlert.bind({});
Small.args = {
	...DefaultStories.args,
	shadow: 'sm',
};
Small.storyName = 'sm';

export const Medium = TemplateAlert.bind({});
Medium.args = {
	...DefaultStories.args,
	shadow: 'md',
};
Medium.storyName = 'md';

export const Large = TemplateAlert.bind({});
Large.args = {
	...DefaultStories.args,
	shadow: 'lg',
};
Large.storyName = 'lg';
