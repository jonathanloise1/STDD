/// <reference types="vite/client" />

interface ImportMetaEnv {
	readonly VITE_SITE_NAME: string;
	readonly VITE_META_DESC: string;
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
