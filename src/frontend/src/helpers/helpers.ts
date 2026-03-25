import dayjs from 'dayjs';
import i18n from 'i18next';
export function test() {
	return null;
}

export function getOS() {
	const { userAgent } = window.navigator;
	const { platform } = window.navigator;
	const macosPlatforms = ['Macintosh', 'MacIntel', 'MacPPC', 'Mac68K'];
	const windowsPlatforms = ['Win32', 'Win64', 'Windows', 'WinCE'];
	const iosPlatforms = ['iPhone', 'iPad', 'iPod'];
	let os = null;

	if (macosPlatforms.indexOf(platform) !== -1) {
		os = 'MacOS';
	} else if (iosPlatforms.indexOf(platform) !== -1) {
		os = 'iOS';
	} else if (windowsPlatforms.indexOf(platform) !== -1) {
		os = 'Windows';
	} else if (/Android/.test(userAgent)) {
		os = 'Android';
	} else if (!os && /Linux/.test(platform)) {
		os = 'Linux';
	}

	// @ts-ignore
	document.documentElement.setAttribute('os', os);
	return os;
}

export const hasNotch = () => {
	/**
	 * For storybook test
	 */
	const storybook = window.location !== window.parent.location;
	// @ts-ignore
	const iPhone = /iPhone/.test(navigator.userAgent) && !window.MSStream;
	const aspect = window.screen.width / window.screen.height;
	const aspectFrame = window.innerWidth / window.innerHeight;
	return (
		(iPhone && aspect.toFixed(3) === '0.462') ||
		(storybook && aspectFrame.toFixed(3) === '0.462')
	);
};

export const mergeRefs = (refs: any[]) => {
	return (value: any) => {
		refs.forEach((ref) => {
			if (typeof ref === 'function') {
				ref(value);
			} else if (ref != null) {
				ref.current = value;
			}
		});
	};
};

export const randomColor = () => {
	const colors = ['primary', 'secondary', 'success', 'info', 'warning', 'danger'];

	const color = Math.floor(Math.random() * colors.length);

	return colors[color];
};

export const priceFormat = (price: number) => {
	return price.toLocaleString('en-US', {
		style: 'currency',
		currency: 'EUR',
	});
};

export const average = (array: any[]) => array.reduce((a, b) => a + b) / array.length;

export const percent = (value1: number, value2: number) =>
	Number(((value1 / value2 - 1) * 100).toFixed(2));

export const getFirstLetter = (text: string, letterCount = 2): string =>
	// @ts-ignore
	text
		.toUpperCase()
		.match(/\b(\w)/g)
		.join('')
		.substring(0, letterCount);

export const debounce = (func: (arg0: any) => void, wait = 1000) => {
	let timeout: string | number | NodeJS.Timeout | undefined;

	return function executedFunction(...args: any[]) {
		const later = () => {
			clearTimeout(timeout);
			// @ts-ignore
			func(...args);
		};

		clearTimeout(timeout);
		timeout = setTimeout(later, wait);
	};
};

export const formatDate = (dateString: string): string => {
	const date = new Date(dateString);
	return new Intl.DateTimeFormat('en-US', {
		day: '2-digit',
		month: '2-digit',
		year: 'numeric',
	}).format(date);
};

/**
 * Format a number as currency using localized format based on current i18n language
 * @param amount Number to format
 * @returns Formatted currency string
 */
export const formatCurrency = (amount?: number | null): string => {
	if (amount == null) return '';
	return new Intl.NumberFormat(i18n.language || 'en', {
		style: 'currency',
		currency: 'EUR',
		minimumFractionDigits: 2,
		maximumFractionDigits: 2,
		useGrouping: true,
	}).format(amount);
};

/**
 * Format a date using localized format based on current i18n language
 * @param date Date or date string
 * @param format Optional format string (default is 'LL')
 * @returns Formatted date string
 */
export const formatLocalizedDate = (date?: string | Date | null, format: string = 'LL'): string => {
	if (!date) return '';
	return toPascalCase(dayjs(date).locale(i18n.language).format(format));
};

/**
 * Format a date to show only month and year, localized based on current i18n language.
 * @param date Date or date string
 * @returns Formatted string with month and year
 */
export const formatMonthYear = (date: string | Date): string => {
	return toPascalCase(dayjs(date).locale(i18n.language).format('MMMM YYYY'));
};

/**
 * Converts estimated hours to a localized string like "2 days and 6 hours"
 * @param hours Total estimated hours
 * @param hoursPerDay Number of working hours per day (default: 8)
 * @returns A localized string e.g., "2 days and 6 hours"
 */
export const formatHoursToDaysAndHours = (hours: number, hoursPerDay = 8): string => {
	const days = Math.floor(hours / hoursPerDay);
	const remainingHours = hours % hoursPerDay;

	const t = i18n.t.bind(i18n);
	const parts: string[] = [];

	if (days > 0) {
		parts.push(t('day', { count: days }));
	}
	if (remainingHours > 0 || parts.length === 0) {
		parts.push(t('hour', { count: remainingHours }));
	}

	return parts.join(` ${t('and')} `);
};

/**
 * Converts an hourly rate to a daily rate (8h/day) with localized label
 * @param hourlyRate number (e.g. 40)
 * @returns localized daily rate string (e.g. "320€/day" or "320€/gg")
 */
export const formatDailyRate = (hourlyRate: number): string => {
	// const t = i18n.t.bind(i18n);
	const dailyRate = (hourlyRate > 0 ? hourlyRate : 30) * 8;
	const suffix = i18n.language === 'it' ? '/gg' : '/day';
	return `${dailyRate}€${suffix}`;
};

/**
 * Converts an Deadline to a number of days with localized label
 * @param deadline string (e.g. "2025-06-01")
 * @returns localized number of days string (e.g. "32 days")
 */

export const formatDaysUntilDeadline = (deadline: string | Date): number => {
	const now = dayjs();
	const end = dayjs(deadline);
	return end.diff(now, 'day');
};

/**
 * Generates an array of timesheet entries for a given month
 * @param monthStr string (e.g. "2025-06")
 * @returns array of timesheet entries
 */
interface TimesheetEntry {
	date: string;
	hours: number;
	description: string;
}

export const generateMonthEntries = (monthStr: string): TimesheetEntry[] => {
	const [year, month] = monthStr.split('-').map(Number); // "2025-06" → [2025, 6]
	const date = new Date(year, month - 1, 1);
	const entries: TimesheetEntry[] = [];

	while (date.getMonth() === month - 1) {
		const day = date.getDay();
		if (day !== 0 && day !== 6) {
			const dateStr = date.toISOString().split('T')[0];
			entries.push({ date: dateStr, hours: 8, description: 'Worked on project tasks' });
		}
		date.setDate(date.getDate() + 1);
	}
	return entries;
};

/**
 * Verifica se un oggetto (anche annidato) contiene una stringa di ricerca in uno qualsiasi dei suoi campi.
 * Supporta stringhe, numeri, booleani, array, oggetti annidati e date.
 * Per le date usa la funzione `formatLocalizedDate` per confrontare in formato localizzato.
 *
 * @param obj Oggetto da analizzare (es. progetto, contratto, profilo, ecc.)
 * @param searchTerm Testo da cercare (case-insensitive)
 * @returns true se almeno un campo dell'oggetto contiene la stringa cercata
 */
export const searchInAllFields = (obj: any, searchTerm: string): boolean => {
	const lowerSearch = searchTerm.toLowerCase();

	const checkValue = (value: any): boolean => {
		if (value == null) return false;

		if (typeof value === 'string') {
			return value.toLowerCase().includes(lowerSearch);
		}

		if (typeof value === 'number' || typeof value === 'boolean') {
			return value.toString().toLowerCase().includes(lowerSearch);
		}

		if (Array.isArray(value)) {
			return value.some((item) => checkValue(item));
		}

		if (typeof value === 'object') {
			return Object.values(value).some((nested) => checkValue(nested));
		}

		return false;
	};

	return checkValue(obj);
};

/**
 * Capitalize each word in a string (Pascal Case)
 * @param str The input string
 * @returns Pascal Cased string
 */
const toPascalCase = (str: string): string => {
	return str.replace(
		/\w\S*/g,
		(word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase(),
	);
};

export const isRichHtmlEmpty = (html?: string | null): boolean => {
	if (!html) return true;
	// Fast path: strip tags & common “empty” filler, then trim
	const stripped = html
		.replace(/<br\s*\/?>/gi, '') // remove <br>
		.replace(/&nbsp;/gi, ' ') // normalize nbsp
		.replace(/<[^>]*>/g, '') // strip all tags
		.replace(/\s+/g, ' ') // collapse whitespace
		.trim();
	if (stripped.length > 0) return false;

	// Safety: if editors inject weird nodes, parse once and read textContent
	const div = document.createElement('div');
	div.innerHTML = html;
	const text = (div.textContent || '').replace(/\u00A0/g, ' ').trim();
	return text.length === 0;
};
