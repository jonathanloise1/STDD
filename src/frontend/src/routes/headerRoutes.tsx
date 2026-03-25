import React from 'react';
import { RouteProps } from 'react-router-dom';

/** @userstory US-MENU-01, US-MENU-04 */
import {
	companyPagesMenu,
	componentPagesMenu,
	dashboardPagesMenu,
	demoPagesMenu,
	gettingStartedPagesMenu,
	tasksMenu,
	pageLayoutTypesPagesMenu,
} from '../menu';

import DashboardHeader from '../pages/_layout/_headers/DashboardHeader';
import DashboardBookingHeader from '../pages/_layout/_headers/DashboardBookingHeader';
import ProfilePageHeader from '../pages/_layout/_headers/ProfilePageHeader';
import SummaryHeader from '../pages/_layout/_headers/SummaryHeader';
import ProductsHeader from '../pages/_layout/_headers/ProductsHeader';
import ProductListHeader from '../pages/_layout/_headers/ProductListHeader';
import PageLayoutHeader from '../pages/_layout/_headers/PageLayoutHeader';
import ComponentsHeader from '../pages/_layout/_headers/ComponentsHeader';
import FormHeader from '../pages/_layout/_headers/FormHeader';
import ChartsHeader from '../pages/_layout/_headers/ChartsHeader';
import ContentHeader from '../pages/_layout/_headers/ContentHeader';
import UtilitiesHeader from '../pages/_layout/_headers/UtilitiesHeader';
import IconHeader from '../pages/_layout/_headers/IconHeader';
// import DefaultHeader from '../pages/_layout/_headers/DefaultHeader';
import DocumentationHeader from '../pages/_layout/_headers/DocumentationHeader';
import SimpleHeader from '../pages/_layout/_headers/SimpleHeader';
import AppHeader from '../pages/_layout/_headers/AppHeader';

const headers: RouteProps[] = [
	// Dashboard Routes
	{ path: companyPagesMenu.dashboard.path, element: <AppHeader /> },

	// Settings Routes (wildcard — covers all settings sub-pages including org edit)
	{ path: 'settings/*', element: <SimpleHeader /> },

	// Legacy routes
	{ path: 'team', element: <SimpleHeader /> },

	{ path: pageLayoutTypesPagesMenu.pageLayout.subMenu.onlySubheader.path, element: null },
	{ path: pageLayoutTypesPagesMenu.pageLayout.subMenu.onlyContent.path, element: null },
	{ path: pageLayoutTypesPagesMenu.blank.path, element: null },
	{ path: demoPagesMenu.login.path, element: null },
	{ path: demoPagesMenu.signUp.path, element: null },
	{ path: demoPagesMenu.page404.path, element: <SimpleHeader /> },
	{ path: demoPagesMenu.knowledge.subMenu.grid.path, element: null },
	{ path: dashboardPagesMenu.dashboard.path, element: <SimpleHeader /> },
	{
		path: demoPagesMenu.projectManagement.subMenu.list.path,
		element: <SimpleHeader />,
	},
	{ path: demoPagesMenu.pricingTable.path, element: <SimpleHeader /> },
	{
		path: dashboardPagesMenu.dashboardBooking.path,
		element: <DashboardBookingHeader />,
	},
	{
		path: demoPagesMenu.appointment.subMenu.calendar.path,
		element: <DashboardBookingHeader />,
	},
	{
		path: demoPagesMenu.appointment.subMenu.employeeList.path,
		element: <DashboardBookingHeader />,
	},
	{
		path: demoPagesMenu.listPages.subMenu.listFluid.path,
		element: <DashboardBookingHeader />,
	},
	{
		path: `${demoPagesMenu.editPages.path}/*`,
		element: <DashboardBookingHeader />,
	},
	{
		path: `${demoPagesMenu.appointment.subMenu.employeeID.path}/*`,
		element: <DashboardBookingHeader />,
	},
	{
		path: `${demoPagesMenu.projectManagement.subMenu.itemID.path}/*`,
		element: <DashboardBookingHeader />,
	},
	{
		path: demoPagesMenu.singlePages.subMenu.fluidSingle.path,
		element: <ProfilePageHeader />,
	},
	{
		path: demoPagesMenu.singlePages.subMenu.boxedSingle.path,
		element: <ProfilePageHeader />,
	},
	{
		path: demoPagesMenu.sales.subMenu.transactions.path,
		element: <ProfilePageHeader />,
	},
	{
		path: demoPagesMenu.chat.subMenu.withListChat.path,
		element: <ProfilePageHeader />,
	},
	{
		path: demoPagesMenu.chat.subMenu.onlyListChat.path,
		element: <ProfilePageHeader />,
	},
	{
		path: `${demoPagesMenu.knowledge.subMenu.itemID.path}/:id`,
		element: <ProfilePageHeader />,
	},
	{
		path: demoPagesMenu.crm.subMenu.dashboard.path,
		element: <ProfilePageHeader />,
	},
	{
		path: demoPagesMenu.crm.subMenu.customersList.path,
		element: <ProfilePageHeader />,
	},
	{
		path: `${demoPagesMenu.crm.subMenu.customerID.path}/:id`,
		element: <ProfilePageHeader />,
	},
	{
		path: dashboardPagesMenu.summary.path,
		element: <SummaryHeader />,
	},
	{
		path: demoPagesMenu.gridPages.subMenu.gridBoxed.path,
		element: <ProductsHeader />,
	},
	{
		path: demoPagesMenu.gridPages.subMenu.gridFluid.path,
		element: <ProductsHeader />,
	},
	{
		path: demoPagesMenu.listPages.subMenu.listBoxed.path,
		element: <ProductListHeader />,
	},
	{
		path: demoPagesMenu.sales.subMenu.salesList.path,
		element: <ProductListHeader />,
	},
	{
		path: demoPagesMenu.sales.subMenu.productsGrid.path,
		element: <ProductListHeader />,
	},
	{
		path: `${demoPagesMenu.sales.subMenu.productID.path}/:id`,
		element: <ProductListHeader />,
	},
	{
		path: `${pageLayoutTypesPagesMenu.asideTypes.path}/*`,
		element: <PageLayoutHeader />,
	},
	{
		path: pageLayoutTypesPagesMenu.pageLayout.subMenu.headerAndSubheader.path,
		element: <PageLayoutHeader />,
	},
	{
		path: pageLayoutTypesPagesMenu.pageLayout.subMenu.onlyHeader.path,
		element: <PageLayoutHeader />,
	},
	{
		path: `${componentPagesMenu.components.path}/*`,
		element: <ComponentsHeader />,
	},
	{
		path: `${componentPagesMenu.forms.path}/*`,
		element: <FormHeader />,
	},
	{
		path: `${componentPagesMenu.charts.path}/*`,
		element: <ChartsHeader />,
	},
	{
		path: `${componentPagesMenu.content.path}/*`,
		element: <ContentHeader />,
	},
	{
		path: `${componentPagesMenu.utilities.path}/*`,
		element: <UtilitiesHeader />,
	},
	{
		path: `${componentPagesMenu.icons.path}/*`,
		element: <IconHeader />,
	},
	{
		path: `${gettingStartedPagesMenu.gettingStarted.path}/*`,
		element: <DocumentationHeader />,
	},
	{
		path: `${gettingStartedPagesMenu.routes.path}/*`,
		element: <DocumentationHeader />,
	},

	// Tasks domain routes
	{ path: tasksMenu.tasks.path, element: <AppHeader /> },

	{
		path: `*`,
		element: <AppHeader />,
	},
];

export default headers;
