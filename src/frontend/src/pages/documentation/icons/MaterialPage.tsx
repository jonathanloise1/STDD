import React from 'react';
import { useFormik } from 'formik';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import Page from '../../../layout/Page/Page';
import SubHeader, { SubHeaderLeft } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../components/bootstrap/Card';
import Input from '../../../components/bootstrap/forms/Input';
import MaterialIcons from '../../../components/icon/material-icons/iconList';
import CommonIconPreview from '../../../common/other/CommonIconPreview';
import { componentPagesMenu } from '../../../menu';

const MaterialPage = () => {
	const formik = useFormik({
		initialValues: {
			filterIcon: '',
		},
		onSubmit: () => {},
	});
	const FILTERED_ICONS = MaterialIcons.filter((name) =>
		name.toLowerCase().includes(formik.values.filterIcon.toLowerCase()),
	);
	return (
		<PageWrapper title={componentPagesMenu.icons.subMenu.material.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{ title: 'Icons', to: '/icons' },
							{ title: 'Material', to: '/icons/material' },
						]}
					/>
				</SubHeaderLeft>
			</SubHeader>
			<Page>
				<div className='row'>
					<div className='col-12'>
						<Card stretch>
							<CardHeader>
								<CardLabel icon='Lightbulb'>
									<CardTitle>
										icon{' '}
										{MaterialIcons.length > FILTERED_ICONS.length && (
											<small>
												including "{formik.values.filterIcon.toLowerCase()}"
												:{FILTERED_ICONS.length}
											</small>
										)}
									</CardTitle>
									<CardSubTitle>Icon</CardSubTitle>
								</CardLabel>
								<CardActions>
									<Input
										name='filterIcon'
										placeholder='Filter icon...'
										aria-label='.form-control-lg example'
										onChange={formik.handleChange}
										value={formik.values.filterIcon}
									/>
								</CardActions>
							</CardHeader>
							<CardBody>
								<div className='row g-4'>
									{FILTERED_ICONS.map((icon) => (
										<CommonIconPreview
											key={icon}
											icon={icon}
											forceFamily='material'
										/>
									))}
								</div>
							</CardBody>
						</Card>
					</div>
				</div>
			</Page>
		</PageWrapper>
	);
};

export default MaterialPage;
