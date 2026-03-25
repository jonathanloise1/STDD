import React, { FC, HTMLAttributes } from 'react';

interface IOptionProps extends HTMLAttributes<HTMLOptionElement> {
	children: string;
	value?: string | number;
	disabled?: boolean;
	ariaLabelledby?: string | null;
}
const Option: FC<IOptionProps> = ({ children, value, disabled, ariaLabelledby, ...props }) => {
	return (
		<option
			value={value}
			disabled={disabled}
			aria-labelledby={ariaLabelledby || children}
			{...props}>
			{children}
		</option>
	);
};

export interface IOptionsProps {
	list: {
		value?: string | number;
		text?: string | number;
		label?: string | number;
	}[];
}
// @ts-ignore
export const Options: FC<IOptionsProps> = ({ list }) => {
	return list?.map((item) => (
		// eslint-disable-next-line react/jsx-props-no-spreading
		<Option key={item.value} value={item.value} {...item}>
			{(item.text as string) || (item.label as string)}
		</Option>
	));
};

export default Option;
