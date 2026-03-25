import React, { FC, useState } from 'react';
import dayjs from 'dayjs';
import { ApexOptions } from 'apexcharts';
import { Link } from 'react-router-dom';
import classNames from 'classnames';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardTitle,
} from '../../../../components/bootstrap/Card';
import Dropdown, {
	DropdownItem,
	DropdownMenu,
	DropdownToggle,
} from '../../../../components/bootstrap/Dropdown';
import Button from '../../../../components/bootstrap/Button';
import Icon from '../../../../components/icon/Icon';
import PaginationButtons, {
	dataPagination,
	PER_COUNT,
} from '../../../../components/PaginationButtons';
import data from '../../../../common/data/dummyProductData';
import useSortableData from '../../../../hooks/useSortableData';
import useDarkMode from '../../../../hooks/useDarkMode';
import { demoPagesMenu } from '../../../../menu';
import Chart from '../../../../components/extras/Chart';
import Badge from '../../../../components/bootstrap/Badge';

interface ITableRowProps {
	id: string;
	image: string;
	name: string;
	category: string;
	series: ApexOptions['series'];
	color: string;
	stock: string;
	price: string;
	store: string;
}
const TableRow: FC<ITableRowProps> = ({
	id,
	image,
	name,
	category,
	series,
	color,
	stock,
	price,
	store,
}) => {
	const { darkModeStatus } = useDarkMode();
	const dummyOptions: ApexOptions = {
		colors: [color],
		chart: {
			type: 'line',
			width: 100,
			height: 35,
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
					formatter(seriesName: string) {
						return '';
					},
				},
			},
		},
		stroke: {
			curve: 'smooth',
			width: 2,
		},
	};
	return (
		<tr>
			<th scope='row'>{id}</th>
			<td>
				<Link
					to={`../${demoPagesMenu.sales.subMenu.productID.path}/${id}`}
					aria-label={name}>
					<img src={image} alt='' width={54} height={54} />
				</Link>
			</td>
			<td>
				<div>
					<Link
						to={`../${demoPagesMenu.sales.subMenu.productID.path}/${id}`}
						className={classNames('fw-bold', {
							'link-dark': !darkModeStatus,
							'link-light': darkModeStatus,
						})}>
						{name}
					</Link>
					<div className='text-muted'>
						<small>{category}</small>
					</div>
				</div>
			</td>
			<td aria-label='Chart'>
				<Chart
					series={series}
					options={dummyOptions}
					type={dummyOptions.chart?.type}
					height={dummyOptions.chart?.height}
					width={dummyOptions.chart?.width}
				/>
			</td>
			<td>
				<span>{stock}</span>
			</td>
			<td>
				<span>
					{
						// @ts-ignore
						price.toLocaleString('en-US', {
							style: 'currency',
							currency: 'USD',
						})
					}
				</span>
			</td>
			<td className='h5'>
				<Badge
					color={
						(store === 'Company A' && 'danger') ||
						(store === 'Company B' && 'warning') ||
						(store === 'Company C' && 'success') ||
						'info'
					}>
					{store}
				</Badge>
			</td>
		</tr>
	);
};

const CommonDashboardTopSeller = () => {
	const TOP_SELLER_FILTER = {
		DAY: 'day',
		WEEK: 'week',
		MONTH: 'month',
	};
	const [topSellerFilter, setTopSellerFilter] = useState(TOP_SELLER_FILTER.DAY);
	const filteredData = data
		.filter(
			(f) =>
				(topSellerFilter === TOP_SELLER_FILTER.DAY && f.id < 6) ||
				(topSellerFilter === TOP_SELLER_FILTER.WEEK && f.name.includes('c')) ||
				(topSellerFilter === TOP_SELLER_FILTER.MONTH && f.price > 13),
		)
		.filter((c, index) => index < 5);

	const [currentPage, setCurrentPage] = useState(1);
	const [perPage, setPerPage] = useState(PER_COUNT['3']);
	const { items, requestSort, getClassNamesFor } = useSortableData(filteredData);
	return (
		<Card stretch>
			<CardHeader>
				<CardLabel icon='Storefront' iconColor='info'>
					<CardTitle tag='div' className='h5'>
						Top Seller
					</CardTitle>
				</CardLabel>
				<CardActions>
					<Dropdown isButtonGroup>
						<Button color='success' isLight icon='WaterfallChart'>
							{(topSellerFilter === TOP_SELLER_FILTER.DAY &&
								dayjs().format('MMM Do')) ||
								(topSellerFilter === TOP_SELLER_FILTER.WEEK &&
									`${dayjs().startOf('week').format('MMM Do')} - ${dayjs()
										.endOf('week')
										.format('MMM Do')}`) ||
								(topSellerFilter === TOP_SELLER_FILTER.MONTH &&
									dayjs().format('MMM YYYY'))}
						</Button>
						<DropdownToggle>
							<Button color='success' isLight isVisuallyHidden />
						</DropdownToggle>
						<DropdownMenu isAlignmentEnd>
							<DropdownItem>
								<Button onClick={() => setTopSellerFilter(TOP_SELLER_FILTER.DAY)}>
									Last Day
								</Button>
							</DropdownItem>
							<DropdownItem>
								<Button onClick={() => setTopSellerFilter(TOP_SELLER_FILTER.WEEK)}>
									Last Week
								</Button>
							</DropdownItem>
							<DropdownItem>
								<Button onClick={() => setTopSellerFilter(TOP_SELLER_FILTER.MONTH)}>
									Last Month
								</Button>
							</DropdownItem>
						</DropdownMenu>
					</Dropdown>
					<Button
						color='info'
						icon='CloudDownload'
						isLight
						tag='a'
						to='/somefile.txt'
						target='_blank'
						download>
						Export
					</Button>
				</CardActions>
			</CardHeader>
			<CardBody className='table-responsive'>
				<table className='table table-modern table-hover'>
					<thead>
						<tr>
							<th
								scope='col'
								onClick={() => requestSort('id')}
								className='cursor-pointer text-decoration-underline'>
								#{' '}
								<Icon
									size='lg'
									className={getClassNamesFor('id')}
									icon='FilterList'
								/>
							</th>
							<th scope='col'>Image</th>
							<th
								scope='col'
								onClick={() => requestSort('name')}
								className='cursor-pointer text-decoration-underline'>
								Name{' '}
								<Icon
									size='lg'
									className={getClassNamesFor('name')}
									icon='FilterList'
								/>
							</th>
							<th scope='col'>Sales</th>
							<th
								scope='col'
								onClick={() => requestSort('stock')}
								className='cursor-pointer text-decoration-underline'>
								Stock{' '}
								<Icon
									size='lg'
									className={getClassNamesFor('stock')}
									icon='FilterList'
								/>
							</th>
							<th
								scope='col'
								onClick={() => requestSort('price')}
								className='cursor-pointer text-decoration-underline'>
								Price{' '}
								<Icon
									size='lg'
									className={getClassNamesFor('price')}
									icon='FilterList'
								/>
							</th>
							<th
								scope='col'
								onClick={() => requestSort('store')}
								className='cursor-pointer text-decoration-underline'>
								Store{' '}
								<Icon
									size='lg'
									className={getClassNamesFor('store')}
									icon='FilterList'
								/>
							</th>
						</tr>
					</thead>
					<tbody>
						{dataPagination(items, currentPage, perPage).map((i) => (
							// eslint-disable-next-line react/jsx-props-no-spreading
							<TableRow key={i.id} {...i} />
						))}
					</tbody>
				</table>
			</CardBody>
			<PaginationButtons
				data={items}
				label='items'
				setCurrentPage={setCurrentPage}
				currentPage={currentPage}
				perPage={perPage}
				setPerPage={setPerPage}
			/>
		</Card>
	);
};

export default CommonDashboardTopSeller;
