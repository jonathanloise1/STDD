import React from 'react';
import { test, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import Button from './Button';

test('Render Light Info Button', () => {
	render(
		<Button color='info' isLight>
			Click
		</Button>,
	);
	const buttonElement = screen.getByText(/click/i);
	expect(buttonElement).toHaveClass('btn-light-info');
	expect(buttonElement).toBeInTheDocument();
});
