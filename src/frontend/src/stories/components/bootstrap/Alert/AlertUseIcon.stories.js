import React from 'react';
import Alert from '../../../../components/bootstrap/Alert';
import AlertStories, { Default } from './Alert.stories';

export default {
	...AlertStories,
	title: 'Components/<Alert>/PROPS/icon',
};

const TemplateAlert = (args) => (
	// eslint-disable-next-line react/jsx-props-no-spreading
	<Alert {...args} />
);

export const Star = TemplateAlert.bind({});
Star.args = {
	...Default.args,
	isLight: true,
	isDismissible: true,
	icon: 'Star',
};

export const Group = TemplateAlert.bind({});
Group.args = {
	...Star.args,
	icon: 'Group',
};
