import React, { useEffect } from 'react';
import { useFormik } from 'formik';
import Checks, { ChecksGroup } from '../../../../components/bootstrap/forms/Checks';

export default {
	title: 'Forms/<Checks>/Sup Component/<ChecksGroup>',
	component: ChecksGroup,
	argTypes: {},
	args: { ...ChecksGroup.defaultProps },
};

// eslint-disable-next-line react/prop-types
const Template = ({ children, ...args }) => {
	const formik = useFormik({
		initialValues: {},
		onSubmit: (values) => {
			alert(JSON.stringify(values, null, 2));
		},
	});

	useEffect(() => {
		formik.setFieldValue('example', args.checked);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [args.checked]);

	return (
		// eslint-disable-next-line react/jsx-props-no-spreading
		<ChecksGroup {...args}>
			{/* eslint-disable-next-line react/prop-types */}
			{children.map((item) => (
				<Checks
					key={item.id}
					// eslint-disable-next-line react/jsx-props-no-spreading
					{...item}
					onChange={formik.handleChange}
					checked={formik.values[item.id]}
				/>
			))}
		</ChecksGroup>
	);
};

export const Default = Template.bind({});
Default.args = {
	children: [
		{ id: 'checksOne', label: 'One (inline)', isInline: true },
		{ id: 'checksTwo', label: 'Two (inline)', isInline: true },
		{ id: 'checksThree', label: 'Three' },
	],
};
