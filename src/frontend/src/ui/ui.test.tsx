// @vitest-environment jsdom
import React, { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom/vitest';
import { MemoryRouter } from 'react-router-dom';
import { createInstance } from 'i18next';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { Button } from './components/Button';
import { Dialog } from './components/Dialog';
import { IconButton } from './components/IconButton';
import { Menu } from './components/Menu';
import { SidePanel } from './components/SidePanel';
import { TextField } from './components/TextField';
import { SelectField } from './components/SelectField';
import { Notice } from './feedback/Notice';
import { LoadingBlock } from './feedback/LoadingBlock';
import { registry } from './icons/registry';

const i18n = createInstance();
await i18n.use(initReactI18next).init({
	lng: 'en',
	resources: { en: { common: { close: 'Close', loading: 'Loading' } } },
	interpolation: { escapeValue: false },
});

// jsdom has no matchMedia; react-bootstrap's Offcanvas asks for it.
window.matchMedia ??= ((query: string) => ({
	matches: false,
	media: query,
	onchange: null,
	addEventListener: () => undefined,
	removeEventListener: () => undefined,
	addListener: () => undefined,
	removeListener: () => undefined,
	dispatchEvent: () => false,
})) as typeof window.matchMedia;

const renderUi = (ui: ReactElement) =>
	render(
		<I18nextProvider i18n={i18n}>
			<MemoryRouter>{ui}</MemoryRouter>
		</I18nextProvider>,
	);

afterEach(cleanup);

describe('ui: Menu', () => {
	it('lists its entries as buttons in a ul.dropdown-menu and closes after a choice', async () => {
		const user = userEvent.setup();
		const onSelect = vi.fn();
		const { container } = renderUi(
			<Menu align='end'>
				<Menu.Toggle aria-label='Change language'>EN</Menu.Toggle>
				<Menu.Items>
					<Menu.Item onSelect={() => undefined}>English</Menu.Item>
					<Menu.Item onSelect={onSelect}>Italiano</Menu.Item>
				</Menu.Items>
			</Menu>,
		);

		await user.click(screen.getByRole('button', { name: 'Change language' }));
		expect(container.querySelector('ul.dropdown-menu')).not.toBeNull();
		expect(container.querySelector('.dropdown-menu.show')).not.toBeNull();
		const item = screen.getByRole('button', { name: 'Italiano' });

		await user.click(item);
		expect(onSelect).toHaveBeenCalledTimes(1);
		expect(container.querySelector('.dropdown-menu.show')).toBeNull();
	});
});

describe('ui: Dialog and SidePanel', () => {
	it('exposes role dialog named by its title, a close button named Close and .modal.show', () => {
		renderUi(
			<Dialog show onHide={() => undefined} title='Delete Task' icon='Delete'>
				Body
			</Dialog>,
		);
		expect(screen.getByRole('dialog', { name: 'Delete Task' })).toBeInTheDocument();
		expect(document.querySelector('.modal.show')).not.toBeNull();
		expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();
	});

	it('calls onHide when Escape is pressed', async () => {
		const user = userEvent.setup();
		const onHide = vi.fn();
		renderUi(
			<Dialog show onHide={onHide} title='Delete Task'>
				Body
			</Dialog>,
		);
		await user.keyboard('{Escape}');
		expect(onHide).toHaveBeenCalled();
	});

	it('renders a non-modal side panel with aria-modal false, named by its title', () => {
		renderUi(
			<SidePanel
				show
				onHide={() => undefined}
				title='Help'
				modal={false}
				data-testid='help-panel'>
				Content
			</SidePanel>,
		);
		const panel = screen.getByRole('dialog', { name: 'Help' });
		expect(panel).toHaveAttribute('aria-modal', 'false');
		expect(panel).toHaveAttribute('data-testid', 'help-panel');
	});

	it('closes a non-modal side panel with Escape', async () => {
		const user = userEvent.setup();
		const onHide = vi.fn();
		renderUi(
			<SidePanel show onHide={onHide} title='Help' modal={false}>
				Content
			</SidePanel>,
		);
		await user.keyboard('{Escape}');
		expect(onHide).toHaveBeenCalled();
	});
});

describe('ui: fields', () => {
	it('puts the message right after the control and the hint after the message', () => {
		renderUi(
			<TextField
				id='title'
				label='Title'
				value=''
				onChange={() => undefined}
				error='Title is required'
				hint='Shown in the list'
			/>,
		);
		const input = screen.getByLabelText('Title');
		expect(input.tagName).toBe('INPUT');
		expect(input).toHaveAttribute('aria-invalid', 'true');
		expect(input).toHaveClass('is-invalid');
		const message = input.nextElementSibling as HTMLElement;
		expect(message).toHaveClass('invalid-feedback');
		expect(message).toHaveTextContent('Title is required');
		expect(message.nextElementSibling).toHaveClass('form-text');
		expect(input).toHaveAccessibleDescription('Title is required Shown in the list');
	});

	it('renders no message and no aria-invalid without an error', () => {
		renderUi(<TextField id='title' label='Title' value='' onChange={() => undefined} />);
		const input = screen.getByLabelText('Title');
		expect(input).not.toHaveAttribute('aria-invalid');
		expect(input.nextElementSibling).toBeNull();
	});

	it('renders a select with a hidden placeholder option and its message next', () => {
		renderUi(
			<SelectField
				id='province'
				label='Province'
				placeholder='Province'
				value=''
				onChange={() => undefined}
				options={[{ value: 'MI', label: 'MI - Milano' }]}
				error='Province is required'
			/>,
		);
		const select = screen.getByLabelText('Province') as HTMLSelectElement;
		expect(select).toHaveClass('form-select');
		expect(select.options[0].disabled).toBe(true);
		expect(select.nextElementSibling).toHaveClass('invalid-feedback');
	});
});

describe('ui: buttons and icons', () => {
	it('names an icon button by its label and hides the icon', () => {
		renderUi(<IconButton icon='Edit' label='Edit' />);
		const button = screen.getByRole('button', { name: 'Edit' });
		expect(button).toHaveAttribute('title', 'Edit');
		expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
	});

	it('disables a busy button and defaults its type to button', () => {
		renderUi(<Button busy>Save</Button>);
		const button = screen.getByRole('button', { name: 'Save' });
		expect(button).toBeDisabled();
		expect(button).toHaveAttribute('type', 'button');
	});

	it('renders a router link with role link when given `to`', () => {
		renderUi(<Button to='/dashboard'>Homepage</Button>);
		expect(screen.getByRole('link', { name: 'Homepage' })).toHaveAttribute(
			'href',
			'/dashboard',
		);
	});

	it('resolves every icon of the registry to a component', () => {
		const values = Object.values(registry);
		expect(values.length).toBeGreaterThan(0);
		values.forEach((value) => {
			expect(['function', 'object']).toContain(typeof value);
			expect(value).not.toBeNull();
		});
	});
});

describe('ui: feedback', () => {
	it('renders a notice without a live role', () => {
		const { container } = renderUi(<Notice icon='Info'>Static note</Notice>);
		expect(container.querySelector('.alert')).not.toBeNull();
		expect(screen.queryByRole('alert')).toBeNull();
	});

	it('announces a loading block as a status', () => {
		renderUi(<LoadingBlock />);
		expect(screen.getByRole('status')).toHaveTextContent('Loading');
	});
});
