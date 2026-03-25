import React, { FC, useEffect, useState } from 'react';
import classNames from 'classnames';
import dayjs from 'dayjs';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../../components/bootstrap/Card';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../../components/bootstrap/Dropdown';
import Button from '../../../../components/bootstrap/Button';
import Chart, { IChartOptions } from '../../../../components/extras/Chart';
import Icon from '../../../../components/icon/Icon';
import { average, priceFormat } from '../../../../helpers/helpers';
import PercentComparison from '../../../../components/extras/PercentComparison';
import useDarkMode from '../../../../hooks/useDarkMode';
import { TABS, TTabs } from './helper';

interface ICommonDashboardIncomeProps {
	activeTab: TTabs;
}
const CommonDashboardIncome: FC<ICommonDashboardIncomeProps> = ({ activeTab }) => {
	const { darkModeStatus } = useDarkMode();

	const [sales, setSales] = useState<IChartOptions>({
		series: [
			{
				data: [34, 32, 36, 34, 34],
			},
		],
		options: {
			colors: [import.meta.env.VITE_WARNING_COLOR],
			chart: {
				type: 'line',
				width: '100%',
				height: 150,
				sparkline: {
					enabled: true,
				},
			},
			tooltip: {
				theme: 'dark',
				fixed: {
					enabled: false,
				},
				x: {
					show: false,
				},
				y: {
					title: {
						// eslint-disable-next-line @typescript-eslint/no-unused-vars
						formatter(seriesName: any) {
							return '';
						},
					},
				},
			},
			stroke: {
				curve: 'smooth',
				width: 2,
			},
		},
		sales: {
			compare: 24,
		},
		campaigns: {
			price: 3265.72,
			compare: 5000,
		},
		coupons: {
			price: 2654.2,
			compare: 2300,
		},
	});
	useEffect(() => {
		if (activeTab === TABS.YEARLY) {
			setSales({
				series: [
					{
						data: [34, 32, 36, 34, 34],
					},
				],
				sales: {
					compare: 24,
				},
				campaigns: {
					price: 3265.72,
					compare: 5000,
				},
				coupons: {
					price: 2654.2,
					compare: 2300,
				},
				options: sales.options,
			});
		}
		if (activeTab === TABS.MONTHLY) {
			setSales({
				series: [
					{
						data: [32, 35, 40, 30, 32],
					},
				],
				sales: {
					compare: 27,
				},
				campaigns: {
					price: 450,
					compare: 480,
				},
				coupons: {
					price: 98,
					compare: 120,
				},
				options: sales.options,
			});
		}
		if (activeTab === TABS.WEEKLY) {
			setSales({
				series: [
					{
						data: [28, 32, 30, 29, 30],
					},
				],
				sales: {
					compare: 12,
				},
				campaigns: {
					price: 94,
					compare: 80,
				},
				coupons: {
					price: 80,
					compare: 45,
				},
				options: sales.options,
			});
		}
		return () => {};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [activeTab]);

	function compareLabel(amount = -1, name = false) {
		if (activeTab === TABS.YEARLY) {
			return name ? 'year' : dayjs().add(amount, 'year').format('YYYY');
		}
		if (activeTab === TABS.MONTHLY) {
			return name ? 'month' : dayjs().add(amount, 'month').format('MMMM');
		}
		return name ? 'week' : dayjs().add(amount, 'week').format('w [th week]');
	}
	return (
		<Card stretch>
			<CardHeader>
				<CardLabel icon='PointOfSale' iconColor='success'>
					<CardTitle tag='div' className='h5'>
						Income
					</CardTitle>
					<CardSubTitle tag='div' className='h6'>
						{activeTab}
					</CardSubTitle>
				</CardLabel>
				<CardActions>
					<Dropdown>
						<DropdownToggle>
							<Button color='info' icon='Compare' isLight>
								Compared to <strong>{compareLabel()}</strong>
							</Button>
						</DropdownToggle>
						<DropdownMenu isAlignmentEnd size='sm'>
							<DropdownItem>
								<Button>{compareLabel(-2)}</Button>
							</DropdownItem>
							<DropdownItem>
								<Button>{compareLabel(-3)}</Button>
							</DropdownItem>
						</DropdownMenu>
					</Dropdown>
				</CardActions>
			</CardHeader>
			<CardBody>
				<div className='row g-4'>
					<div className='col-md-6'>
						<Card
							className={classNames('transition-base rounded-2 mb-0 text-dark', {
								'bg-l25-warning bg-l10-warning-hover': !darkModeStatus,
								'bg-lo50-warning bg-lo25-warning-hover': darkModeStatus,
							})}
							stretch
							shadow='sm'>
							<CardHeader className='bg-transparent'>
								<CardLabel>
									<CardTitle tag='div' className='h5'>
										Sales
									</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<Chart
									className='mx-n4'
									series={sales.series}
									options={sales.options}
									type={sales.options.chart?.type}
									height={sales.options.chart?.height}
									width={sales.options.chart?.width}
								/>
								<div className='d-flex align-items-center pb-3'>
									<div className='flex-shrink-0'>
										<Icon icon='AttachMoney' size='4x' color='warning' />
									</div>
									<div className='flex-grow-1 ms-3'>
										<div className='fw-bold fs-3 mb-0'>
											{priceFormat(
												// @ts-ignore
												average(sales.series[0].data),
											)}
											<PercentComparison
												valueOne={average(
													// @ts-ignore
													sales.series[0].data,
												)}
												valueTwo={sales.sales.compare}
											/>
										</div>
										<div
											className={classNames({
												'text-muted': !darkModeStatus,
												'text-light': darkModeStatus,
											})}>
											Compared to ({priceFormat(sales.sales.compare)} last{' '}
											{compareLabel(0, true)})
										</div>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-6'>
						<Card
							className={classNames('transition-base rounded-2 mb-4 text-dark', {
								'bg-l25-secondary bg-l10-secondary-hover': !darkModeStatus,
								'bg-lo50-secondary bg-lo25-secondary-hover': darkModeStatus,
							})}
							shadow='sm'>
							<CardHeader className='bg-transparent'>
								<CardLabel>
									<CardTitle tag='div' className='h5'>
										Campaigns
									</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<div className='d-flex align-items-center pb-3'>
									<div className='flex-shrink-0'>
										<Icon icon='LocalOffer' size='4x' color='secondary' />
									</div>
									<div className='flex-grow-1 ms-3'>
										<div className='fw-bold fs-3 mb-0'>
											{priceFormat(sales.campaigns.price)}
											<PercentComparison
												valueOne={sales.campaigns.price}
												valueTwo={sales.campaigns.compare}
											/>
										</div>
										<div
											className={classNames({
												'text-muted': !darkModeStatus,
												'text-light': darkModeStatus,
											})}>
											Compared to ({priceFormat(sales.campaigns.compare)} last{' '}
											{compareLabel(0, true)})
										</div>
									</div>
								</div>
							</CardBody>
						</Card>
						<Card
							className={classNames('transition-base rounded-2 mb-0 text-dark', {
								'bg-l25-primary bg-l10-primary-hover': !darkModeStatus,
								'bg-lo50-primary bg-lo25-primary-hover': darkModeStatus,
							})}
							shadow='sm'>
							<CardHeader className='bg-transparent'>
								<CardLabel>
									<CardTitle tag='div' className='h5'>
										Coupons
									</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<div className='d-flex align-items-center pb-3'>
									<div className='flex-shrink-0'>
										<Icon icon='ConfirmationNumber' size='4x' color='primary' />
									</div>
									<div className='flex-grow-1 ms-3'>
										<div className='fw-bold fs-3 mb-0'>
											{priceFormat(sales.coupons.price)}
											<PercentComparison
												valueOne={sales.coupons.price}
												valueTwo={sales.coupons.compare}
											/>
										</div>
										<div
											className={classNames({
												'text-muted': !darkModeStatus,
												'text-light': darkModeStatus,
											})}>
											Compared to ({priceFormat(sales.coupons.compare)} last{' '}
											{compareLabel(0, true)})
										</div>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
				</div>
			</CardBody>
		</Card>
	);
};

export default CommonDashboardIncome;
