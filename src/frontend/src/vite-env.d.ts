/// <reference types="vite/client" />

interface ImportMetaEnv {
	readonly VITE_SITE_NAME: string;
	readonly VITE_STORYBOOK_URL: string;
	readonly VITE_MOBILE_BREAKPOINT_SIZE: string;
	readonly VITE_ASIDE_MINIMIZE_BREAKPOINT_SIZE: string;
	readonly VITE_META_DESC: string;
	readonly VITE_MODERN_DESGIN: string;
	readonly VITE_ASIDE_TOUCH_STATUS: string;
	readonly VITE_DARK_MODE: string;
	readonly VITE_ASIDE_WIDTH_PX: string;
	readonly VITE_SPACER_PX: string;
	readonly VITE_PRIMARY_COLOR: string;
	readonly VITE_SECONDARY_COLOR: string;
	readonly VITE_SUCCESS_COLOR: string;
	readonly VITE_INFO_COLOR: string;
	readonly VITE_WARNING_COLOR: string;
	readonly VITE_DANGER_COLOR: string;
	readonly VITE_LIGHT_COLOR: string;
	readonly VITE_DARK_COLOR: string;
	readonly VITE_AZURE_AD_B2C_CLIENT_ID: string;
	readonly VITE_AZURE_AD_B2C_AUTHORITY: string;
	readonly VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES: string;
	readonly VITE_AZURE_AD_B2C_REDIRECT_URI: string;
	readonly VITE_API_SCOPE: string;
	readonly VITE_API_AUDIENCE: string;
	readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
	readonly env: ImportMetaEnv;
}

declare module '*.png';
declare module '*.webp';
declare module '*.svg';
declare module '*.jpg';
declare module '*.jpeg';
declare module '*.gif';
