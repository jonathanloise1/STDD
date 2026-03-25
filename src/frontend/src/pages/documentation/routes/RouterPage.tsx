import React, { useState } from 'react';
import classNames from 'classnames';
import Card, {
	CardActions,
	CardBody,
	CardCodeView,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../components/bootstrap/Card';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import { componentPagesMenu, gettingStartedPagesMenu } from '../../../menu';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import Page from '../../../layout/Page/Page';
import Icon from '../../../components/icon/Icon';
import Button from '../../../components/bootstrap/Button';
import FormGroup from '../../../components/bootstrap/forms/FormGroup';
import Label from '../../../components/bootstrap/forms/Label';
import Checks, { ChecksGroup } from '../../../components/bootstrap/forms/Checks';
import Visible from '../../../components/extras/Visible';
import Accordion, { AccordionItem } from '../../../components/bootstrap/Accordion';
import Alert, { AlertHeading } from '../../../components/bootstrap/Alert';
import CommonUnderConstruction from '../getting-started/_common/CommonUnderConstruction';

type TStarterItems = 'Create React App' | 'Vite' | 'NextJs';
type TStarter = { [key: string]: TStarterItems };

const RouterPage = () => {
	const STARTER_PACKAGES: TStarter = {
		cra: 'Create React App',
		vite: 'Vite',
		nextjs: 'NextJs',
	};
	const [starterValue, setStarterValue] = useState<TStarterItems>(
		(localStorage.getItem('MyApp_docChooseStarter') as TStarterItems) || STARTER_PACKAGES.cra,
	);

	const [viewAsideStatus, setViewAsideStatus] = useState<boolean>(false);
	const [viewHeaderStatus, setViewHeaderStatus] = useState<boolean>(false);
	const [viewContentStatus, setViewContentStatus] = useState<boolean>(true);
	const [viewFooterStatus, setViewFooterStatus] = useState<boolean>(false);

	const DECLARE_TYPE = `
export interface PathRouteProps {
    caseSensitive?: NonIndexRouteObject["caseSensitive"];
    path?: NonIndexRouteObject["path"];
    id?: NonIndexRouteObject["id"];
    loader?: NonIndexRouteObject["loader"];
    action?: NonIndexRouteObject["action"];
    hasErrorBoundary?: NonIndexRouteObject["hasErrorBoundary"];
    shouldRevalidate?: NonIndexRouteObject["shouldRevalidate"];
    handle?: NonIndexRouteObject["handle"];
    index?: false;
    children?: React.ReactNode;
    element?: React.ReactNode | null;
    errorElement?: React.ReactNode | null;
}
export interface LayoutRouteProps extends PathRouteProps {}
export interface IndexRouteProps {
    caseSensitive?: IndexRouteObject["caseSensitive"];
    path?: IndexRouteObject["path"];
    id?: IndexRouteObject["id"];
    loader?: IndexRouteObject["loader"];
    action?: IndexRouteObject["action"];
    hasErrorBoundary?: IndexRouteObject["hasErrorBoundary"];
    shouldRevalidate?: IndexRouteObject["shouldRevalidate"];
    handle?: IndexRouteObject["handle"];
    index: true;
    children?: undefined;
    element?: React.ReactNode | null;
    errorElement?: React.ReactNode | null;
}

export declare type RouteProps = PathRouteProps | LayoutRouteProps | IndexRouteProps;
`;

	const ASIDE = {
		cra: `
import React from 'react';
import { RouteProps } from 'react-router-dom';
import DefaultAside from '../pages/_layout/_asides/DefaultAside';
import ExamplePageAside from '../pages/_layout/_asides/ExamplePageAside';

const asides: RouteProps[] = [
\t{ path: 'login', element: null },
\t{ path: 'example-page', element: <ExamplePageAside /> },
\t{ path: '*', element: <DefaultAside /> },
];

export default asides;
`,
		vite: `
import React from 'react';
import { RouteProps } from 'react-router-dom';
import DefaultAside from '../pages/_layout/_asides/DefaultAside';
import ExamplePageAside from '../pages/_layout/_asides/ExamplePageAside';

const asides: RouteProps[] = [
\t{ path: 'login', element: null },
\t{ path: 'example-page', element: <ExamplePageAside /> },
\t{ path: '*', element: <DefaultAside /> },
];

export default asides;
`,
		nextjs: ``,
	};
	const EXAMPLE_ASIDE = {
		cra: `
import React, { useContext } from 'react';
import Brand from '../../../layout/Brand/Brand';
import Navigation from '../../../layout/Navigation/Navigation';
import User from '../../../layout/User/User';
import { exampleMenu } from '../../../menu';
import ThemeContext from '../../../contexts/themeContext';
import Aside, { AsideHead, AsideBody, AsideFoot } from '../../../layout/Aside/Aside';

const ExamplePageAside = () => {
\tconst { asideStatus, setAsideStatus } = useContext(ThemeContext);

\treturn (
\t\t<Aside>
\t\t\t<AsideHead>
\t\t\t\t<Brand asideStatus={asideStatus} setAsideStatus={setAsideStatus} />
\t\t\t</AsideHead>
\t\t\t<AsideBody>
\t\t\t\t<Navigation menu={exampleMenu} id='example-menu' />
\t\t\t</AsideBody>
\t\t\t<AsideFoot>
\t\t\t\t<User />
\t\t\t</AsideFoot>
\t\t</Aside>
\t);
};

export default ExamplePageAside;
`,
		vite: `
import React, { useContext } from 'react';
import Brand from '../../../layout/Brand/Brand';
import Navigation from '../../../layout/Navigation/Navigation';
import User from '../../../layout/User/User';
import { exampleMenu } from '../../../menu';
import ThemeContext from '../../../contexts/themeContext';
import Aside, { AsideHead, AsideBody, AsideFoot } from '../../../layout/Aside/Aside';

const ExamplePageAside = () => {
\tconst { asideStatus, setAsideStatus } = useContext(ThemeContext);

\treturn (
\t\t<Aside>
\t\t\t<AsideHead>
\t\t\t\t<Brand asideStatus={asideStatus} setAsideStatus={setAsideStatus} />
\t\t\t</AsideHead>
\t\t\t<AsideBody>
\t\t\t\t<Navigation menu={exampleMenu} id='example-menu' />
\t\t\t</AsideBody>
\t\t\t<AsideFoot>
\t\t\t\t<User />
\t\t\t</AsideFoot>
\t\t</Aside>
\t);
};

export default ExamplePageAside;
`,
		nextjs: ``,
	};

	const HEADER = {
		cra: `
import React from 'react';
import { RouteProps } from 'react-router-dom';
import DefaultHeader from '../pages/_layout/_headers/DefaultHeader';
import ExamplePageHeader from '../pages/_layout/_headers/ExamplePageAside';

const headers: RouteProps[] = [
\t{ path: 'login', element: null },
\t{ path: 'example-page', element: <ExamplePageHeader /> },
\t{ path: '*', element: <DefaultHeader /> },
];

export default headers;
`,
		vite: `
import React from 'react';
import { RouteProps } from 'react-router-dom';
import DefaultHeader from '../pages/_layout/_headers/DefaultHeader';
import ExamplePageHeader from '../pages/_layout/_headers/ExamplePageAside';

const headers: RouteProps[] = [
\t{ path: 'login', element: null },
\t{ path: 'example-page', element: <ExamplePageHeader /> },
\t{ path: '*', element: <DefaultHeader /> },
];

export default headers;
`,
		nextjs: ``,
	};
	const EXAMPLE_HEADER = {
		cra: `
import React  from 'react';
import Header, { HeaderLeft, HeaderRight } from '../../../layout/Header/Header';
import Navigation from '../../../layout/Navigation/Navigation';
import { exampleMenu } from '../../../menu';

const ExamplePageHeader = () => {
\treturn (
\t\t<Header>
\t\t\t<HeaderLeft>
\t\t\t\t<Navigation menu={exampleMenu} id='example-menu' horizontal/>
\t\t\t</HeaderLeft>
\t\t\t<HeaderRight>
\t\t\t\tHeader Right Content
\t\t\t</HeaderRight>
\t\t</Header>
\t);
};

export default ExamplePageHeader;
`,
		vite: `
import React  from 'react';
import Header, { HeaderLeft, HeaderRight } from '../../../layout/Header/Header';
import Navigation from '../../../layout/Navigation/Navigation';
import { exampleMenu } from '../../../menu';

const ExamplePageHeader = () => {
\treturn (
\t\t<Header>
\t\t\t<HeaderLeft>
\t\t\t\t<Navigation menu={exampleMenu} id='example-menu' horizontal/>
\t\t\t</HeaderLeft>
\t\t\t<HeaderRight>
\t\t\t\tHeader Right Content
\t\t\t</HeaderRight>
\t\t</Header>
\t);
};

export default ExamplePageHeader;
`,
		nextjs: ``,
	};

	const CONTENT = {
		cra: `
import React, { lazy } from 'react';
import { RouteProps } from 'react-router-dom';
import HomePage from '../pages/HomePage';
// import Login from '../pages/presentation/auth/Login';

const ExamplePage: lazy(() => import('../pages/ExamplePage'));

const contents: RouteProps[] = [
\t{ path: '/', element: <HomePage /> },
// \t{ path: 'login', element: <Login /> },
\t{ path: 'example-page', element: <ExamplePage /> },
];

export default contents;
`,
		vite: `
import React, { lazy } from 'react';
import { RouteProps } from 'react-router-dom';
import HomePage from '../pages/HomePage';
// import Login from '../pages/presentation/auth/Login';

const ExamplePage: lazy(() => import('../pages/ExamplePage'));

const contents: RouteProps[] = [
\t{ path: '/', element: <HomePage /> },
\t{ path: 'login', element: <Login /> },
\t{ path: 'example-page', element: <ExamplePage /> },
];

export default contents;
`,
		nextjs: ``,
	};
	const EXAMPLE_CONTENT = {
		cra: `
import React from 'react';
import PageWrapper from '../layout/PageWrapper/PageWrapper';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../layout/SubHeader/SubHeader';
import Page from '../layout/Page/Page';

const ExamplePage = () => {
\treturn (
\t\t<PageWrapper title='Example Page' isProtected>
\t\t\t<SubHeader>
\t\t\t\t<SubHeaderLeft>Sub Header Left</SubHeaderLeft>
\t\t\t\t<SubHeaderRight>Sub Header Right</SubHeaderRight>
\t\t\t</SubHeader>
\t\t\t<Page>Page content</Page>
\t\t</PageWrapper>
\t);
};

export default ExamplePage;

`,
		vite: `
import React from 'react';
import PageWrapper from '../layout/PageWrapper/PageWrapper';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../layout/SubHeader/SubHeader';
import Page from '../layout/Page/Page';

const ExamplePage = () => {
\treturn (
\t\t<PageWrapper title='Example Page' isProtected>
\t\t\t<SubHeader>
\t\t\t\t<SubHeaderLeft>Sub Header Left</SubHeaderLeft>
\t\t\t\t<SubHeaderRight>Sub Header Right</SubHeaderRight>
\t\t\t</SubHeader>
\t\t\t<Page>Page content</Page>
\t\t</PageWrapper>
\t);
};

export default ExamplePage;

`,
		nextjs: ``,
	};

	const FOOTER = {
		cra: `
import React from 'react';
import { RouteProps } from 'react-router-dom';
import DefaultFooter from '../pages/_layout/_footers/DefaultFooter';
import ExamplePageFooter from '../pages/_layout/_footers/ExamplePageFooter';

const footers: RouteProps[] = [
\t{ path: 'login', element: null },
\t{ path: 'example-page', element: <ExamplePageFooter /> },
\t{ path: '*', element: <DefaultFooter /> },
];

export default footers;
`,
		vite: `
import React from 'react';
import { RouteProps } from 'react-router-dom';
import DefaultFooter from '../pages/_layout/_footers/DefaultFooter';
import ExamplePageFooter from '../pages/_layout/_footers/ExamplePageFooter';

const footers: RouteProps[] = [
\t{ path: 'login', element: null },
\t{ path: 'example-page', element: <ExamplePageFooter /> },
\t{ path: '*', element: <DefaultFooter /> },
];

export default footers;
`,
		nextjs: ``,
	};
	const EXAMPLE_FOOTER = {
		cra: `
import React from 'react';
import Footer from '../../../layout/Footer/Footer';

const DefaultFooter = () => {
\treturn (
\t\t<Footer>
\t\t\t<div className='container-fluid'>
\t\t\t\t<div className='row'>
\t\t\t\t\t<div className='col'>
\t\t\t\t\t\tFooter Left
\t\t\t\t\t</div>
\t\t\t\t\t<div className='col-auto'>
\t\t\t\t\t\tFooter Right
\t\t\t\t\t</div>
\t\t\t\t</div>
\t\t\t</div>
\t\t</Footer>
\t);
};

export default DefaultFooter;

`,
		vite: `
import React from 'react';
import Footer from '../../../layout/Footer/Footer';

const DefaultFooter = () => {
\treturn (
\t\t<Footer>
\t\t\t<div className='container-fluid'>
\t\t\t\t<div className='row'>
\t\t\t\t\t<div className='col'>
\t\t\t\t\t\tFooter Left
\t\t\t\t\t</div>
\t\t\t\t\t<div className='col-auto'>
\t\t\t\t\t\tFooter Right
\t\t\t\t\t</div>
\t\t\t\t</div>
\t\t\t</div>
\t\t</Footer>
\t);
};

export default DefaultFooter;

`,
		nextjs: ``,
	};

	return (
		<PageWrapper title={gettingStartedPagesMenu.routes.subMenu.router.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{
								title: gettingStartedPagesMenu.routes.text,
								to: gettingStartedPagesMenu.routes.path,
							},
							{
								title: gettingStartedPagesMenu.routes.subMenu.router.text,
								to: gettingStartedPagesMenu.routes.subMenu.router.path,
							},
						]}
					/>
				</SubHeaderLeft>
				<SubHeaderRight>
					<Button
						tag='a'
						color='info'
						isLight
						icon={componentPagesMenu.components.icon}
						to={`../${componentPagesMenu.components.path}`}>
						{componentPagesMenu.components.text}
					</Button>
					<Button
						tag='a'
						color='info'
						isLight
						icon={componentPagesMenu.forms.icon}
						to={`../${componentPagesMenu.forms.path}`}>
						{componentPagesMenu.forms.text}
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
									<Label>Choose</Label>
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
						<Visible
							is={[STARTER_PACKAGES.cra, STARTER_PACKAGES.vite].includes(
								starterValue,
							)}>
							<Visible
								is={[STARTER_PACKAGES.cra, STARTER_PACKAGES.vite].includes(
									starterValue,
								)}>
								<Card>
									<CardHeader>
										<CardLabel icon='AltRoute'>
											<CardTitle>Route</CardTitle>
										</CardLabel>
									</CardHeader>
									<CardBody>
										<p>
											Routes are perhaps the most important part of a React
											Router app. They couple URL segments to components, data
											loading and data mutations. Through route nesting,
											complex application layouts and data dependencies become
											simple and declarative.
										</p>
										<p>
											One of the most unique aspects of the project is
											definitely the "Route" structure. You can set Aside,
											Header, Footer and of course Content for any path you
											want, you can even not put them if you wish.
										</p>

										<strong>
											You can use all props of the{' '}
											<a
												href='https://reactrouter.com/en/main/route/route'
												target='_blank'
												rel='noreferrer'>
												React Router Dom
											</a>{' '}
											when defining a route.
										</strong>
										<Accordion id='RoutePropsAccordion' className='mt-3'>
											<AccordionItem
												id='RouteProps'
												title='Type Declaration - RouteProps[]'
												icon='VpnKey'>
												<CardCodeView>{DECLARE_TYPE}</CardCodeView>
											</AccordionItem>
										</Accordion>
									</CardBody>
								</Card>
							</Visible>

							<div className='row mb-4' style={{ height: 400 }}>
								<div className='col-3 h-100'>
									<div className='h-100 d-flex justify-content-center align-items-center border border-3 border-warning rounded rounded-4 fw-bold text-warning'>
										asideRoutes
									</div>
								</div>
								<div className='col-9 h-100'>
									<div className='h-100 d-flex flex-column align-items-stretch'>
										<div className='d-flex justify-content-center align-items-center border border-3 border-info rounded rounded-4 fw-bold text-info p-3'>
											headerRoutes
										</div>
										<div className='h-100 d-flex justify-content-center align-items-center border border-3 border-success rounded rounded-4 fw-bold text-success p-3 my-4'>
											contentRoutes
										</div>
										<div className='d-flex justify-content-center align-items-center border border-3 border-danger rounded rounded-4 fw-bold text-danger p-3'>
											footerRoutes
										</div>
									</div>
								</div>
							</div>

							<Card>
								<CardHeader>
									<CardLabel icon='AccountTree' iconColor='warning'>
										<CardTitle>asideRoutes.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>src/routes/asideRoutes.tsx</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{ASIDE.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{ASIDE.vite}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
										Under Construction
									</Visible>
									<Alert color='warning' isLight icon='Warning' className='mt-3'>
										If you don't want it to be <b>Aside</b> in a path, simply
										give the
										<code className='px-2'>element</code> prop the value{' '}
										<code className='px-2'>null</code>.
									</Alert>
								</CardBody>
							</Card>
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='warning'>
										<CardTitle>ExamplePageAside.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>
												src/pages/_layout/_asides/ExamplePageAside.tsx
											</CardSubTitle>
										</Visible>
									</CardLabel>
									<CardActions>
										<Button
											isOutline
											color={viewAsideStatus ? 'danger' : 'info'}
											icon={viewAsideStatus ? 'VisibilityOff' : 'Visibility'}
											onClick={() => setViewAsideStatus(!viewAsideStatus)}>
											{viewAsideStatus ? 'Hide' : 'Show'}
										</Button>
									</CardActions>
								</CardHeader>
								<CardBody>
									<Visible is={viewAsideStatus}>
										<Visible is={starterValue === STARTER_PACKAGES.cra}>
											<CardCodeView>{EXAMPLE_ASIDE.cra}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.vite}>
											<CardCodeView>{EXAMPLE_ASIDE.vite}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
											Under Construction
										</Visible>
									</Visible>
									<Alert
										className={classNames({ 'mt-3': viewAsideStatus })}
										icon='Info'
										color='info'
										isLight>
										Much more detailed usage is available in demos.
									</Alert>
								</CardBody>
							</Card>

							<Card>
								<CardHeader>
									<CardLabel icon='AccountTree' iconColor='info'>
										<CardTitle>headerRoutes.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>src/routes/headerRoutes.tsx</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{HEADER.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{HEADER.vite}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
										Under Construction
									</Visible>
									<Alert color='warning' isLight icon='Warning' className='mt-3'>
										If you don't want it to be <b>Header</b> in a path, simply
										give the
										<code className='px-2'>element</code> prop the value{' '}
										<code className='px-2'>null</code>.
									</Alert>
								</CardBody>
							</Card>
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='info'>
										<CardTitle>ExamplePageHeader.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>
												src/pages/_layout/_headers/ExamplePageHeader.tsx
											</CardSubTitle>
										</Visible>
									</CardLabel>
									<CardActions>
										<Button
											isOutline
											color={viewHeaderStatus ? 'danger' : 'info'}
											icon={viewHeaderStatus ? 'VisibilityOff' : 'Visibility'}
											onClick={() => setViewHeaderStatus(!viewHeaderStatus)}>
											{viewHeaderStatus ? 'Hide' : 'Show'}
										</Button>
									</CardActions>
								</CardHeader>
								<CardBody>
									<Visible is={viewHeaderStatus}>
										<Visible is={starterValue === STARTER_PACKAGES.cra}>
											<CardCodeView>{EXAMPLE_HEADER.cra}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.vite}>
											<CardCodeView>{EXAMPLE_HEADER.vite}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
											Under Construction
										</Visible>
									</Visible>
									<Alert
										className={classNames({ 'mt-3': viewHeaderStatus })}
										icon='Info'
										color='info'
										isLight>
										Much more detailed usage is available in demos.
									</Alert>
								</CardBody>
							</Card>

							<Card>
								<CardHeader>
									<CardLabel icon='AccountTree' iconColor='success'>
										<CardTitle>contentRoutes.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>
												src/routes/contentRoutes.tsx
											</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{CONTENT.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{CONTENT.vite}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
										Under Construction
									</Visible>
								</CardBody>
							</Card>
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='success'>
										<CardTitle>ExamplePage.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>src/pages/ExamplePage.tsx</CardSubTitle>
										</Visible>
									</CardLabel>
									<CardActions>
										<Button
											isOutline
											color={viewContentStatus ? 'danger' : 'info'}
											icon={
												viewContentStatus ? 'VisibilityOff' : 'Visibility'
											}
											onClick={() =>
												setViewContentStatus(!viewContentStatus)
											}>
											{viewContentStatus ? 'Hide' : 'Show'}
										</Button>
									</CardActions>
								</CardHeader>
								<CardBody>
									<Alert
										icon='Shield'
										color='warning'
										className='flex-nowrap'
										isLight>
										<AlertHeading>Public and Private Page</AlertHeading>
										You can check if a page is private from within the page. By
										default, all pages are private, but if you wish, you can
										give the <code>PageWrapper</code> component the value{' '}
										<code>false</code> to the <code>isProtected</code> prop
										(Example: Login, 404, etc.).
									</Alert>
									<Visible is={viewContentStatus}>
										<Visible is={starterValue === STARTER_PACKAGES.cra}>
											<CardCodeView>{EXAMPLE_CONTENT.cra}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.vite}>
											<CardCodeView>{EXAMPLE_CONTENT.vite}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
											Under Construction
										</Visible>
									</Visible>
									<Alert
										className={classNames({ 'mt-3': viewContentStatus })}
										icon='Info'
										color='info'
										isLight>
										Much more detailed usage is available in demos.
									</Alert>
								</CardBody>
							</Card>

							<Card>
								<CardHeader>
									<CardLabel icon='AccountTree' iconColor='danger'>
										<CardTitle>footerRoutes.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>src/routes/footerRoutes.tsx</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{FOOTER.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{FOOTER.vite}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
										Under Construction
									</Visible>
									<Alert color='warning' isLight icon='Warning' className='mt-3'>
										If you don't want it to be <b>Footer</b> in a path, simply
										give the
										<code className='px-2'>element</code> prop the value{' '}
										<code className='px-2'>null</code>.
									</Alert>
								</CardBody>
							</Card>
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='danger'>
										<CardTitle>ExamplePageFooter.tsx</CardTitle>

										<Visible
											is={[
												STARTER_PACKAGES.cra,
												STARTER_PACKAGES.vite,
											].includes(starterValue)}>
											<CardSubTitle>
												src/pages/_layout/_footers/ExamplePageFooter.tsx
											</CardSubTitle>
										</Visible>
									</CardLabel>
									<CardActions>
										<Button
											isOutline
											color={viewFooterStatus ? 'danger' : 'info'}
											icon={viewFooterStatus ? 'VisibilityOff' : 'Visibility'}
											onClick={() => setViewFooterStatus(!viewFooterStatus)}>
											{viewFooterStatus ? 'Hide' : 'Show'}
										</Button>
									</CardActions>
								</CardHeader>
								<CardBody>
									<Visible is={viewFooterStatus}>
										<Visible is={starterValue === STARTER_PACKAGES.cra}>
											<CardCodeView>{EXAMPLE_FOOTER.cra}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.vite}>
											<CardCodeView>{EXAMPLE_FOOTER.vite}</CardCodeView>
										</Visible>
										<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
											Under Construction
										</Visible>
									</Visible>
									<Alert
										className={classNames({ 'mt-3': viewFooterStatus })}
										icon='Info'
										color='info'
										isLight>
										Much more detailed usage is available in demos.
									</Alert>
								</CardBody>
							</Card>
						</Visible>
						<Visible is={starterValue === STARTER_PACKAGES.nextjs}>
							<Card>
								<CardBody>
									<CommonUnderConstruction />
								</CardBody>
							</Card>
						</Visible>
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
											color='info'
											isLight
											icon={componentPagesMenu.components.icon}
											to={`../${componentPagesMenu.components.path}`}>
											{componentPagesMenu.components.text}
										</Button>
									</div>
									<div className='col-auto'>
										<Button
											tag='a'
											color='info'
											isLight
											icon={componentPagesMenu.forms.icon}
											to={`../${componentPagesMenu.forms.path}`}>
											{componentPagesMenu.forms.text}
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

export default RouterPage;
