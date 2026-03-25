import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import dayjs from 'dayjs';
import classNames from 'classnames';
import { getUserDataWithId } from '../../../common/data/userDummyData';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import SubHeader, {
	SubHeaderLeft,
	SubHeaderRight,
	SubheaderSeparator,
} from '../../../layout/SubHeader/SubHeader';
import Button from '../../../components/bootstrap/Button';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardTitle,
} from '../../../components/bootstrap/Card';
import Avatar from '../../../components/Avatar';
import Icon from '../../../components/icon/Icon';
import { demoPagesMenu } from '../../../menu';
import Badge from '../../../components/bootstrap/Badge';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../components/bootstrap/Dropdown';
import Chart, { IChartOptions } from '../../../components/extras/Chart';
import dummyEventsData from '../../../common/data/dummyEventsData';
import { priceFormat } from '../../../helpers/helpers';
import EVENT_STATUS from '../../../common/data/enumEventStatus';
import Alert from '../../../components/bootstrap/Alert';
import CommonAvatarTeam from '../../../common/other/CommonAvatarTeam';
import COLORS from '../../../common/data/enumColors';
import useDarkMode from '../../../hooks/useDarkMode';
import useTourStep from '../../../hooks/useTourStep';

const EmployeePage = () => {
	useTourStep(19);
	const { darkModeStatus } = useDarkMode();

	const { id } = useParams();
	const data = getUserDataWithId(id);

	const [dayHours] = useState<IChartOptions>({
		series: [
			{
				data: [8, 12, 15, 20, 15, 22, 9],
			},
		],
		options: {
			colors: [import.meta.env.VITE_SUCCESS_COLOR],
			chart: {
				type: 'radar',
				width: 200,
				height: 200,
				sparkline: {
					enabled: true,
				},
			},
			xaxis: {
				categories: [
					'Monday',
					'Tuesday',
					'Wednesday',
					'Thursday',
					'Friday',
					'Saturday',
					'Sunday',
				],
				// convertedCatToNumeric: false,
			},
			tooltip: {
				theme: 'dark',
				fixed: {
					enabled: false,
				},
				x: {
					show: true,
				},
				y: {
					title: {
						// eslint-disable-next-line @typescript-eslint/no-unused-vars
						formatter(seriesName) {
							return 'Hours';
						},
					},
				},
			},
			stroke: {
				curve: 'smooth',
				width: 2,
			},
			plotOptions: {
				radar: {
					polygons: {
						strokeColors: `${COLORS.SUCCESS.code}50`,
						strokeWidth: '1',
						connectorColors: `${COLORS.SUCCESS.code}50`,
					},
				},
			},
		},
	});

	const userTasks = dummyEventsData.filter((f) => f.assigned.username === data.username);

	return (
		<PageWrapper title={`${data.name} ${data.surname}`}>
			<SubHeader>
				<SubHeaderLeft>
					<Button
						color='info'
						isLink
						icon='ArrowBack'
						tag='a'
						to={`../${demoPagesMenu.appointment.subMenu.employeeList.path}`}>
						Back to List
					</Button>
					<SubheaderSeparator />
					<CommonAvatarTeam isAlignmentEnd>
						<strong>Sports</strong> Team
					</CommonAvatarTeam>
				</SubHeaderLeft>
				<SubHeaderRight>
					<span className='text-muted fst-italic me-2'>Last update:</span>
					<span className='fw-bold'>13 hours ago</span>
				</SubHeaderRight>
			</SubHeader>
			<Page>
				<div className='pt-3 pb-5 d-flex align-items-center'>
					<span className='display-4 fw-bold me-3'>{`${data.name} ${data.surname}`}</span>
					<span className='border border-success border-2 text-success fw-bold px-3 py-2 rounded'>
						{data.position}
					</span>
				</div>
				<div className='row'>
					<div className='col-lg-4'>
						<Card className='shadow-3d-info'>
							<CardBody>
								<div className='row g-5'>
									<div className='col-12 d-flex justify-content-center'>
										<Avatar
											src={data.src}
											srcSet={data.srcSet}
											color={data.color}
											isOnline={data.isOnline}
										/>
									</div>
									<div className='col-12'>
										<div className='row g-2'>
											<div className='col-12'>
												<div className='d-flex align-items-center'>
													<div className='flex-shrink-0'>
														<Icon icon='Mail' size='3x' color='info' />
													</div>
													<div className='flex-grow-1 ms-3'>
														<div className='fw-bold fs-5 mb-0'>
															{`${data.username}@site.com`}
														</div>
														<div className='text-muted'>
															Email Address
														</div>
													</div>
												</div>
											</div>
											<div className='col-12'>
												<div className='d-flex align-items-center'>
													<div className='flex-shrink-0'>
														<Icon icon='Tag' size='3x' color='info' />
													</div>
													<div className='flex-grow-1 ms-3'>
														<div className='fw-bold fs-5 mb-0'>
															{`@${data.username}`}
														</div>
														<div className='text-muted'>
															Social name
														</div>
													</div>
												</div>
											</div>
										</div>
									</div>
								</div>
							</CardBody>
						</Card>
						<Card>
							<CardHeader>
								<CardLabel icon='Stream' iconColor='warning'>
									<CardTitle tag='div' className='h5'>
										Skill
									</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								{data.services ? (
									<div className='row g-2'>
										{data?.services.map((service) => (
											<div key={service.name} className='col-auto'>
												<Badge
													isLight
													color={service.color}
													className='px-3 py-2'>
													<Icon
														icon={service.icon}
														size='lg'
														className='me-1'
													/>
													{service.name}
												</Badge>
											</div>
										))}
									</div>
								) : (
									<div className='row'>
										<div className='col'>
											<Alert
												color='warning'
												isLight
												icon='Report'
												className='mb-0'>
												No results to show
											</Alert>
										</div>
									</div>
								)}
							</CardBody>
						</Card>
						<Card>
							<CardHeader>
								<CardLabel icon='ShowChart' iconColor='secondary'>
									<CardTitle tag='div' className='h5'>
										Statics
									</CardTitle>
								</CardLabel>
								<CardActions>
									Only in <strong>{dayjs().format('MMM')}</strong>.
								</CardActions>
							</CardHeader>
							<CardBody>
								<div className='row g-4 align-items-center'>
									<div className='col-xl-6'>
										<div
											className={classNames(
												'd-flex align-items-center rounded-2 p-3',
												{
													'bg-l10-warning': !darkModeStatus,
													'bg-lo25-warning': darkModeStatus,
												},
											)}>
											<div className='flex-shrink-0'>
												<Icon icon='DoneAll' size='3x' color='warning' />
											</div>
											<div className='flex-grow-1 ms-3'>
												<div className='fw-bold fs-3 mb-0'>15</div>
												<div className='text-muted mt-n2 truncate-line-1'>
													Completed tasks
												</div>
											</div>
										</div>
									</div>
									<div className='col-xl-6'>
										<div
											className={classNames(
												'd-flex align-items-center rounded-2 p-3',
												{
													'bg-l10-info': !darkModeStatus,
													'bg-lo25-info': darkModeStatus,
												},
											)}>
											<div className='flex-shrink-0'>
												<Icon icon='Savings' size='3x' color='info' />
											</div>
											<div className='flex-grow-1 ms-3'>
												<div className='fw-bold fs-3 mb-0'>1,280</div>
												<div className='text-muted mt-n2 truncate-line-1'>
													Earning
												</div>
											</div>
										</div>
									</div>
									<div className='col-xl-6'>
										<div
											className={classNames(
												'd-flex align-items-center rounded-2 p-3',
												{
													'bg-l10-primary': !darkModeStatus,
													'bg-lo25-primary': darkModeStatus,
												},
											)}>
											<div className='flex-shrink-0'>
												<Icon
													icon='Celebration'
													size='3x'
													color='primary'
												/>
											</div>
											<div className='flex-grow-1 ms-3'>
												<div className='fw-bold fs-3 mb-0'>76</div>
												<div className='text-muted mt-n2 truncate-line-1'>
													Occupancy
												</div>
											</div>
										</div>
									</div>
									<div className='col-xl-6'>
										<div
											className={classNames(
												'd-flex align-items-center rounded-2 p-3',
												{
													'bg-l10-success': !darkModeStatus,
													'bg-lo25-success': darkModeStatus,
												},
											)}>
											<div className='flex-shrink-0'>
												<Icon icon='Timer' size='3x' color='success' />
											</div>
											<div className='flex-grow-1 ms-3'>
												<div className='fw-bold fs-3 mb-0'>42</div>
												<div className='text-muted mt-n2'>Hours</div>
											</div>
										</div>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-lg-8'>
						<Card className='shadow-3d-primary'>
							<CardHeader>
								<CardLabel icon='Summarize' iconColor='success'>
									<CardTitle tag='div' className='h5'>
										Summary
									</CardTitle>
								</CardLabel>
								<CardActions>
									<Dropdown>
										<DropdownToggle>
											<Button color='info' icon='Compare' isLight>
												Compared to{' '}
												<strong>
													{Number(dayjs().format('YYYY')) - 1}
												</strong>
												.
											</Button>
										</DropdownToggle>
										<DropdownMenu isAlignmentEnd size='sm'>
											<DropdownItem>
												<span>{Number(dayjs().format('YYYY')) - 2}</span>
											</DropdownItem>
											<DropdownItem>
												<span>{Number(dayjs().format('YYYY')) - 3}</span>
											</DropdownItem>
										</DropdownMenu>
									</Dropdown>
								</CardActions>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-md-6'>
										<Card
											className={`bg-l${
												darkModeStatus ? 'o25' : '25'
											}-primary bg-l${
												darkModeStatus ? 'o50' : '10'
											}-primary-hover transition-base rounded-2 mb-4`}
											shadow='sm'>
											<CardHeader className='bg-transparent'>
												<CardLabel>
													<CardTitle tag='div' className='h5'>
														Customer Happiness
													</CardTitle>
												</CardLabel>
											</CardHeader>
											<CardBody>
												<div className='d-flex align-items-center pb-3'>
													<div className='flex-shrink-0'>
														<Icon
															icon='EmojiEmotions'
															size='4x'
															color='primary'
														/>
													</div>
													<div className='flex-grow-1 ms-3'>
														<div className='fw-bold fs-3 mb-0'>
															100%
															<span className='text-info fs-5 fw-bold ms-3'>
																0
																<Icon icon='TrendingFlat' />
															</span>
														</div>
														<div className='text-muted'>
															Compared to ($5000 last year)
														</div>
													</div>
												</div>
											</CardBody>
										</Card>
										<Card
											className={`bg-l${
												darkModeStatus ? 'o25' : '25'
											}-danger bg-l${
												darkModeStatus ? 'o50' : '10'
											}-danger-hover transition-base rounded-2 mb-0`}
											shadow='sm'>
											<CardHeader className='bg-transparent'>
												<CardLabel>
													<CardTitle tag='div' className='h5'>
														Injury
													</CardTitle>
												</CardLabel>
											</CardHeader>
											<CardBody>
												<div className='d-flex align-items-center pb-3'>
													<div className='flex-shrink-0'>
														<Icon
															icon='Healing'
															size='4x'
															color='danger'
														/>
													</div>
													<div className='flex-grow-1 ms-3'>
														<div className='fw-bold fs-3 mb-0'>
															1
															<span className='text-danger fs-5 fw-bold ms-3'>
																-50%
																<Icon icon='TrendingDown' />
															</span>
														</div>
														<div className='text-muted'>
															Compared to (2 last week)
														</div>
													</div>
												</div>
											</CardBody>
										</Card>
									</div>
									<div className='col-md-6'>
										<Card
											className={`bg-l${
												darkModeStatus ? 'o25' : '25'
											}-success bg-l${
												darkModeStatus ? 'o50' : '10'
											}-success-hover transition-base rounded-2 mb-0`}
											stretch
											shadow='sm'>
											<CardHeader className='bg-transparent'>
												<CardLabel>
													<CardTitle tag='div' className='h5'>
														Daily Occupancy
													</CardTitle>
												</CardLabel>
											</CardHeader>
											<CardBody className='pt-0'>
												<Chart
													className='d-flex justify-content-center'
													series={dayHours.series}
													options={dayHours.options}
													type={dayHours.options.chart?.type}
													height={dayHours.options.chart?.height}
													width={dayHours.options.chart?.width}
												/>
												<div className='d-flex align-items-center pb-3'>
													<div className='flex-shrink-0'>
														<Icon
															icon='Timer'
															size='4x'
															color='success'
														/>
													</div>
													<div className='flex-grow-1 ms-3'>
														<div className='fw-bold fs-3 mb-0'>
															~22H
															<span className='text-success fs-5 fw-bold ms-3'>
																+12.5%
																<Icon icon='TrendingUp' />
															</span>
														</div>
														<div className='text-muted'>
															Compared to (~19H 30M last week)
														</div>
													</div>
												</div>
											</CardBody>
										</Card>
									</div>
								</div>
							</CardBody>
						</Card>
						<Card>
							<CardHeader>
								<CardLabel icon='Task' iconColor='danger'>
									<CardTitle>
										<CardLabel tag='div' className='h5'>
											Assigned
										</CardLabel>
									</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<div className='table-responsive'>
									<table className='table table-modern mb-0'>
										<thead>
											<tr>
												<th>Date / Time</th>
												<th>Customer</th>
												<th>Service</th>
												<th>Duration</th>
												<th>Payment</th>
												<th>Status</th>
											</tr>
										</thead>
										<tbody>
											{userTasks.map((item) => (
												<tr key={item.id}>
													<td>
														<div className='d-flex align-items-center'>
															<span
																className={classNames(
																	'badge',
																	'border border-2 border-light',
																	'rounded-circle',
																	'bg-success',
																	'p-2 me-2',
																	`bg-${item.status.color}`,
																)}>
																<span className='visually-hidden'>
																	{item.status.name}
																</span>
															</span>
															<span className='text-nowrap'>
																{dayjs(
																	`${item.date} ${item.time}`,
																).format('MMM Do YYYY, h:mm a')}
															</span>
														</div>
													</td>
													<td>
														<div>
															<div>{item.customer.name}</div>
															<div className='small text-muted'>
																{item.customer.email}
															</div>
														</div>
													</td>
													<td>{item.service.name}</td>
													<td>{item.duration}</td>
													<td>
														{item.payment && priceFormat(item.payment)}
													</td>
													<td>
														<Dropdown>
															<DropdownToggle hasIcon={false}>
																<Button
																	isLink
																	color={item.status.color}
																	icon='Circle'
																	className='text-nowrap'>
																	{item.status.name}
																</Button>
															</DropdownToggle>
															<DropdownMenu>
																{Object.keys(EVENT_STATUS).map(
																	(key) => (
																		<DropdownItem key={key}>
																			<div>
																				<Icon
																					icon='Circle'
																					color={
																						EVENT_STATUS[
																							key
																						].color
																					}
																				/>
																				{
																					EVENT_STATUS[
																						key
																					].name
																				}
																			</div>
																		</DropdownItem>
																	),
																)}
															</DropdownMenu>
														</Dropdown>
													</td>
												</tr>
											))}
										</tbody>
									</table>
								</div>
								{!userTasks.length && (
									<Alert color='warning' isLight icon='Report' className='mt-3'>
										There is no scheduled and assigned task.
									</Alert>
								)}
							</CardBody>
						</Card>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default EmployeePage;
