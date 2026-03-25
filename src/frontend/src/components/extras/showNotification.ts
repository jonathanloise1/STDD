import React from 'react';
import { toast, TypeOptions } from 'react-toastify';

const typeMap: Record<string, TypeOptions> = {
	default: 'default',
	success: 'success',
	danger: 'error',
	info: 'info',
	warning: 'warning',
};

/**
 * MyApp standard toast notification — Premium level.
 *
 * Layout (with icon):
 *   ┌────────────────────────────────┐
 *   │  [icon]  **title**             │
 *   │          message (muted)       │
 *   │  ━━━━━━━━━░░░░░░ progress      │
 *   └────────────────────────────────┘
 *
 * - `title` — bold heading (string or ReactNode)
 * - `message` — optional body text (string or ReactNode), rendered smaller + muted
 * - `type` — 'default' | 'success' | 'danger' | 'info' | 'warning'
 * - `icon` — optional custom ReactNode shown left of the title (e.g. flag emoji, Icon component)
 *
 * When only `message` is provided (title is falsy), the toast renders
 * the message alone without any wrapper.
 */
const showNotification = (
	title: string | React.ReactNode,
	message: string | React.ReactNode,
	type = 'default',
	icon?: React.ReactNode,
) => {
	const toastType = typeMap[type] ?? 'default';

	let content: React.ReactNode;
	if (!title) {
		content = message;
	} else {
		// Title row (with optional icon)
		const titleContent = icon
			? React.createElement(
					'div',
					{ style: { display: 'flex', alignItems: 'center', gap: '0.5rem' } },
					React.createElement('span', { style: { flexShrink: 0, fontSize: '1.125rem', lineHeight: 1 } }, icon),
					React.createElement('span', null, title),
				)
			: title;

		content = React.createElement(
			'div',
			{ style: { lineHeight: 1.45 } },
			React.createElement(
				'div',
				{ style: { fontWeight: 600, fontSize: '0.8125rem' } },
				titleContent,
			),
			message
				? React.createElement(
						'div',
						{
							style: {
								fontSize: '0.75rem',
								opacity: 0.6,
								marginTop: 2,
								paddingLeft: icon ? '1.625rem' : undefined,
							},
						},
						message,
					)
				: null,
		);
	}

	toast(content, {
		type: toastType,
		position: 'top-right',
		autoClose: 3500,
		pauseOnHover: true,
		icon: false, // We hide the default icon, using our custom one instead
	});
};

export default showNotification;
