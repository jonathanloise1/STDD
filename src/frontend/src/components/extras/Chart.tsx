import React, { FC, HTMLAttributes, memo } from 'react';
import ReactApexChart from 'react-apexcharts';
import classNames from 'classnames';
import { ApexOptions } from 'apexcharts';

interface IChartProps extends HTMLAttributes<HTMLDivElement> {
	series: ApexOptions['series'];
	options: ApexOptions;
	type?: ApexChart['type'];
	width?: string | number;
	height?: string | number;
	className?: string;
}
const Chart: FC<IChartProps> = ({
	series,
	options,
	type = 'line',
	width = '100%',
	height = 'auto',
	className,
	...props
}) => {
	return (
		<div className={classNames('apex-chart', className)} {...props}>
			<ReactApexChart
				options={{
					colors: [
						import.meta.env.VITE_PRIMARY_COLOR,
						import.meta.env.VITE_SECONDARY_COLOR,
						import.meta.env.VITE_SUCCESS_COLOR,
						import.meta.env.VITE_INFO_COLOR,
						import.meta.env.VITE_WARNING_COLOR,
						import.meta.env.VITE_DANGER_COLOR,
					],
					plotOptions: {
						candlestick: {
							colors: {
								upward: import.meta.env.VITE_SUCCESS_COLOR,
								downward: import.meta.env.VITE_DANGER_COLOR,
							},
						},
						boxPlot: {
							colors: {
								upper: import.meta.env.VITE_SUCCESS_COLOR,
								lower: import.meta.env.VITE_DANGER_COLOR,
							},
						},
					},
					...options,
				}}
				series={series}
				// @ts-ignore
				type={type}
				width={width}
				height={height}
			/>
		</div>
	);
};

/**
 * For use useState
 */
export interface IChartOptions extends Record<string, any> {
	series: ApexOptions['series'];
	options: ApexOptions;
}

export default memo(Chart);
