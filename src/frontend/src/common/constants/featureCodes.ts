/**
 * Feature codes for the feature gating system.
 * These codes must match the backend FeatureCodes constants
 * (MyApp.Domain.Entities.Subscriptions.SubscriptionPlanFeature.FeatureCodes).
 *
 * @userstory US-FG-01, US-FG-04, US-FG-05, US-FG-06, US-FG-07, US-FG-08, US-FG-09, US-FG-11, US-FG-12, US-FG-13, US-SUB-02
 */
export const FeatureCodes = {
	// Organization features
	ORGANIZATION_SEND_MESSAGES: 'ORGANIZATION_SEND_MESSAGES',
	ORGANIZATION_SAVED_SEARCHES: 'ORGANIZATION_SAVED_SEARCHES',
	ORGANIZATION_CONTRACTS: 'ORGANIZATION_CONTRACTS',
	ORGANIZATION_QUOTES: 'ORGANIZATION_QUOTES',
} as const;

export type FeatureCode = (typeof FeatureCodes)[keyof typeof FeatureCodes];

/**
 * Feature types
 */
export type FeatureType = 'boolean' | 'limit';

/**
 * Plan types matching backend SubscriptionPlanTypes.
 * Used to identify the user's current subscription tier.
 *
 * @userstory US-FG-01, US-FG-09
 */
export const PlanTypes = {
	// Organization plans
	ORGANIZATION_FREE: 'ORGANIZATION_FREE',
	ORGANIZATION_PLUS: 'ORGANIZATION_PLUS',
	ORGANIZATION_PRO: 'ORGANIZATION_PRO',
} as const;

export type PlanType = (typeof PlanTypes)[keyof typeof PlanTypes];

/**
 * Feature display names for UI rendering.
 * Used by FeatureErrorModal, UpgradePrompt, and other components
 * to show human-readable feature names.
 *
 * @userstory US-FG-02, US-FG-10
 */
export const FeatureNames: Record<FeatureCode, string> = {
	ORGANIZATION_SEND_MESSAGES: 'Send Messages',
	ORGANIZATION_SAVED_SEARCHES: 'Saved Searches',
	ORGANIZATION_CONTRACTS: 'Contracts',
	ORGANIZATION_QUOTES: 'Quotes',
};
