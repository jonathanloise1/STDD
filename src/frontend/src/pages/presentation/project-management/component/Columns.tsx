import React, { FC, useState } from 'react';
import { useFormik } from 'formik';
import classNames from 'classnames';
import { Droppable, DroppableProvided, DroppableStateSnapshot } from '@hello-pangea/dnd';
import useDarkMode from '../../../../hooks/useDarkMode';
import Card, {
	CardActions,
	CardBody,
	CardFooter,
	CardFooterLeft,
	CardHeader,
	CardLabel,
	CardTitle,
} from '../../../../components/bootstrap/Card';
import Badge from '../../../../components/bootstrap/Badge';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../../components/bootstrap/Dropdown';
import Button from '../../../../components/bootstrap/Button';
import { getListStyle } from '../helper/style';
import Modal, {
	ModalBody,
	ModalFooter,
	ModalHeader,
	ModalTitle,
} from '../../../../components/bootstrap/Modal';
import FormGroup from '../../../../components/bootstrap/forms/FormGroup';
import Input from '../../../../components/bootstrap/forms/Input';
import Textarea from '../../../../components/bootstrap/forms/Textarea';
import Select from '../../../../components/bootstrap/forms/Select';
import Option from '../../../../components/bootstrap/Option';
import USERS from '../../../../common/data/userDummyData';
import TAGS from '../../../../common/data/boardTagsData';
import { TCards, TColumnData, TColumnsData } from '../type/types';
import ColumnCardWrapper from './ColumnCardWrapper';

interface IColumns {
	cardsData: TCards;
	columnsData: TColumnsData;
	setCardsData(...args: unknown[]): unknown;
}
const Columns: FC<IColumns> = ({ cardsData, columnsData, setCardsData }) => {
	const { darkModeStatus } = useDarkMode();

	const [editModalStatus, setEditModalStatus] = useState<boolean>(false);

	const formik = useFormik({
		initialValues: {
			cardName: '',
			groupId: '',
			description: '',
			assignee: '',
			task: [],
			tags: [],
		},
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		onSubmit: (values) => {
			setEditModalStatus(false);
		},
	});
	return (
		<>
			{Object.keys(columnsData).map((columnKey) => {
				const columnData: TColumnData = columnsData[columnKey];
				return (
					<div key={columnKey} className='col-auto'>
						<Card className={classNames(`board-group shadow-3d-${columnData.color}`)}>
							<CardHeader>
								<CardLabel icon={columnData.icon} iconColor={columnData.color}>
									<CardTitle tag='div' className='h5'>
										<>{columnData.title} </>
										<Badge color={columnData.color} isLight>
											{cardsData[columnKey].length}
										</Badge>
									</CardTitle>
								</CardLabel>
								<CardActions>
									<Dropdown>
										<DropdownToggle hasIcon={false}>
											<Button
												icon='MoreVert'
												color={darkModeStatus ? 'dark' : undefined}
												aria-label='More actions'
											/>
										</DropdownToggle>
										<DropdownMenu isAlignmentEnd>
											<DropdownItem>
												<Button icon='Edit'>Rename column</Button>
											</DropdownItem>
											<DropdownItem>
												<Button icon='Speed'>Set column limit</Button>
											</DropdownItem>
											<DropdownItem>
												<Button icon='AddBox'>Add new column</Button>
											</DropdownItem>
											<DropdownItem isDivider />
											<DropdownItem>
												<Button icon='Delete'>Delete column</Button>
											</DropdownItem>
										</DropdownMenu>
									</Dropdown>
								</CardActions>
							</CardHeader>
							<Droppable droppableId={columnKey}>
								{(
									providedDroppable: DroppableProvided,
									snapshotDroppable: DroppableStateSnapshot,
								) => (
									<CardBody
										// eslint-disable-next-line react/jsx-props-no-spreading
										{...providedDroppable.droppableProps}
										ref={providedDroppable.innerRef}
										style={getListStyle(snapshotDroppable.isDraggingOver)}>
										<ColumnCardWrapper
											columnKey={columnKey}
											columnsData={columnsData}
											cardsData={cardsData}
											setCardsData={setCardsData}
										/>
										{providedDroppable.placeholder}
									</CardBody>
								)}
							</Droppable>
							<CardFooter>
								<CardFooterLeft>
									<Button
										color={columnData.color}
										isLight
										icon='AddTask'
										onClick={() => setEditModalStatus(true)}>
										Create
									</Button>
								</CardFooterLeft>
							</CardFooter>
						</Card>
					</div>
				);
			})}
			<Modal setIsOpen={setEditModalStatus} isOpen={editModalStatus} size='lg' isScrollable>
				<ModalHeader className='px-4' setIsOpen={setEditModalStatus}>
					<ModalTitle id='project-edit'>New Card</ModalTitle>
				</ModalHeader>
				<ModalBody className='px-4'>
					<div className='row'>
						<div className='col-md-8'>
							<Card shadow='sm'>
								<CardHeader>
									<CardLabel icon='Info' iconColor='success'>
										<CardTitle>Task Information</CardTitle>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<div className='row g-4'>
										<FormGroup
											className='col-12'
											id='cardName'
											label='Task Name'>
											<Input
												onChange={formik.handleChange}
												value={formik.values.cardName}
											/>
										</FormGroup>
										<FormGroup
											className='col-12'
											id='description'
											label='Description'>
											<Textarea
												onChange={formik.handleChange}
												value={formik.values.description}
											/>
										</FormGroup>
									</div>
								</CardBody>
							</Card>
						</div>
						<div className='col-md-4'>
							<div className='row g-4 sticky-top'>
								<FormGroup className='col-12' id='groupId' label='Status'>
									<Select
										ariaLabel='Board select'
										placeholder='Select group'
										onChange={formik.handleChange}
										value={formik.values.groupId}>
										{Object.keys(columnsData).map((columnItemKey) => (
											<Option
												key={columnsData[columnItemKey].id}
												value={columnsData[columnItemKey].id}>
												{columnsData[columnItemKey].title}
											</Option>
										))}
									</Select>
								</FormGroup>
								<FormGroup className='col-12' id='assignee' label='Assignee'>
									<Select
										ariaLabel='Board select'
										placeholder='Select group'
										onChange={formik.handleChange}
										value={formik.values.assignee}>
										{Object.keys(USERS).map((u) => (
											// @ts-ignore
											<Option key={USERS[u].id} value={USERS[u].username}>
												{
													// @ts-ignore
													`${USERS[u].name} ${USERS[u].surname}`
												}
											</Option>
										))}
									</Select>
								</FormGroup>
								<FormGroup className='col-12' id='tags' label='Tags'>
									<Select
										multiple
										ariaLabel='Board select'
										placeholder='Select group'
										onChange={formik.handleChange}
										value={formik.values.tags}>
										{Object.keys(TAGS).map((t) => (
											// @ts-ignore
											<Option key={TAGS[t].id} value={TAGS[t].id}>
												{
													// @ts-ignore
													TAGS[t].title
												}
											</Option>
										))}
									</Select>
								</FormGroup>
							</div>
						</div>
					</div>
				</ModalBody>
				<ModalFooter className='px-4 pb-4'>
					<Button
						color='primary'
						className='w-100'
						type='submit'
						onClick={formik.handleSubmit}>
						Save
					</Button>
				</ModalFooter>
			</Modal>
		</>
	);
};

export default Columns;
