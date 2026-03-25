import React from 'react';
import Icon from '../../../../components/icon/Icon';
import DropdownStories, { DropdownTemplate } from './Dropdown.stories';

export default {
	...DropdownStories,
	title: 'Components/<Dropdown>/Sub Components/<DropdownToggle>/PROPS/children*',
};

export const WithButton = DropdownTemplate.bind({});
WithButton.args = {};
WithButton.storyName = '<Button>';

export const WithIcon = DropdownTemplate.bind({});
WithIcon.args = {
	childrenDropdownToggle: <Icon icon='Bolt' color='primary' size='2x' />,
};
WithIcon.storyName = '<Icon>';

export const Div = DropdownTemplate.bind({});
Div.args = {
	childrenDropdownToggle: <div>Dropdown</div>,
};
Div.storyName = '<div>';
