import { use } from 'react';
import ThemeContext from '../contexts/themeContext';

export default function useDarkMode() {
	const { darkModeStatus, setDarkModeStatus } = use(ThemeContext);

	const themeStatus: 'dark' | 'light' = darkModeStatus ? 'dark' : 'light';

	return { themeStatus, darkModeStatus, setDarkModeStatus };
}
