import { join } from 'node:path';
import { isFile, readText } from './fsutil.ts';

/** Current version of a contract from docs/contracts.json, or null when the file or the entry is missing. */
export function contractVersion(root: string, key: string): number | null {
	const abs = join(root, 'docs/contracts.json');
	if (!isFile(abs)) return null;
	try {
		const v = JSON.parse(readText(abs))?.contracts?.[key]?.version;
		return typeof v === 'number' ? v : null;
	} catch {
		return null;
	}
}
