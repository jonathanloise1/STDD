import '../src/styles/styles.scss';
import 'bootstrap/dist/js/bootstrap.bundle';
import { BrowserRouter as Router } from 'react-router-dom';
import React from 'react';
import { ThemeContextProvider } from '../src/contexts/themeContext';

export const parameters = {
	actions: { argTypesRegex: '^on[A-Z].*' },
	controls: {
		expanded: true,
		matchers: {
			color: /(background|color)$/i,
			date: /Date$/,
		},
	},
};

export const decorators = [
	(Story) => (
		<ThemeContextProvider>
			<Router>
				<Story />
			</Router>
		</ThemeContextProvider>
	),
];
