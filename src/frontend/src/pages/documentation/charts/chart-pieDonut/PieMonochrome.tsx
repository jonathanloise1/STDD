import React, { useState } from 'react';
import Card, {
	CardBody,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../../components/bootstrap/Card';
import Chart, { IChartOptions } from '../../../../components/extras/Chart';

const PieMonochrome = () => {
	const [state] = useState<IChartOptions>({
		series: [25, 15, 44, 55, 41, 17],
		options: {
			chart: {
				width: 400,
				type: 'pie',
			},
			labels: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'],
			theme: {
				monochrome: {
					enabled: true,
				},
			},
			plotOptions: {
				pie: {
					dataLabels: {
						offset: -5,
					},
				},
			},
			title: {
				text: 'Monochrome Pie',
			},
			dataLabels: {
				// @ts-ignore
				formatter(val, opts) {
					const name = opts.w.globals.labels[opts.seriesIndex];
					return [name, `${Number(val).toFixed(1)}%`];
				},
			},
			legend: {
				show: false,
			},
		},
	});
	return (
		<div className='col-lg-6'>
			<Card stretch>
				<CardHeader>
					<CardLabel icon='PieChart'>
						<CardTitle>
							type <small>pie</small>
						</CardTitle>
						<CardSubTitle>Chart</CardSubTitle>
					</CardLabel>
				</CardHeader>
				<CardBody>
					<Chart
						series={state.series}
						options={state.options}
						type={state.options.chart?.type}
						width={state.options.chart?.width}
					/>
				</CardBody>
			</Card>
		</div>
	);
};

export default PieMonochrome;
