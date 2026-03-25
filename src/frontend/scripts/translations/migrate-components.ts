/**
 * Component Migration Script
 *
 * Migrates components to use new namespace imports in useTranslation().
 *
 * Usage: npx ts-node scripts/translations/migrate-components.ts
 */

import * as fs from 'fs';
import * as path from 'path';
import * as glob from 'glob';

const SRC_DIR = path.join(__dirname, '../../src');

// Mapping of translation key patterns to their namespaces
const NAMESPACE_PATTERNS: Record<string, RegExp[]> = {
	legal: [/t\(['"`]legal\./],
	team: [/t\(['"`]team\./],
	subscription: [/t\(['"`]subscription\./],
	features: [/t\(['"`]features\./, /t\(['"`]roles\./, /t\(['"`]permissions\./],
	profile: [/t\(['"`]profile\./, /t\(['"`]media\./],
	billing: [/t\(['"`]billing\./],
	settings: [/t\(['"`]settings\./],
	messages: [/t\(['"`]messages\./],
	timesheets: [/t\(['"`]timesheets\./],
	contracts: [/t\(['"`]contracts\./],
	organization: [/t\(['"`]organization\./],
	common: [/t\(['"`]common\./],
};

interface MigrationResult {
	file: string;
	namespacesDetected: string[];
	modified: boolean;
}

function detectNamespacesInFile(content: string): string[] {
	const namespaces: Set<string> = new Set();

	for (const [namespace, patterns] of Object.entries(NAMESPACE_PATTERNS)) {
		for (const pattern of patterns) {
			if (pattern.test(content)) {
				namespaces.add(namespace);
				break;
			}
		}
	}

	return Array.from(namespaces);
}

function migrateFile(filePath: string): MigrationResult {
	const content = fs.readFileSync(filePath, 'utf-8');
	const result: MigrationResult = {
		file: filePath,
		namespacesDetected: [],
		modified: false,
	};

	// Detect namespaces used in file
	const detectedNamespaces = detectNamespacesInFile(content);
	result.namespacesDetected = detectedNamespaces;

	if (detectedNamespaces.length === 0) {
		return result;
	}

	// Check if useTranslation is already using namespaces
	const useTranslationMatch = content.match(
		/useTranslation\s*\(\s*(\[.*?\]|['"`][^'"`]+['"`])?\s*\)/,
	);

	if (!useTranslationMatch) {
		// No useTranslation found
		return result;
	}

	const currentArg = useTranslationMatch[1];

	// If no argument or empty argument, need to add namespaces
	if (!currentArg || currentArg === '()') {
		const newNamespaces = [...detectedNamespaces, 'common'].filter(
			(v, i, a) => a.indexOf(v) === i,
		);
		const newArg =
			newNamespaces.length === 1
				? `'${newNamespaces[0]}'`
				: `[${newNamespaces.map((ns) => `'${ns}'`).join(', ')}]`;

		const newContent = content.replace(/useTranslation\s*\(\s*\)/, `useTranslation(${newArg})`);

		if (newContent !== content) {
			fs.writeFileSync(filePath, newContent, 'utf-8');
			result.modified = true;
		}
	}

	return result;
}

function main(): void {
	console.log('🔄 Starting component migration...\n');

	const files = glob.sync('**/*.tsx', {
		cwd: SRC_DIR,
		absolute: true,
		ignore: ['**/*.test.tsx', '**/*.stories.tsx'],
	});

	console.log(`Found ${files.length} TSX files\n`);

	const results: MigrationResult[] = [];
	const filesWithNamespaces: MigrationResult[] = [];

	for (const file of files) {
		const result = migrateFile(file);
		results.push(result);

		if (result.namespacesDetected.length > 0) {
			filesWithNamespaces.push(result);
		}
	}

	// Print summary
	console.log('📊 Summary:\n');
	console.log(`Total files scanned: ${files.length}`);
	console.log(`Files using namespaced keys: ${filesWithNamespaces.length}`);
	console.log(`Files modified: ${results.filter((r) => r.modified).length}`);

	console.log('\n📁 Files with namespaced keys:\n');
	for (const result of filesWithNamespaces) {
		const relativePath = path.relative(SRC_DIR, result.file);
		const status = result.modified ? '✅ migrated' : '⚠️  needs review';
		console.log(`  ${status} ${relativePath}`);
		console.log(`         Namespaces: ${result.namespacesDetected.join(', ')}`);
	}
}

main();
