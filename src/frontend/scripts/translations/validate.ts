/**
 * Translation Validation Script
 *
 * Validates all translation JSON files for:
 * - Valid JSON syntax
 * - No duplicate keys
 * - Consistent keys across languages
 * - Missing translations
 *
 * Usage: npx ts-node scripts/translations/validate.ts
 */

import * as fs from 'fs';
import * as path from 'path';

const LOCALES_DIR = path.join(__dirname, '../../public/locales');
const REFERENCE_LANG = 'it'; // Italian is the reference language

interface ValidationResult {
	file: string;
	errors: string[];
	warnings: string[];
}

interface TranslationKeys {
	[namespace: string]: Set<string>;
}

function flattenKeys(obj: Record<string, unknown>, prefix = ''): string[] {
	const keys: string[] = [];

	for (const [key, value] of Object.entries(obj)) {
		const fullKey = prefix ? `${prefix}.${key}` : key;

		if (value && typeof value === 'object' && !Array.isArray(value)) {
			keys.push(...flattenKeys(value as Record<string, unknown>, fullKey));
		} else {
			keys.push(fullKey);
		}
	}

	return keys;
}

function findDuplicateKeys(jsonString: string): string[] {
	const duplicates: string[] = [];
	const keyRegex = /"([^"]+)":/g;
	const keyCount: Record<string, number> = {};

	let match;
	while ((match = keyRegex.exec(jsonString)) !== null) {
		const key = match[1];
		keyCount[key] = (keyCount[key] || 0) + 1;
		if (keyCount[key] === 2) {
			duplicates.push(key);
		}
	}

	return duplicates;
}

function validateJsonFile(filePath: string): ValidationResult {
	const result: ValidationResult = {
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
		result.errors.push(`Invalid JSON: ${(e as Error).message}`);
	}

	return result;
}

function getLanguageDirs(): string[] {
	return fs
		.readdirSync(LOCALES_DIR)
		.filter((dir) => fs.statSync(path.join(LOCALES_DIR, dir)).isDirectory());
}

function getNamespaceFiles(langDir: string): string[] {
	const langPath = path.join(LOCALES_DIR, langDir);
	return fs
		.readdirSync(langPath)
		.filter((file) => file.endsWith('.json'))
		.map((file) => file.replace('.json', ''));
}

function loadTranslation(lang: string, namespace: string): Record<string, unknown> | null {
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

function validateAllFiles(): void {
	console.log('🔍 Validating translation files...\n');

	const languages = getLanguageDirs();
	const results: ValidationResult[] = [];
	const referenceKeys: TranslationKeys = {};

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
				const existingResult = results.find((r) => r.file === `${lang}/${ns}.json`);
				const warning = `Missing ${missingKeys.length} keys: ${missingKeys.slice(0, 5).join(', ')}${missingKeys.length > 5 ? '...' : ''}`;

				if (existingResult) {
					existingResult.warnings.push(warning);
				} else {
					results.push({
						file: path.join(LOCALES_DIR, lang, `${ns}.json`),
						errors: [],
						warnings: [warning],
					});
				}
			}
		}
	}

	// Print results
	let hasErrors = false;

	for (const result of results) {
		const relativePath = result.file.replace(LOCALES_DIR + path.sep, '');

		if (result.errors.length > 0) {
			hasErrors = true;
			console.log(`❌ ${relativePath}`);
			result.errors.forEach((err) => console.log(`   ERROR: ${err}`));
		}

		if (result.warnings.length > 0) {
			console.log(`⚠️  ${relativePath}`);
			result.warnings.forEach((warn) => console.log(`   WARNING: ${warn}`));
		}
	}

	// Summary
	console.log(`\n${'='.repeat(50)}`);
	console.log('📊 Summary:');
	console.log(`   Languages: ${languages.join(', ')}`);
	console.log(`   Reference language: ${REFERENCE_LANG}`);
	console.log(`   Files with errors: ${results.filter((r) => r.errors.length > 0).length}`);
	console.log(`   Files with warnings: ${results.filter((r) => r.warnings.length > 0).length}`);

	if (hasErrors) {
		console.log('\n❌ Validation failed!');
		process.exit(1);
	} else {
		console.log('\n✅ Validation passed!');
	}
}

// Run validation
validateAllFiles();
