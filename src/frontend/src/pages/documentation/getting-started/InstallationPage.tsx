import React from 'react';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import { gettingStartedPagesMenu } from '../../../menu';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Card, {
	CardBody,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
	ICardProps,
} from '../../../components/bootstrap/Card';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import CommonCodeCopy from './_common/CommonCodeCopy';

const InstallationPage = () => {
	const cardProps: Partial<ICardProps> = {
		shadow: 'none',
		borderSize: 1,
		className: 'mb-0 rounded-1',
	};

	return (
		<PageWrapper title={gettingStartedPagesMenu.gettingStarted.subMenu.installation.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{
								title: gettingStartedPagesMenu.gettingStarted.text,
								to: gettingStartedPagesMenu.gettingStarted.path,
							},
							{
								title: gettingStartedPagesMenu.gettingStarted.subMenu.installation
									.text,
								to: gettingStartedPagesMenu.gettingStarted.subMenu.installation
									.path,
							},
						]}
					/>
				</SubHeaderLeft>
				<SubHeaderRight>
					<span>
						<i>Next step</i>
					</span>
					<Button
						tag='a'
						color='success'
						isLight
						icon={gettingStartedPagesMenu.gettingStarted.subMenu.dev.icon}
						to={`../${gettingStartedPagesMenu.gettingStarted.subMenu.dev.path}`}>
						{gettingStartedPagesMenu.gettingStarted.subMenu.dev.text}
					</Button>
				</SubHeaderRight>
			</SubHeader>
			<Page>
				<div className='row h-100 align-content-start'>
					<div className='col-12'>
						<Card>
							<CardHeader>
								<CardLabel icon='BuildCircle' iconColor='info'>
									<CardTitle>Before you start</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<p>
									While explaining these documents, it is assumed that you have a
									basic knowledge of JS, HTML, CSS and ReactJS. You can learn more
									about ReactJS at{' '}
									<a href='https://reactjs.org/' target='_blank' rel='noreferrer'>
										https://reactjs.org/
									</a>
									.
								</p>
								<p>
									If you do not have nodejs installed on your computer, you can
									download <b>versions 16</b> and above from{' '}
									<a href='https://nodejs.org/' target='_blank' rel='noreferrer'>
										https://nodejs.org/
									</a>
									. When you install node js, <Icon icon='CustomNpm' /> npm is
									also installed with it.
								</p>
								<p>
									If you want to use <Icon icon='CustomYarn' /> Yarn you can
									follow the instructions here{' '}
									<a
										href='https://classic.yarnpkg.com/en/docs/install/'
										target='_blank'
										rel='noreferrer'>
										https://classic.yarnpkg.com/en/docs/install/
									</a>
									.
								</p>
							</CardBody>
						</Card>
						<Card>
							<CardHeader>
								<CardLabel icon='CloudDownload' iconColor='info'>
									<CardTitle>Install Dependencies</CardTitle>
									<CardSubTitle>Step 1</CardSubTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<p>
									First, go to the root folder of the project with the terminal.
									You can use npm or yarn to download the required dependencies.
								</p>
								<div className='row g-4'>
									<div className='col-md-6'>
										{/* eslint-disable-next-line react/jsx-props-no-spreading */}
										<Card {...cardProps}>
											<CardHeader>
												<CardLabel icon='CustomNpm'>
													<CardTitle>npm</CardTitle>
												</CardLabel>
											</CardHeader>
											<CardBody>
												<CommonCodeCopy>
													npm install --legacy-peer-deps
												</CommonCodeCopy>
											</CardBody>
										</Card>
									</div>
									<div className='col-md-6'>
										{/* eslint-disable-next-line react/jsx-props-no-spreading */}
										<Card {...cardProps}>
											<CardHeader>
												<CardLabel icon='CustomYarn'>
													<CardTitle>yarn</CardTitle>
												</CardLabel>
											</CardHeader>
											<CardBody>
												<CommonCodeCopy>yarn install</CommonCodeCopy>
											</CardBody>
										</Card>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
				</div>
				<div className='row'>
					<div className='col-12'>
						<Card>
							<CardBody>
								<div className='row align-items-center'>
									<div className='col'>
										<Icon
											icon='CheckCircle'
											size='lg'
											className='me-2 text-muted'
										/>
										<span>
											If you are ready, you can proceed to the next step.
										</span>
									</div>
									<div className='col-auto'>
										<Button
											tag='a'
											color='success'
											isLight
											icon='NavigateNext'
											to={`../${gettingStartedPagesMenu.gettingStarted.subMenu.dev.path}`}>
											Next
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

export default InstallationPage;
