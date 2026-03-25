import React, { useEffect } from 'react';
import { useFormik } from 'formik';
import Checks from '../../../../components/bootstrap/forms/Checks';

export default {
	title: 'Forms/<Checks>',
	component: Checks,
	argTypes: {},
	args: {
		...Checks.defaultProps,
		id: 'example',
		name: 'example',
	},
};

const Template = (args) => {
	const formik = useFormik({
		initialValues: {
			example: args.checked,
		},
		onSubmit: (values) => {
			alert(JSON.stringify(values, null, 2));
		},
	});

	useEffect(() => {
		formik.setFieldValue('example', args.checked);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [args.checked]);

	return (
		<Checks
			// eslint-disable-next-line react/jsx-props-no-spreading
			{...args}
			onChange={formik.handleChange}
			checked={formik.values.example}
		/>
	);
};

export const Checkbox = Template.bind({});
Checkbox.args = {
	checked: true,
	label: 'Checkbox',
};

export const Switch = Template.bind({});
Switch.args = {
	checked: true,
	label: 'Switch',
	type: 'switch',
};

export const Radio = Template.bind({});
Radio.args = {
	value: 'radio value',
	checked: 'radio value',
	label: 'Radio',
	type: 'radio',
};
