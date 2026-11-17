// Renders the chat template of an exported model with transformers.js (models.md 6.6).
//
//   node training/assistant/js/template_check.mjs cases.json
//
// cases.json: { "model_dir": "...", "kwargs": { ... }, "cases": [ [ {role, content}, ... ], ... ] }
// Prints a JSON array with the rendered prompt of every case (generation prompt added, not tokenized), in order.
// The template check of `export` compares these strings byte for byte with the Python tokenizer's and llama.cpp's.
// Needs Node 22.18 or later and the package @huggingface/transformers, installed where this script finds it. Node resolves an
// ES module's bare imports from the folder of the script upward (node_modules next to it, then in each parent folder): not
// from the working directory, not from a global install, not from NODE_PATH and not from the transformers.js checkout.
// `export` runs this file by its absolute path, so install the package beside it (models.md 9.1 step 9):
//   npm install --prefix training/assistant/js --no-save --no-package-lock @huggingface/transformers@{version of src/frontend/package.json}
import { readFileSync } from "node:fs";

let AutoTokenizer;
try {
  ({ AutoTokenizer } = await import("@huggingface/transformers"));
} catch (error) {
  process.stderr.write(
    `template_check: cannot load @huggingface/transformers from ${import.meta.dirname}: ${error.message}\n` +
      "Install it beside this script: npm install --prefix training/assistant/js --no-save --no-package-lock @huggingface/transformers@{version}\n",
  );
  process.exit(1);
}
const spec = JSON.parse(readFileSync(process.argv[2], "utf8"));
const tokenizer = await AutoTokenizer.from_pretrained(spec.model_dir, { local_files_only: true });
const out = spec.cases.map((messages) =>
  tokenizer.apply_chat_template(messages, { tokenize: false, add_generation_prompt: true, ...spec.kwargs }),
);
process.stdout.write(JSON.stringify(out));
