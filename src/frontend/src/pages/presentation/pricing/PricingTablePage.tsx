import React, { useState } from 'react';
import classNames from 'classnames';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import SubHeader, { SubHeaderLeft, SubheaderSeparator } from '../../../layout/SubHeader/SubHeader';
import Page from '../../../layout/Page/Page';
import { demoPagesMenu } from '../../../menu';
import Card, { CardBody, CardHeader } from '../../../components/bootstrap/Card';
import Icon from '../../../components/icon/Icon';
import Button from '../../../components/bootstrap/Button';
import useDarkMode from '../../../hooks/useDarkMode';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import ScrollspyNav from '../../../components/bootstrap/ScrollspyNav';

const PricingTablePage = () => {
	const { darkModeStatus } = useDarkMode();
	const [activeElementId, setActiveElementId] = useState<string | null>(null);
	return (
		<PageWrapper title={demoPagesMenu.pricingTable.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{
								title: demoPagesMenu.pricingTable.text,
								to: `/${demoPagesMenu.pricingTable.path}`,
							},
						]}
					/>
					<SubheaderSeparator />
					<ScrollspyNav
						items={['first', 'second', 'third']}
						setActiveId={setActiveElementId}
						offset={-500}
					/>
					<Button
						tag='a'
						to='#first'
						color='primary'
						isLight
						isActive={activeElementId === 'first'}>
						Design 1
					</Button>
					<Button
						tag='a'
						to='#second'
						color='primary'
						isLight
						isActive={activeElementId === 'second'}>
						Design 2
					</Button>
					<Button
						tag='a'
						to='#third'
						color='primary'
						isLight
						isActive={activeElementId === 'third'}>
						Design 3
					</Button>
				</SubHeaderLeft>
			</SubHeader>
			<Page>
				<div id='first' className='row scroll-margin'>
					<div className='col-12 mb-3'>
						<div className='display-4 fw-bold py-3'>Design 1</div>
					</div>
					<div className='col-md-3'>
						<Card stretch className='bg-transparent' shadow='none'>
							<CardBody>
								<div className='h-100 d-flex align-items-center justify-content-center'>
									<div className='row text-center'>
										<div className='col-12 text-uppercase fw-light'>
											Per Month
										</div>
										<div className='col-12 text-uppercase h2 fw-bold mb-2'>
											Select Your Perfect Plan
										</div>
										<div className='col-12 mb-2'>
											Vivamus ut magna pharetra, ultricies nunc eu, dignissim
											lorem. Proin et est nec ante ultricies dignissim sit
											amet eget libero.
										</div>
										<Icon icon='Verified' size='5x' color='info' />
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-3'>
						<Card>
							<CardBody>
								<div className='row pt-5 g-4 text-center'>
									<div className='col-12'>
										<Icon icon='CustomRocketLaunch' size='7x' color='info' />
									</div>
									<div className='col-12'>
										<h2>Startup Company</h2>
									</div>
									<div className='col-12'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>219
											<span className='display-6'>/mo</span>
										</h3>
									</div>
									<div className='col-12'>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Exclusive
											Workspace
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Internet
											Connection
										</div>
										<div className='lead text-muted'>
											<Icon icon='Close' color='danger' /> Meeting Room
										</div>
										<div className='lead text-muted'>
											<Icon icon='Close' color='danger' /> Small Rest Room
										</div>
									</div>
									<div className='col-12'>
										<p>Lorem ipsum dolor sit amet.</p>
									</div>
									<div className='col-12'>
										<Button
											color='info'
											isLight
											className='w-100 py-3 text-uppercase'
											size='lg'>
											Select Plan
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-3'>
						<Card>
							<CardBody>
								<div className='row pt-5 g-4 text-center'>
									<div className='col-12'>
										<Icon icon='Maps Home Work' size='7x' color='success' />
									</div>
									<div className='col-12'>
										<h2>Mid-Size Company</h2>
									</div>
									<div className='col-12'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>339
											<span className='display-6'>/mo</span>
										</h3>
									</div>
									<div className='col-12'>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Exclusive
											Workspace
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Internet
											Connection
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Five
											Meeting Room
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Small Rest
											Room
										</div>
									</div>
									<div className='col-12'>
										<p>Lorem ipsum dolor sit amet.</p>
									</div>
									<div className='col-12'>
										<Button
											color='success'
											className='w-100 py-3 text-uppercase'
											size='lg'>
											Select Plan
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-3'>
						<Card>
							<CardBody>
								<div className='row pt-5 g-4 text-center'>
									<div className='col-12'>
										<Icon icon='CustomFactory' size='7x' color='info' />
									</div>
									<div className='col-12'>
										<h2>Large Company</h2>
									</div>
									<div className='col-12'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>339
											<span className='display-6'>/mo</span>
										</h3>
									</div>
									<div className='col-12'>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Exclusive
											Workspace
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Internet
											Connection
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Five
											Meeting Room
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Large Rest
											Room
										</div>
									</div>
									<div className='col-12'>
										<p>Lorem ipsum dolor sit amet.</p>
									</div>
									<div className='col-12'>
										<Button
											color='info'
											isLight
											className='w-100 py-3 text-uppercase'
											size='lg'>
											Select Plan
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
				</div>

				<div id='second' className='row scroll-margin'>
					<div className='col-12 mb-3'>
						<div className='display-4 fw-bold py-3'>Design 2</div>
					</div>

					<div className='col-md-4'>
						<Card stretch>
							<CardBody>
								<div className='row pt-5 g-4 align-items-center'>
									<div className='col-auto'>
										<Icon icon='CustomRocketLaunch' size='5x' color='warning' />
									</div>
									<div className='col'>
										<h2>Startup Company</h2>
									</div>
									<div className='col-12'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>219
											<span className='display-6'>/mo</span>
										</h3>
									</div>
									<div className='col-12'>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Exclusive
											Workspace
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Internet
											Connection
										</div>
										<div className='lead text-muted'>
											<Icon icon='Close' color='danger' /> Meeting Room
										</div>
										<div className='lead text-muted'>
											<Icon icon='Close' color='danger' /> Small Rest Room
										</div>
									</div>
									<div className='col-12'>
										<p>Lorem ipsum dolor sit amet.</p>
									</div>
									<div className='col-12'>
										<Button
											color='warning'
											isLight
											className='w-100 py-3 text-uppercase'
											size='lg'>
											Select Plan
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-4'>
						<Card stretch borderColor='primary' borderSize={3} shadow='lg'>
							<CardBody>
								<div className='row pt-5 g-4 align-items-center'>
									<div className='col-auto'>
										<Icon icon='Maps Home Work' size='5x' color='primary' />
									</div>
									<div className='col'>
										<h2>Mid-Size Company</h2>
									</div>
									<div className='col-12'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>339
											<span className='display-6'>/mo</span>
										</h3>
									</div>
									<div className='col-12'>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Exclusive
											Workspace
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Internet
											Connection
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Five
											Meeting Room
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Small Rest
											Room
										</div>
									</div>
									<div className='col-12'>
										<p>Lorem ipsum dolor sit amet.</p>
									</div>
									<div className='col-12'>
										<Button
											color='primary'
											className='w-100 py-3 text-uppercase'
											size='lg'>
											Select Plan
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-4'>
						<Card stretch>
							<CardBody>
								<div className='row pt-5 g-4 align-items-center'>
									<div className='col-auto'>
										<Icon icon='CustomFactory' size='5x' color='info' />
									</div>
									<div className='col'>
										<h2>Large Company</h2>
									</div>
									<div className='col-12'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>339
											<span className='display-6'>/mo</span>
										</h3>
									</div>
									<div className='col-12'>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Exclusive
											Workspace
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Internet
											Connection
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Five
											Meeting Room
										</div>
										<div className='lead'>
											<Icon icon='Done Outline' color='success' /> Large Rest
											Room
										</div>
									</div>
									<div className='col-12'>
										<p>Lorem ipsum dolor sit amet.</p>
									</div>
									<div className='col-12'>
										<Button
											color='info'
											isLight
											className='w-100 py-3 text-uppercase'
											size='lg'>
											Select Plan
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
				</div>

				<div id='third' className='row scroll-margin'>
					<div className='col-12 my-3'>
						<div className='display-4 fw-bold py-3'>Design 3</div>
					</div>
					<div className='col-md-4'>
						<Card>
							<CardHeader borderSize={1}>
								<div className='col text-center py-4'>
									<h2 className='fw-bold display-5'>Personal</h2>
									<div className='h5 fw-light'>Get the hack started.</div>
								</div>
							</CardHeader>
							<CardBody>
								<div className='row g-5 pt-4 text-center'>
									<div className='col-auto mx-auto'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>135
										</h3>
										<div className='text-end mt-n4'>
											<span className='fw-bold text-muted text-uppercase'>
												Per Month
											</span>
										</div>
									</div>
									<div className='col-8 mx-auto'>
										Lorem ipsum dolor sit amet, consectetur adipiscing elit.
									</div>
									<div className='col-12'>
										<Button color='primary' isLight size='lg' className='w-100'>
											Get Started
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-4'>
						<Card
							className={classNames({
								'bg-lo25-primary': darkModeStatus,
								'bg-l25-primary': !darkModeStatus,
							})}>
							<CardHeader
								borderSize={1}
								borderColor='primary'
								className='bg-transparent'>
								<div className='col text-center py-4 position-relative'>
									<h2 className='fw-bold display-5'>Business</h2>
									<div className='h5 fw-light'>Get the hack started.</div>
									<span className='position-absolute top-0 end-0 border border-primary border-2 text-primary fw-bold px-2 py-1 rounded-1 lead text-uppercase'>
										propose
									</span>
								</div>
							</CardHeader>
							<CardBody>
								<div className='row g-5 pt-4 text-center'>
									<div className='col-auto mx-auto'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>175
										</h3>
										<div className='text-end mt-n4'>
											<span className='fw-bold text-muted text-uppercase'>
												Per Month
											</span>
										</div>
									</div>
									<div className='col-8 mx-auto'>
										Lorem ipsum dolor sit amet, consectetur adipiscing elit.
									</div>
									<div className='col-12'>
										<Button color='primary' size='lg' className='w-100'>
											Get Started
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-4'>
						<Card>
							<CardHeader borderSize={1}>
								<div className='col text-center py-4'>
									<h2 className='fw-bold display-5'>Expert</h2>
									<div className='h5 fw-light'>Get the hack started.</div>
								</div>
							</CardHeader>
							<CardBody>
								<div className='row g-5 pt-4 text-center'>
									<div className='col-auto mx-auto'>
										<h3 className='display-1 fw-bold'>
											<span className='display-4 fw-bold'>$</span>380
										</h3>
										<div className='text-end mt-n4'>
											<span className='fw-bold text-muted text-uppercase'>
												Per Month
											</span>
										</div>
									</div>
									<div className='col-8 mx-auto'>
										Lorem ipsum dolor sit amet, consectetur adipiscing elit.
									</div>
									<div className='col-12'>
										<Button color='primary' isLight size='lg' className='w-100'>
											Get Started
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default PricingTablePage;
