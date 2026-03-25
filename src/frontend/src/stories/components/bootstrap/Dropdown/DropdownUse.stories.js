import DropdownStories, { DropdownTemplate } from './Dropdown.stories';

export default {
	...DropdownStories,
	title: 'Components/<Dropdown>/PROPS',
};

export const Direction = DropdownTemplate.bind({});
Direction.args = {};
Direction.storyName = 'direction';
