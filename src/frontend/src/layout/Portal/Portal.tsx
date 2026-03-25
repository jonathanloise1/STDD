import ReactDOM from 'react-dom';
import { FC, ReactNode, use } from 'react';
import ThemeContext from '../../contexts/themeContext';

interface IPortalProps {
	children: ReactNode;
	id?: string;
}
// @ts-ignore
const Portal: FC<IPortalProps> = ({ id = 'portal-root', children }) => {
	const { fullScreenStatus } = use(ThemeContext);

	// @ts-ignore
	const mount = document.getElementById(id);
	if (fullScreenStatus) return children;
	if (mount) return ReactDOM.createPortal(children, mount);
	return null;
};

export default Portal;
