/**
 * Migrate remaining keys from translation.json to appropriate namespaces
 * This script analyzes keys and distributes them to the correct namespace files
 */

const fs = require('fs');
const path = require('path');

const LOCALES_DIR = path.join(__dirname, '../../public/locales');
const LANGUAGES = ['it', 'en'];

// Mapping of key patterns to namespaces
const NAMESPACE_MAPPING = {
	// Menu related
	menu: 'menu',
	Dashboard: 'dashboard',
	Dashboards: 'dashboard',
	'Dashboard Overview': 'dashboard',
	'Revenue Overview': 'dashboard',
	'Company Dashboard': 'dashboard',
	'Freelance Dashboard': 'dashboard',

	// Navigation/Common
	Back: 'common',
	Notification: 'common',
	DarkMode: 'common',
	LightMode: 'common',
	'Heat Map': 'common',
	'Tree Map': 'common',
	Hooks: 'common',
	Name: 'common',
	Email: 'common',
	Phone: 'common',
	Address: 'common',
	City: 'common',
	State: 'common',
	Zip: 'common',
	Upload: 'common',
	Rate: 'common',
	'Hourly Rate': 'common',
	'Email Address': 'common',
	About: 'common',
	Cancel: 'common',
	Save: 'common',
	'Save Changes': 'common',
	Edit: 'common',
	Delete: 'common',
	Remove: 'common',
	Add: 'common',
	Create: 'common',
	Update: 'common',
	Search: 'common',
	Filter: 'common',
	Sort: 'common',
	Actions: 'common',
	Status: 'common',
	Date: 'common',
	Time: 'common',
	Title: 'common',
	Description: 'common',
	Notes: 'common',
	Details: 'common',
	Summary: 'common',
	Total: 'common',
	Subtotal: 'common',
	Amount: 'common',
	Price: 'common',
	Quantity: 'common',
	Unit: 'common',
	Type: 'common',
	Category: 'common',
	Tags: 'common',
	Label: 'common',
	Value: 'common',
	Yes: 'common',
	No: 'common',
	OK: 'common',
	Confirm: 'common',
	Close: 'common',
	Open: 'common',
	View: 'common',
	Download: 'common',
	Print: 'common',
	Export: 'common',
	Import: 'common',
	Loading: 'common',
	Saving: 'common',
	Processing: 'common',
	Success: 'common',
	Error: 'common',
	Warning: 'common',
	Info: 'common',
	items: 'common',
	Showing: 'common',
	to: 'common',
	of: 'common',
	days: 'common',
	months: 'common',
	years: 'common',
	day: 'common',
	month: 'common',
	year: 'common',
	hour: 'common',
	hours: 'common',
	minute: 'common',
	minutes: 'common',
	Timeline: 'common',
	Completed: 'common',
	Pending: 'common',
	Active: 'common',
	Inactive: 'common',
	Approved: 'common',
	Rejected: 'common',
	Draft: 'common',
	Sent: 'common',
	Expired: 'common',
	Terminated: 'common',
	Owner: 'common',
	Viewer: 'common',
	Manager: 'common',
	External: 'common',
	Internal: 'common',
	Remote: 'common',
	'On-Site': 'common',
	'On-site': 'common',
	Hybrid: 'common',
	Location: 'common',

	// Organization
	organization: 'organization',
	Organization: 'organization',
	Organizations: 'organization',
	Company: 'organization',
	'Edit Organization': 'organization',
	'Organization Details': 'organization',
	'Organization Name': 'organization',
	'Billing Name': 'organization',
	'VAT Number': 'organization',
	'Fiscal Code': 'organization',
	IBAN: 'organization',
	'SDI Code': 'organization',
	'Billing Email': 'organization',
	'Contact Information': 'organization',
	Members: 'organization',
	'Invite new members': 'organization',

	// Team
	team: 'team',
	Team: 'team',
	Invite: 'team',
	Role: 'team',
	Roles: 'team',
	Permissions: 'team',

	// Profile
	profile: 'profile',
	Profile: 'profile',
	'My Profile': 'profile',
	'About me': 'profile',
	Designation: 'profile',
	Skills: 'profile',
	Availability: 'profile',
	Portfolio: 'profile',
	Experience: 'profile',
	Education: 'profile',
	Certifications: 'profile',
	Languages: 'profile',
	'Social Links': 'profile',
	Curriculum: 'profile',
	CV: 'profile',
	Resume: 'profile',
	'Introduction Video': 'profile',
	Gallery: 'profile',
	Media: 'profile',

	// Settings
	settings: 'settings',
	Settings: 'settings',
	'Billing Settings': 'settings',
	'Account Settings': 'settings',
	'Notification Settings': 'settings',
	'Privacy Settings': 'settings',
	'Security Settings': 'settings',

	// Projects
	projects: 'projects',
	Project: 'projects',
	Projects: 'projects',
	'My Projects': 'projects',
	Milestones: 'projects',
	Milestone: 'projects',
	'Create Project': 'projects',
	'Edit Project': 'projects',
	'Project Details': 'projects',
	Collaborators: 'projects',

	// Contracts
	contracts: 'contracts',
	Contract: 'contracts',
	Contracts: 'contracts',

	// Talents
	talents: 'talents',
	Talent: 'talents',
	Talents: 'talents',
	'Talent Name': 'talents',
	'AI Search': 'talents',
	'My Talent': 'talents',
	'Interested Companies': 'talents',
	'AI Generator': 'talents',

	// Invoices
	invoices: 'invoices',
	Invoice: 'invoices',
	Invoices: 'invoices',
	Payment: 'invoices',
	Payments: 'invoices',

	// Timesheets
	timesheets: 'timesheets',
	Timesheet: 'timesheets',
	Timesheets: 'timesheets',

	// Messages
	messages: 'messages',
	Message: 'messages',
	Messages: 'messages',

	// Subscription
	subscription: 'subscription',
	Subscription: 'subscription',
	Plan: 'subscription',
	Upgrade: 'subscription',

	// Features
	features: 'features',
	Feature: 'features',

	// Legal
	legal: 'legal',
	Legal: 'legal',
	'Master Agreement': 'legal',
	'Contract Types': 'legal',

	// Billing
	billing: 'billing',
	Billing: 'billing',

	// Quotes (deprecated)
	quotes: 'quotes',
	Quote: 'quotes',
	Quotes: 'quotes',

	// Logout is common
	Logout: 'common',
};

function findNamespace(key) {
	// Check exact match first
	if (NAMESPACE_MAPPING[key]) {
		return NAMESPACE_MAPPING[key];
	}

	// Check if key starts with a known prefix
	const keyLower = key.toLowerCase();

	// Check nested object keys
	if (key === 'organization' || keyLower.startsWith('organization')) return 'organization';
	if (key === 'team' || keyLower.startsWith('team')) return 'team';
	if (key === 'profile' || keyLower.startsWith('profile')) return 'profile';
	if (key === 'settings' || keyLower.startsWith('settings')) return 'settings';
	if (key === 'projects' || keyLower.startsWith('project')) return 'projects';
	if (key === 'contracts' || keyLower.startsWith('contract')) return 'contracts';
	if (key === 'talents' || keyLower.startsWith('talent')) return 'talents';
	if (key === 'invoices' || keyLower.startsWith('invoice')) return 'invoices';
	if (key === 'timesheets' || keyLower.startsWith('timesheet')) return 'timesheets';
	if (key === 'messages' || keyLower.startsWith('message')) return 'messages';
	if (key === 'subscription' || keyLower.startsWith('subscription')) return 'subscription';
	if (key === 'features' || keyLower.startsWith('feature')) return 'features';
	if (key === 'legal' || keyLower.startsWith('legal')) return 'legal';
	if (key === 'billing' || keyLower.startsWith('billing')) return 'billing';
	if (key === 'quotes' || keyLower.startsWith('quote')) return 'quotes';
	if (key === 'menu' || keyLower.startsWith('menu')) return 'menu';
	if (key === 'dashboard' || keyLower.startsWith('dashboard')) return 'dashboard';

	// Keywords in key
	if (keyLower.includes('organization') || keyLower.includes('company')) return 'organization';
	if (keyLower.includes('project') || keyLower.includes('milestone')) return 'projects';
	if (keyLower.includes('contract')) return 'contracts';
	if (keyLower.includes('talent') || keyLower.includes('freelance')) return 'talents';
	if (keyLower.includes('invoice') || keyLower.includes('payment') || keyLower.includes('fiscal'))
		return 'invoices';
	if (keyLower.includes('timesheet')) return 'timesheets';
	if (keyLower.includes('team') || keyLower.includes('member') || keyLower.includes('invite'))
		return 'team';
	if (
		keyLower.includes('profile') ||
		keyLower.includes('skill') ||
		keyLower.includes('portfolio')
	)
		return 'profile';
	if (
		keyLower.includes('subscription') ||
		keyLower.includes('plan') ||
		keyLower.includes('upgrade')
	)
		return 'subscription';
	if (keyLower.includes('legal') || keyLower.includes('agreement') || keyLower.includes('clause'))
		return 'legal';
	if (keyLower.includes('billing') || keyLower.includes('vat') || keyLower.includes('iban'))
		return 'billing';
	if (keyLower.includes('quote')) return 'quotes';

	// Default to common
	return 'common';
}

function loadJson(filePath) {
	try {
		return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
	} catch {
		return {};
	}
}

function saveJson(filePath, data) {
	fs.writeFileSync(filePath, `${JSON.stringify(data, null, '\t')}\n`, 'utf-8');
}

function mergeDeep(target, source) {
	for (const key of Object.keys(source)) {
		if (source[key] && typeof source[key] === 'object' && !Array.isArray(source[key])) {
			if (!target[key]) target[key] = {};
			mergeDeep(target[key], source[key]);
		} else {
			if (!target[key]) {
				target[key] = source[key];
			}
		}
	}
	return target;
}

function migrateLanguage(lang) {
	console.log(`\n📁 ${lang.toUpperCase()}:`);

	const translationPath = path.join(LOCALES_DIR, lang, 'translation.json');

	if (!fs.existsSync(translationPath)) {
		console.log('   ⏭️  No translation.json found, skipping');
		return;
	}

	const translation = loadJson(translationPath);
	const migrated = {};
	const remaining = {};

	// Categorize keys
	for (const [key, value] of Object.entries(translation)) {
		const namespace = findNamespace(key);

		if (!migrated[namespace]) {
			migrated[namespace] = {};
		}

		if (typeof value === 'object' && value !== null) {
			// For nested objects, merge into the namespace
			migrated[namespace][key] = value;
		} else {
			migrated[namespace][key] = value;
		}
	}

	// Merge into existing namespace files
	let totalMigrated = 0;

	for (const [namespace, keys] of Object.entries(migrated)) {
		const nsPath = path.join(LOCALES_DIR, lang, `${namespace}.json`);
		const existing = loadJson(nsPath);

		const keyCount = Object.keys(keys).length;
		const merged = mergeDeep(existing, keys);

		saveJson(nsPath, merged);
		console.log(`   ✅ ${namespace}.json: Added ${keyCount} keys`);
		totalMigrated += keyCount;
	}

	// Remove translation.json after migration
	fs.unlinkSync(translationPath);
	console.log(`   🗑️  Removed translation.json (migrated ${totalMigrated} keys)`);
}

function main() {
	console.log('🚀 Migrating translation.json to namespaces...');

	for (const lang of LANGUAGES) {
		migrateLanguage(lang);
	}

	console.log('\n✅ Migration complete!');
}

main();
