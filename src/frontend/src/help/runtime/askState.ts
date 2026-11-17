/**
 * The state of the "Ask" tab (manual.md 7.2, 7.3) as a pure reducer. Every action carries the sequence number of the
 * question it belongs to, so a late answer to an earlier question never replaces the current one.
 */
import type { EngineNotice } from './engine.ts';
import { MAX_CARDS } from './searcher.ts';
import type { SectionView } from './searcher.ts';
import type { GenerationTier } from './types.ts';

export type Rating = 'none' | 'yes' | 'no';

export interface AnswerSource {
	/** The number shown in the chip: `[n]`. */
	n: number;
	url: string;
}

export interface AnswerView {
	text: string;
	tier: GenerationTier;
	/** Empty while the answer streams; the chips appear when it is complete and valid. */
	sources: AnswerSource[];
	complete: boolean;
}

/** What heads the cards: the retrieval header or the message of a declined answer. */
export type CardsHeader = 'best' | 'declined' | 'none';

export type AskState =
	| { phase: 'idle'; seq: number }
	| { phase: 'unavailable'; seq: number }
	| { phase: 'outOfScope'; seq: number; question: string; rating: Rating }
	| {
			phase: 'results';
			seq: number;
			question: string;
			cards: SectionView[];
			answer: AnswerView | null;
			header: CardsHeader;
			notice: EngineNotice | null;
			/** True once the tiers have answered or given way: only then is the result rated. */
			done: boolean;
			rating: Rating;
	  };

export type AskAction =
	| { type: 'started'; seq: number }
	| { type: 'unavailable'; seq: number }
	| { type: 'outOfScope'; seq: number; question: string }
	| { type: 'retrieved'; seq: number; question: string; cards: SectionView[] }
	| { type: 'notice'; seq: number; notice: EngineNotice | null }
	| { type: 'text'; seq: number; text: string; tier: GenerationTier }
	| {
			type: 'finished';
			seq: number;
			cards: SectionView[];
			header: CardsHeader;
			answer: AnswerView | null;
	  }
	| { type: 'rated'; seq: number; rating: Exclude<Rating, 'none'> };

export const initialAskState: AskState = { phase: 'idle', seq: 0 };

export function askReducer(state: AskState, action: AskAction): AskState {
	if (action.type === 'started') return { phase: 'idle', seq: action.seq };
	if (action.seq !== state.seq) return state;
	switch (action.type) {
		case 'unavailable':
			return { phase: 'unavailable', seq: state.seq };
		case 'outOfScope':
			return {
				phase: 'outOfScope',
				seq: state.seq,
				question: action.question,
				rating: 'none',
			};
		case 'retrieved':
			return {
				phase: 'results',
				seq: state.seq,
				question: action.question,
				cards: action.cards,
				answer: null,
				header: 'best',
				notice: null,
				done: false,
				rating: 'none',
			};
		case 'notice':
			return state.phase === 'results' ? { ...state, notice: action.notice } : state;
		case 'text':
			if (state.phase !== 'results' || action.text === '') return state;
			return {
				...state,
				answer: { text: action.text, tier: action.tier, sources: [], complete: false },
				header: 'none',
			};
		case 'finished':
			return state.phase === 'results'
				? {
						...state,
						cards: action.cards,
						header: action.header,
						answer: action.answer,
						done: true,
					}
				: state;
		case 'rated':
			return state.phase === 'results' || state.phase === 'outOfScope'
				? { ...state, rating: action.rating }
				: state;
		default:
			return state;
	}
}

/**
 * The cards of an answer: the cited sections first, in the order of the answer, then the best matches, at most
 * three in all (manual.md 7.2 step 3).
 */
export function orderCards(
	ranked: readonly SectionView[],
	citedIds: readonly string[],
	max = MAX_CARDS,
): SectionView[] {
	const byId = new Map(ranked.map((c) => [c.id, c]));
	const cited = citedIds.map((id) => byId.get(id)).filter((c): c is SectionView => !!c);
	const rest = ranked.filter((c) => !citedIds.includes(c.id));
	return [...cited, ...rest].slice(0, max);
}
