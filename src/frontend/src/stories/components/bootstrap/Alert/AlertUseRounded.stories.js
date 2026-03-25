import React from 'react';
import Alert from '../../../../components/bootstrap/Alert';
import AlertStories, { Default as DefaultStories } from './Alert.stories';

export default {
	...AlertStories,
	title: 'Components/<Alert>/PROPS/rounded',
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
	...DefaultStories.args,
	rounded: '0',
};
Zero.storyName = '0';

export const One = TemplateAlert.bind({});
One.args = {
	...DefaultStories.args,
	rounded: '1',
};
One.storyName = '1';

export const Two = TemplateAlert.bind({});
Two.args = {
	...DefaultStories.args,
	rounded: '2',
};
Two.storyName = '2';

export const Three = TemplateAlert.bind({});
Three.args = {
	...DefaultStories.args,
	rounded: '3',
};
Three.storyName = '3';

export const Pill = TemplateAlert.bind({});
Pill.args = {
	...DefaultStories.args,
	rounded: 'pill',
};
Pill.storyName = 'pill';
