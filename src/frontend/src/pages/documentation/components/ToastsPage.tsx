import React from 'react';
import { NavLink } from 'react-router-dom';
import { toast } from 'react-toastify';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import Button from '../../../components/bootstrap/Button';
import Card, {
	CardActions,
	CardBody,
	CardCodeView,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../components/bootstrap/Card';
import Toasts from '../../../components/bootstrap/Toasts';
import { componentPagesMenu } from '../../../menu';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import CommonStoryBtn from '../../../common/other/CommonStoryBtn';
import CommonHowToUse from '../../../common/other/CommonHowToUse';

const ToastsPage = () => {
	const GENERAL_USAGE = `
import Toasts from '../components/bootstrap/Toasts';
import { toast } from 'react-toastify';`;

	const GENERAL_USAGE_2 = `
	() => toast(
		<Toasts
			icon={ String }
			iconColor={ String } // 'primary' || 'secondary' || 'success' || 'info' || 'warning' || 'danger' || 'light' || 'dark'
			title={ String || Node }
			time={ String }
			{...props}>
			... { // String, HTML tag or React Component }
		</Toasts>,
		{
			closeButton: Boolean ,
			autoClose: Number // Examples: 1000, 3000, ...
		}
	)
`;

	return (
		<PageWrapper title={componentPagesMenu.components.subMenu.toasts.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{
								title: componentPagesMenu.components.text,
								to: `/${componentPagesMenu.components.path}`,
							},
							{
								title: componentPagesMenu.components.subMenu.toasts.text,
								to: `/${componentPagesMenu.components.subMenu.toasts.path}`,
							},
						]}
					/>
				</SubHeaderLeft>
				<SubHeaderRight>
					<CommonStoryBtn to='/docs/components-toasts--default' />
				</SubHeaderRight>
			</SubHeader>
			<Page>
				<div className='row'>
					{/* General Usage */}
					<div className='col-12'>
						<Card>
							<CardHeader>
								<CardLabel icon='Assignment'>
									<CardTitle>General Usage</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<CardCodeView className='mb-4'>{GENERAL_USAGE}</CardCodeView>
								<CardCodeView>{GENERAL_USAGE_2}</CardCodeView>
							</CardBody>
						</Card>
					</div>

					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='FilterNone' iconColor='primary'>
									<CardTitle>icon</CardTitle>
									<CardSubTitle>Toasts</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=icon:Info' />
								</CardActions>
							</CardHeader>
							<CardHeader>
								<CommonHowToUse>
									<div>icon: PropTypes.string,</div>
									{/* eslint-disable-next-line react/jsx-no-comment-textnodes */}
									<div>
										// <NavLink to='/icons/material'>Material</NavLink> or{' '}
										<NavLink to='/icons/bootstrap-icon'>Bootstrap</NavLink> to
										see the icon names and previews.
									</div>
								</CommonHowToUse>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='primary'
											icon='VerifiedUser'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='VerifiedUser'
														iconColor='primary'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
													{
														autoClose: false,
													},
												)
											}>
											Add Toast aaaaaa
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='primary'
											icon='Info'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Info'
														iconColor='primary'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='primary'
											icon='Warning'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Warning'
														iconColor='primary'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='primary'
											icon='Bolt'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Bolt'
														iconColor='primary'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Palette' iconColor='info'>
									<CardTitle>iconColor</CardTitle>
									<CardSubTitle>Toasts</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=icon:Info;iconColor:info' />
								</CardActions>
							</CardHeader>
							<CardHeader>
								<CommonHowToUse>
									iconColor: PropTypes.oneOf([ 'primary', 'secondary', 'success',
									'info', 'warning', 'danger', 'light', 'dark', ]),
								</CommonHowToUse>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='success'
											icon='VerifiedUser'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='VerifiedUser'
														iconColor='success'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='info'
											icon='Info'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Info'
														iconColor='info'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='warning'
											icon='Warning'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Warning'
														iconColor='warning'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='danger'
											icon='Bolt'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Bolt'
														iconColor='danger'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Add Toast
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>

					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Title' iconColor='secondary'>
									<CardTitle>title</CardTitle>
									<CardSubTitle>Toasts</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=title:Title+is+here' />
								</CardActions>
							</CardHeader>
							<CardHeader>
								<CommonHowToUse>title: PropTypes.node.isRequired,</CommonHowToUse>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='secondary'
											icon='BorderColor'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='VerifiedUser'
														iconColor='primary'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											String title
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='secondary'
											icon='Code'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title={
															<>
																Toasts{' '}
																<u>
																	<b>notifications</b>
																</u>
															</>
														}
														icon='Info'
														iconColor='primary'
														time='Now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Node title
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Timer' iconColor='success'>
									<CardTitle>time</CardTitle>
									<CardSubTitle>Toasts</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=time:Time+is+here' />
								</CardActions>
							</CardHeader>
							<CardHeader>
								<CommonHowToUse>time: PropTypes.string,</CommonHowToUse>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='success'
											icon='Update'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Update'
														iconColor='success'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											Just now
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='success'
											icon='AlarmOn'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='AlarmOn'
														iconColor='success'
														time='11 minutes ago'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											11 minutes ago
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='success'
											icon='TimerOff'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='TimerOff'
														iconColor='success'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											None
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>

					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Cancel' iconColor='danger'>
									<CardTitle>closeButton</CardTitle>
									<CardSubTitle>options</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=isDismiss:true' />
								</CardActions>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='danger'
											icon='Cancel'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Cancel'
														iconColor='danger'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
													{ closeButton: false },
												)
											}>
											No close button
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='success'
											icon='Cancel'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Cancel'
														iconColor='success'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											With close button
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>
					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Flaky' iconColor='warning'>
									<CardTitle>autoClose</CardTitle>
									<CardSubTitle>options</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=autoDismiss:true' />
								</CardActions>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='danger'
											icon='Cancel'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Cancel'
														iconColor='danger'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
													{ autoClose: false },
												)
											}>
											autoDismiss value is false
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='success'
											icon='Cancel'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Cancel'
														iconColor='success'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
												)
											}>
											autoDismiss value is true
										</Button>
									</div>
								</div>
							</CardBody>
						</Card>
					</div>

					<div className='col-lg-6'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Timer' iconColor='warning'>
									<CardTitle>autoClose</CardTitle>
									<CardSubTitle>options</CardSubTitle>
								</CardLabel>
								<CardActions>
									<CommonStoryBtn to='/story/components-toasts--default&args=autoDismissTimeout:3000' />
								</CardActions>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									<div className='col-auto'>
										<Button
											color='info'
											icon='Bolt'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Bolt'
														iconColor='info'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
													{ autoClose: 1000 },
												)
											}>
											autoDismissTimeout value is 1000
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='success'
											icon='Cancel'
											isLight
											onClick={() =>
												toast(
													<Toasts
														title='Toasts notifications'
														icon='Cancel'
														iconColor='success'
														time='Just now'>
														Hello, world! This is a toast message.
													</Toasts>,
													{ autoClose: 5000 },
												)
											}>
											autoDismiss value is null (5000)
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

export default ToastsPage;
