import React from 'react';
import { NavLink } from 'react-router-dom';
import Nav, { NavItem, NavLinkDropdown } from '../../../../components/bootstrap/Nav';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../../components/bootstrap/Dropdown';

export default {
	title: 'Components/<Nav>/Sub Components/<NavLinkDropdown>',
	component: NavLinkDropdown,
	argTypes: {
		className: { control: 'text' },
		children: { control: 'text' },
	},
	args: { ...NavLinkDropdown.defaultProps },
	decorators: [
		(Story) => (
			<Nav>
				<NavItem>
					{}
					<a href='#'>Link</a>
				</NavItem>
				<NavItem>
					{}
					<a href='#'>Link</a>
				</NavItem>
				<NavItem isDisable>
					{}
					<a href='#'>Disabled</a>
				</NavItem>
				<NavItem>
					<Story />
				</NavItem>
			</Nav>
		),
	],
};

const Template = ({ ...args }) => (
	<Dropdown>
		<DropdownToggle>
			{/* eslint-disable-next-line react/jsx-props-no-spreading */}
			<NavLinkDropdown {...args} />
		</DropdownToggle>
		<DropdownMenu>
			<DropdownItem>
				<NavLink to='component'>nav link 2</NavLink>
			</DropdownItem>
		</DropdownMenu>
	</Dropdown>
);

export const Default = Template.bind({});
Default.args = {
	children: 'Drop',
};
