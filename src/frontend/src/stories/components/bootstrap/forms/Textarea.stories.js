import React, { useEffect } from 'react';
import { useFormik } from 'formik';
import Textarea from '../../../../components/bootstrap/forms/Textarea';

export default {
	title: 'Forms/<Textarea>',
	component: Textarea,
	argTypes: {
		isTouched: { table: { category: 'Validation' } },
		isValid: { table: { category: 'Validation' } },
		invalidFeedback: { table: { category: 'Validation' } },
		validFeedback: { table: { category: 'Validation' } },
		isValidMessage: { table: { category: 'Validation' } },
		isTooltipFeedback: { table: { category: 'Validation' } },

		onBlur: { table: { category: 'functions' } },
		onChange: { table: { category: 'functions' } },
		onFocus: { table: { category: 'functions' } },
		onInput: { table: { category: 'functions' } },
		onInvalid: { table: { category: 'functions' } },
		onSelect: { table: { category: 'functions' } },
	},
	args: {
		...Textarea.defaultProps,
		id: 'example',
	},
};

const Template = (args) => {
	const formik = useFormik({
		initialValues: {
			example: args.value,
		},
		onSubmit: (values) => {
			alert(JSON.stringify(values, null, 2));
		},
	});

	useEffect(() => {
		formik.setFieldValue('example', args.value);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [args.value]);

	return (
		<Textarea
			// eslint-disable-next-line react/jsx-props-no-spreading
			{...args}
			onChange={formik.handleChange}
			value={formik.values.example}
		/>
	);
};

export const Default = Template.bind({});
Default.args = {
	value: 'Change value',
};
