import React from 'react';
import { HashLink } from 'react-router-hash-link';
import SubHeader, { SubHeaderLeft } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import Page from '../../../layout/Page/Page';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Card, {
	CardBody,
	CardHeader,
	CardLabel,
	CardTitle,
} from '../../../components/bootstrap/Card';
import CommonCodePreview from '../../../common/other/CommonCodePreview';
import PlaceholderImage from '../../../components/extras/PlaceholderImage';
import { componentPagesMenu } from '../../../menu';

const SizingPage = () => {
	const RELATIVE = `
<div className='w-25 p-3' style={{ backgroundColor: '#eee' }}>Width 25%</div>
<div className='w-50 p-3' style={{ backgroundColor: '#eee' }}>Width 50%</div>
<div className='w-75 p-3' style={{ backgroundColor: '#eee' }}>Width 75%</div>
<div className='w-100 p-3' style={{ backgroundColor: '#eee' }}>Width 100%</div>
<div className='w-auto p-3' style={{ backgroundColor: '#eee' }}>Width auto</div>`;
	const RELATIVE_2 = `
<div style={{ height: 100, backgroundColor: 'rgba(255, 0, 0, 0.1)' }}>
	<div className='h-25 d-inline-block' style={{ width: 120, backgroundColor: 'rgba(0, 0, 255, .1)' }}>Height 25%</div>
	<div className='h-50 d-inline-block' style={{ width: 120, backgroundColor: 'rgba(0, 0, 255, .1)' }}>Height 50%</div>
	<div className='h-75 d-inline-block' style={{ width: 120, backgroundColor: 'rgba(0, 0, 255, .1)' }}>Height 75%</div>
	<div className='h-100 d-inline-block' style={{ width: 120, backgroundColor: 'rgba(0, 0, 255, .1)' }}>Height 100%</div>
	<div className='h-auto d-inline-block' style={{ width: 120, backgroundColor: 'rgba(0, 0, 255, .1)' }}>Height auto</div>
</div>`;
	const RELATIVE_3 = `<img src='...' className='mw-100' alt='...' />`;
	const RELATIVE_4 = `
<div style={{ height: 100, backgroundColor: 'rgba(255, 0, 0, .1)' }}>
	<div className='mh-100' style={{ width: 100, height: 200, backgroundColor: 'rgba(0, 0, 255, .1)' }}>
		Max-height 100%
	</div>
</div>`;
	const RELATIVE_5 = `
<div className='min-vw-100'>Min-width 100vw</div>
<div className='min-vh-100'>Min-height 100vh</div>
<div className='vw-100'>Width 100vw</div>
<div className='vh-100'>Height 100vh</div>`;

	const UTILITIES_API = `    
"width": (
  	property: width,
  	class: w,
  	values: (
		25: 25%,
		50: 50%,
		75: 75%,
		100: 100%,
		auto: auto
  	)
),
"max-width": (
  	property: max-width,
  	class: mw,
  	values: (100: 100%)
),
"viewport-width": (
  	property: width,
  	class: vw,
  	values: (100: 100vw)
),
"min-viewport-width": (
  	property: min-width,
  	class: min-vw,
  	values: (100: 100vw)
),
"height": (
  	property: height,
  	class: h,
  	values: (
		25: 25%,
		50: 50%,
		75: 75%,
		100: 100%,
		auto: auto
  	)
),
"max-height": (
  	property: max-height,
  	class: mh,
  	values: (100: 100%)
),
"viewport-height": (
  	property: height,
  	class: vh,
  	values: (100: 100vh)
),
"min-viewport-height": (
  	property: min-height,
  	class: min-vh,
  	values: (100: 100vh)
),`;

	return (
		<PageWrapper title={componentPagesMenu.utilities.subMenu.sizing.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{ title: 'Utilities', to: '/utilities' },
							{ title: 'Sizing', to: '/utilities/sizing' },
						]}
					/>
				</SubHeaderLeft>
			</SubHeader>
			<Page>
				<div className='row'>
					<div className='col-12 my-4'>
						<h1>Sizing</h1>
						<p className='lead'>
							Easily make an element as wide or as tall with our width and height
							utilities.
						</p>
					</div>

					<div className='col-12'>
						<Card id='relative-to-the-parent' className='scroll-margin'>
							<CardHeader>
								<CardLabel>
									<CardTitle tag='h3'>Relative to the parent</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<p>
									Width and height utilities are generated from the utility API in{' '}
									<code>_utilities.scss</code>. Includes support for{' '}
									<code>25%</code>, <code>50%</code>, <code>75%</code>,{' '}
									<code>100%</code>, and <code>auto</code> by default. Modify
									those values as you need to generate different utilities here.
								</p>
								<CommonCodePreview code={RELATIVE}>
									<div className='w-25 p-3' style={{ backgroundColor: '#eee' }}>
										Width 25%
									</div>
									<div className='w-50 p-3' style={{ backgroundColor: '#eee' }}>
										Width 50%
									</div>
									<div className='w-75 p-3' style={{ backgroundColor: '#eee' }}>
										Width 75%
									</div>
									<div className='w-100 p-3' style={{ backgroundColor: '#eee' }}>
										Width 100%
									</div>
									<div className='w-auto p-3' style={{ backgroundColor: '#eee' }}>
										Width auto
									</div>
								</CommonCodePreview>
								<CommonCodePreview code={RELATIVE_2}>
									<div
										style={{
											height: 100,
											backgroundColor: 'rgba(255, 0, 0, 0.1)',
										}}>
										<div
											className='h-25 d-inline-block'
											style={{
												width: 120,
												backgroundColor: 'rgba(0, 0, 255, 0.1)',
											}}>
											Height 25%
										</div>
										<div
											className='h-50 d-inline-block'
											style={{
												width: 120,
												backgroundColor: 'rgba(0, 0, 255, 0.1)',
											}}>
											Height 50%
										</div>
										<div
											className='h-75 d-inline-block'
											style={{
												width: 120,
												backgroundColor: 'rgba(0, 0, 255, 0.1)',
											}}>
											Height 75%
										</div>
										<div
											className='h-100 d-inline-block'
											style={{
												width: 120,
												backgroundColor: 'rgba(0, 0, 255, 0.1)',
											}}>
											Height 100%
										</div>
										<div
											className='h-auto d-inline-block'
											style={{
												width: 120,
												backgroundColor: 'rgba(0, 0, 255, 0.1)',
											}}>
											Height auto
										</div>
									</div>
								</CommonCodePreview>
								<p>
									You can also use <code>max-width: 100%;</code> and{' '}
									<code>max-height: 100%;</code> utilities as needed.
								</p>
								<CommonCodePreview code={RELATIVE_3}>
									<PlaceholderImage
										width='100%'
										height={100}
										text='Max-width 100%'
										className='mw-100'
									/>
								</CommonCodePreview>
								<CommonCodePreview code={RELATIVE_4}>
									<div
										style={{
											height: 100,
											backgroundColor: 'rgba(255, 0, 0, 0.1)',
										}}>
										<div
											className='mh-100'
											style={{
												width: 100,
												height: 200,
												backgroundColor: 'rgba(0, 0, 255, 0.1)',
											}}>
											Max-height 100%
										</div>
									</div>
								</CommonCodePreview>
							</CardBody>
						</Card>
					</div>

					<div className='col-12'>
						<Card id='relative-to-the-viewport' className='scroll-margin'>
							<CardHeader>
								<CardLabel>
									<CardTitle tag='h3'>Relative to the viewport</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<p>
									You can also use utilities to set the width and height relative
									to the viewport.
								</p>
								<CommonCodePreview code={RELATIVE_5} />
							</CardBody>
						</Card>
					</div>

					<div className='col-12'>
						<Card id='sass' className='scroll-margin'>
							<CardHeader>
								<CardLabel>
									<CardTitle tag='h3'>Sass</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<Card id='utilities-api' className='scroll-margin rounded-2'>
									<CardHeader>
										<CardLabel>
											<CardTitle>Utilities API</CardTitle>
										</CardLabel>
									</CardHeader>
									<CardBody>
										<p>
											Sizing utilities are declared in our utilities API in{' '}
											<code>node_modules/bootstrap/scss/_utilities.scss</code>
											.{' '}
											<HashLink to='../utilities/api#using-the-api'>
												Learn how to use the utilities API.
											</HashLink>
										</p>
										<CommonCodePreview code={UTILITIES_API} language='scss' />
									</CardBody>
								</Card>
							</CardBody>
						</Card>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default SizingPage;
