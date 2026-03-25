import React from 'react';
import { RouteProps } from 'react-router-dom';
import { demoPagesMenu, pageLayoutTypesPagesMenu } from '../menu';
import AppAside from '../pages/_layout/_asides/AppAside';

const asides: RouteProps[] = [
	{ path: demoPagesMenu.login.path, element: null },
	{ path: demoPagesMenu.signUp.path, element: null },
	{ path: pageLayoutTypesPagesMenu.blank.path, element: null },
	{ path: '*', element: <AppAside /> },
];

export default asides;
