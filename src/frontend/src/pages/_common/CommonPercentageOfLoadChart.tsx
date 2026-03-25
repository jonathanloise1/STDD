import React from 'react';
import dayjs from 'dayjs';
import classNames from 'classnames';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../components/bootstrap/Card';
import Icon from '../../components/icon/Icon';
import Button from '../../components/bootstrap/Button';
import { demoPagesMenu } from '../../menu';
import Chart, { IChartOptions } from '../../components/extras/Chart';
import useDarkMode from '../../hooks/useDarkMode';

const CommonPercentageOfLoadChart = () => {
	const { darkModeStatus } = useDarkMode();
	const percentageOfLoad: IChartOptions = {
		series: [
			{
				name: 'Approved',
				data: [44, 55, 43, 54, 65, 44, 12],
			},
		],
		options: {
			colors: [import.meta.env.VITE_PRIMARY_COLOR],
			chart: {
				type: 'line',
				height: 200,
				dropShadow: {
					enabled: false,
					color: import.meta.env.VITE_PRIMARY_COLOR,
					top: 0,
					left: 0,
					blur: 10,
					opacity: 0.5,
				},
				toolbar: {
					show: false,
				},
				redrawOnParentResize: true,
				redrawOnWindowResize: true,
			},
			dataLabels: {
				enabled: false,
			},
			xaxis: {
				type: 'datetime',
				categories: [
					dayjs().format(),
					dayjs().add(1, 'days').format(),
					dayjs().add(2, 'days').format(),
					dayjs().add(3, 'days').format(),
					dayjs().add(4, 'days').format(),
					dayjs().add(5, 'days').format(),
					dayjs().add(6, 'days').format(),
				],
				labels: {
					rotate: -15,
					rotateAlways: true,
				},
			},
			yaxis: {
				labels: {
					show: false,
				},
			},
			tooltip: {
				theme: 'dark',
				y: {
					formatter(val: number): string {
						return `${val} Appointments`;
					},
				},
			},
			grid: {
				show: false,
			},
		},
	};
	return (
		<Card
			className={classNames('shadow-3d-primary', {
				'text-dark': darkModeStatus,
				'bg-lo50-primary': darkModeStatus,
				'bg-l25-primary': !darkModeStatus,
			})}
			isCompact>
			<CardHeader className='bg-transparent'>
				<CardLabel>
					<CardTitle>Percentage of Load</CardTitle>
					<CardSubTitle>
						<span className='text-danger fw-bold'>
							-68% <Icon icon='ArrowDownward' />
						</span>
					</CardSubTitle>
				</CardLabel>
				<CardActions>
					<Button
						color='primary'
						tag='a'
						to={`../${demoPagesMenu.appointment.subMenu.employeeList.path}`}>
						View Employees
					</Button>
				</CardActions>
			</CardHeader>
			<CardBody>
				<div className='position-absolute'>
					<span className='display-3 me-3'>18%</span>
				</div>
				<Chart
					series={percentageOfLoad.series}
					options={percentageOfLoad.options}
					type={percentageOfLoad.options.chart?.type}
					height={percentageOfLoad.options.chart?.height}
				/>
			</CardBody>
		</Card>
	);
};

export default CommonPercentageOfLoadChart;
