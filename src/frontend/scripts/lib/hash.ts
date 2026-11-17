import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';

/** Git blob id: lower-case hex SHA-1 of `blob {byte length}\0{bytes}` (manual.md 2.3). */
export function gitBlobId(bytes: Uint8Array): string {
	const h = createHash('sha1');
	h.update(`blob ${bytes.length}\0`);
	h.update(bytes);
	return h.digest('hex');
}

/** Git blob id of a file as stored on disk. */
export function gitBlobIdOfFile(absPath: string): string {
	return gitBlobId(readFileSync(absPath));
}

/** Lower-case hex SHA-256 of bytes or of a string (UTF-8). */
export function sha256Hex(data: Uint8Array | string): string {
	return createHash('sha256').update(data).digest('hex');
}
