import React from 'react';
import { NavLink } from 'react-router-dom';
import Nav, { NavItem, NavLinkDropdown } from '../../../../components/bootstrap/Nav';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../../components/bootstrap/Dropdown';

export default {
	title: 'Components/<Nav>/Sub Components/<NavItem>',
	component: NavItem,
	subcomponent: { NavLink, Dropdown },
	argTypes: {
		isActive: { control: 'boolean' },
		isDisable: { control: 'boolean' },
		className: { control: 'text' },
		children: { control: false },
	},
	args: { ...NavItem.defaultProps },
	decorators: [
		(Story) => (
			<Nav>
				<Story />
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
					<Dropdown>
						<DropdownToggle>
							<NavLinkDropdown>Drop</NavLinkDropdown>
						</DropdownToggle>
						<DropdownMenu>
							<DropdownItem>
								<NavLink to='component'>nav link 2</NavLink>
							</DropdownItem>
						</DropdownMenu>
					</Dropdown>
				</NavItem>
			</Nav>
		),
	],
};

const Template = ({ ...args }) => (
	<NavItem {...args}>
		{}
		<a href='#'>Link</a>
	</NavItem>
);

export const Default = Template.bind({});
Default.args = {
	isActive: false,
	isDisable: false,
};
