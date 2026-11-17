/**
 * The only module that touches `@huggingface/transformers`. It is imported by the worker alone and the library is
 * loaded through a dynamic `import()`, so it never enters the main bundle. Not covered by unit tests: it needs a
 * real model and a real GPU (see the report of task E6).
 */
import type { TextModel, TransformersLib } from './workerCore.ts';

/** The calls used from the library, typed loosely on purpose. */
interface Tokenizer {
	(text: string, options: { add_special_tokens: boolean }): { input_ids: { dims: number[] } };
	apply_chat_template(messages: unknown, options: Record<string, unknown>): string;
}
interface CausalModel {
	generate(options: Record<string, unknown>): Promise<{ dims: number[] }>;
}

export async function loadTransformers(): Promise<TransformersLib> {
	const t = await import('@huggingface/transformers');
	return {
		env: t.env as unknown as Record<string, unknown>,
		async loadModel(modelId, options) {
			const progress_callback = (event: {
				status?: string;
				file?: string;
				loaded?: number;
				total?: number;
			}) => {
				if (event.status === 'progress' && event.file)
					options.onProgress(event.file, event.loaded ?? 0, event.total ?? 0);
			};
			const tokenizer = (await t.AutoTokenizer.from_pretrained(modelId, {
				progress_callback,
			})) as unknown as Tokenizer;
			const causal = (await t.AutoModelForCausalLM.from_pretrained(modelId, {
				device: options.device,
				dtype: options.dtype,
				progress_callback,
			})) as unknown as CausalModel;

			const textModel: TextModel = {
				// The same call as training/assistant/js/template_check.mjs, so the prompt is the one of training
				renderPrompt: (messages, templateKwargs) =>
					tokenizer.apply_chat_template(messages, {
						tokenize: false,
						add_generation_prompt: true,
						...templateKwargs,
					}),
				async generate(prompt, { maxNewTokens, minNewTokens, onText, signal }) {
					// The template already carries the special tokens
					const inputs = tokenizer(prompt, { add_special_tokens: false });
					const stopping = new t.InterruptableStoppingCriteria();
					const interrupt = () => stopping.interrupt();
					signal.addEventListener('abort', interrupt);
					let text = '';
					const streamer = new t.TextStreamer(
						tokenizer as unknown as ConstructorParameters<typeof t.TextStreamer>[0],
						{
							skip_prompt: true,
							skip_special_tokens: true,
							callback_function: (chunk: string) => {
								text += chunk;
								onText(text);
							},
						},
					);
					try {
						// Greedy decoding, stop at the template's end-of-turn token (models.md 3.8)
						const output = await causal.generate({
							...inputs,
							max_new_tokens: maxNewTokens,
							...(minNewTokens ? { min_new_tokens: minNewTokens } : {}),
							do_sample: false,
							streamer,
							stopping_criteria: stopping,
						});
						const tokens = output.dims[1] - inputs.input_ids.dims[1];
						return { text, tokens };
					} finally {
						signal.removeEventListener('abort', interrupt);
					}
				},
			};
			return textModel;
		},
	};
}
