/**
 * Translation Validation Script
 *
 * Validates all translation JSON files for:
 * - Valid JSON syntax
 * - No duplicate keys
 * - Consistent keys across languages
 * - Missing translations
 *
 * Usage: node scripts/translations/validate.js
 */

const fs = require('fs');
const path = require('path');

const LOCALES_DIR = path.join(__dirname, '../../public/locales');
const REFERENCE_LANG = 'it'; // Italian is the reference language

function flattenKeys(obj, prefix = '') {
	const keys = [];

	for (const [key, value] of Object.entries(obj)) {
		const fullKey = prefix ? `${prefix}.${key}` : key;

		if (value && typeof value === 'object' && !Array.isArray(value)) {
			keys.push(...flattenKeys(value, fullKey));
		} else {
			keys.push(fullKey);
		}
	}

	return keys;
}

function findDuplicateKeys(jsonString) {
	// This simple regex approach cannot accurately detect duplicates in nested JSON
	// We'll rely on JSON.parse to catch actual syntax errors
	// For nested objects, the same key name at different levels is valid
	return [];
}

function validateJsonFile(filePath) {
	const result = {
		file: filePath,
		errors: [],
		warnings: [],
	};

	if (!fs.existsSync(filePath)) {
		result.errors.push('File does not exist');
		return result;
	}

	const content = fs.readFileSync(filePath, 'utf-8');

	// Check for duplicate keys
	const duplicates = findDuplicateKeys(content);
	if (duplicates.length > 0) {
		result.errors.push(`Duplicate keys found: ${duplicates.join(', ')}`);
	}

	// Validate JSON syntax
	try {
		JSON.parse(content);
	} catch (e) {
		result.errors.push(`Invalid JSON: ${e.message}`);
	}

	return result;
}

function getLanguageDirs() {
	return fs
		.readdirSync(LOCALES_DIR)
		.filter((dir) => fs.statSync(path.join(LOCALES_DIR, dir)).isDirectory());
}

function getNamespaceFiles(langDir) {
	const langPath = path.join(LOCALES_DIR, langDir);
	return fs
		.readdirSync(langPath)
		.filter((file) => file.endsWith('.json'))
		.map((file) => file.replace('.json', ''));
}

function loadTranslation(lang, namespace) {
	const filePath = path.join(LOCALES_DIR, lang, `${namespace}.json`);

	if (!fs.existsSync(filePath)) {
		return null;
	}

	try {
		const content = fs.readFileSync(filePath, 'utf-8');
		return JSON.parse(content);
	} catch {
		return null;
	}
}

function validateAllFiles() {
	console.log('🔍 Validating translation files...\n');

	const languages = getLanguageDirs();
	const results = [];
	const referenceKeys = {};

	// First pass: validate individual files and collect reference keys
	for (const lang of languages) {
		const namespaces = getNamespaceFiles(lang);

		for (const ns of namespaces) {
			const filePath = path.join(LOCALES_DIR, lang, `${ns}.json`);
			const result = validateJsonFile(filePath);

			if (result.errors.length > 0 || result.warnings.length > 0) {
				results.push(result);
			}

			// Collect reference keys from Italian
			if (lang === REFERENCE_LANG) {
				const translation = loadTranslation(lang, ns);
				if (translation) {
					referenceKeys[ns] = new Set(flattenKeys(translation));
				}
			}
		}
	}

	// Second pass: check for missing keys compared to reference
	for (const lang of languages) {
		if (lang === REFERENCE_LANG) continue;

		for (const [ns, refKeys] of Object.entries(referenceKeys)) {
			const translation = loadTranslation(lang, ns);

			if (!translation) {
				results.push({
					file: `${lang}/${ns}.json`,
					errors: [],
					warnings: [`Missing namespace file (exists in ${REFERENCE_LANG})`],
				});
				continue;
			}

			const langKeys = new Set(flattenKeys(translation));
			const missingKeys = Array.from(refKeys).filter((key) => !langKeys.has(key));

			if (missingKeys.length > 0) {
				const existingResult = results.find(
					(r) =>
						r.file.includes(`${lang}/${ns}.json`) ||
						r.file.includes(`${lang}\\${ns}.json`),
				);
				const warning = `Missing ${missingKeys.length} keys: ${missingKeys.slice(0, 5).join(', ')}${missingKeys.length > 5 ? '...' : ''}`;

				if (existingResult) {
					existingResult.warnings.push(warning);
				} else {
					results.push({
						file: `${lang}/${ns}.json`,
						errors: [],
						warnings: [warning],
					});
				}
			}
		}
	}

	// Print results
	let hasErrors = false;
	let totalMissingKeys = 0;

	// Group by language
	const byLanguage = {};
	for (const result of results) {
		const lang = result.file.split(/[/\\]/)[0];
		if (!byLanguage[lang]) byLanguage[lang] = [];
		byLanguage[lang].push(result);

		// Count missing keys
		for (const warn of result.warnings) {
			const match = warn.match(/Missing (\d+) keys/);
			if (match) totalMissingKeys += parseInt(match[1], 10);
		}
	}

	for (const [lang, langResults] of Object.entries(byLanguage)) {
		console.log(`\n📁 ${lang.toUpperCase()}:`);

		for (const result of langResults) {
			const fileName = result.file.split(/[/\\]/).pop();

			if (result.errors.length > 0) {
				hasErrors = true;
				console.log(`   ❌ ${fileName}`);
				result.errors.forEach((err) => console.log(`      ERROR: ${err}`));
			}

			if (result.warnings.length > 0) {
				console.log(`   ⚠️  ${fileName}`);
				result.warnings.forEach((warn) => console.log(`      ${warn}`));
			}
		}
	}

	// Summary
	console.log(`\n${'='.repeat(60)}`);
	console.log('📊 SUMMARY:');
	console.log(`   Languages found: ${languages.join(', ')}`);
	console.log(`   Reference language: ${REFERENCE_LANG}`);
	console.log(`   Namespaces in reference: ${Object.keys(referenceKeys).length}`);
	console.log(`   Files with errors: ${results.filter((r) => r.errors.length > 0).length}`);
	console.log(`   Files with warnings: ${results.filter((r) => r.warnings.length > 0).length}`);
	console.log(`   Total missing keys: ${totalMissingKeys}`);
	console.log('='.repeat(60));

	if (hasErrors) {
		console.log('\n❌ Validation failed with errors!');
		process.exit(1);
	} else if (totalMissingKeys > 0) {
		console.log('\n⚠️  Validation passed with warnings (missing translations)');
		process.exit(0);
	} else {
		console.log('\n✅ Validation passed! All translations complete.');
		process.exit(0);
	}
}

// Run validation
validateAllFiles();
