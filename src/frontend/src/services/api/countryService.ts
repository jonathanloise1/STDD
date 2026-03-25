import apiClient from './apiClient';

export interface Country {
	isoCode: string;
	name: string;
	phonePrefix: string;
	flagEmoji: string;
}

class CountryService {
	/**
	 * Get all supported countries for billing and phone validation
	 */
	async getCountries(): Promise<Country[]> {
		const response = await apiClient.get<Country[]>('/api/countries');
		return response.data;
	}

	/**
	 * Get EU countries for phone validation
	 */
	async getEuCountries(): Promise<Country[]> {
		const response = await apiClient.get<Country[]>('/api/countries/eu');
		return response.data;
	}
}

export default new CountryService();
