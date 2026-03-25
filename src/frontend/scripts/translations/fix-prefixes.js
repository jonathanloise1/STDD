/**
 * Fix translation key prefixes in components
 * When a component uses useTranslation(['profile', ...]), it should use t('key') not t('profile.key')
 * Same for other namespaces
 */

const fs = require('fs');
const path = require('path');
const glob = require('glob');

const SRC_DIR = path.join(__dirname, '../../src');

// Pattern to find useTranslation with namespace array
const USE_TRANSLATION_REGEX = /useTranslation\(\[['"](\w+)['"]/g;

// Pattern to find t('namespace.key') calls
const T_CALL_REGEX = /t\(['"](\w+)\.([^'"]+)['"]/g;

function processFile(filePath) {
	const content = fs.readFileSync(filePath, 'utf-8');

	// Find the primary namespace from useTranslation
	const useTransMatch = content.match(/useTranslation\(\[['"](\w+)['"]/);
	if (!useTransMatch) {
		return { file: filePath, changes: 0 };
	}

	const primaryNs = useTransMatch[1];
	let newContent = content;
	let changes = 0;

	// Replace t('namespace.key') with t('key') when namespace matches primaryNs
	newContent = newContent.replace(
		/t\(['"](\w+)\.([^'"]+)['"](,\s*\{[^}]*\})?\)/g,
		(match, ns, key, opts) => {
			if (ns === primaryNs) {
				changes++;
				const optsPart = opts || '';
				return `t('${key}'${optsPart})`;
			}
			return match;
		},
	);

	if (changes > 0) {
		fs.writeFileSync(filePath, newContent, 'utf-8');
		console.log(`✅ ${path.relative(SRC_DIR, filePath)}: Fixed ${changes} translation keys`);
	}

	return { file: filePath, changes };
}

function main() {
	console.log('🔧 Fixing translation key prefixes...\n');

	// Find all TypeScript/TSX files in src
	const files = glob.sync(path.join(SRC_DIR, '**/*.{ts,tsx}'), {
		ignore: ['**/node_modules/**', '**/*.d.ts'],
	});

	let totalChanges = 0;
	let filesChanged = 0;

	for (const file of files) {
		const result = processFile(file);
		if (result.changes > 0) {
			totalChanges += result.changes;
			filesChanged++;
		}
	}

	console.log(`\n📊 Summary: Fixed ${totalChanges} keys in ${filesChanged} files`);
}

main();
