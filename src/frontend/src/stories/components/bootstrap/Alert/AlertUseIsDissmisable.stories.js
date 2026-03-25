import React from 'react';
import Alert from '../../../../components/bootstrap/Alert';
import AlertStories, { Default } from './Alert.stories';

export default {
	...AlertStories,
	title: 'Components/<Alert>/PROPS/isDismissible',
	component: Alert,
};

const TemplateAlert = (args) => (
	// eslint-disable-next-line react/jsx-props-no-spreading
	<Alert {...args} />
);

export const True = TemplateAlert.bind({});
True.args = {
	...Default.args,
	isLight: true,
	isDismissible: true,
};
True.storyName = 'true';

export const False = TemplateAlert.bind({});
False.args = {
	...True.args,
	isDismissible: false,
};
False.storyName = 'false';
