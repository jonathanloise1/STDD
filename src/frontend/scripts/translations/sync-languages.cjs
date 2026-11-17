/**
 * Sync translations between the two supported languages (en, it).
 * Keys that exist in only one of them are copied to the other (add-only),
 * so a translator can then replace the copied text.
 */

const fs = require('fs');
const path = require('path');

const LOCALES_DIR = path.join(__dirname, '../../public/locales');
const PRIMARY_LANG = 'en';
const SECONDARY_LANG = 'it';

const NAMESPACES = [
	'common',
	'menu',
	'dashboard',
	'organization',
	'tasks',
	'team',
	'settings',
	'audit',
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

	console.log('📋 Fill missing EN keys from IT');

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

	console.log('📋 Fill missing IT keys from EN');
	let totalItAdded = 0;
	for (const ns of NAMESPACES) {
		const itPath = path.join(LOCALES_DIR, SECONDARY_LANG, `${ns}.json`);
		const enPath = path.join(LOCALES_DIR, PRIMARY_LANG, `${ns}.json`);
		const itData = loadJson(itPath);
		const enData = loadJson(enPath);
		const addedCount = mergeDeepAddOnly(itData, enData);
		if (addedCount > 0) {
			saveJson(itPath, itData);
			console.log(`   ✅ ${ns}.json: Added ${addedCount} keys from EN to IT`);
			totalItAdded += addedCount;
		}
	}
	console.log(`   📊 Total: ${totalItAdded} keys added to IT from EN\n`);

	console.log('✅ Sync complete!');
}

// Run
syncLanguages();
