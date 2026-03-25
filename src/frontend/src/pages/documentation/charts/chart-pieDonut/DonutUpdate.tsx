import React, { useState } from 'react';
import Card, {
	CardBody,
	CardFooter,
	CardFooterLeft,
	CardFooterRight,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../../components/bootstrap/Card';
import Chart, { IChartOptions } from '../../../../components/extras/Chart';
import Button from '../../../../components/bootstrap/Button';

const DonutUpdate = () => {
	const [state, setState] = useState<IChartOptions>({
		series: [44, 55, 13, 33],
		options: {
			chart: {
				width: 380,
				type: 'donut',
			},
			dataLabels: {
				enabled: false,
			},
			legend: {
				position: 'right',
				offsetY: 0,
				height: 230,
			},
		},
	});

	function appendData() {
		if (state.series) {
			const arr = state.series.slice();
			// @ts-ignore
			arr.push(Math.floor(Math.random() * (100 - 1 + 1)) + 1);

			setState({
				series: arr,
				options: state.options,
			});
		}
	}

	function removeData() {
		if (state.series) {
			if (state.series.length === 1) return;

			const arr = state.series.slice();
			arr.pop();

			setState({
				series: arr,
				options: state.options,
			});
		}
	}

	function randomize() {
		if (state.series) {
			setState({
				series: state.series.map(() => {
					return Math.floor(Math.random() * (100 - 1 + 1)) + 1;
				}),
				options: state.options,
			});
		}
	}

	function reset() {
		setState({
			series: [44, 55, 13, 33],
			options: state.options,
		});
	}

	return (
		<div className='col-lg-6'>
			<Card stretch>
				<CardHeader>
					<CardLabel icon='PieChart'>
						<CardTitle>
							type <small>donut</small>
						</CardTitle>
						<CardSubTitle>Chart</CardSubTitle>
					</CardLabel>
				</CardHeader>
				<CardBody>
					<Chart series={state.series} options={state.options} type='donut' width={380} />
				</CardBody>
				<CardFooter>
					<CardFooterLeft>
						<Button color='info' isLink onClick={() => reset()}>
							RESET
						</Button>
					</CardFooterLeft>
					<CardFooterRight>
						<Button color='info' isOutline onClick={() => randomize()}>
							RANDOMIZE
						</Button>
						<Button
							color='success'
							isLight
							onClick={() => appendData()}
							isDisable={state.series && state.series.length >= 6}>
							+ ADD
						</Button>
						<Button
							color='danger'
							isLight
							onClick={() => removeData()}
							isDisable={state.series && state.series.length === 1}>
							- REMOVE
						</Button>
					</CardFooterRight>
				</CardFooter>
			</Card>
		</div>
	);
};

export default DonutUpdate;
