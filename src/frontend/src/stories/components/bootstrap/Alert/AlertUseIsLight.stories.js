import React from 'react';
import Alert from '../../../../components/bootstrap/Alert';
import AlertStories, { Default } from './Alert.stories';

export default {
	...AlertStories,
	title: 'Components/<Alert>/PROPS/isLight',
};

const TemplateAlert = (args) => (
	// eslint-disable-next-line react/jsx-props-no-spreading
	<Alert {...args} />
);

export const primary = TemplateAlert.bind({});
primary.args = {
	...Default.args,
	isLight: true,
};
primary.storyName = 'primary';

export const secondary = TemplateAlert.bind({});
secondary.args = {
	...Default.args,
	color: 'secondary',
};
secondary.storyName = 'secondary';

export const success = TemplateAlert.bind({});
success.args = {
	...Default.args,
	color: 'success',
};
success.storyName = 'success';

export const info = TemplateAlert.bind({});
info.args = {
	...Default.args,
	color: 'info',
};
info.storyName = 'info';

export const warning = TemplateAlert.bind({});
warning.args = {
	...Default.args,
	color: 'warning',
};
warning.storyName = 'warning';

export const danger = TemplateAlert.bind({});
danger.args = {
	...Default.args,
	color: 'danger',
};
danger.storyName = 'danger';

export const light = TemplateAlert.bind({});
light.args = {
	...Default.args,
	color: 'light',
};
light.storyName = 'light';

export const dark = TemplateAlert.bind({});
dark.args = {
	...Default.args,
	color: 'dark',
};
dark.storyName = 'dark';
