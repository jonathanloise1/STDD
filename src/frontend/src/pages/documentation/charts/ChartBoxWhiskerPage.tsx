import React from 'react';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import Page from '../../../layout/Page/Page';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import BoxWhiskerBasic from './chart-boxWhisker/BoxWhiskerBasic';
import BoxWhiskerBoxPlotScatter from './chart-boxWhisker/BoxWhiskerBoxPlotScatter';
import CommonStoryBtn from '../../../common/other/CommonStoryBtn';
import { componentPagesMenu } from '../../../menu';

const ChartBoxWhiskerPage = () => {
	return (
		<PageWrapper title={componentPagesMenu.charts.subMenu.chartsBoxWhisker.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{ title: 'Charts', to: '/charts' },
							{ title: 'Box Whisker', to: '/charts/box-whisker' },
						]}
					/>
				</SubHeaderLeft>
				<SubHeaderRight>
					<CommonStoryBtn to='/story/extra-chart-box-whisker--box-whisker-basic' />
				</SubHeaderRight>
			</SubHeader>
			<Page>
				<div className='row'>
					<BoxWhiskerBasic />
					<BoxWhiskerBoxPlotScatter />
				</div>
			</Page>
		</PageWrapper>
	);
};

export default ChartBoxWhiskerPage;
