import { use } from 'react';
import { useWindowSize } from 'react-use';
import ThemeContext from '../contexts/themeContext';

const useNavigationItemHandle = () => {
	const { setAsideStatus, setLeftMenuStatus, setRightMenuStatus } = use(ThemeContext);
	const { width } = useWindowSize();

	return () => {
		if (width < Number(import.meta.env.VITE_MOBILE_BREAKPOINT_SIZE)) setAsideStatus(false);
		setLeftMenuStatus(false);
		setRightMenuStatus(false);
	};
};
export default useNavigationItemHandle;
