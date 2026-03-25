import React from 'react';
// import ReactDOM from 'react-dom'; // For React 17
import { createRoot } from 'react-dom/client'; // For React 18
import { BrowserRouter as Router } from 'react-router-dom';
import './styles/styles.scss';
import App from './App/App';
import reportWebVitals from './reportWebVitals';
import { ThemeContextProvider } from './contexts/themeContext';
import { AuthContextProvider } from './contexts/authContext';
import { OrganizationProvider } from './contexts/organizationContext';
import { PermissionProvider } from './contexts/permissionContext';
// Side-effect import: initialises i18next with language detection,
// namespace loading, and fallback configuration.
// @userstory US-I18N-01 — Bootstraps translation system at application startup
import './i18n';

const children = (
	<Router>
		<ThemeContextProvider>
			<AuthContextProvider>
				<PermissionProvider>
				<OrganizationProvider>
					<React.StrictMode>
						<App />
					</React.StrictMode>
				</OrganizationProvider>
				</PermissionProvider>
			</AuthContextProvider>
		</ThemeContextProvider>
	</Router>
);

const container = document.getElementById('root');

// ReactDOM.render(children, container); // For React 17
createRoot(container as Element).render(children); // For React 18

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
