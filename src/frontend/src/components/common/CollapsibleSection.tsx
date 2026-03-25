import React, { useState, useEffect, ReactNode } from 'react';
import Card, { CardBody, CardHeader } from '../bootstrap/Card';
import Icon from '../icon/Icon';
import './CollapsibleSection.scss';

interface CollapsibleSectionProps {
	title: string;
	icon?: string;
	defaultExpanded?: boolean;
	children: ReactNode;
	className?: string;
	badge?: ReactNode;
	persistKey?: string; // Key for localStorage persistence
}

const CollapsibleSection: React.FC<CollapsibleSectionProps> = ({
	title,
	icon,
	defaultExpanded = true,
	children,
	className = '',
	badge,
	persistKey,
}) => {
	// Initialize from localStorage if persistKey is provided
	const getInitialState = (): boolean => {
		if (persistKey) {
			const stored = localStorage.getItem(`collapsible-${persistKey}`);
			if (stored !== null) {
				return stored === 'true';
			}
		}
		return defaultExpanded;
	};

	const [isExpanded, setIsExpanded] = useState(getInitialState);

	// Persist state to localStorage
	useEffect(() => {
		if (persistKey) {
			localStorage.setItem(`collapsible-${persistKey}`, String(isExpanded));
		}
	}, [isExpanded, persistKey]);

	const handleToggle = () => {
		setIsExpanded(!isExpanded);
	};

	const handleKeyDown = (e: React.KeyboardEvent) => {
		if (e.key === 'Enter' || e.key === ' ') {
			e.preventDefault();
			handleToggle();
		}
	};

	return (
		<Card
			className={`collapsible-section ${isExpanded ? 'expanded' : 'collapsed'} ${className}`}>
			<CardHeader
				className='collapsible-header'
				onClick={handleToggle}
				role='button'
				tabIndex={0}
				onKeyDown={handleKeyDown}
				aria-expanded={isExpanded}>
				<div className='d-flex align-items-center flex-grow-1'>
					{icon && <Icon icon={icon} className='me-2 section-icon' />}
					<h5 className='mb-0 section-title'>{title}</h5>
					{badge && <span className='ms-2'>{badge}</span>}
				</div>
				<Icon
					icon={isExpanded ? 'ChevronUp' : 'ChevronDown'}
					className='collapse-indicator'
				/>
			</CardHeader>
			<div className={`collapsible-content ${isExpanded ? 'show' : ''}`}>
				<CardBody>{children}</CardBody>
			</div>
		</Card>
	);
};

export default CollapsibleSection;
