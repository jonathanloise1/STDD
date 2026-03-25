/**
 * Script to copy English translations as base for other languages
 * This provides a starting point for translators
 */

const fs = require('fs');
const path = require('path');

const LOCALES_DIR = path.join(__dirname, '../../public/locales');
const SOURCE_LANG = 'en';
const TARGET_LANGS = ['de', 'fr', 'es', 'pt', 'pl'];

// Namespaces to copy (excluding translation.json and translation.remaining.json)
const NAMESPACES = [
	'billing',
	'common',
	'contracts',
	'dashboard',
	'errors',
	'features',
	'invoices',
	'legal',
	'menu',
	'messages',
	'organization',
	'profile',
	'projects',
	'quotes',
	'settings',
	'subscription',
	'talents',
	'team',
	'timesheets',
	'validation',
];

function copyTranslations() {
	console.log('📋 Copying English translations to other languages...\n');

	let totalCopied = 0;
	let totalSkipped = 0;

	for (const lang of TARGET_LANGS) {
		console.log(`\n📁 ${lang.toUpperCase()}:`);

		for (const ns of NAMESPACES) {
			const sourcePath = path.join(LOCALES_DIR, SOURCE_LANG, `${ns}.json`);
			const targetPath = path.join(LOCALES_DIR, lang, `${ns}.json`);

			// Check if source exists
			if (!fs.existsSync(sourcePath)) {
				console.log(`   ⚠️  ${ns}.json: Source not found`);
				continue;
			}

			// Check if target already has content
			if (fs.existsSync(targetPath)) {
				const targetContent = fs.readFileSync(targetPath, 'utf-8').trim();
				const targetObj = JSON.parse(targetContent);

				// If target has more than 0 keys, skip
				if (Object.keys(targetObj).length > 0) {
					console.log(
						`   ⏭️  ${ns}.json: Already has ${Object.keys(targetObj).length} keys, skipping`,
					);
					totalSkipped++;
					continue;
				}
			}

			// Copy from source
			const sourceContent = fs.readFileSync(sourcePath, 'utf-8');
			fs.writeFileSync(targetPath, sourceContent, 'utf-8');

			const sourceObj = JSON.parse(sourceContent);
			const keyCount = countKeys(sourceObj);
			console.log(`   ✅ ${ns}.json: Copied ${keyCount} keys from EN`);
			totalCopied++;
		}
	}

	console.log(`\n${'='.repeat(50)}`);
	console.log(`📊 Summary:`);
	console.log(`   Files copied: ${totalCopied}`);
	console.log(`   Files skipped (already had content): ${totalSkipped}`);
	console.log('\n💡 Next steps:');
	console.log('   1. Send files to translators');
	console.log('   2. Or use a translation service API');
	console.log('   3. English text is now placeholder for all languages');
}

function countKeys(obj, count = 0) {
	for (const value of Object.values(obj)) {
		if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
			count = countKeys(value, count);
		} else {
			count++;
		}
	}
	return count;
}

copyTranslations();
