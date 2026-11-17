import apiClient from './apiClient';

export interface UserProfileDto {
	id: string;
	firstName: string;
	lastName: string;
	email: string;
	preferredLanguage: string;
}

const userProfileService = {
	/**
	 * Persists the user's preferred UI language so it survives across sessions.
	 */
	// US-AUTH-08
	updateLanguage: async (language: string): Promise<void> => {
		await apiClient.put('/api/users/me/language', { language });
	},

	/** Fetches the authenticated user's profile. */
	getProfile: async (): Promise<UserProfileDto> => {
		const response = await apiClient.get('/api/users/me/profile');
		return response.data;
	},
};

export default userProfileService;
