import { useState, useCallback, useEffect, useRef } from 'react';

interface UseBlobUrlOptions {
	/** The URL to manage (may contain SAS token) */
	url: string | undefined | null;
	/** Callback to get fresh URL when current expires */
	onExpired?: () => Promise<string>;
	/** Whether this is a private URL with SAS token */
	isPrivate?: boolean;
	/** Time buffer before expiry to trigger refresh (ms). Default: 60000 (1 min) */
	refreshBuffer?: number;
}

interface UseBlobUrlResult {
	/** The current valid URL to use */
	currentUrl: string | undefined;
	/** Whether the URL has expired */
	isExpired: boolean;
	/** Whether refresh is in progress */
	isRefreshing: boolean;
	/** Error message if refresh failed */
	error: string | undefined;
	/** Manually trigger refresh */
	refresh: () => Promise<void>;
	/** Time until expiry in milliseconds (null if no expiry or not a SAS URL) */
	timeUntilExpiry: number | null;
}

/**
 * Hook to manage blob URLs, handling SAS token expiry gracefully.
 * Automatically refreshes URLs before they expire if onExpired callback is provided.
 *
 * @example
 * ```tsx
 * // For a video player that needs fresh URLs
 * const { currentUrl, isExpired, refresh } = useBlobUrl({
 *   url: video.fileUrl,
 *   isPrivate: true,
 *   onExpired: async () => {
 *     const freshData = await api.getVideoDetails(video.id);
 *     return freshData.fileUrl;
 *   },
 * });
 *
 * // Handle expired URL in video player
 * <video
 *   src={currentUrl}
 *   onError={() => isExpired && refresh()}
 * />
 * ```
 */
export function useBlobUrl({
	url,
	onExpired,
	isPrivate = false,
	refreshBuffer = 60000, // 1 minute before expiry
}: UseBlobUrlOptions): UseBlobUrlResult {
	const [currentUrl, setCurrentUrl] = useState<string | undefined>(url ?? undefined);
	const [isExpired, setIsExpired] = useState(false);
	const [isRefreshing, setIsRefreshing] = useState(false);
	const [error, setError] = useState<string | undefined>();
	const [timeUntilExpiry, setTimeUntilExpiry] = useState<number | null>(null);

	const timerRef = useRef<ReturnType<typeof setTimeout>>(undefined);
	const checkIntervalRef = useRef<ReturnType<typeof setInterval>>(undefined);
	const onExpiredRef = useRef(onExpired);

	// Keep onExpired ref up to date
	useEffect(() => {
		onExpiredRef.current = onExpired;
	}, [onExpired]);

	/**
	 * Extract expiry date from SAS URL
	 */
	const getSasExpiry = useCallback((sasUrl: string): Date | null => {
		if (!sasUrl) return null;

		try {
			const urlObj = new URL(sasUrl);
			const se = urlObj.searchParams.get('se');
			if (se) {
				return new Date(se);
			}
		} catch {
			// Not a valid URL
		}
		return null;
	}, []);

	/**
	 * Check if URL is expired
	 */
	const checkExpiry = useCallback(
		(urlToCheck: string): boolean => {
			if (!isPrivate) return false;

			const expiry = getSasExpiry(urlToCheck);
			if (!expiry) return false;

			return new Date() > expiry;
		},
		[isPrivate, getSasExpiry],
	);

	/**
	 * Calculate time until expiry
	 */
	const calculateTimeUntilExpiry = useCallback(
		(urlToCheck: string): number | null => {
			if (!isPrivate) return null;

			const expiry = getSasExpiry(urlToCheck);
			if (!expiry) return null;

			const remaining = expiry.getTime() - Date.now();
			return remaining > 0 ? remaining : 0;
		},
		[isPrivate, getSasExpiry],
	);

	/**
	 * Refresh the URL by calling onExpired callback
	 */
	const refresh = useCallback(async () => {
		if (isRefreshing) return;
		if (!onExpiredRef.current) {
			console.warn('[useBlobUrl] Cannot refresh: no onExpired callback provided');
			return;
		}

		setIsRefreshing(true);
		setError(undefined);

		try {
			const freshUrl = await onExpiredRef.current();
			setCurrentUrl(freshUrl);
			setIsExpired(false);
			setTimeUntilExpiry(calculateTimeUntilExpiry(freshUrl));
		} catch (err) {
			const errorMessage = err instanceof Error ? err.message : 'Failed to refresh URL';
			setError(errorMessage);
			console.error('[useBlobUrl] Refresh failed:', err);
		} finally {
			setIsRefreshing(false);
		}
	}, [isRefreshing, calculateTimeUntilExpiry]);

	// Update when URL prop changes
	useEffect(() => {
		if (url) {
			setCurrentUrl(url);
			setIsExpired(checkExpiry(url));
			setTimeUntilExpiry(calculateTimeUntilExpiry(url));
			setError(undefined);
		} else {
			setCurrentUrl(undefined);
			setIsExpired(false);
			setTimeUntilExpiry(null);
		}
	}, [url, checkExpiry, calculateTimeUntilExpiry]);

	// Set up automatic refresh before expiry
	useEffect(() => {
		if (!currentUrl || !isPrivate || !onExpiredRef.current) return;

		const expiry = getSasExpiry(currentUrl);
		if (!expiry) return;

		const now = Date.now();
		const expiryTime = expiry.getTime();
		const timeToRefresh = expiryTime - now - refreshBuffer;

		// Clear existing timer
		if (timerRef.current) {
			clearTimeout(timerRef.current);
		}

		if (timeToRefresh > 0) {
			// Schedule refresh before expiry
			timerRef.current = setTimeout(() => {
				console.log('[useBlobUrl] Auto-refreshing before expiry');
				refresh();
			}, timeToRefresh);
		} else if (now < expiryTime) {
			// Less than buffer time remaining, refresh now
			refresh();
		} else {
			// Already expired
			setIsExpired(true);
		}

		return () => {
			if (timerRef.current) {
				clearTimeout(timerRef.current);
			}
		};
	}, [currentUrl, isPrivate, getSasExpiry, refreshBuffer, refresh]);

	// Update timeUntilExpiry periodically
	useEffect(() => {
		if (!currentUrl || !isPrivate) return;

		const updateTimeRemaining = () => {
			const remaining = calculateTimeUntilExpiry(currentUrl);
			setTimeUntilExpiry(remaining);

			if (remaining !== null && remaining <= 0) {
				setIsExpired(true);
			}
		};

		// Update every 10 seconds
		checkIntervalRef.current = setInterval(updateTimeRemaining, 10000);

		return () => {
			if (checkIntervalRef.current) {
				clearInterval(checkIntervalRef.current);
			}
		};
	}, [currentUrl, isPrivate, calculateTimeUntilExpiry]);

	return {
		currentUrl,
		isExpired,
		isRefreshing,
		error,
		refresh,
		timeUntilExpiry,
	};
}

export default useBlobUrl;
