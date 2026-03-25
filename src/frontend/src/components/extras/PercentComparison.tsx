import React, { FC } from 'react';
import Icon from '../icon/Icon';
import { percent } from '../../helpers/helpers';

interface IPercentComparisonProps {
	valueOne: number;
	valueTwo: number;
}
const PercentComparison: FC<IPercentComparisonProps> = ({ valueOne, valueTwo }) => {
	const VALUE = percent(valueOne, valueTwo);
	function getIcon() {
		if (VALUE > 0) {
			return { icon: 'TrendingUp', color: 'success' };
		}
		if (VALUE < 0) {
			return { icon: 'TrendingDown', color: 'danger' };
		}
		return { icon: 'TrendingFlat', color: 'info' };
	}
	return (
		<span className={`text-${getIcon().color} fs-5 fw-bold ms-3 d-inline`}>
			{`${VALUE}%`}
			<Icon icon={getIcon().icon} />
		</span>
	);
};

export default PercentComparison;
