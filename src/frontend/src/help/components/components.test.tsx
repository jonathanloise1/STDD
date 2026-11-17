import React from 'react';
import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import { createPackSearcher } from '../runtime/searcher.ts';
import { testPack } from '../runtime/fixtures/pack.ts';
import { ManualBlocks } from './ManualBlocks.tsx';
import { SearchResults } from './SearchResults.tsx';

const ps = createPackSearcher(testPack());

afterEach(cleanup);

describe('search results', () => {
	it('shows page title, heading and snippet in each result with the matching word in bold', () => {
		render(
			<MemoryRouter>
				<SearchResults ps={ps} query='tasks' />
			</MemoryRouter>,
		);
		const results = screen.getAllByTestId('search-result');
		expect(results.length).toBeGreaterThan(0);
		expect(results.length).toBeLessThanOrEqual(10);
		const first = within(results[0]);
		expect(first.getAllByText(/task/i).length).toBeGreaterThan(0);
		expect(results[0].querySelectorAll('strong').length).toBeGreaterThan(0);
		expect(results[0].getAttribute('href')).toMatch(/^\/manual\//);
	});

	it('shows the message and the way back when nothing matches', () => {
		render(
			<MemoryRouter>
				<SearchResults ps={ps} query='zzqxj' />
			</MemoryRouter>,
		);
		expect(screen.getByTestId('search-empty')).toBeTruthy();
		expect(screen.getByRole('link').getAttribute('href')).toBe('/manual');
	});
});

describe('manual blocks', () => {
	it('gives the fixed H2 headings their key as id, so a link to a section scrolls to it', () => {
		const { container } = render(
			<MemoryRouter>
				<ManualBlocks
					lang='en'
					blocks={[
						{ type: 'heading', level: 1, text: 'Create a task' },
						{ type: 'heading', level: 2, text: 'Steps' },
						{
							type: 'ordered',
							items: [
								{
									inlines: [{ type: 'bold', text: 'Open' }],
									number: 1,
									image: null,
								},
							],
						},
					]}
				/>
			</MemoryRouter>,
		);
		expect(container.querySelector('h2#steps')?.textContent).toBe('Steps');
		expect(container.querySelector('ol strong')?.textContent).toBe('Open');
	});

	it('renders no HTML from the bundle', () => {
		const { container } = render(
			<MemoryRouter>
				<ManualBlocks
					lang='en'
					blocks={[
						{
							type: 'paragraph',
							inlines: [{ type: 'text', text: '<img src=x onerror=alert(1)>' }],
						},
					]}
				/>
			</MemoryRouter>,
		);
		expect(container.querySelector('img')).toBeNull();
	});
});
