import React, { FC, ReactNode, use, useRef } from 'react';
import classNames from 'classnames';
import { motion, MotionStyle } from 'framer-motion';
import ThemeContext from '../../contexts/themeContext';
import Tooltips from '../../components/bootstrap/Tooltips';
import useAsideTouch from '../../hooks/useAsideTouch';

interface IAsideHeadProps {
	children: ReactNode;
}
export const AsideHead: FC<IAsideHeadProps> = ({ children }) => {
	return <div className='aside-head bg-white-desktop'>{children}</div>;
};

interface IAsideBodyProps {
	children: ReactNode;
}
export const AsideBody: FC<IAsideBodyProps> = ({ children }) => {
	return <div className='aside-body'>{children}</div>;
};

interface IAsideFootProps {
	children: ReactNode;
}
export const AsideFoot: FC<IAsideFootProps> = ({ children }) => {
	return <div className='aside-foot'>{children}</div>;
};

interface IAsideProps {
	children: any;
}
const Aside: FC<IAsideProps> = ({ children }) => {
	const { asideStatus, mobileDesign } = use(ThemeContext);

	const { asideStyle, touchStatus, hasTouchButton, asideWidthWithSpace, x } = useAsideTouch();

	const isModernDesign = import.meta.env.VITE_MODERN_DESGIN === 'true';

	const constraintsRef = useRef(null);

	// DEBUG: Track asideStatus in document title
	React.useEffect(() => {
		document.title = `Aside: ${asideStatus ? 'OPEN' : 'CLOSED'}`;
	}, [asideStatus]);

	const classes = classNames(
		'aside',
		{ open: asideStatus },
		{
			'aside-touch-bar': hasTouchButton && isModernDesign,
			'aside-touch-bar-close': !touchStatus && hasTouchButton && isModernDesign,
			'aside-touch-bar-open': touchStatus && hasTouchButton && isModernDesign,
		},
	);

	return (
		<>
			{mobileDesign ? (
				<aside className={classes}>{children}</aside>
			) : (
				<motion.aside style={asideStyle as MotionStyle} className={classes}>
					{children}
				</motion.aside>
			)}

			{asideStatus && hasTouchButton && isModernDesign && (
				<>
					<motion.div className='aside-drag-area' ref={constraintsRef} />
					<Tooltips title='Toggle Aside' flip={['top', 'right']}>
						<motion.div
							className='aside-touch'
							drag='x'
							whileDrag={{ scale: 1.2 }}
							whileHover={{ scale: 1.1 }}
							dragConstraints={constraintsRef}
							// onDrag={(event, info) => console.log(info.point.x, info.point.y)}
							dragElastic={0.1}
							style={{ x, zIndex: 1039 }}
							onClick={() => x.set(x.get() === 0 ? asideWidthWithSpace : 0)}
						/>
					</Tooltips>
				</>
			)}
		</>
	);
};

export default Aside;
