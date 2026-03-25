/**
 * Script to remove duplicate keys from JSON files
 * Keeps the first occurrence of each key (case-insensitive check)
 */

const fs = require('fs');
const path = require('path');

const LOCALES_DIR = path.join(__dirname, '../../public/locales');

function removeDuplicateKeys(obj, seenKeys = new Set(), parentPath = '') {
	if (typeof obj !== 'object' || obj === null || Array.isArray(obj)) {
		return obj;
	}

	const result = {};

	for (const [key, value] of Object.entries(obj)) {
		const fullPath = parentPath ? `${parentPath}.${key}` : key;
		const lowerKey = key.toLowerCase();
		const checkKey = parentPath ? `${parentPath}.${lowerKey}` : lowerKey;

		if (seenKeys.has(checkKey)) {
			console.log(`   Removing duplicate key: "${key}" at ${fullPath}`);
			continue;
		}

		seenKeys.add(checkKey);

		if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
			result[key] = removeDuplicateKeys(value, new Set(), fullPath);
		} else {
			result[key] = value;
		}
	}

	return result;
}

function processFile(filePath) {
	const relativePath = path.relative(LOCALES_DIR, filePath);

	try {
		const content = fs.readFileSync(filePath, 'utf-8');

		// Parse with reviver to detect duplicates during parsing
		const seenKeys = new Set();
		let hasDuplicates = false;

		// First, try to parse and check for issues
		let parsed;
		try {
			parsed = JSON.parse(content);
		} catch (e) {
			console.log(`❌ ${relativePath}: Invalid JSON - ${e.message}`);
			return false;
		}

		// Check for duplicate keys at top level by re-reading the file
		const keyPattern = /^\s*"([^"]+)":/gm;
		const foundKeys = new Map();
		let match;

		while ((match = keyPattern.exec(content)) !== null) {
			const key = match[1];
			const lowerKey = key.toLowerCase();

			if (foundKeys.has(lowerKey) && foundKeys.get(lowerKey) !== key) {
				hasDuplicates = true;
				console.log(`   Found duplicate: "${key}" vs "${foundKeys.get(lowerKey)}"`);
			}
			foundKeys.set(lowerKey, key);
		}

		if (!hasDuplicates) {
			console.log(`✅ ${relativePath}: No duplicates`);
			return true;
		}

		console.log(`🔧 ${relativePath}: Cleaning duplicates...`);

		// Clean the object
		const cleaned = removeDuplicateKeys(parsed);

		// Write back
		fs.writeFileSync(filePath, `${JSON.stringify(cleaned, null, '\t')}\n`, 'utf-8');
		console.log(`   ✅ Cleaned and saved`);

		return true;
	} catch (e) {
		console.log(`❌ ${relativePath}: Error - ${e.message}`);
		return false;
	}
}

function main() {
	console.log('🧹 Cleaning duplicate keys from translation files...\n');

	const languages = fs
		.readdirSync(LOCALES_DIR)
		.filter((dir) => fs.statSync(path.join(LOCALES_DIR, dir)).isDirectory());

	let totalFiles = 0;
	let cleanedFiles = 0;

	for (const lang of languages) {
		console.log(`\n📁 ${lang.toUpperCase()}:`);

		const langDir = path.join(LOCALES_DIR, lang);
		const files = fs.readdirSync(langDir).filter((f) => f.endsWith('.json'));

		for (const file of files) {
			totalFiles++;
			if (processFile(path.join(langDir, file))) {
				cleanedFiles++;
			}
		}
	}

	console.log(`\n${'='.repeat(50)}`);
	console.log(`📊 Processed ${totalFiles} files, ${cleanedFiles} successful`);
}

main();
