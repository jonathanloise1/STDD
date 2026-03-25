import React, { FC, ReactNode, use } from 'react';
import classNames from 'classnames';
import Content from '../Content/Content';
import WrapperOverlay from './WrapperOverlay';
import HeaderRoutes from '../Header/HeaderRoutes';
import FooterRoutes from '../Footer/FooterRoutes';
import ThemeContext from '../../contexts/themeContext';
// US-SHORT-01, US-SHORT-02: Keyboard shortcuts
import useKeyboardShortcuts from '../../hooks/useKeyboardShortcuts';
import KeyboardShortcutsModal from '../../components/common/KeyboardShortcutsModal';

interface IWrapperContainerProps {
	children: ReactNode;
	className?: string;
}
export const WrapperContainer: FC<IWrapperContainerProps> = ({ children, className, ...props }) => {
	const { rightPanel } = use(ThemeContext);
	return (
		<div
			className={classNames(
				'wrapper',
				{ 'wrapper-right-panel-active': rightPanel },
				className,
			)}
			{...props}>
			{children}
		</div>
	);
};

const Wrapper = () => {
	// US-SHORT-01, US-SHORT-02: Global keyboard shortcuts
	const { isHelpOpen, setIsHelpOpen } = useKeyboardShortcuts();

	return (
		<>
			<WrapperContainer>
				<HeaderRoutes />
				<Content />
				<FooterRoutes />
			</WrapperContainer>
			<WrapperOverlay />
			{/* US-SHORT-02: Keyboard shortcuts help modal */}
			<KeyboardShortcutsModal isOpen={isHelpOpen} setIsOpen={setIsHelpOpen} />
		</>
	);
};

export default Wrapper;
