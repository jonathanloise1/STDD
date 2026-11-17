import axios, { AxiosInstance, AxiosError } from 'axios';
import axiosRetry from 'axios-retry';
import { jwtDecode } from 'jwt-decode';
import { randomHex, TRACE_ID_BYTES, SPAN_ID_BYTES } from './apiProblem';
import { runtimeSetting } from '../../config/runtimeConfig';

declare module 'axios' {
	interface InternalAxiosRequestConfig {
		/** W3C trace id of the request (32 lower-case hex); kept across retries. */
		traceId?: string;
	}
}

// Constants
// A run time value (the Docker image) wins, and an empty one means the page's own origin, where nginx forwards /api
const API_BASE_URL =
	runtimeSetting('VITE_API_BASE_URL') ??
	(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5248');
const ACCESS_TOKEN_KEY = 'MyApp_access_token';
const ID_TOKEN_KEY = 'MyApp_id_token';
const EXPECTED_AUDIENCE =
	runtimeSetting('VITE_API_AUDIENCE') ?? (import.meta.env.VITE_API_AUDIENCE || '');
const E2E_MOCK_USER_KEY = 'E2E_MOCK_USER';
const IMPERSONATION_HEADER = 'X-MyApp-Impersonate-UserId';
const API_TIMEOUT = 30000;
const RETRY_COUNT = 1; // Number of retry attempts
const RETRY_DELAY = 1000; // Delay between retries in ms

// Create Axios instance with default config
const apiClient: AxiosInstance = axios.create({
	baseURL: API_BASE_URL,
	headers: {
		'Content-Type': 'application/json',
	},
	timeout: API_TIMEOUT, // Increased timeout
});

// Configure retry behavior
axiosRetry(apiClient, {
	retries: RETRY_COUNT,
	retryDelay: (retryCount: number, error: AxiosError) => {
		// Check if the server provided a Retry-After header
		const retryAfterHeader = error.response?.headers['retry-after'];
		if (retryAfterHeader) {
			const retryAfterSeconds = parseInt(retryAfterHeader, 10);
			if (!isNaN(retryAfterSeconds)) {
				// Optional: Add jitter (random delay) to avoid synchronized retries
				const jitter = Math.floor(Math.random() * 500); // up to 0.5s extra
				return retryAfterSeconds * 1000 + jitter;
			}
		}
		// Fallback to exponential backoff if Retry-After is not present
		return retryCount * RETRY_DELAY;
	},
	retryCondition: (error: AxiosError) => {
		const status = error.response?.status;
		const method = (error.config?.method || 'get').toLowerCase();
		const isRead = ['get', 'head', 'options'].includes(method);
		// A write is retried only when the server states it did not process it (429, 503);
		// a network error or a timeout could hide a processed write, so only reads are retried then.
		if (status === 429 || status === 503) return true;
		if (!isRead) return false;
		return (
			!error.response || // Network error
			[408, 522].includes(status as number) ||
			error.code === 'ECONNABORTED' // Timeout
		);
	},
});

// Helper function to validate token audience
const hasValidAudience = (token: string): boolean => {
	try {
		const decoded: any = jwtDecode(token);
		return decoded.aud === EXPECTED_AUDIENCE;
	} catch {
		return false;
	}
};

// Helper function to check if token is expired
const isTokenExpired = (token: string): boolean => {
	try {
		const decoded: any = jwtDecode(token);
		if (!decoded.exp) return true;

		// Add a 5-minute margin to detect tokens that are about to expire
		const currentTime = Date.now() / 1000;
		const expiryWithMargin = decoded.exp - 300; // 5 minutes in seconds

		return currentTime > expiryWithMargin;
	} catch {
		return true;
	}
};

// Track token refresh state
let isRefreshingToken = false;
const refreshSubscribers: Array<(token: string | null) => void> = [];

// Helper to subscribe to token refresh
const subscribeTokenRefresh = (callback: (token: string | null) => void) => {
	refreshSubscribers.push(callback);
};

// Helper to notify subscribers with new token
const onTokenRefreshed = (token: string | null) => {
	refreshSubscribers.forEach((callback) => callback(token));
	refreshSubscribers.length = 0;
};

// Modified request interceptor to handle token validation and queuing
apiClient.interceptors.request.use(
	async (config) => {
		// W3C trace context (operations.md 4.1): a retry keeps the trace id and draws a new span id.
		config.traceId = config.traceId ?? randomHex(TRACE_ID_BYTES);
		config.headers['traceparent'] = `00-${config.traceId}-${randomHex(SPAN_ID_BYTES)}-01`;

		// ── Dev / E2E impersonation bypass ──
		// When E2E_MOCK_USER is present in localStorage, skip Bearer token
		// and send the impersonation header instead so the backend
		// ImpersonationMiddleware can create a ClaimsPrincipal.
		try {
			const mockRaw = localStorage.getItem(E2E_MOCK_USER_KEY);
			if (mockRaw) {
				const mockUser = JSON.parse(mockRaw);
				if (mockUser?.isE2EMode && mockUser?.id) {
					config.headers[IMPERSONATION_HEADER] = mockUser.id;
					delete config.headers['Authorization'];
					return config;
				}
			}
		} catch {
			/* ignore parse errors */
		}

		const token = localStorage.getItem(ACCESS_TOKEN_KEY);

		if (token) {
			// Verify token audience before making the request
			if (!hasValidAudience(token)) {
				console.error(`Token has incorrect audience. API call to ${config.url} blocked.`);
				// Return a rejected promise to prevent the request
				return Promise.reject(new Error('Invalid token audience'));
			}

			// Check if token is expired
			if (isTokenExpired(token)) {
				console.log(
					`Access token expired or expiring soon, requesting new token for: ${config.url}`,
				);

				try {
					let newToken: string | null;

					// If already refreshing, wait for that to complete rather than triggering parallel refresh
					if (isRefreshingToken) {
						console.log('Token refresh already in progress, waiting for completion');
						newToken = await new Promise<string | null>((resolve) => {
							subscribeTokenRefresh(resolve);
						});
					} else {
						// Start token refresh process
						isRefreshingToken = true;
						// Notify app to refresh the token using MSAL
						window.dispatchEvent(new CustomEvent('auth-token-refresh-needed'));

						// Wait for token refresh to complete
						await new Promise((resolve) => setTimeout(resolve, 1000));

						// Get the potentially refreshed token
						newToken = localStorage.getItem(ACCESS_TOKEN_KEY);

						// Clear refreshing flag and notify subscribers
						isRefreshingToken = false;
						onTokenRefreshed(newToken);
					}

					if (newToken && newToken !== token) {
						console.log('Using newly refreshed token for request');
						config.headers['Authorization'] = `Bearer ${newToken}`;
					} else {
						// If refresh failed or didn't complete in time, use existing token
						console.log('Using existing token for request (refresh may have failed)');
						config.headers['Authorization'] = `Bearer ${token}`;
					}
				} catch (refreshError) {
					console.error('Error during token refresh:', refreshError);
					// Continue with original token if refresh fails
					config.headers['Authorization'] = `Bearer ${token}`;
				}
			} else {
				// Token is valid, use it
				config.headers['Authorization'] = `Bearer ${token}`;
			}
		}

		return config;
	},
	(error) => Promise.reject(error),
);

// Add a response interceptor for error handling
apiClient.interceptors.response.use(
	(response) => response,
	async (error: AxiosError) => {
		// Store original request for retry
		const originalRequest = error.config;

		// Handle 401 Unauthorized errors
		if (error.response && error.response.status === 401 && originalRequest) {
			console.log(
				`Received 401 Unauthorized for ${originalRequest.url}, attempting token refresh`,
			);

			// Don't retry if this is already a retry attempt
			if ((originalRequest as any)._retry) {
				console.log('Request already retried, not attempting another retry');
				return Promise.reject(error);
			}

			try {
				// If we're not already refreshing a token
				if (!isRefreshingToken) {
					isRefreshingToken = true;

					// Try to refresh token
					window.dispatchEvent(new CustomEvent('auth-token-refresh-needed'));

					// Wait for token refresh
					await new Promise((resolve) => setTimeout(resolve, 2000));

					// Get new token
					const newToken = localStorage.getItem(ACCESS_TOKEN_KEY);

					// Reset flag and notify subscribers
					isRefreshingToken = false;
					onTokenRefreshed(newToken);

					// If we have a new token, retry the request
					if (newToken) {
						console.log('Token refreshed, retrying original request with new token');
						// Mark this as a retry attempt
						(originalRequest as any)._retry = true;
						// Update authorization header
						originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
						// Retry the request
						return axios(originalRequest);
					}
				} else {
					// Already refreshing, wait for new token then retry request
					console.log(
						'Token refresh already in progress, waiting before retrying request',
					);
					const newToken = await new Promise<string | null>((resolve) => {
						subscribeTokenRefresh(resolve);
					});

					if (newToken) {
						(originalRequest as any)._retry = true;
						originalRequest.headers['Authorization'] = `Bearer ${newToken}`;
						return axios(originalRequest);
					}
				}

				// If token refresh failed, clear tokens and trigger reauth
				console.log('Token refresh failed or token not available, clearing tokens');
				localStorage.removeItem(ACCESS_TOKEN_KEY);
				localStorage.removeItem(ID_TOKEN_KEY);
				window.dispatchEvent(new CustomEvent('auth-token-error'));
			} catch (retryError) {
				console.error('Error retrying request after token refresh:', retryError);
				window.dispatchEvent(new CustomEvent('auth-token-error'));
			}
		}

		return Promise.reject(error);
	},
);

export default apiClient;
