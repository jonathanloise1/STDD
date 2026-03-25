import React, { useEffect, useRef } from 'react';
import { FormikHelpers, useFormik } from 'formik';
import Checks from '../components/bootstrap/forms/Checks';

const useSelectTable = (data: any[]) => {
	const selectTable = useFormik({
		onSubmit<Values>(
			// eslint-disable-next-line @typescript-eslint/no-unused-vars
			values: Values,
			// eslint-disable-next-line @typescript-eslint/no-unused-vars
			formikHelpers: FormikHelpers<Values>,
		): void | Promise<any> {
			return undefined;
		},
		initialValues: {
			selectAll: false,
			selectedList: [],
		},
	});

	// Update Select List
	useEffect(() => {
		if (selectTable.values.selectAll) {
			selectTable.setValues({
				...selectTable.values,
				// @ts-ignore
				selectedList: data.map((d) => d.id.toString()),
			});
		} else {
			selectTable.setValues({
				...selectTable.values,
				selectedList: [],
			});
		}
		return () => {};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [selectTable.values.selectAll]);

	// Select All -- indeterminate
	const ref = useRef<HTMLInputElement>(null);
	useEffect(() => {
		if (ref.current) {
			if (
				!!selectTable.values.selectedList.length &&
				selectTable.values.selectedList.length !== data.map((d) => d.id.toString()).length
			) {
				ref.current.checked = false;
				ref.current.indeterminate = true;
			} else if (
				selectTable.values.selectedList.length === data.map((d) => d.id.toString()).length
			) {
				ref.current.checked = true;
				ref.current.indeterminate = false;
			} else if (selectTable.values.selectedList.length === 0) {
				ref.current.checked = false;
				ref.current.indeterminate = false;
			}
		}
	}, [selectTable.values.selectAll, selectTable.values.selectedList, data]);

	const SelectAllCheck = (
		<Checks
			ref={ref}
			id='selectAll'
			onChange={selectTable.handleChange}
			checked={selectTable.values.selectAll}
			ariaLabel='Select All'
		/>
	);

	const selectItemHandleChange = selectTable.handleChange;
	const selectedIdList = selectTable.values.selectedList;

	return { selectTable, selectItemHandleChange, selectedIdList, SelectAllCheck };
};
export default useSelectTable;
