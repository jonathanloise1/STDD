/**
 * Sync translations across all languages
 * - First sync EN with IT for missing keys (especially legal)
 * - Then sync all other languages with EN as base
 */

const fs = require('fs');
const path = require('path');

const LOCALES_DIR = path.join(__dirname, '../../public/locales');
const PRIMARY_LANG = 'en';
const SECONDARY_LANG = 'it';
const TARGET_LANGUAGES = ['de', 'fr', 'es', 'pt', 'pl'];

const NAMESPACES = [
	'common',
	'menu',
	'dashboard',
	'organization',
	'projects',
	'contracts',
	'talents',
	'team',
	'invoices',
	'timesheets',
	'messages',
	'profile',
	'settings',
	'billing',
	'legal',
	'subscription',
	'features',
	'validation',
	'errors',
	'quotes',
];

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

function countKeys(obj, prefix = '') {
	let count = 0;
	for (const key of Object.keys(obj)) {
		if (typeof obj[key] === 'object' && obj[key] !== null) {
			count += countKeys(obj[key], `${prefix}${key}.`);
		} else {
			count++;
		}
	}
	return count;
}

function mergeDeepAddOnly(target, source, path = '') {
	let addedCount = 0;

	for (const key of Object.keys(source)) {
		const fullPath = path ? `${path}.${key}` : key;

		if (
			typeof source[key] === 'object' &&
			source[key] !== null &&
			!Array.isArray(source[key])
		) {
			if (!target[key]) {
				target[key] = {};
			}
			addedCount += mergeDeepAddOnly(target[key], source[key], fullPath);
		} else {
			if (target[key] === undefined) {
				target[key] = source[key];
				addedCount++;
			}
		}
	}

	return addedCount;
}

function syncLanguages() {
	console.log('🚀 Syncing translations across languages...\n');

	// Step 1: Sync EN with IT (for legal and other missing keys)
	console.log('📋 Step 1: Sync EN with IT (fill missing EN keys from IT)');

	let totalEnAdded = 0;
	for (const ns of NAMESPACES) {
		const itPath = path.join(LOCALES_DIR, SECONDARY_LANG, `${ns}.json`);
		const enPath = path.join(LOCALES_DIR, PRIMARY_LANG, `${ns}.json`);

		const itData = loadJson(itPath);
		const enData = loadJson(enPath);

		const itKeyCount = countKeys(itData);
		const enKeyCountBefore = countKeys(enData);

		const addedCount = mergeDeepAddOnly(enData, itData);

		if (addedCount > 0) {
			saveJson(enPath, enData);
			console.log(`   ✅ ${ns}.json: Added ${addedCount} keys from IT to EN`);
			totalEnAdded += addedCount;
		}
	}

	console.log(`   📊 Total: ${totalEnAdded} keys added to EN from IT\n`);

	// Step 2: Sync all target languages with EN
	console.log('📋 Step 2: Sync target languages with EN as base\n');

	for (const targetLang of TARGET_LANGUAGES) {
		console.log(`📁 ${targetLang.toUpperCase()}:`);

		const targetDir = path.join(LOCALES_DIR, targetLang);
		if (!fs.existsSync(targetDir)) {
			fs.mkdirSync(targetDir, { recursive: true });
		}

		let totalAdded = 0;

		for (const ns of NAMESPACES) {
			const enPath = path.join(LOCALES_DIR, PRIMARY_LANG, `${ns}.json`);
			const targetPath = path.join(LOCALES_DIR, targetLang, `${ns}.json`);

			const enData = loadJson(enPath);
			const targetData = loadJson(targetPath);

			const addedCount = mergeDeepAddOnly(targetData, enData);

			if (addedCount > 0 || Object.keys(targetData).length > 0) {
				saveJson(targetPath, targetData);
				if (addedCount > 0) {
					console.log(`   ✅ ${ns}.json: Added ${addedCount} keys`);
					totalAdded += addedCount;
				}
			}
		}

		console.log(`   📊 Total: ${totalAdded} keys added\n`);
	}

	console.log('✅ Sync complete!');
}

// Run
syncLanguages();
