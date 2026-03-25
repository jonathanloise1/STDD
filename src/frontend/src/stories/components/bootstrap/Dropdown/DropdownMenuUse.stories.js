import DropdownStories, { DropdownTemplate } from './Dropdown.stories';

export default {
	...DropdownStories,
	title: 'Components/<Dropdown>/Sub Components/<DropdownMenu>/PROPS',
};

export const IsAlignmentEnd = DropdownTemplate.bind({});
IsAlignmentEnd.args = {
	isAlignmentEndDropdownMenu: true,
};
IsAlignmentEnd.storyName = 'isAlignmentEnd';

export const Breakpoint = DropdownTemplate.bind({});
Breakpoint.args = {
	breakpointDropdownMenu: 'xl',
};
Breakpoint.storyName = 'breakpoint';

export const Size = DropdownTemplate.bind({});
Size.args = {
	sizeDropdownMenu: 'lg',
};
Size.storyName = 'size';
