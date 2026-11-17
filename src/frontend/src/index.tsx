import './ui/theme';
import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter as Router } from 'react-router-dom';
import App from './App/App';
import { AuthContextProvider } from './contexts/authContext';
import { OrganizationProvider } from './contexts/organizationContext';
import { PermissionProvider } from './contexts/permissionContext';
import { HelpProvider } from './contexts/helpContext';
// Side-effect import: initialises i18next with language detection,
// namespace loading, and fallback configuration.
import './i18n';

const children = (
	<Router>
		<AuthContextProvider>
			<PermissionProvider>
				<OrganizationProvider>
					<HelpProvider>
						<React.StrictMode>
							<App />
						</React.StrictMode>
					</HelpProvider>
				</OrganizationProvider>
			</PermissionProvider>
		</AuthContextProvider>
	</Router>
);

const container = document.getElementById('root');

createRoot(container as Element).render(children);
