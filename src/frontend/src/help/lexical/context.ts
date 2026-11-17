/** Context selection for the generation tiers (models.md 3.5): a section is never cut. */
import type { ProtocolContext } from './types.ts';

export function selectContext<T extends { text: string }>(
	ranked: readonly T[],
	cfg: Pick<ProtocolContext, 'max_sections' | 'max_context_chars' | 'scan_limit'>,
): T[] {
	const selected: T[] = [];
	let total = 0;
	for (const s of ranked.slice(0, cfg.scan_limit)) {
		if (selected.length === cfg.max_sections) break;
		if (total + [...s.text].length > cfg.max_context_chars) continue;
		selected.push(s);
		total += [...s.text].length;
	}
	return selected;
}
