import React from 'react';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import Card, {
	CardBody,
	CardCodeView,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../components/bootstrap/Card';
import SubHeader, { SubHeaderLeft } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import showNotification from '../../../components/extras/showNotification';
import Button from '../../../components/bootstrap/Button';
import Icon from '../../../components/icon/Icon';
import CommonDesc from '../../../common/other/CommonDesc';
import { AlertLink } from '../../../components/bootstrap/Alert';
import { componentPagesMenu } from '../../../menu';

const NotificationPage = () => {
	const GENERAL_USAGE = `
() => {
	showNotification(
		'title', // String, HTML or Component
		'message', // String, HTML or Component
		'type' // 'default' || 'info' || 'warning' || 'success' || 'danger',
	);
}`;

	const TITLE = (
		<span className='d-flex align-items-center'>
			<Icon icon='Info' size='lg' className='me-1' />
			<span>This is a Title</span>
		</span>
	);
	const MESSAGE = (
		<div>
			<p>
				Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed elementum dapibus orci,
				non consectetur sem aliquam nec.
			</p>
		</div>
	);

	return (
		<PageWrapper title={componentPagesMenu.notification.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb list={[{ title: 'Notification', to: '/notifications' }]} />
				</SubHeaderLeft>
			</SubHeader>
			<Page>
				<div className='row'>
					<div className='col-12'>
						<Card>
							<CardHeader>
								<CardLabel icon='Assignment'>
									<CardTitle>General Usage</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<CardCodeView className='mb-4'>{GENERAL_USAGE}</CardCodeView>
								<CommonDesc>
									We use <strong>react-toastify</strong> for
									notifications with theme compatibility. If you
									want to make changes, path{' '}
									<code>src/components/extras/showNotification</code>. For more
									information,{' '}
									<AlertLink
										href='https://fkhadra.github.io/react-toastify/introduction/'
										target='blank'
										rel='noreferrer'>
										react-toastify Documentation
									</AlertLink>
									.
								</CommonDesc>
							</CardBody>
						</Card>
					</div>

					<div className='col-xl-6'>
						<Card>
							<CardHeader>
								<CardLabel icon='FormatSize'>
									<CardTitle>'message'</CardTitle>
									<CardSubTitle>showNotification</CardSubTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<Button
									color='primary'
									isLight
									icon='Notifications'
									onClick={() => {
										showNotification('', MESSAGE);
									}}>
									Click
								</Button>
							</CardBody>
						</Card>
					</div>
					<div className='col-xl-6'>
						<Card>
							<CardHeader>
								<CardLabel icon='AnchorCenterDown'>
									<CardTitle>'title'</CardTitle>
									<CardSubTitle>showNotification</CardSubTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<Button
									color='primary'
									isLight
									icon='Notifications'
									onClick={() => {
										showNotification(TITLE, MESSAGE);
									}}>
									Click
								</Button>
							</CardBody>
						</Card>
					</div>

					<div className='col-xl-6'>
						<Card>
							<CardHeader>
								<CardLabel icon='Color'>
									<CardTitle>'type'</CardTitle>
									<CardSubTitle>showNotification</CardSubTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<div className='row g-3'>
									<div className='col-auto'>
										<Button
											color='dark'
											isLight
											icon='Notifications'
											onClick={() => {
												showNotification(TITLE, MESSAGE);
											}}>
											Default
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='info'
											isLight
											icon='Notifications'
											onClick={() => {
												showNotification(TITLE, MESSAGE, 'info');
											}}>
											Info
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='success'
											isLight
											icon='Notifications'
											onClick={() => {
												showNotification(TITLE, MESSAGE, 'success');
											}}>
											Success
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											color='warning'
											isLight
											icon='Notifications'
											onClick={() => {
												showNotification(TITLE, MESSAGE, 'warning');
											}}>
											Warning
										</Button>
									</div>

									<div className='col-auto'>
										<Button
											color='danger'
											isLight
											icon='Notifications'
											onClick={() => {
												showNotification(TITLE, MESSAGE, 'danger');
											}}>
											Danger
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

export default NotificationPage;
