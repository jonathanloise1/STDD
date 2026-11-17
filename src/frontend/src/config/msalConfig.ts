import { Configuration, LogLevel } from '@azure/msal-browser';
import { runtimeSetting } from './runtimeConfig';

/** A VITE_* setting: the value given at run time (the Docker image), else the one baked in at build time. */
const setting = (key: string, buildTime: string | undefined): string =>
	runtimeSetting(key) ?? buildTime ?? '';

const KNOWN_AUTHORITIES = setting(
	'VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES',
	import.meta.env.VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES,
);
const API_SCOPE = setting('VITE_API_SCOPE', import.meta.env.VITE_API_SCOPE);

/**
 * MSAL (Microsoft Authentication Library) configuration for Azure AD B2C.
 * Configures the PublicClientApplication used by AuthContextProvider for login,
 * token acquisition, and logout.
 *
 * Required environment variables:
 *   - REACT_APP_AZURE_AD_B2C_CLIENT_ID: Azure AD B2C app registration client ID
 *   - REACT_APP_AZURE_AD_B2C_AUTHORITY: B2C authority URL (e.g., https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy})
 *   - REACT_APP_AZURE_AD_B2C_KNOWN_AUTHORITIES: B2C known authorities domain
 *   - REACT_APP_API_SCOPE: API scope for access token requests
 */
const msalConfig: Configuration = {
	auth: {
		clientId: setting(
			'VITE_AZURE_AD_B2C_CLIENT_ID',
			import.meta.env.VITE_AZURE_AD_B2C_CLIENT_ID,
		),
		authority: setting(
			'VITE_AZURE_AD_B2C_AUTHORITY',
			import.meta.env.VITE_AZURE_AD_B2C_AUTHORITY,
		),
		knownAuthorities: KNOWN_AUTHORITIES ? [KNOWN_AUTHORITIES] : [],
		redirectUri: window.location.origin,
		postLogoutRedirectUri: window.location.origin,
		navigateToLoginRequestUrl: true,
	},
	cache: {
		cacheLocation: 'localStorage',
		storeAuthStateInCookie: false,
	},
	system: {
		loggerOptions: {
			loggerCallback: (level, message, containsPii) => {
				if (containsPii) {
					return;
				}
				switch (level) {
					case LogLevel.Error:
						console.error(message);
						break;
					case LogLevel.Info:
						console.info(message);
						break;
					case LogLevel.Verbose:
						console.debug(message);
						break;
					case LogLevel.Warning:
						console.warn(message);
						break;
				}
			},
			piiLoggingEnabled: false,
			logLevel: LogLevel.Warning,
		},
		windowHashTimeout: 60000,
		iframeHashTimeout: 6000,
		loadFrameTimeout: 0,
	},
};

/**
 * Request object for MSAL login
 */
export const loginRequest = {
	scopes: ['openid', 'profile', 'email'],
	prompt: 'select_account',
};

/**
 * Request object for acquiring tokens
 */
export const tokenRequest = {
	scopes: [API_SCOPE],
	forceRefresh: false,
};

/**
 * Default API scope for authenticating user
 */
export const apiScopes = [API_SCOPE];

export default msalConfig;
