import React, { FC, ReactNode, useId } from 'react';
import classNames from 'classnames';
import { Offcanvas } from 'react-bootstrap';
import { useTranslation } from 'react-i18next';

export interface ISidePanelProps {
	show: boolean;
	onHide: () => void;
	/** Its text is the accessible name of the panel. */
	title: ReactNode;
	placement?: 'end' | 'bottom';
	/** False: no backdrop, the page behind stays usable and the panel reports `aria-modal="false"`. */
	modal?: boolean;
	id?: string;
	'data-testid'?: string;
	className?: string;
	children?: ReactNode;
}

/** Content next to the screen (help) or a bottom sheet (More) (frontend.md 6.6, 11). */
export const SidePanel: FC<ISidePanelProps> = ({
	show,
	onHide,
	title,
	placement = 'end',
	modal = true,
	id,
	'data-testid': testId,
	className,
	children,
}) => {
	const { t } = useTranslation('common');
	const titleId = useId();

	return (
		<Offcanvas
			show={show}
			onHide={onHide}
			placement={placement}
			id={id}
			data-testid={testId}
			className={classNames(className)}
			aria-labelledby={titleId}
			backdrop={modal}
			scroll={!modal}
			aria-modal={modal ? undefined : false}>
			<Offcanvas.Header closeButton closeLabel={t('close')}>
				<Offcanvas.Title as='h2' id={titleId} className='h5'>
					{title}
				</Offcanvas.Title>
			</Offcanvas.Header>
			<Offcanvas.Body>{children}</Offcanvas.Body>
		</Offcanvas>
	);
};
