import React from 'react';
import InputGroup, { InputGroupText } from '../../../../components/bootstrap/forms/InputGroup';
import Input from '../../../../components/bootstrap/forms/Input';
import Select from '../../../../components/bootstrap/forms/Select';
import Textarea from '../../../../components/bootstrap/forms/Textarea';
import Checks from '../../../../components/bootstrap/forms/Checks';
import Button from '../../../../components/bootstrap/Button';
import {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../../components/bootstrap/Dropdown';

export default {
	title: 'Forms/<InputGroup>',
	component: InputGroup,
	subcomponents: { InputGroupText, Input, Select, Textarea },
	argTypes: {
		size: { control: { type: 'inline-radio', options: [null, 'sm', 'lg'] } },
	},
	args: { ...InputGroup.defaultProps },
};

const Template = (args) => {
	// eslint-disable-next-line react/jsx-props-no-spreading
	return <InputGroup {...args} />;
};

export const Start = Template.bind({});
Start.args = {
	children: [<InputGroupText key={1}>Text</InputGroupText>, <Input key={2} ariaLabel='Input' />],
};

export const End = Template.bind({});
End.args = {
	children: [<Input key={1} ariaLabel='Input' />, <InputGroupText key={2}>Text</InputGroupText>],
};

export const Multiple = Template.bind({});
Multiple.args = {
	children: [
		<InputGroupText key={1}>Start</InputGroupText>,
		<Input key={2} ariaLabel='First input' />,
		<InputGroupText key={3}>Center</InputGroupText>,
		<Input key={4} ariaLabel='Second input' />,
		<InputGroupText key={5}>End</InputGroupText>,
	],
};

export const WithChecks = Template.bind({});
WithChecks.args = {
	children: [
		<InputGroupText key={1} tag='div' id='addon-wrapping'>
			<Checks id='exampleInputCheck' aria-label='Checkbox for following text input' />
		</InputGroupText>,
		<Input
			key={2}
			id='exampleInput'
			placeholder='Username'
			ariaLabel='Username'
			ariaDescribedby='addon-wrapping'
			autoComplete='username'
		/>,
		<InputGroupText key={3}>End</InputGroupText>,
	],
};

export const WithButton = Template.bind({});
WithButton.args = {
	children: [
		<Button key={1} isOutline color='primary' id='button-addon1'>
			Button
		</Button>,
		<Input
			key={2}
			placeholder=''
			ariaLabel='Example text with button addon'
			ariaDescribedby='button-addon1'
		/>,
	],
};

export const WithDropdown = Template.bind({});
WithDropdown.args = {
	children: [
		<DropdownToggle key={1}>
			<Button color='primary' isOutline hoverShadow='sm'>
				Dropdown
			</Button>
		</DropdownToggle>,
		<DropdownMenu key={2}>
			<DropdownItem>
				<a href='/'>Link Item</a>
			</DropdownItem>
		</DropdownMenu>,
		<Input key={3} placeholder='' ariaLabel='Text input with dropdown button' />,
	],
};

export const Custom = Template.bind({});
Custom.args = {
	children: [
		<Input key={1} type='file' />,
		<Button key={2} isOutline color='dark' icon='CloudUpload'>
			Upload
		</Button>,
	],
};
