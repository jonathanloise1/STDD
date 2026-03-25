import React, { useEffect, useState } from 'react';
import dayjs from 'dayjs';
import classNames from 'classnames';
import { Calendar, dayjsLocalizer, View as TView, Views } from 'react-big-calendar';
import { useFormik } from 'formik';
import { Calendar as DatePicker } from 'react-date-range';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';

import Page from '../../../layout/Page/Page';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Icon from '../../../components/icon/Icon';
import Button from '../../../components/bootstrap/Button';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardTitle,
} from '../../../components/bootstrap/Card';
import CommonUpcomingEvents from '../../_common/CommonUpcomingEvents';
import eventList, { IEvents } from '../../../common/data/events';
import OffCanvas, {
	OffCanvasBody,
	OffCanvasHeader,
	OffCanvasTitle,
} from '../../../components/bootstrap/OffCanvas';
import FormGroup from '../../../components/bootstrap/forms/FormGroup';
import Input from '../../../components/bootstrap/forms/Input';
import Checks from '../../../components/bootstrap/forms/Checks';
import Select from '../../../components/bootstrap/forms/Select';
import USERS, { getUserDataWithUsername, IUserProps } from '../../../common/data/userDummyData';
import Avatar, { AvatarGroup } from '../../../components/Avatar';
import useMinimizeAside from '../../../hooks/useMinimizeAside';
import Popovers from '../../../components/bootstrap/Popovers';
import {
	CalendarTodayButton,
	CalendarViewModeButtons,
	getLabel,
	getUnitType,
	getViews,
} from '../../../components/extras/calendarHelper';
import { demoPagesMenu } from '../../../menu';
import SERVICES, { getServiceDataWithServiceName } from '../../../common/data/serviceDummyData';
import Option from '../../../components/bootstrap/Option';
import CommonApprovedAppointmentChart from '../../_common/CommonApprovedAppointmentChart';
import CommonPercentageOfLoadChart from '../../_common/CommonPercentageOfLoadChart';
import CommonDashboardBookingLists from '../../_common/BookingComponents/CommonDashboardBookingLists';
import CommonRightPanel from '../../_common/BookingComponents/CommonRightPanel';
import useDarkMode from '../../../hooks/useDarkMode';
import { TColor } from '../../../type/color-type';
import Dropdown, { DropdownMenu, DropdownToggle } from '../../../components/bootstrap/Dropdown';

const localizer = dayjsLocalizer(dayjs);
const now = new Date();

interface IEvent extends IEvents {
	user?: IUserProps;
	users?: IUserProps[];
	color?: TColor;
}

const MyEvent = (data: { event: IEvent }) => {
	const { darkModeStatus } = useDarkMode();

	const { event } = data;
	return (
		<div className='row g-2'>
			<div className='col text-truncate'>
				{event?.icon && <Icon icon={event?.icon} size='lg' className='me-2' />}
				{event?.name}
			</div>
			{event?.user?.src && (
				<div className='col-auto'>
					<div className='row g-1 align-items-baseline'>
						<div className='col-auto'>
							<Avatar src={event?.user?.src} srcSet={event?.user?.srcSet} size={18} />
						</div>
						<small
							className={classNames('col-auto text-truncate', {
								'text-dark': !darkModeStatus,
								'text-white': darkModeStatus,
							})}>
							{event?.user?.name}
						</small>
					</div>
				</div>
			)}
			{event?.users && (
				<div className='col-auto'>
					<AvatarGroup size={18}>
						{event.users.map((user) => (
							// eslint-disable-next-line react/jsx-props-no-spreading
							<Avatar key={user.src} {...user} />
						))}
					</AvatarGroup>
				</div>
			)}
		</div>
	);
};
const MyWeekEvent = (data: { event: IEvent }) => {
	const { darkModeStatus } = useDarkMode();

	const { event } = data;
	return (
		<div className='row g-2'>
			<div className='col-12 text-truncate'>
				{event?.icon && <Icon icon={event?.icon} size='lg' className='me-2' />}
				{event?.name}
			</div>
			{event?.user && (
				<div className='col-12'>
					<div className='row g-1 align-items-baseline'>
						<div className='col-auto'>
							{/* eslint-disable-next-line react/jsx-props-no-spreading */}
							<Avatar {...event?.user} size={18} />
						</div>
						<small
							className={classNames('col-auto text-truncate', {
								'text-dark': !darkModeStatus,
								'text-white': darkModeStatus,
							})}>
							{event?.user?.name}
						</small>
					</div>
				</div>
			)}
			{event?.users && (
				<div className='col-12'>
					<AvatarGroup size={18}>
						{event.users.map((user) => (
							// eslint-disable-next-line react/jsx-props-no-spreading
							<Avatar key={user.src} {...user} />
						))}
					</AvatarGroup>
				</div>
			)}
		</div>
	);
};

const DashboardBookingPage = () => {
	const { darkModeStatus, themeStatus } = useDarkMode();
	useMinimizeAside();

	const [toggleRightPanel, setToggleRightPanel] = useState(true);

	// BEGIN :: Calendar
	// Active employee
	const [employeeList, setEmployeeList] = useState({
		[USERS.JOHN.username]: true,
		[USERS.ELLA.username]: true,
		[USERS.RYAN.username]: true,
		[USERS.GRACE.username]: true,
	});
	// Events
	const [events, setEvents] = useState(eventList);

	// FOR DEV
	useEffect(() => {
		setEvents(eventList);
		return () => {};
	}, []);

	const initialEventItem: IEvent = {
		start: undefined,
		end: undefined,
		name: undefined,
		id: undefined,
		user: undefined,
	};
	// Selected Event
	const [eventItem, setEventItem] = useState<IEvent>(initialEventItem);
	// Calendar View Mode
	const [viewMode, setViewMode] = useState<TView>(Views.MONTH);
	// Calendar Date
	const [date, setDate] = useState(new Date());
	// Item edit panel status
	const [toggleInfoEventCanvas, setToggleInfoEventCanvas] = useState<boolean>(false);
	const setInfoEvent = () => setToggleInfoEventCanvas(!toggleInfoEventCanvas);
	const [eventAdding, setEventAdding] = useState<boolean>(false);

	// Calendar Unit Type
	const unitType = getUnitType(viewMode);
	// Calendar Date Label
	const calendarDateLabel = getLabel(date, viewMode);

	// Change view mode
	const handleViewMode = (e: dayjs.ConfigType) => {
		setDate(dayjs(e).toDate());
		setViewMode(Views.DAY);
	};
	// View modes; Month, Week, Work Week, Day and Agenda
	const views = getViews();

	// New Event
	const handleSelect = ({ start, end }: { start: any; end: any }) => {
		setEventAdding(true);
		setEventItem({ ...initialEventItem, start, end });
	};

	useEffect(() => {
		if (eventAdding) {
			setInfoEvent();
		}
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [eventAdding]);

	/**
	 * Calendar Item Container Style
	 * @param event
	 * @param start
	 * @param end
	 * @param isSelected
	 * @returns {{className: string}}
	 */

	const eventStyleGetter = (
		event: { color?: TColor },
		start: any,
		end: any,
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		isSelected: boolean,
	): { className: string } => {
		const isActiveEvent = start <= now && end >= now;
		const isPastEvent = end < now;
		const color = isActiveEvent ? 'success' : event.color;

		return {
			className: classNames({
				[`bg-l${darkModeStatus ? 'o25' : '10'}-${color} text-${color}`]: color,
				'border border-success': isActiveEvent,
				'opacity-50': isPastEvent,
			}),
		};
	};

	const formik = useFormik({
		initialValues: {
			eventName: '',
			eventStart: '',
			eventEnd: '',
			eventEmployee: '',
			eventAllDay: false,
		},
		onSubmit: (values) => {
			if (eventAdding) {
				setEvents((prevEvents) => [
					...prevEvents,
					{
						id: values.eventStart,
						...getServiceDataWithServiceName(values.eventName),
						end: values.eventEnd,
						start: values.eventStart,
						user: { ...getUserDataWithUsername(values.eventEmployee) },
					},
				]);
			}
			setToggleInfoEventCanvas(false);
			setEventAdding(false);
			setEventItem(initialEventItem);
			formik.setValues({
				eventName: '',
				eventStart: '',
				eventEnd: '',
				eventEmployee: '',
				eventAllDay: false,
			});
		},
	});

	useEffect(() => {
		if (eventItem)
			formik.setValues({
				...formik.values,
				// @ts-ignore
				eventId: eventItem.id || null,
				eventName: eventItem.name || '',
				eventStart: dayjs(eventItem.start).format(),
				eventEnd: dayjs(eventItem.end).format(),
				eventEmployee: eventItem?.user?.username || '',
			});
		return () => {};
		//	eslint-disable-next-line react-hooks/exhaustive-deps
	}, [eventItem]);
	// END:: Calendar

	const [toggleCalendar, setToggleCalendar] = useState(true);

	return (
		<PageWrapper title={demoPagesMenu.appointment.subMenu.dashboard.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Icon icon='Info' className='me-2' size='2x' />
					<span className='text-muted'>
						You have{' '}
						<Icon icon='Check Circle ' color='success' className='mx-1' size='lg' /> 12
						approved appointments and{' '}
						<Icon icon='pending_actions ' color='danger' className='mx-1' size='lg' /> 3
						pending appointments for today.
					</span>
				</SubHeaderLeft>
				<SubHeaderRight>
					<Button
						icon='Groups'
						onClick={() => setToggleRightPanel(!toggleRightPanel)}
						color={toggleRightPanel ? 'dark' : 'light'}
						aria-label='Toggle right panel'
					/>
					<Button
						icon='AreaChart'
						onClick={() => setToggleCalendar(!toggleCalendar)}
						color={toggleCalendar ? 'dark' : 'light'}
						aria-label='Toggle calendar & charts'
					/>
					<Dropdown>
						<DropdownToggle hasIcon={false}>
							<Button color={darkModeStatus ? 'light' : 'dark'} isLight>
								{calendarDateLabel}
							</Button>
						</DropdownToggle>
						<DropdownMenu isAlignmentEnd>
							<DatePicker
								onChange={(item) => setDate(item)}
								date={date}
								color={import.meta.env.VITE_PRIMARY_COLOR}
							/>
						</DropdownMenu>
					</Dropdown>
				</SubHeaderRight>
			</SubHeader>
			<Page container='fluid'>
				{toggleCalendar && (
					<>
						<div className='row mb-4 g-3'>
							{Object.keys(USERS).map((u) => (
								<div key={USERS[u].username} className='col-auto'>
									<Popovers
										trigger='hover'
										desc={
											<>
												<div className='h6'>{`${USERS[u].name} ${USERS[u].surname}`}</div>
												<div>
													<b>Event: </b>
													{
														events.filter(
															(i) =>
																i.user?.username ===
																USERS[u].username,
														).length
													}
												</div>
												<div>
													<b>Approved: </b>
													{
														events.filter(
															(i) =>
																i.user?.username ===
																	USERS[u].username &&
																i.color === 'info',
														).length
													}
												</div>
											</>
										}>
										<div className='position-relative'>
											<Avatar
												srcSet={USERS[u].srcSet}
												src={USERS[u].src}
												color={USERS[u].color}
												size={64}
												border={4}
												className='cursor-pointer'
												borderColor={
													employeeList[USERS[u].username]
														? 'info'
														: themeStatus
												}
												onClick={() =>
													setEmployeeList({
														...employeeList,
														[USERS[u].username]:
															!employeeList[USERS[u].username],
													})
												}
											/>
											{!!events.filter(
												(i) =>
													i.user?.username === USERS[u].username &&
													i.start &&
													i.start < now &&
													i.end &&
													i.end > now,
											).length && (
												<span className='position-absolute top-85 start-85 translate-middle badge border border-2 border-light rounded-circle bg-success p-2'>
													<span className='visually-hidden'>
														Online user
													</span>
												</span>
											)}
										</div>
									</Popovers>
								</div>
							))}
						</div>
						<div className='row h-100'>
							<div
								className={classNames({
									'col-xxl-8': !toggleRightPanel,
									'col-12': toggleRightPanel,
								})}>
								<Card stretch style={{ minHeight: 680 }}>
									<CardHeader>
										<CardActions>
											<CalendarTodayButton
												unitType={unitType}
												date={date}
												setDate={setDate}
												viewMode={viewMode}
											/>
										</CardActions>
										<CardActions>
											<CalendarViewModeButtons
												setViewMode={setViewMode}
												viewMode={viewMode}
											/>
										</CardActions>
									</CardHeader>
									<CardBody isScrollable>
										<Calendar
											selectable
											toolbar={false}
											localizer={localizer}
											events={events.filter(
												(i) => i?.user && employeeList[i.user.username],
											)}
											defaultView={Views.WEEK}
											views={views}
											view={viewMode}
											date={date}
											onNavigate={(_date) => setDate(_date)}
											scrollToTime={new Date(1970, 1, 1, 6)}
											defaultDate={new Date()}
											onSelectEvent={(event) => {
												setInfoEvent();
												setEventItem(event);
											}}
											onSelectSlot={handleSelect}
											onView={handleViewMode}
											onDrillDown={handleViewMode}
											components={{
												event: MyEvent, // used by each view (Month, Day, Week)
												week: {
													event: MyWeekEvent,
												},
												work_week: {
													event: MyWeekEvent,
												},
											}}
											eventPropGetter={eventStyleGetter}
										/>
									</CardBody>
								</Card>
							</div>
							<div
								className={classNames({
									'col-xxl-4': !toggleRightPanel,
									'col-12': toggleRightPanel,
								})}>
								<div className='row'>
									<div
										className={classNames(
											{
												'col-xxl-12': !toggleRightPanel,
											},
											'col-md-6',
										)}>
										<CommonApprovedAppointmentChart />
									</div>
									<div
										className={classNames(
											{
												'col-xxl-12': !toggleRightPanel,
											},
											'col-md-6',
										)}>
										<CommonPercentageOfLoadChart />
									</div>
								</div>
							</div>
						</div>
					</>
				)}
				<div className='row'>
					<div className='col-12'>
						<CommonDashboardBookingLists />
					</div>
					<div className='col-12'>
						<CommonUpcomingEvents />
					</div>
				</div>

				<OffCanvas
					setOpen={(status: boolean) => {
						setToggleInfoEventCanvas(status);
						setEventAdding(status);
					}}
					isOpen={toggleInfoEventCanvas}
					titleId='canvas-title'>
					<OffCanvasHeader
						setOpen={(status: boolean) => {
							setToggleInfoEventCanvas(status);
							setEventAdding(status);
						}}
						className='p-4'>
						<OffCanvasTitle id='canvas-title'>
							{eventAdding ? 'Add Event' : 'Edit Event'}
						</OffCanvasTitle>
					</OffCanvasHeader>
					<OffCanvasBody tag='form' onSubmit={formik.handleSubmit} className='p-4'>
						<div className='row g-4'>
							{/* Name */}
							<div className='col-12'>
								<FormGroup id='eventName' label='Name'>
									<Select
										ariaLabel='Service select'
										placeholder='Please select...'
										size='lg'
										value={formik.values.eventName}
										onChange={formik.handleChange}>
										{Object.keys(SERVICES).map((s) => (
											<Option key={SERVICES[s].name} value={SERVICES[s].name}>
												{SERVICES[s].name}
											</Option>
										))}
									</Select>
								</FormGroup>
							</div>
							{/* Date */}
							<div className='col-12'>
								<Card className='mb-0 bg-l10-info' shadow='sm'>
									<CardHeader className='bg-l25-info'>
										<CardLabel icon='DateRange' iconColor='info'>
											<CardTitle className='text-info'>
												Date Options
											</CardTitle>
										</CardLabel>
									</CardHeader>
									<CardBody>
										<div className='row g-3'>
											<div className='col-12'>
												<FormGroup id='eventAllDay'>
													<Checks
														type='switch'
														value='true'
														label='All day event'
														checked={formik.values.eventAllDay}
														onChange={formik.handleChange}
													/>
												</FormGroup>
											</div>
											<div className='col-12'>
												<FormGroup
													id='eventStart'
													label={
														formik.values.eventAllDay
															? 'Date'
															: 'Start Date'
													}>
													<Input
														type={
															formik.values.eventAllDay
																? 'date'
																: 'datetime-local'
														}
														value={
															formik.values.eventAllDay
																? dayjs(
																		formik.values.eventStart,
																	).format('YYYY-MM-DD')
																: dayjs(
																		formik.values.eventStart,
																	).format('YYYY-MM-DDTHH:mm')
														}
														onChange={formik.handleChange}
													/>
												</FormGroup>
											</div>

											{!formik.values.eventAllDay && (
												<div className='col-12'>
													<FormGroup id='eventEnd' label='End Date'>
														<Input
															type='datetime-local'
															value={dayjs(
																formik.values.eventEnd,
															).format('YYYY-MM-DDTHH:mm')}
															onChange={formik.handleChange}
														/>
													</FormGroup>
												</div>
											)}
										</div>
									</CardBody>
								</Card>
							</div>
							{/* Employee */}
							<div className='col-12'>
								<Card className='mb-0 bg-l10-dark' shadow='sm'>
									<CardHeader className='bg-l25-dark'>
										<CardLabel icon='Person Add' iconColor='dark'>
											<CardTitle>Employee Options</CardTitle>
										</CardLabel>
									</CardHeader>
									<CardBody>
										<FormGroup id='eventEmployee' label='Employee'>
											<Select
												placeholder='Please select...'
												value={formik.values.eventEmployee}
												onChange={formik.handleChange}
												ariaLabel='Employee select'>
												{Object.keys(USERS).map((u) => (
													<Option
														key={USERS[u].id}
														value={USERS[u].username}>
														{`${USERS[u].name} ${USERS[u].surname}`}
													</Option>
												))}
											</Select>
										</FormGroup>
									</CardBody>
								</Card>
							</div>
							<div className='col'>
								<Button color='info' type='submit'>
									Save
								</Button>
							</div>
						</div>
					</OffCanvasBody>
				</OffCanvas>

				<CommonRightPanel setOpen={setToggleRightPanel} isOpen={toggleRightPanel} />
			</Page>
		</PageWrapper>
	);
};

export default DashboardBookingPage;
