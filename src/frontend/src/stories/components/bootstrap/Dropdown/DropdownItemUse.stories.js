import DropdownStories, { DropdownTemplate } from './Dropdown.stories';

export default {
	...DropdownStories,
	title: 'Components/<Dropdown>/Sub Components/<DropdownMenu>/Sub Components/<DropdownItem>/PROPS',
};

export const IsHeader = DropdownTemplate.bind({});
IsHeader.args = {
	isHeaderDropdownItem: true,
	childrenDropdownItem: 'This is a Header',
};
IsHeader.storyName = 'isHeader';

export const IsDivider = DropdownTemplate.bind({});
IsDivider.args = {
	isDividerDropdownItem: true,
};
IsDivider.storyName = 'isDivider';

export const IsText = DropdownTemplate.bind({});
IsText.args = {
	isTextDropdownItem: true,
	childrenDropdownItem: 'This is a Text',
};
IsText.storyName = 'isText';
