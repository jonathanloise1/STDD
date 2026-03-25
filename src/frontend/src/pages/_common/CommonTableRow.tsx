import React, { FC } from 'react';
import { Link } from 'react-router-dom';
import classNames from 'classnames';
import { ApexOptions } from 'apexcharts';
import Checks from '../../components/bootstrap/forms/Checks';
import Chart from '../../components/extras/Chart';
import Badge from '../../components/bootstrap/Badge';
import Button from '../../components/bootstrap/Button';
import { demoPagesMenu } from '../../menu';
import useDarkMode from '../../hooks/useDarkMode';

interface ICommonTableRowProps {
	id: string | number;
	image: string;
	name: string;
	category: string;
	series: ApexOptions['series'];
	color: string;
	stock: string | number;
	price: number;
	store: string;
	selectOnChange: any;
	selectChecked: any;
	selectName: string;
}
const CommonTableRow: FC<ICommonTableRowProps> = ({
	id,
	image,
	name,
	category,
	series,
	color,
	stock,
	price,
	store,
	selectOnChange,
	selectChecked,
	selectName,
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
			<th scope='row' aria-label='Check'>
				<Checks
					id={id.toString()}
					name={selectName}
					value={id}
					onChange={selectOnChange}
					checked={selectChecked}
					ariaLabel={selectName}
				/>
			</th>
			<th scope='row'>{id}</th>
			<td>
				<Link to={`../${demoPagesMenu.sales.subMenu.productID.path}/${id}`}>
					<img src={image} alt={name} width={54} height={54} />
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
					{price.toLocaleString('en-US', {
						style: 'currency',
						currency: 'USD',
					})}
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
			<td className='text-end'>
				<Button
					color='dark'
					isLight
					icon='Edit'
					tag='a'
					to={`../${demoPagesMenu.sales.subMenu.productID.path}/${id}`}
					aria-label='Edit'
				/>
			</td>
		</tr>
	);
};

export default CommonTableRow;
