/**
 * PageHelpGuide
 * A "?" icon button that opens a modal overlay with the page guide.
 * Shows the same content as the empty state, so new users can learn
 * what the page is about even when data is already populated.
 */
import React, { useState } from 'react';
import Button from '../bootstrap/Button';
import Modal, {
	ModalHeader,
	ModalTitle,
	ModalBody,
} from '../bootstrap/Modal';
import EmptyStateOnboarding from './EmptyStateOnboarding';
import useDarkMode from '../../hooks/useDarkMode';

export interface PageHelpGuideProps {
	/** Unique page identifier used for all sub-element ids */
	pageId: string;
	/** Modal title */
	title: string;
	/** Large emoji displayed in the guide */
	emoji: string;
	/** Guide description text */
	description: string;
	/** Optional CTA button label */
	ctaLabel?: string;
	/** Optional CTA navigation target */
	ctaTo?: string;
}

const PageHelpGuide: React.FC<PageHelpGuideProps> = ({
	pageId,
	title,
	emoji,
	description,
	ctaLabel,
	ctaTo,
}) => {
	const [isOpen, setIsOpen] = useState(false);
	const { darkModeStatus } = useDarkMode();

	return (
		<>
			<Button
				id={`${pageId}-help-btn`}
				color={darkModeStatus ? 'light' : 'dark'}
				isLight
				size='sm'
				className='rounded-circle d-flex align-items-center justify-content-center'
				style={{
					width: 32,
					height: 32,
					minWidth: 32,
					padding: 0,
					fontSize: '0.85rem',
					fontWeight: 700,
				}}
				onClick={() => setIsOpen(true)}>
				?
			</Button>

			<Modal
				id={`${pageId}-help-modal`}
				isOpen={isOpen}
				setIsOpen={setIsOpen}
				size='lg'
				isCentered
				isStaticBackdrop>
				<ModalHeader setIsOpen={setIsOpen}>
					<ModalTitle id={`${pageId}-help-modal-title`}>
						{title}
					</ModalTitle>
				</ModalHeader>
				<ModalBody>
					<EmptyStateOnboarding
						id={`${pageId}-help-guide`}
						emoji={emoji}
						title={title}
						description={description}
						ctaLabel={ctaLabel}
						ctaTo={ctaTo}
						animateIn={false}
						compact
					/>
				</ModalBody>
			</Modal>
		</>
	);
};

export default PageHelpGuide;
