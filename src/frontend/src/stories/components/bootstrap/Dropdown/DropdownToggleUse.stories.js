import DropdownStories, { DropdownTemplate } from './Dropdown.stories';

export default {
	...DropdownStories,
	title: 'Components/<Dropdown>/Sub Components/<DropdownToggle>/PROPS',
};

export const HasIcon = DropdownTemplate.bind({});
HasIcon.args = {};
HasIcon.storyName = 'hasIcon';
