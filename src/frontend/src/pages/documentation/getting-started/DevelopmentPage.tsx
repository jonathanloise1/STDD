import React, { useState } from 'react';
import Card, {
	CardBody,
	CardCodeView,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
	ICardProps,
} from '../../../components/bootstrap/Card';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import { gettingStartedPagesMenu } from '../../../menu';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import Page from '../../../layout/Page/Page';
import Icon from '../../../components/icon/Icon';
import CommonCodeCopy from './_common/CommonCodeCopy';
import Button from '../../../components/bootstrap/Button';
import FormGroup from '../../../components/bootstrap/forms/FormGroup';
import Label from '../../../components/bootstrap/forms/Label';
import Checks, { ChecksGroup } from '../../../components/bootstrap/forms/Checks';
import Alert from '../../../components/bootstrap/Alert';
import Visible from '../../../components/extras/Visible';

type TStarterItems = 'Create React App' | 'Vite' | 'NextJs';
type TStarter = { [key: string]: TStarterItems };

const DevelopmentPage = () => {
	const cardProps: Partial<ICardProps> = {
		shadow: 'none',
		borderSize: 1,
		className: 'mb-0 rounded-1',
	};

	const STARTER_PACKAGES: TStarter = {
		cra: 'Create React App',
		vite: 'Vite',
		nextjs: 'NextJs',
	};
	const [starterValue, setStarterValue] = useState<TStarterItems>(
		(localStorage.getItem('MyApp_docChooseStarter') as TStarterItems) || STARTER_PACKAGES.cra,
	);

	const SCRIPTS = {
		cra: `
"scripts": {
    "start": "react-scripts start",
    "build": "react-scripts build",
    "serve": "serve -s build",
    "test": "react-scripts test",
    "eject": "react-scripts eject",
    "lint": "eslint .",
    "lint:fix": "eslint --fix --ext .js --ext .ts --ext .jsx --ext .tsx ./src",
    "lint:check-scss": "stylelint-config-prettier-scss-check",
    "lint:scss": "stylelint **/*.scss",
    "lint:fix-scss": "stylelint --fix **/*.scss",
    "icon": "svgr SvgIcons -d src/components/icon/svg-icons --typescript",
    "storybook": "start-storybook -p 6006",
    "storybook-withoutCache": "start-storybook -p 6006 --no-manager-cache",
    "build-storybook": "build-storybook"
},`,
		vite: `
"scripts": {
    "dev": "vite",
    "build": "vite build",
    "preview": "vite preview",
    "lint": "eslint .",
    "lint:fix": "eslint --fix --ext .js --ext .ts --ext .jsx --ext .tsx ./src",
    "lint:check-scss": "stylelint-config-prettier-scss-check",
    "lint:scss": "stylelint **/*.scss",
    "lint:fix-scss": "stylelint --fix **/*.scss"
},`,
		nextjs: `
"scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "lint": "next lint",
    "lint:check-scss": "stylelint-config-prettier-scss-check",
    "lint:scss": "stylelint **/*.scss",
    "lint:fix-scss": "stylelint --fix **/*.scss"
},`,
	};

	return (
		<PageWrapper title={gettingStartedPagesMenu.gettingStarted.subMenu.dev.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{
								title: gettingStartedPagesMenu.gettingStarted.text,
								to: gettingStartedPagesMenu.gettingStarted.path,
							},
							{
								title: gettingStartedPagesMenu.gettingStarted.subMenu.dev.text,
								to: gettingStartedPagesMenu.gettingStarted.subMenu.dev.path,
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
						icon={gettingStartedPagesMenu.gettingStarted.subMenu.folderStructure.icon}
						to={`../${gettingStartedPagesMenu.gettingStarted.subMenu.folderStructure.path}`}>
						{gettingStartedPagesMenu.gettingStarted.subMenu.folderStructure.text}
					</Button>
				</SubHeaderRight>
			</SubHeader>
			<Page>
				<div className='row h-100 align-content-start'>
					<div className='col-md-3'>
						<Card className='position-sticky sticky-top-size'>
							<CardHeader>
								<CardLabel icon='FollowTheSigns' iconColor='success'>
									<CardTitle>Choose your preference</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<p>
									3 different options are offered according to the needs of your
									project.
								</p>
								<FormGroup>
									<Label htmlFor='chooseStarter'>Choose</Label>
									<ChecksGroup>
										{Object.keys(STARTER_PACKAGES).map((item) => (
											<Checks
												key={item}
												type='radio'
												name='chooseStarter'
												id={STARTER_PACKAGES[item]}
												label={STARTER_PACKAGES[item]}
												value={STARTER_PACKAGES[item]}
												onChange={(
													event: React.ChangeEvent<HTMLInputElement>,
												): void => {
													localStorage.setItem(
														'MyApp_docChooseStarter',
														event.target.value,
													);
													setStarterValue(
														event.target.value as TStarterItems,
													);
												}}
												checked={starterValue}
											/>
										))}
									</ChecksGroup>
								</FormGroup>
							</CardBody>
						</Card>
					</div>
					<div className='col-md-9'>
						<Card>
							<CardHeader>
								<CardLabel icon='Flag' iconColor='info'>
									<CardTitle>Development Server</CardTitle>
									<CardSubTitle>Step 3</CardSubTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<p>
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
												<Visible is={starterValue === STARTER_PACKAGES.cra}>
													<CommonCodeCopy>npm start</CommonCodeCopy>
												</Visible>
												<Visible
													is={starterValue === STARTER_PACKAGES.vite}>
													<CommonCodeCopy>npm run dev</CommonCodeCopy>
												</Visible>
												<Visible
													is={starterValue === STARTER_PACKAGES.nextjs}>
													<CommonCodeCopy>npm run dev</CommonCodeCopy>
												</Visible>
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
												<Visible is={starterValue === STARTER_PACKAGES.cra}>
													<CommonCodeCopy>yarn start</CommonCodeCopy>
												</Visible>
												<Visible
													is={starterValue === STARTER_PACKAGES.vite}>
													<CommonCodeCopy>yarn dev</CommonCodeCopy>
												</Visible>
												<Visible
													is={starterValue === STARTER_PACKAGES.nextjs}>
													<CommonCodeCopy>yarn dev</CommonCodeCopy>
												</Visible>
											</CardBody>
										</Card>
									</div>
								</div>
							</CardBody>
						</Card>
						<Card>
							<CardHeader>
								<CardLabel icon='Code' iconColor='success'>
									<CardTitle>Other {starterValue} Scripts</CardTitle>
								</CardLabel>
							</CardHeader>
							<CardBody>
								<Alert color='info' isLight icon='Info'>
									You can run the following codes as
									<code className='mx-2'>npm run SCRIPTS_KEY</code> or
									<code className='mx-2'>yarn SCRIPTS_KEY</code>.
								</Alert>
								<Visible is={starterValue === STARTER_PACKAGES.cra}>
									<CardCodeView>{SCRIPTS.cra}</CardCodeView>
								</Visible>
								<Visible is={starterValue === STARTER_PACKAGES.vite}>
									<CardCodeView>{SCRIPTS.vite}</CardCodeView>
								</Visible>
								<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
									<CardCodeView>{SCRIPTS.nextjs}</CardCodeView>
								</Visible>
								<Alert color='success' isLight icon='Verified' className='mt-3'>
									Example:
									<code className='mx-2'>npm run storybook</code> or
									<code className='mx-2'>yarn storybook</code>.
								</Alert>
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
											to={`../${gettingStartedPagesMenu.gettingStarted.subMenu.folderStructure.path}`}>
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

export default DevelopmentPage;
