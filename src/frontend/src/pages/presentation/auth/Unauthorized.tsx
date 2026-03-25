import React from 'react';
import { useNavigate } from 'react-router-dom';
import classNames from 'classnames';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import Button from '../../../components/bootstrap/Button';

const Unauthorized = () => {
	const navigate = useNavigate();

	const handleGoBack = () => {
		navigate('/dashboard');
	};

	return (
		<PageWrapper title='Unauthorized Access'>
			<Page className='p-0'>
				<div className='container-fluid'>
					<div
						className={classNames(
							'row',
							'd-flex',
							'align-items-center',
							'justify-content-center',
						)}
						style={{ minHeight: '80vh' }}>
						<div className='col-12 col-md-8 col-lg-6'>
							<div
								className={classNames(
									'rounded-3',
									'd-flex',
									'flex-column',
									'flex-md-row',
									'align-items-center',
									'justify-content-center',
									'mb-4',
								)}>
								<div className='bg-danger rounded-circle p-3 me-3 mb-3 mb-md-0'>
									<h1 className='display-1 text-white mb-0'>
										<i className='fas fa-ban' />
									</h1>
								</div>
								<div>
									<h2 className='mb-2'>Unauthorized Access</h2>
									<h3 className='text-muted'>
										You don't have permission to access this resource.
									</h3>
								</div>
							</div>
							<div className='d-flex flex-column'>
								<Button
									color='info'
									size='lg'
									className='mb-3'
									onClick={handleGoBack}>
									Go to Dashboard
								</Button>
							</div>
						</div>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default Unauthorized;
