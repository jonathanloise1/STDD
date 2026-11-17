/**
 * Answer-quality gates of the project bench (manual.md 8.3). They need a model, so they are a separate step: they
 * read the report written by `python -m training.assistant.eval --suite project` (models.md 4.6) and never run a model.
 */
import { MIN_GATED_ITEMS } from './metrics.ts';
import type { Gate } from './metrics.ts';

export const ANSWER_THRESHOLDS = [
	{ metric: 'Faithful (of answered)', key: 'faithful', op: '>=', value: 0.95 },
	{ metric: 'Useful (of in-scope)', key: 'useful', op: '>=', value: 0.65 },
	{ metric: 'Wrongful refusal', key: 'wrongful_refusal', op: '<=', value: 0.15 },
	{ metric: 'Out-of-scope refusal', key: 'oos_refusal', op: '>=', value: 0.9 },
	{ metric: 'Correct source (of answered)', key: 'correct_source', op: '>=', value: 0.85 },
] as const;

export interface AnswerLangResult {
	lang: string;
	n: number;
	gating: boolean;
	gates: Gate[];
}

/**
 * Gates from the `results.sample_app` block of an evaluation report. A language without metrics fails;
 * a language with fewer than 20 items reports but does not gate (the report only carries the total `n`).
 */
export function answerGates(
	report: unknown,
	langs: readonly string[] = ['en', 'it'],
): AnswerLangResult[] {
	const block = (
		report as { results?: { sample_app?: Record<string, Record<string, number>> } } | null
	)?.results?.sample_app;
	return langs.map((lang) => {
		const m = block?.[lang];
		const n = typeof m?.n === 'number' ? m.n : 0;
		const gating = n >= MIN_GATED_ITEMS;
		const gates = ANSWER_THRESHOLDS.map((t): Gate => {
			const raw = m?.[t.key];
			const value = typeof raw === 'number' ? raw : null;
			const ok = value !== null && (t.op === '>=' ? value >= t.value : value <= t.value);
			return {
				metric: t.metric,
				value,
				threshold: `${t.op} ${t.value.toFixed(2)}`,
				pass: !gating && m ? true : ok,
				gated: gating,
			};
		});
		return { lang, n, gating, gates };
	});
}

export function answerGatesPass(results: AnswerLangResult[]): boolean {
	return results.every((r) => r.gates.every((g) => g.pass));
}
