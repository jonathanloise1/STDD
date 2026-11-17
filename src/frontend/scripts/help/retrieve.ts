/** help:retrieve (manual.md 8.4, models.md 4): retrieval for a list of questions against a built pack. */
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { selectContext } from '../../src/help/lexical/context.ts';
import type { ProtocolContext } from '../../src/help/lexical/types.ts';
import { consoleIo, isEntry, parseOptions } from './cli.ts';
import type { Io } from './cli.ts';
import { loadPackFile } from './pack.ts';

const DEFAULT_CONTEXT: ProtocolContext = { max_sections: 4, max_section_chars: 1200, max_context_chars: 4000, max_question_chars: 300, scan_limit: 8 };

export interface RetrieveInput {
	id: string | number;
	question: string;
}

/** Parses JSONL input: each line `{ "question": "...", "id": "..." }`; the id defaults to the 1-based line number. */
export function parseInput(text: string): RetrieveInput[] {
	const out: RetrieveInput[] = [];
	text.split('\n').forEach((line, i) => {
		if (!line.trim()) return;
		const row = JSON.parse(line) as { id?: string | number; question?: unknown };
		if (typeof row.question !== 'string') throw new Error(`line ${i + 1}: "question" must be a string`);
		out.push({ id: row.id ?? out.length + 1, question: row.question });
	});
	return out;
}

export function main(argv: string[], io: Io): number {
	const o = parseOptions(argv, { '--pack': 'value', '--in': 'value', '--out': 'value' });
	const pack = o.values.get('--pack');
	const inFile = o.values.get('--in');
	const outFile = o.values.get('--out');
	if (o.errors.length || !pack || !inFile || !outFile) {
		for (const e of o.errors) io.err(`help:retrieve: ${e}`);
		io.err('usage: help:retrieve --pack {file} --in {jsonl} --out {jsonl}');
		return 2;
	}
	try {
		const loaded = loadPackFile(resolve(pack));
		const ctx = (loaded.pack.assistant.protocol.content.context as ProtocolContext | undefined) ?? DEFAULT_CONTEXT;
		const lines = parseInput(readFileSync(resolve(inFile), 'utf8')).map((item) => {
			const hits = loaded.searcher.search(item.question);
			const decision = loaded.searcher.decide(item.question, hits);
			const context = selectContext(
				hits.map((h) => ({ id: h.id, text: loaded.pack.sections[h.index].text })),
				ctx,
			);
			return JSON.stringify({
				id: item.id,
				question: item.question,
				retrieved: hits.map((h) => h.id),
				scores: hits.map((h) => h.score),
				context: context.map((c) => c.id),
				in_scope_decision: decision.inScope,
				coverage: decision.coverage,
			});
		});
		writeFileSync(resolve(outFile), lines.length ? `${lines.join('\n')}\n` : '');
		io.out(`help:retrieve: ${lines.length} questions, language ${loaded.pack.lang}`);
		return 0;
	} catch (e) {
		io.err(`help:retrieve: ${(e as Error).message}`);
		return 1;
	}
}

if (isEntry(import.meta.url)) process.exitCode = main(process.argv.slice(2), consoleIo);
