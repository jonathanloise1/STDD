import apiClient from './apiClient';
import { jwtDecode } from 'jwt-decode';
import { UserType } from '../../common/data/dummyRolesData';
import { OrganizationMembership } from '../../type/organization-types';

// Token keys for storage
const ID_TOKEN_KEY = 'MyApp_id_token';
const ACCESS_TOKEN_KEY = 'MyApp_access_token';

// Default safety margin for token refresh (5 minutes in milliseconds)
const TOKEN_REFRESH_MARGIN_MS = 5 * 60 * 1000;

/**
 * Response from POST /api/auth/sync (maps to backend AuthenticatedUserDto).
 * Contains user identity and org memberships.
 * @userstory US-AUTH-04, US-AUTH-08
 */
export interface IUserSyncResponse {
	id: string;
	email: string;
	preferredLanguage?: string;
	hasCompletedOnboarding?: boolean;
	organizations?: OrganizationMembership[];
}

/**
 * User profile extracted from JWT token claims (ID token or access token).
 * @userstory US-AUTH-01
 */
export interface IUserProfile {
	id: string;
	email: string;
	name?: string;
	surname?: string;
	username?: string;
	role?: UserType;
}

/**
 * Auth service — handles token storage, JWT claim extraction, token validation,
 * and the POST /api/auth/sync API call.
 * @userstory US-AUTH-01, US-AUTH-03, US-AUTH-04, US-AUTH-06
 */
const authService = {
	/**
	 * Calls POST /api/auth/sync to synchronize the authenticated user with the backend.
	 * On timeout, falls back to extracting user identity from the access token.
	 * @param organizationId Optional organization ID to scope the sync to.
	 * @userstory US-AUTH-04
	 */
	syncUser: async (organizationId?: string): Promise<IUserSyncResponse> => {
		try {
			// API call to sync user data
			const response = await apiClient.post('/api/auth/sync', 
				organizationId ? { organizationId } : {}
			);
			return response.data;
		} catch (error: any) {
			// Handle timeout errors specifically
			if (error.code === 'ECONNABORTED') {
				const accessToken = authService.getAccessToken();
				if (!accessToken) {
					throw new Error('Access token is missing');
				}

				const profile = authService.extractUserProfileFromAccessToken(accessToken);
				if (profile) {
					return {
						id: profile.id,
						email: profile.email,
					};
				}
			}
			throw error;
		}
	},

	/**
	 * Stores ID token in localStorage under 'MyApp_id_token'.
	 * @userstory US-AUTH-03
	 */
	setIdToken: (token: string): void => {
		localStorage.setItem(ID_TOKEN_KEY, token);
	},

	/**
	 * Gets ID token from localStorage.
	 * @userstory US-AUTH-03
	 */
	getIdToken: (): string => {
		return localStorage.getItem(ID_TOKEN_KEY) || '';
	},

	/**
	 * Stores access token in localStorage under 'MyApp_access_token'.
	 * @userstory US-AUTH-03
	 */
	setAccessToken: (token: string): void => {
		localStorage.setItem(ACCESS_TOKEN_KEY, token);
	},

	/**
	 * Gets access token from localStorage.
	 * @userstory US-AUTH-03
	 */
	getAccessToken: (): string => {
		return localStorage.getItem(ACCESS_TOKEN_KEY) || '';
	},

	/**
	 * @deprecated Use getIdToken() instead. Kept for backward compatibility.
	 */
	getToken: (): string => {
		return localStorage.getItem(ID_TOKEN_KEY) || '';
	},

	/**
	 * @deprecated Use setIdToken() instead. Kept for backward compatibility.
	 */
	setToken: (token: string): void => {
		localStorage.setItem(ID_TOKEN_KEY, token);
	},

	/**
	 * Extracts user profile from ID token JWT claims (sub, email, given_name, family_name, UserType).
	 * @userstory US-AUTH-01
	 */
	extractUserProfileFromIdToken: (idToken: string): IUserProfile | null => {
		try {
			const decoded: any = jwtDecode(idToken);
			return {
				id: decoded.sub,
				email: decoded.email,
				name: decoded.given_name,
				surname: decoded.family_name,
				username: decoded.given_name,
				role: decoded.UserType as UserType,
			};
		} catch (error) {
			console.error('Error extracting profile from ID token:', error);
			return null;
		}
	},

	/**
	 * Extracts user profile from access token JWT claims (sub, email, given_name, family_name, UserType).
	 * @userstory US-AUTH-01
	 */
	extractUserProfileFromAccessToken: (accessToken: string): IUserProfile | null => {
		try {
			const decoded: any = jwtDecode(accessToken);
			return {
				id: decoded.sub,
				email: decoded.email,
				name: decoded.given_name,
				surname: decoded.family_name,
				username: decoded.given_name,
				role: decoded.UserType as UserType,
			};
		} catch (error) {
			console.error('Error extracting profile from access token:', error);
			return null;
		}
	},

	/**
	 * Checks if token is valid and not expired.
	 * When withMargin=true, considers the token expired 5 minutes early (TOKEN_REFRESH_MARGIN_MS).
	 * @userstory US-AUTH-03
	 */
	isTokenValid: (token: string, withMargin = false): boolean => {
		try {
			if (!token) return false;
			const decoded: any = jwtDecode(token);
			if (!decoded.exp) return false;

			const currentTime = Date.now() / 1000;
			const margin = withMargin ? TOKEN_REFRESH_MARGIN_MS / 1000 : 0;
			return decoded.exp > currentTime + margin;
		} catch (error) {
			console.error('Error checking token validity:', error);
			return false;
		}
	},

	/**
	 * Gets token expiration time as Unix timestamp in milliseconds.
	 * @userstory US-AUTH-03
	 */
	getTokenExpiration: (token: string): number | null => {
		try {
			const decoded: any = jwtDecode(token);
			return decoded.exp * 1000; // Convert to milliseconds
		} catch {
			return null;
		}
	},

	/**
	 * Clears all auth data from localStorage: ID token, access token,
	 * and all MSAL-related cache entries.
	 * @userstory US-AUTH-06
	 */
	clearAuth: (): void => {
		localStorage.removeItem(ID_TOKEN_KEY);
		localStorage.removeItem(ACCESS_TOKEN_KEY);

		// Clear all MSAL-related items from localStorage
		Object.keys(localStorage)
			.filter(
				(key) =>
					key.includes('msal.') ||
					key.includes('MyApp_auth_') ||
					key.startsWith('msal'),
			)
			.forEach((key) => localStorage.removeItem(key));
	},

	/**
	 * Checks if the user has a valid, non-expired ID token in localStorage.
	 * @userstory US-AUTH-03
	 */
	isAuthenticated: (withMargin = false): boolean => {
		const idToken = authService.getIdToken();
		return !!idToken && authService.isTokenValid(idToken, withMargin);
	},

	/**
	 * Gets time remaining until token expiration in milliseconds.
	 * Returns 0 if token is expired or invalid.
	 * Used by the preemptive refresh effect in authContext.
	 * @userstory US-AUTH-03
	 */
	getTimeUntilExpiration: (token: string): number => {
		try {
			const decoded: any = jwtDecode(token);
			if (!decoded.exp) return 0;

			const expiresAtMs = decoded.exp * 1000;
			const timeRemainingMs = expiresAtMs - Date.now();

			return timeRemainingMs > 0 ? timeRemainingMs : 0;
		} catch (error) {
			console.error('Error calculating token expiration:', error);
			return 0;
		}
	},
};

export default authService;
