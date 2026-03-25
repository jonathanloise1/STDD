import React from 'react';

import Button from '../../../../components/bootstrap/Button';
import ButtonStories, { Default as DefaultStories } from './Button.stories';

export default {
	...ButtonStories,
	title: 'Components/<Button>/PROPS/rounded',
};

// eslint-disable-next-line react/jsx-props-no-spreading
const Template = (args) => <Button {...args} />;

export const Default = Template.bind({});
Default.args = {
	...DefaultStories.args,
	rounded: 'default',
};

export const Zero = Template.bind({});
Zero.args = {
	...DefaultStories.args,
	rounded: '0',
};

export const One = Template.bind({});
One.args = {
	...DefaultStories.args,
	rounded: '1',
};

export const Two = Template.bind({});
Two.args = {
	...DefaultStories.args,
	rounded: '2',
};

export const Three = Template.bind({});
Three.args = {
	...DefaultStories.args,
	rounded: '3',
};

export const Bottom = Template.bind({});
Bottom.args = {
	...DefaultStories.args,
	rounded: 'bottom',
};

export const Top = Template.bind({});
Top.args = {
	...DefaultStories.args,
	rounded: 'top',
};

export const Circle = Template.bind({});
Circle.args = {
	...DefaultStories.args,
	rounded: 'circle',
};

export const End = Template.bind({});
End.args = {
	...DefaultStories.args,
	rounded: 'end',
};

export const Start = Template.bind({});
Start.args = {
	...DefaultStories.args,
	rounded: 'start',
};

export const Pill = Template.bind({});
Pill.args = {
	...DefaultStories.args,
	rounded: 'pill',
};
