import React, { useState } from 'react';
import Card, {
	CardBody,
	CardCodeView,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../components/bootstrap/Card';
import PageWrapper from '../../../layout/PageWrapper/PageWrapper';
import { gettingStartedPagesMenu } from '../../../menu';
import SubHeader, { SubHeaderLeft, SubHeaderRight } from '../../../layout/SubHeader/SubHeader';
import Breadcrumb from '../../../components/bootstrap/Breadcrumb';
import Page from '../../../layout/Page/Page';
import Icon from '../../../components/icon/Icon';
import Button from '../../../components/bootstrap/Button';
import FormGroup from '../../../components/bootstrap/forms/FormGroup';
import Label from '../../../components/bootstrap/forms/Label';
import Checks, { ChecksGroup } from '../../../components/bootstrap/forms/Checks';
import Visible from '../../../components/extras/Visible';
import Alert from '../../../components/bootstrap/Alert';
import CommonUnderConstruction from './_common/CommonUnderConstruction';

type TStarterItems = 'Create React App' | 'Vite' | 'NextJs';
type TStarter = { [key: string]: TStarterItems };

const ProjectStructurePage = () => {
	const STARTER_PACKAGES: TStarter = {
		cra: 'Create React App',
		vite: 'Vite',
		nextjs: 'NextJs',
	};
	const [starterValue, setStarterValue] = useState<TStarterItems>(
		(localStorage.getItem('MyApp_docChooseStarter') as TStarterItems) || STARTER_PACKAGES.cra,
	);

	const INDEX = {
		cra: `
import React from 'react';
// import ReactDOM from 'react-dom'; // For React 17
import { createRoot } from 'react-dom/client'; // For React 18
import { BrowserRouter as Router } from 'react-router-dom';
import './styles/styles.scss';
import App from './App/App';
import reportWebVitals from './reportWebVitals';
import { ThemeContextProvider } from './contexts/themeContext';
import { AuthContextProvider } from './contexts/authContext';
import './i18n';

const children = (
\t<AuthContextProvider>
\t\t<ThemeContextProvider>
\t\t\t<Router>
\t\t\t\t<React.StrictMode>
\t\t\t\t\t<App />
\t\t\t\t</React.StrictMode>
\t\t\t</Router>
\t\t</ThemeContextProvider>
\t</AuthContextProvider>
);

const container = document.getElementById('root');

// ReactDOM.render(children, container); // For React 17
createRoot(container as Element).render(children); // For React 18

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
`,
		vite: `
import React from 'react';
// import ReactDOM from 'react-dom'; // For React 17
import { createRoot } from 'react-dom/client'; // For React 18
import { BrowserRouter as Router } from 'react-router-dom';
import './styles/styles.scss';
import App from './App/App';
import reportWebVitals from './reportWebVitals';
import { ThemeContextProvider } from './contexts/themeContext';
import { AuthContextProvider } from './contexts/authContext';
import './i18n';

const children = (
\t<AuthContextProvider>
\t\t<ThemeContextProvider>
\t\t\t<Router>
\t\t\t\t<React.StrictMode>
\t\t\t\t\t<App />
\t\t\t\t</React.StrictMode>
\t\t\t</Router>
\t\t</ThemeContextProvider>
\t</AuthContextProvider>
);

const container = document.getElementById('root');

// ReactDOM.render(children, container); // For React 17
createRoot(container as Element).render(children); // For React 18

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
`,
		nextjs: `
import React from 'react';
// import ReactDOM from 'react-dom'; // For React 17
import { createRoot } from 'react-dom/client'; // For React 18
import { BrowserRouter as Router } from 'react-router-dom';
import './styles/styles.scss';
import App from './App/App';
import reportWebVitals from './reportWebVitals';
import { ThemeContextProvider } from './contexts/themeContext';
import { AuthContextProvider } from './contexts/authContext';
import './i18n';

const children = (
\t<AuthContextProvider>
\t\t<ThemeContextProvider>
\t\t\t<Router>
\t\t\t\t<React.StrictMode>
\t\t\t\t\t<App />
\t\t\t\t</React.StrictMode>
\t\t\t</Router>
\t\t</ThemeContextProvider>
\t</AuthContextProvider>
);

const container = document.getElementById('root');

// ReactDOM.render(children, container); // For React 17
createRoot(container as Element).render(children); // For React 18

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
`,
	};

	const APP = {
		cra: `
/**
 * HERE ARE THE IMPORTS FOR THE APP
 */

const App = () => {
/**
 * HERE ARE THE FUNCTIONS FOR THE APP
 */
\treturn (
\t\t<ThemeProvider {...themeProviderProps}>
\t\t\t<ToastProvider {...toastProviderProps}>
\t\t\t\t<TourProvider {...tourProviderProps}>
\t\t\t\t\t<div className='app' {...appProps}>
\t\t\t\t\t\t<AsideRoutes />
\t\t\t\t\t\t<Wrapper />
\t\t\t\t\t</div>
\t\t\t\t\t<Portal id='portal-notification'>
\t\t\t\t\t\t<ReactNotifications />
\t\t\t\t\t</Portal>
\t\t\t\t</TourProvider>
\t\t\t</ToastProvider>
\t\t</ThemeProvider>
\t);
};

export default App;
`,
		vite: `
/**
 * HERE ARE THE IMPORTS FOR THE APP
 */

const App = () => {
/**
 * HERE ARE THE FUNCTIONS FOR THE APP
 */
\treturn (
\t\t<ThemeProvider {...themeProviderProps}>
\t\t\t<ToastProvider {...toastProviderProps}>
\t\t\t\t<TourProvider {...tourProviderProps}>
\t\t\t\t\t<div className='app' {...appProps}>
\t\t\t\t\t\t<AsideRoutes />
\t\t\t\t\t\t<Wrapper />
\t\t\t\t\t</div>
\t\t\t\t\t<Portal id='portal-notification'>
\t\t\t\t\t\t<ReactNotifications />
\t\t\t\t\t</Portal>
\t\t\t\t</TourProvider>
\t\t\t</ToastProvider>
\t\t</ThemeProvider>
\t);
};

export default App;
`,
		nextjs: `
import React from 'react';
// import ReactDOM from 'react-dom'; // For React 17
import { createRoot } from 'react-dom/client'; // For React 18
import { BrowserRouter as Router } from 'react-router-dom';
import './styles/styles.scss';
import App from './App/App';
import reportWebVitals from './reportWebVitals';
import { ThemeContextProvider } from './contexts/themeContext';
import { AuthContextProvider } from './contexts/authContext';
import './i18n';

const children = (
\t<AuthContextProvider>
\t\t<ThemeContextProvider>
\t\t\t<Router>
\t\t\t\t<React.StrictMode>
\t\t\t\t\t<App />
\t\t\t\t</React.StrictMode>
\t\t\t</Router>
\t\t</ThemeContextProvider>
\t</AuthContextProvider>
);

const container = document.getElementById('root');

// ReactDOM.render(children, container); // For React 17
createRoot(container as Element).render(children); // For React 18

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
`,
	};

	const WRAPPER = {
		cra: `
/**
 * HERE ARE THE IMPORTS FOR THE WRAPPER
 */
 
const Wrapper = () => {
\treturn (
\t\t<>
\t\t\t<WrapperContainer>
\t\t\t\t<HeaderRoutes />
\t\t\t\t<Content />
\t\t\t\t<FooterRoutes />
\t\t\t</WrapperContainer>
\t\t\t<WrapperOverlay />
\t\t</>
\t);
};

export default Wrapper;
`,
		vite: `
/**
 * HERE ARE THE IMPORTS FOR THE WRAPPER
 */
 
const Wrapper = () => {
\treturn (
\t\t<>
\t\t\t<WrapperContainer>
\t\t\t\t<HeaderRoutes />
\t\t\t\t<Content />
\t\t\t\t<FooterRoutes />
\t\t\t</WrapperContainer>
\t\t\t<WrapperOverlay />
\t\t</>
\t);
};

export default Wrapper;
`,
		nextjs: `
import React from 'react';
// import ReactDOM from 'react-dom'; // For React 17
import { createRoot } from 'react-dom/client'; // For React 18
import { BrowserRouter as Router } from 'react-router-dom';
import './styles/styles.scss';
import App from './App/App';
import reportWebVitals from './reportWebVitals';
import { ThemeContextProvider } from './contexts/themeContext';
import { AuthContextProvider } from './contexts/authContext';
import './i18n';

const children = (
\t<AuthContextProvider>
\t\t<ThemeContextProvider>
\t\t\t<Router>
\t\t\t\t<React.StrictMode>
\t\t\t\t\t<App />
\t\t\t\t</React.StrictMode>
\t\t\t</Router>
\t\t</ThemeContextProvider>
\t</AuthContextProvider>
);

const container = document.getElementById('root');

// ReactDOM.render(children, container); // For React 17
createRoot(container as Element).render(children); // For React 18

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
`,
	};

	return (
		<PageWrapper title={gettingStartedPagesMenu.gettingStarted.subMenu.projectStructure.text}>
			<SubHeader>
				<SubHeaderLeft>
					<Breadcrumb
						list={[
							{
								title: gettingStartedPagesMenu.gettingStarted.text,
								to: gettingStartedPagesMenu.gettingStarted.path,
							},
							{
								title: gettingStartedPagesMenu.gettingStarted.subMenu
									.projectStructure.text,
								to: gettingStartedPagesMenu.gettingStarted.subMenu.projectStructure
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
						icon={gettingStartedPagesMenu.routes.subMenu.router.icon}
						to={`../${gettingStartedPagesMenu.routes.subMenu.router.path}`}>
						{gettingStartedPagesMenu.routes.subMenu.router.text}
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
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='success'>
										<CardTitle>index.tsx</CardTitle>

										<Visible
											is={
												starterValue === STARTER_PACKAGES.cra ||
												starterValue === STARTER_PACKAGES.vite
											}>
											<CardSubTitle>src/index.tsx</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{INDEX.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{INDEX.vite}</CardCodeView>
									</Visible>
									<Visible
										is={[STARTER_PACKAGES.cra, STARTER_PACKAGES.vite].includes(
											starterValue,
										)}>
										<Alert
											color='info'
											isLight
											icon='Info'
											className='flex-nowrap mt-4'>
											If you want to use React 17, you can uncomment the lines
											for 17 and then comment the lines for 18.
										</Alert>
									</Visible>
								</CardBody>
							</Card>
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='success'>
										<CardTitle>App.tsx</CardTitle>

										<Visible
											is={
												starterValue === STARTER_PACKAGES.cra ||
												starterValue === STARTER_PACKAGES.vite
											}>
											<CardSubTitle>src/App/App.tsx</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{APP.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{APP.vite}</CardCodeView>
									</Visible>
								</CardBody>
							</Card>
							<Card>
								<CardHeader>
									<CardLabel icon='InsertDriveFile' iconColor='success'>
										<CardTitle>Wrapper.tsx</CardTitle>

										<Visible
											is={
												starterValue === STARTER_PACKAGES.cra ||
												starterValue === STARTER_PACKAGES.vite
											}>
											<CardSubTitle>
												src/layout/Wrapper/Wrapper.tsx
											</CardSubTitle>
										</Visible>
									</CardLabel>
								</CardHeader>
								<CardBody>
									<Visible is={starterValue === STARTER_PACKAGES.cra}>
										<CardCodeView>{WRAPPER.cra}</CardCodeView>
									</Visible>
									<Visible is={starterValue === STARTER_PACKAGES.vite}>
										<CardCodeView>{WRAPPER.vite}</CardCodeView>
									</Visible>
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
											color='success'
											isLight
											icon='NavigateNext'
											to={`../${gettingStartedPagesMenu.routes.subMenu.router.path}`}>
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

export default ProjectStructurePage;
