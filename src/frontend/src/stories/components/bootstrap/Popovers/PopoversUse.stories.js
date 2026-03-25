import React from 'react';
import Popovers from '../../../../components/bootstrap/Popovers';
import PopoversStories, { Default } from './Popovers.stories';

export default {
	...PopoversStories,
	title: 'Components/<Popovers>/PROPS',
};

const Template = (args) => {
	// eslint-disable-next-line react/jsx-props-no-spreading
	return <Popovers {...args} />;
};

export const Placement = Template.bind({});
Placement.args = {
	...Default.args,
	placement: 'auto',
};
Placement.storyName = 'placement';

export const Trigger = Template.bind({});
Trigger.args = {
	...Default.args,
	trigger: 'hover',
};
Trigger.storyName = 'trigger';

export const Flip = Template.bind({});
Flip.args = {
	...Default.args,
	flip: ['auto-start', 'auto-end', 'bottom'],
};
Flip.storyName = 'flip';

export const Delay = Template.bind({});
Delay.args = {
	...Default.args,
	delay: 1500,
};
Delay.storyName = 'delay';

export const Modifiers = Template.bind({});
Modifiers.args = {
	...Default.args,
	modifiers: {
		name: 'offset',
		options: {
			offset: [10, 50],
		},
	},
};
Modifiers.storyName = 'modifiers';

const TemplateSecond = (args) => {
	return (
		<>
			{/* eslint-disable-next-line react/jsx-props-no-spreading */}
			Before text <Popovers {...args} /> after text.
		</>
	);
};
export const IsDisplayInline = TemplateSecond.bind({});
IsDisplayInline.args = {
	...Default.args,
	children: (
		<div>
			<b>this is a div</b>
		</div>
	),
	isDisplayInline: true,
	trigger: 'hover',
};
IsDisplayInline.storyName = 'isDisplayInline';
