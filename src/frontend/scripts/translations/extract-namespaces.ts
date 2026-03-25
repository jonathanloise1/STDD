/**
 * Translation Extraction Script
 *
 * Extracts nested sections from translation.json into separate namespace files.
 *
 * Usage: npx ts-node scripts/translations/extract-namespaces.ts
 */

import * as fs from 'fs';
import * as path from 'path';

const LOCALES_DIR = path.join(__dirname, '../../public/locales');

// Extraction rules: map translation.json sections to new namespace files
interface ExtractionRule {
	/** Pattern to match keys (supports nested keys like 'legal.*') */
	pattern: string | RegExp;
	/** Target namespace file (without .json extension) */
	namespace: string;
	/** Whether to strip the pattern prefix from keys */
	stripPrefix?: boolean;
}

const EXTRACTION_RULES: ExtractionRule[] = [
	// Nested sections with their own namespace
	{ pattern: /^legal\./, namespace: 'legal', stripPrefix: true },
	{ pattern: /^subscription\./, namespace: 'subscription', stripPrefix: true },
	{ pattern: /^team\./, namespace: 'team', stripPrefix: true },
	{ pattern: /^profile\./, namespace: 'profile', stripPrefix: true },
	{ pattern: /^media\./, namespace: 'profile', stripPrefix: true },
	{ pattern: /^billing\./, namespace: 'billing', stripPrefix: true },
	{ pattern: /^settings\./, namespace: 'settings', stripPrefix: true },
	{ pattern: /^messages\./, namespace: 'messages', stripPrefix: true },
	{ pattern: /^timesheets\./, namespace: 'timesheets', stripPrefix: true },
	{ pattern: /^contracts\./, namespace: 'contracts', stripPrefix: true },
	{ pattern: /^features\./, namespace: 'features', stripPrefix: true },
	{ pattern: /^roles\./, namespace: 'features', stripPrefix: true },
	{ pattern: /^permissions\./, namespace: 'features', stripPrefix: true },
	{ pattern: /^organization\./, namespace: 'organization', stripPrefix: true },
	{ pattern: /^common\./, namespace: 'common', stripPrefix: true },
];

// Keys that should go to specific namespaces (flat keys)
const FLAT_KEY_MAPPINGS: Record<string, string[]> = {
	common: [
		'Back',
		'Save',
		'Cancel',
		'Edit',
		'Delete',
		'Create',
		'Update',
		'Search',
		'Yes',
		'No',
		'OK',
		'Close',
		'Submit',
		'Loading',
		'Error',
		'Success',
		'Name',
		'Email',
		'Phone',
		'Address',
		'City',
		'State',
		'Zip',
		'Country',
		'Status',
		'Actions',
		'Details',
		'Description',
		'Title',
		'Type',
		'Date',
		'Upload',
		'Download',
		'Remove',
		'Add',
		'View',
		'Filter',
		'Sort',
		'Showing',
		'to',
		'of',
		'items',
		'Total',
		'Amount',
		'Note',
		'Notes',
		'Saving...',
		'Submitting...',
		'Loading...',
		'Processing...',
		'Active',
		'Inactive',
		'Pending',
		'Completed',
		'Draft',
		'Sent',
		'Approved',
		'Rejected',
		'First Name',
		'Last Name',
		'Full Name',
		'Role',
		'Member',
		'Owner',
		'Manager',
		'Viewer',
		'Remote',
		'On-Site',
		'Hybrid',
		'Available',
		'Unavailable',
		'days',
		'months',
		'years',
		'Hours',
		'Minutes',
	],
	dashboard: [
		'Dashboard',
		'Dashboards',
		'Dashboard Overview',
		'Company Dashboard',
		'Overview',
		'Quick Actions',
		'Back to Dashboard',
	],
	organization: [
		'Organizations',
		'Organization',
		'Company',
		'Company Name',
		'Create Organization',
		'Create New Organization',
		'Edit Organization',
		'Update Organization',
		'Organization Details',
		'Organization Name',
		'Select Organization',
		'No Organization Selected',
		'Go to Organizations',
		'No Organizations Found',
		'Create new company',
		'SDI Code',
		'VAT Number',
		'VAT',
		'Fiscal Code',
		'Billing Email',
		'Certified Email',
		'Country Code',
		'Billing Name',
		'IBAN',
	],
	projects: [
		'Projects',
		'Project',
		'My Projects',
		'Create Project',
		'Create New Project',
		'Project Name',
		'Project Code',
		'Project Details',
		'Project Description',
		'Milestones',
		'Milestone',
		'Create Milestone',
		'Milestone Details',
		'Budget',
		'Total budget',
		'Total spent',
		'Remaining budget',
		'Spending',
		'Collaborators',
		'Collaborator',
		'Add Collaborator',
		'Remove Collaborator',
		'Due in',
		'Deadline',
		'DueDate',
		'Until',
		'Configuration',
		'Activities',
		'Weekly',
		'Monthly',
		'Yearly',
	],
	contracts: [
		'Contracts',
		'Contract',
		'Create Contract',
		'Contract Details',
		'Contract List',
		'View Contract',
		'Contract Status',
		'Contract Summary',
		'Contract Terms',
		'Start Contract',
		'Contract Duration',
		'Contract Value',
		'Manage Contracts',
		'Payment Terms',
		'Trial Period',
		'Notice Period',
		'Auto Renewal',
		'Purchased Hours',
		'Purchased Days',
		'Hourly Rate',
		'Daily Rate',
		'Start Date',
		'End Date',
		'Work Mode',
		'Scope',
		'Services',
		'Send for Signature',
		'Download Signed',
		'Download Evidence',
		'Signature Expiration',
		'Signature Expiration Date',
		'Signing User',
	],
	talents: [
		'Talents',
		'Talent',
		'Talent Name',
		'AI Search',
		'My Talent',
		'Search Talents',
		'View Profile',
		'Talent Profile',
		'Talent Details',
		'AI Talents Search',
		'Classic Talents Search',
		'Classic Search',
		'Keyword Search',
		'Skills',
		'Availability',
		'Experience',
		'Rating',
		'Hourly Rate',
		'Remote',
		'On-Site',
		'Hybrid',
		'Available From',
		'Available Immediately',
		'Saved Searches',
		'Save Search',
		'Create Saved Search',
	],
	timesheets: [
		'Timesheets',
		'Timesheet',
		'Create Timesheet',
		'Edit Timesheet',
		'Timesheet Details',
		'View Timesheets',
		'No Timesheets Found',
		'Time Entries',
		'Add Entry',
		'Month',
		'Current Month',
		'Total Hours',
		'Submitted',
		'Not Submitted',
	],
	team: [
		'Team',
		'Team Management',
		'Members',
		'Team Member',
		'Team Members',
		'Invite',
		'Invite Collaborator',
		'Invite new members',
		'Pending Invitations',
	],
	profile: [
		'Profile',
		'My Profile',
		'About',
		'About me',
		'BioHtml',
		'Personal Statement',
		'What Looking For',
		'Online Visibility',
		'Visible',
		'Hidden',
		'Location',
		'Designation',
	],
};

function flattenObject(obj: Record<string, unknown>, prefix = ''): Record<string, unknown> {
	const result: Record<string, unknown> = {};

	for (const [key, value] of Object.entries(obj)) {
		const fullKey = prefix ? `${prefix}.${key}` : key;

		if (value && typeof value === 'object' && !Array.isArray(value)) {
			Object.assign(result, flattenObject(value as Record<string, unknown>, fullKey));
		} else {
			result[fullKey] = value;
		}
	}

	return result;
}

function unflattenObject(obj: Record<string, unknown>): Record<string, unknown> {
	const result: Record<string, unknown> = {};

	for (const [key, value] of Object.entries(obj)) {
		const parts = key.split('.');
		let current = result;

		for (let i = 0; i < parts.length - 1; i++) {
			const part = parts[i];
			if (!(part in current)) {
				current[part] = {};
			}
			current = current[part] as Record<string, unknown>;
		}

		current[parts[parts.length - 1]] = value;
	}

	return result;
}

function extractNamespaces(lang: string): void {
	const translationPath = path.join(LOCALES_DIR, lang, 'translation.json');

	if (!fs.existsSync(translationPath)) {
		console.log(`⚠️  ${lang}/translation.json not found, skipping`);
		return;
	}

	console.log(`\n📂 Processing ${lang}...`);

	const content = fs.readFileSync(translationPath, 'utf-8');
	let translation: Record<string, unknown>;

	try {
		translation = JSON.parse(content);
	} catch (e) {
		console.error(`❌ Failed to parse ${lang}/translation.json:`, e);
		return;
	}

	const flattened = flattenObject(translation);
	const namespaceContents: Record<string, Record<string, unknown>> = {};
	const remainingKeys: Record<string, unknown> = {};

	// Process each flattened key
	for (const [key, value] of Object.entries(flattened)) {
		let matched = false;

		// Check extraction rules for nested sections
		for (const rule of EXTRACTION_RULES) {
			const pattern =
				typeof rule.pattern === 'string'
					? new RegExp(`^${rule.pattern.replace('.', '\\.')}`)
					: rule.pattern;

			if (pattern.test(key)) {
				if (!namespaceContents[rule.namespace]) {
					namespaceContents[rule.namespace] = {};
				}

				const newKey = rule.stripPrefix ? key.replace(pattern, '') : key;

				namespaceContents[rule.namespace][newKey] = value;
				matched = true;
				break;
			}
		}

		// Check flat key mappings
		if (!matched) {
			for (const [ns, keys] of Object.entries(FLAT_KEY_MAPPINGS)) {
				if (keys.includes(key)) {
					if (!namespaceContents[ns]) {
						namespaceContents[ns] = {};
					}
					namespaceContents[ns][key] = value;
					matched = true;
					break;
				}
			}
		}

		// Keep unmatched keys in remaining
		if (!matched) {
			remainingKeys[key] = value;
		}
	}

	// Write namespace files
	for (const [ns, content] of Object.entries(namespaceContents)) {
		const nsPath = path.join(LOCALES_DIR, lang, `${ns}.json`);
		let existingContent: Record<string, unknown> = {};

		// Merge with existing content if file exists
		if (fs.existsSync(nsPath)) {
			try {
				existingContent = JSON.parse(fs.readFileSync(nsPath, 'utf-8'));
			} catch {
				// Ignore parse errors, start fresh
			}
		}

		const merged = { ...existingContent, ...unflattenObject(content) };
		fs.writeFileSync(nsPath, JSON.stringify(merged, null, '\t'), 'utf-8');
		console.log(`   ✅ ${ns}.json: ${Object.keys(content).length} keys`);
	}

	// Write remaining keys back to translation.json (as backup)
	if (Object.keys(remainingKeys).length > 0) {
		const remainingPath = path.join(LOCALES_DIR, lang, 'translation.remaining.json');
		fs.writeFileSync(
			remainingPath,
			JSON.stringify(unflattenObject(remainingKeys), null, '\t'),
			'utf-8',
		);
		console.log(
			`   ⚠️  translation.remaining.json: ${Object.keys(remainingKeys).length} unmatched keys`,
		);
	}
}

function main(): void {
	console.log('🚀 Starting namespace extraction...');
	console.log(`   Source: translation.json`);
	console.log(`   Target: Individual namespace files`);

	const languages = fs
		.readdirSync(LOCALES_DIR)
		.filter((dir) => fs.statSync(path.join(LOCALES_DIR, dir)).isDirectory());

	for (const lang of languages) {
		extractNamespaces(lang);
	}

	console.log('\n✅ Extraction complete!');
	console.log('\n📝 Next steps:');
	console.log('   1. Review generated namespace files');
	console.log('   2. Check translation.remaining.json for unmatched keys');
	console.log('   3. Update components to use new namespaces');
	console.log('   4. Run validation: npx ts-node scripts/translations/validate.ts');
}

main();
