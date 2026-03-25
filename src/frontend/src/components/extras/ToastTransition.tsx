import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';

/**
 * Custom framer-motion transition for react-toastify.
 *
 * Entrance: spring scale (0.92 → 1) + slide from right (40px → 0) + fade.
 * Exit: quick fade + slide right.
 *
 * Implements the react-toastify `cssTransition`-compatible interface:
 * receives `isIn`, `done`, `nodeRef`, `position`, `children`.
 *
 * @standard MyApp Toast Standard — Premium level
 */
const ToastTransition: React.FC<any> = ({ isIn, done, nodeRef, children }) => {
	return (
		<AnimatePresence onExitComplete={done}>
			{isIn && (
				<motion.div
					ref={nodeRef}
					key='toast'
					initial={{ opacity: 0, x: 40, scale: 0.92 }}
					animate={{ opacity: 1, x: 0, scale: 1 }}
					exit={{ opacity: 0, x: 60, scale: 0.95 }}
					transition={{
						type: 'spring',
						stiffness: 400,
						damping: 25,
						mass: 0.8,
					}}
				>
					{children}
				</motion.div>
			)}
		</AnimatePresence>
	);
};

export default ToastTransition;
