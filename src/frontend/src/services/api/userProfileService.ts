// US-AUTH-08: User profile API service — language preference persistence
import apiClient from './apiClient';

export interface UserProfileDto {
	id: string;
	firstName: string;
	lastName: string;
	email: string;
	preferredLanguage: string;
	hasCompletedOnboarding: boolean;
}

const userProfileService = {
	/**
	 * US-AUTH-08: Updates the user's preferred UI language.
	 * Persists the choice to the backend so it survives across sessions.
	 */
	updateLanguage: async (language: string): Promise<void> => {
		await apiClient.put('/api/users/me/language', { language });
	},

	/** Fetches the authenticated user's profile. */
	getProfile: async (): Promise<UserProfileDto> => {
		const response = await apiClient.get('/api/users/me/profile');
		return response.data;
	},

	// US-ONBOARD-01: Marks the user's onboarding as completed
	completeOnboarding: async (): Promise<void> => {
		await apiClient.post('/api/users/me/complete-onboarding');
	},
};

export default userProfileService;
