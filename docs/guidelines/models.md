# MyApp - Model Guidelines

> The lifecycle of the model-backed features of the template: the in-page assistant, the operations
> investigator, and the embedding table of the help's retrieval. Protocol, evaluation, synthetic data, training, export, runtime, lock file and the guide for the
> first training. Formats defined here are registered in [docs/contracts.json](../contracts.json). The help pack and
> the project bench are in [manual.md](manual.md); the investigator's protocol and evaluation are in
> [operations.md](operations.md).

**How to read this.** An adopting project training for the first time reads section 9 and follows it. An
implementer reads the section of the step being built. Numbers in tables are the defaults the pipeline ships with;
changing one is a reviewed change of this document and of the matching config file.

---

## 1. Principles and scope

| # | Principle | Consequence in this document |
|---|-----------|------------------------------|
| 1 | Models learn the template's formats and protocol, never a project's content | Training data is synthetic, written in the template's formats for many fictional applications (section 5). No file of `docs/` of any real project, including the sample app, enters a training set (validator V15) |
| 2 | A project ships knowledge next to the model | The help pack (manual.md 6) carries all project knowledge; the model carries behaviour |
| 3 | The pipeline ships with the template | `training/` holds every step as one command (section 2); section 9 is the walkthrough |
| 4 | Evaluation before training | The evaluation sets and thresholds (section 4) are built and frozen before the first training run; existing models are measured first (4.1) |
| 5 | The smallest model that passes ships | Selection rule in 6.1 |
| 6 | GPU first, then CPU, never a dead end | Runtime ladder in section 7; every chain ends in a tier without a model |

| Model id | Feature | Size class | Protocol and evaluation |
|----------|---------|------------|-------------------------|
| `assistant` | In-page assistant: answers a user's question from retrieved manual sections, or refuses | About 0.6B to 2B parameters | This document, sections 3 to 6 |
| `investigator` | Operations: investigates residual incidents with read-only tools and writes a report | Larger; tiers chosen by evaluation (6.1) | `docs/guidelines/operations.md`. This document gives only the shared lifecycle (sections 2, 6.4, 6.5, 7.6, 8, 9.2, 11, 12) |
| `embeddings` | Help retrieval: a static table of one vector per stem, distilled at build time from a Qwen embedding model and fused with lexical ranking. No model runs at query time | Encoder of 0.6B parameters at build time only; the table is at most 8 MiB per language | Section 7.8, 8.1, 9.5; format, query time and calibration in manual.md 6.7 |

The `embeddings` entry is not a trained model: the encoder's weights never change, and it reads single words of the
project's vocabulary, never a sentence, to build a table that belongs to the project like the help pack (principle 2;
what may enter it is manual.md 6.7.3). It follows principle 6 too: the build uses a GPU when present and the CPU
otherwise, and without a table the help ranks lexically.

Lessons that shaped the design (from an earlier experiment with a 0.6B model trained on a single application's
guide): it was faithful (59 of 60) and refused every out-of-scope question, but only 17 of 60 answers were useful
and 28 were wrongful refusals, including 19 of 20 questions in the user's own words. Causes and the rule that
answers each:

| Cause | Rule |
|-------|------|
| Refusals built by pairing questions with wrong sections, positives worded close to the text: the model learned "different words means refuse" | Refusal and positive classes have the same distribution of word overlap (5.4, V14) |
| Validation split by example inside one domain reported 100% | Hold-out by whole domain and sector (5.1) |
| Training loss fell to 0.009 (memorisation) | Early stopping on held-out domains, checkpoint selection by metrics, memorisation stop (6.3) |
| Context cut at 1,500 characters lost the answer | Sections are whole and bounded at pack build; the context never truncates a section (3.5) |
| The browser export did not run without WebGPU; weights re-downloaded every session | Server tier with the same weights; versioned, cached weights (7.2 to 7.4) |
| An untrained 1B model answered more often but invented content in 43 of 60 answers | Faithfulness gates first; answer rate alone is never a success measure (4.5) |

---

## 2. Layout and commands

### 2.1 Folders

```
training/
  requirements.txt                 # pinned Python dependencies
  common/                          # shared: config, hashing, teacher client, judge, hardware, lock, card, fetch,
                                   #   answer backends, training loop, export orchestration
  assistant/
    protocol.json                  # the single definition of the assistant protocol (3.1)
    protocol.golden.json           # rendering and parsing golden cases (3.7)
    retrieval.json                 # retrieval parameters (manual.md 6.5)
    protocol.py                    # Python renderer and parser
    config/
      dataset.json                 # 5.6
      domains.json                 # 40 fictional domain briefs and their split (5.1)
      candidates.json              # base and existing models to measure (6.1)
      train.json                   # 6.2, 6.3
      export.json                  # exporter pins and command templates (6.5)
      eval.json                    # suites, thresholds, judge (4.4, 4.5)
      blocklist.txt                # names that must never appear in data (V04)
    teacher/                       # teacher prompt templates, one file per stage, versioned in the name
    eval/judge.v1.txt              # judge prompt (4.4)
    eval/human-labels.jsonl        # 120 items labelled by a person (4.4); the project writes it
    js/template_check.mjs          # renders the chat template with transformers.js (6.6)
    generate.py validate.py train.py export.py eval.py golden.py
    embed.py                       # the help embedding table: build and probe (7.8)
    config/embed.json              # embedding model pin, table and calibration settings (7.8)
    config/words.{lang}.txt        # optional general word lists for the table; none ships (manual.md 6.7.3)
  investigator/                    # operations.md 11
    protocol.json protocol.golden.json thresholds.json config.json
    config/                        # candidates.json, train.json, export.json, eval.json
    worlds/{domain}.json           # 24 fictional applications (operations.md 11.6)
    generate.py validate.py train.py export.py eval.py golden.py host.py
  tests/                           # pytest: protocol golden cases, thresholds equal this document, validators,
                                   #   offline rehearsals of every command with fakes
  out/                             # git-ignored: teacher cache, datasets, checkpoints, exports, eval runs
models/
  model.lock                       # JSON, section 8
  model.lock.example               # the fields of an entry with fake values, for reading only
  assistant/
    MODEL_CARD.md                  # 8.3
    eval/{model_version}.json      # evaluation report of the recorded version (4.6), with -onnx and -gguf beside it
    eval/candidates/{id}.json      # untrained candidates' reports (4.1)
  investigator/                    # same shape; reports are named {model_version}-{tier}.json
  embeddings/
    calibration/{model_version}.json  # calibration report of the recorded tables (manual.md 6.7.7)
    weights/                       # git-ignored: en.bin and it.bin, filled by fetch
```

The assistant's and the investigator's weights are not in the repository: `fetch` writes them to `$MYAPP_MODELS_DIR/{model}/` (default `~/myapp-models`), the folder compose mounts as `MODELS_DIR` (`assistant/assistant.gguf`, `investigator/investigator.gguf`, `teacher/...`), so one folder serves the scripts and Docker.

`training/out/` (with the embedding runs in `training/out/embeddings/runs/{run_id}/`), `models/*/weights/` and
`src/frontend/public/models/` are git-ignored. Weights, datasets and
teacher outputs are never committed (`CLAUDE.md`).

### 2.2 Commands

Run from the repository root, inside the Python environment of 9.1 (on a Windows PC, inside WSL 2: 9.4). Section 9 gives
the order, the prerequisites and what to do on failure; `--help` of each command lists every flag.

| Step | Command | Output | Approval |
|------|---------|--------|----------|
| Hardware check | `python -m training.common.hardware [--parameters N] [--json]` | Device, memory, disk, expected durations; exit 1 without CUDA or Apple MPS | No |
| Time estimate | `python -m training.assistant.generate --estimate [--splits ...]` | Request counts, tokens and estimated duration per stage, and an `estimate_id` (5.6) | No |
| Measure the teacher host | `python -m training.assistant.generate --measure [--concurrency N]`; `python -m training.assistant.eval --measure` for the judge host | Tokens per second of the running local host (a few short requests), the values for the config (5.6) | No |
| Dry run | `python -m training.assistant.generate --dry-run` | Renders every request and prints three samples per stage; no request is sent, writes nothing | No |
| Generate | `python -m training.assistant.generate --splits {test,validation\|train} --approved-estimate {estimate_id}` | Teacher outputs in the cache; synthetic domain trees | Yes `[COST]` |
| Validate | `python -m training.assistant.validate [--tools auto\|off\|require] [--docs-root DIR]` | `training/out/assistant/dataset/{split}.jsonl` (also `seen.jsonl`), `rejected.jsonl`, `stats.json` | No |
| Judge estimate | `python -m training.assistant.eval --estimate` | Judge calls, tokens, estimated duration and the id to approve; no model runs | No |
| Freeze the sets | `python -m training.assistant.eval --freeze-sets` | SHA-256 of the test and validation sets in `eval.json` | No |
| Label for the judge | `python -m training.assistant.labels select [--answers items.jsonl]`, then `label`, then `finish` | `eval/human-labels.jsonl`, the 120 hand-labelled items of 4.4 | No |
| Measure candidates | `python -m training.assistant.eval --candidates --approved-estimate {id} [--only {candidate}] [--no-judge]` | One report per candidate, untrained (4.1) | Yes `[COST]` (judge run) |
| Plan training | `python -m training.{model}.train --dry-run [--tier T] [--candidate {id}]` | The plan and its `plan_id`; imports no torch | No |
| Train | `python -m training.{model}.train --approved-plan {plan_id} [--tier T] [--candidate {id}] [--run {run_id}] [--model-version V]` | Checkpoints and the selected checkpoint (6.3) | Yes `[COST]` (long run) |
| Export | `python -m training.{model}.export [--dry-run] --run {run_id}`, or `--candidate {id}`; `[--formats ...] [--variant FORMAT=VARIANT]` | Merged weights, exports, manifest, template check (6.5) | No |
| Evaluate | `python -m training.assistant.eval --export {merged\|onnx\|gguf} --run {run_id} --approved-estimate {id} --items {jsonl}` | Platform evaluation report (4.6) | Yes `[COST]` (judge run) |
| Project bench, answers | `python -m training.assistant.eval --suite project --items {jsonl} --export gguf --run {run_id} --approved-estimate {id}` | Answer metrics of manual.md 8.3, `eval-project-gguf.json` | Yes `[COST]` (judge run) |
| Synthetic incidents | `python -m training.investigator.generate --dry-run`, or `--split {train\|validation\|holdout\|all} [--limit N]` | World and example files (operations.md 11.6) | No |
| Validate incidents | `python -m training.investigator.validate` | Dataset files, `rejected.jsonl`, `stats.json` | No |
| Evaluate investigator | `python -m training.investigator.eval --freeze-sets`, `--candidates --tier T`, or `--export {merged\|gguf} --run {run_id} [--suite S] [--tier T]` | Metrics of operations.md 11.4 | Long compute; no model host |
| Embedding table | `python -m training.assistant.embed build [--dry-run] [--langs en,it] [--packs DIR] [--run {run_id}] [--model-version V] [--device auto\|cuda\|mps\|cpu] [--allow-unverified]` | `{lang}.bin`, `build-{lang}.json`, `run.json` and `exports.json` in `training/out/embeddings/runs/{run_id}/` (7.8) | Yes: it downloads (the first time) and runs the embedding model; minutes, not `[COST]` |
| Probe the table | `python -m training.assistant.embed probe --run {run_id} --lang {lang} --question TEXT` | Query stems and weights, the top 5 sections lexical, by cosine and fused, and the decision (7.8); no model runs | No |
| Calibrate the table | `cd src/frontend && npm run help:bench -- --calibrate --embeddings {run folder} --unlocked` | `calibration.json` in the run folder (manual.md 6.7.7) | No |
| Record | `python -m training.common.lock --record --model {model} --run {run_id} --artifact-url {url}` | Entry in `models/model.lock`, reports copied to `models/{model}/eval/` | No |
| Card | `python -m training.common.card --model {model}` | `models/{model}/MODEL_CARD.md` tables refreshed | No |
| Verify | `python -m training.common.lock --verify [--model {model}]` | Checks L01 to L09 (8.2); exit 1 on any failure | No |
| Fetch weights | `python -m training.common.fetch --model {model} [--runtime browser\|server] [--format onnx\|gguf] [--to DIR]` | Weights downloaded to `$MYAPP_MODELS_DIR/{model}/` (`models/embeddings/weights/` for `embeddings`; `{to}/{model_version}/` with `--to`) and checked by SHA-256 | No |

`{model}` is `assistant` or `investigator`; `lock` and `fetch` also take `embeddings` (7.8), `card` does not. `--tier` is
`small` or `large` and applies to the investigator only.

Commands are resumable: `generate`, the judge and `export` skip work whose output exists with a matching input hash, so
running the same command again continues. `train` resumes with `--run {run_id}`; without it a new run starts. Every command
that does model work writes `training/out/{model}/runs/{run_id}/run.json` with the command, config hashes, code commit and
durations. `{run_id}` is `{yyyy-mm-dd}-{letter}`. Commands that take `--run` default to the latest run, and `generate` also
creates run folders, so pass it explicitly.

`[COST]` marks a command that occupies the machine for a long time: hours of a local model host or of the training GPU. Nothing in the pipeline costs money per request, because the teacher and the judge run on a local host (5.6). The owner approves each one, quoting its token and time estimate.

Subagents never start a `[COST]` command. The main session asks the owner, quoting the estimate.

---

## 3. Assistant protocol

### 3.1 Single definition

`training/assistant/protocol.json` is the only definition of the prompt, the answer format and the context
budget. Training reads it directly; the help pack carries a byte-equivalent copy with its SHA-256 (manual.md 6.3);
the browser and the backend read only the copy in the pack. The lock records the SHA-256 the model was trained
with, and a mismatch switches generation off (7.5). So the prompt cannot differ between training and run time.

```json
{
  "schema_version": 1,
  "system": "You answer questions from users of {app_name}. Use only the numbered manual sections in the user message.\nRules:\n1. Use only facts stated in the sections. Never add steps, labels, limits or features that the sections do not state.\n2. Users often use other words than the manual. Match the meaning, not the words: if a section states what the user needs, answer from it.\n3. If no section states the answer, reply with exactly this sentence and nothing else: {refusal}\n4. Answer in {language_name}. Write button, field and message names exactly as the sections write them.\n5. Be short: at most three sentences, or at most six numbered steps.\n6. End with a last line that starts with \"Source:\" and lists the numbers of the sections you used, for example: Source: [2]",
  "user": "Manual sections:\n\n{sections}\n\nQuestion: {question}",
  "section": "[{n}] {title}\n{text}",
  "section_separator": "\n\n",
  "languages": {
    "en": { "language_name": "English", "refusal": "I can't find this in the manual." },
    "it": { "language_name": "Italian", "refusal": "Non trovo questa informazione nel manuale." }
  },
  "context": { "max_sections": 4, "max_section_chars": 1200, "max_context_chars": 4000, "max_question_chars": 300, "scan_limit": 8 },
  "answer": { "source_prefix": "Source: ", "max_body_chars": 600, "max_body_lines": 7 },
  "decoding": { "temperature": 0, "max_new_tokens": 200, "reasoning": "off" }
}
```

### 3.2 Messages

Two messages, `system` and `user`, passed to the base model's own chat template. Placeholders:

| Placeholder | Value |
|-------------|-------|
| `{app_name}` | `project.name` of the pack (manual.md 6.3); in training, the fictional application's name |
| `{refusal}`, `{language_name}` | From `languages.{lang}` |
| `{sections}` | The selected sections (3.5), each rendered with `section`, joined with `section_separator` |
| `{n}` | 1-based position in the context |
| `{title}`, `{text}` | Section title and text from the pack |
| `{question}` | The user's question, normalised (3.7) |

Example of a rendered user message:

```
Manual sections:

[1] Create a task - Steps
1. Open the Tasks page.
2. Click "New Task".
3. In the "Title" field, type the title.
4. Click "Create Task".

[2] Create a task - If something goes wrong
What you see: "You no longer have access to this organization.". What to do: Ask an admin of the organization to add them back.

Question: how do i put a new thing on the team list
```

### 3.3 Answer format

```
answer   = refusal | body LF source
refusal  = the refusal sentence of the language, exactly, nothing else
body     = 1 to max_body_lines lines, at most max_body_chars characters, no line starting with "Source:"
source   = "Source: " ref *(", " ref)
ref      = "[" n "]"            n in 1..K (K = sections in the context), distinct, ascending
```

Example: `Click "New Task" on the Tasks page, type a title and click "Create Task".` then the line
`Source: [1]`.

The prefix `Source: ` is the same in both languages: it is machine syntax that the panel turns into links and never
shows as text (manual.md 7.2).

### 3.4 Parsing

1. Replace `\r\n` with `\n`; trim trailing whitespace of the whole output.
2. Equal to the refusal sentence of the language: `refused`.
3. Last line matches `^Source: \[[1-9]\](, \[[1-9]\])*$`, numbers distinct, ascending and at most K; body valid
   per 3.3: `answered` with the cited positions.
4. Anything else, including the refusal sentence followed by other text: `invalid` (treated as a refusal by the
   panel, manual.md 7.3).

### 3.5 Context budget and selection

The pack guarantees sections of at most 1,200 characters (manual.md 6.1). At run time, from the ranked sections:

```
selected = [], total = 0
for s in ranked[0 : scan_limit]:
    if len(selected) == max_sections: stop
    if total + len(s.text) > max_context_chars: skip s
    selected.append(s); total += len(s.text)
```

- Sections are numbered in rank order. A section is never cut.
- With 4,000 characters of sections, a question of at most 300 characters and the system message, the prompt stays
  under about 1,800 tokens in both languages; with 200 new tokens the model needs a context of 2,048 tokens or more.
  Training uses a maximum sequence length of 2,048 (6.2).
- The model is called only when the out-of-scope decision says in scope (manual.md 6.6).

### 3.6 Languages

- The UI language chooses the pack, `language_name` and the refusal sentence. The model answers in that language.
- English and Italian are equal: same share of training data, same thresholds, measured separately.
- A question written in the other language is not translated. When it is judged out of scope and has at least two
  stopwords of the other language and more than of its own, the panel adds "Your question looks like Italian.
  Switch the language to search the Italian manual." (and the Italian counterpart).

### 3.7 Rendering rules and golden cases

| Rule | Value |
|------|-------|
| Input normalisation | Unicode NFC; `\r\n` to `\n`; question: trim, collapse whitespace runs to one space, cut at `max_question_chars` code points |
| Substitution | One left-to-right pass over the template; each `{name}` of 3.2 is replaced by its value; substituted values are not scanned again (a `{` in a question stays literal) |
| Chat template | The base model's template, from the tokenizer files of the exported model; any reasoning or thinking mode is switched off with the template's own option, recorded in `candidates.json` as `chat_template_kwargs`, and passed identically in training, in the browser and on the server |
| Implementations | Python `training/assistant/protocol.py`; TypeScript `src/frontend/src/help/assistant/protocol.ts`; C# `src/backend/MyApp.Application/Help/AssistantProtocol.cs` |
| Golden cases | `training/assistant/protocol.golden.json`: at least 12 cases (1 to 4 sections, Italian accents, braces and quotes in the question, CRLF input, an over-long question, every parse outcome). Each implementation has a unit test that renders and parses every case byte for byte |

### 3.8 Decoding

Greedy (temperature 0), at most 200 new tokens, stop at the template's end-of-turn token. Same settings in
evaluation, browser and server.

---

## 4. Evaluation

Designed, built and frozen before any training run.

### 4.1 Candidates first

Before any training, `eval --candidates` measures on the platform evaluation (4.2), with the protocol unchanged:

- each base model of `candidates.json`, untrained;
- each existing small model trained for document-grounded answering that meets the requirements of 6.1.

If one passes every threshold of 4.5, the smallest passing one is recorded and shipped without training
(`status` `untrained-candidate` in the lock) and sections 6.2 to 6.3 are skipped. Training happens only when no
candidate passes. An untrained model has nothing to transfer, so the transfer gap is not measured for it and does not gate
it (its reports say `untrained: true` and `results.transfer_gap` null); every other threshold applies, and the training
set need not exist yet. The candidate reports are kept in `models/assistant/eval/candidates/` as the baseline every trained
model must beat.

### 4.2 Suites

| Suite | Items | Context | Gates |
|-------|-------|---------|-------|
| Platform test | 6 held-out domains x 2 languages x 60 items = 720: per domain and language 36 in scope (at least 18 with overlap <= 0.15, 10 between 0.15 and 0.35, 8 above), 12 near-miss, 12 out of scope | `oracle`: retrieval as at run time, then the gold sections inserted when retrieval missed them (replacing the lowest-ranked) | Model thresholds of 4.5 |
| Platform test, end to end | Same items | `runtime`: retrieval exactly as at run time | Reported |
| Sample app | The project bench of the sample app (manual.md 8), both languages | `runtime` | manual.md 8.3 answer gates |
| Seen-domain check | 5% of the questions of every training domain, removed from training before assembly | `oracle` | Transfer gap of 4.5 |
| Validation | 4 validation domains x 2 languages x 40 items = 320 | `oracle` | Used only for early stopping and checkpoint selection (6.3), never reported as a result |
| Project bench | manual.md 8, of the adopting project, at each release | `runtime` | manual.md 8.3 |

Overlap is defined in 5.4. Test and validation domains never contribute training examples. The sample app is never
a training domain. The platform test set is frozen by its SHA-256 in `eval.json`; changing it bumps the suite
version.

### 4.3 Metrics

| Metric | Population | Definition | How measured |
|--------|------------|------------|--------------|
| Format valid | All items | Parse outcome is not `invalid` | Automatic |
| Out-of-scope refusal | Out-of-scope items | Share `refused` | Automatic |
| Near-miss refusal | Near-miss items | Share `refused` | Automatic |
| Wrongful refusal | In-scope items with a gold section in the context | Share `refused` or `invalid` | Automatic |
| Wrongful refusal, distant | Same, overlap <= 0.15 | Share `refused` or `invalid` | Automatic |
| Correct source | `answered` in-scope items | At least one cited position holds a gold section | Automatic |
| Faithful | `answered` items | Every statement of the body is supported by the cited or other context sections | Judge, audited by humans |
| Useful | In-scope items with a gold section in the context | `answered` and the body gives what the user needs to act: the right fact or steps, nothing missing that the gold section states and the question needs | Judge, audited by humans |
| Latency | 100 in-scope items per tier and hardware | Time to first token and to the last token, p50 and p95, at most 120 new tokens | Automatic |

### 4.4 Judge

| Rule | Value |
|------|-------|
| Judge | A larger open-weight instruct model served by a local OpenAI-compatible host (`eval.json` `judge.base_url`, `judge.model`), temperature 0 |
| Judge model | An instruct model that follows the JSON output format reliably and reads Italian and English well. It must not be the model under evaluation, nor the base model of the candidate being trained, nor a model fine-tuned from it. It may be the same model as the teacher (the shipped default `qwen3.8-27b`, one host serves both); `eval` refuses a judge whose name matches the model under evaluation; a different one removes the shared blind spots of 5.6 and is better when the machine allows it. Starting candidates are in 5.6 (unverified, the owner confirms them). A weak judge gives noisy labels: its agreement with the human labels below is what decides whether its results count |
| Input | Question, the numbered context, the model's answer, the gold positions and the teacher's reference answer |
| Output | JSON `{ "faithful": true, "useful": true, "unsupported": ["{statement}"] }`, schema-checked; an invalid output is retried once, then the item counts as not faithful and not useful |
| Run control | Only `answered` items are judged (refusals are scored automatically); results are cached by SHA-256 of (judge prompt version, item, answer), so an unchanged answer is never judged twice and an interrupted run resumes; the estimate (calls, tokens, duration from `judge.tokens_per_second`) is printed before the run |
| Calibration | `training/assistant/eval/human-labels.jsonl`: 120 items labelled by a person once per suite version (faithful, useful). The judge must agree on at least 0.90 of them for each metric, or the judge prompt is revised before any result counts |
| Audit | 30 random judged items per recorded model version are checked by a person; the agreement is written in the model card |

### 4.5 Thresholds

Platform test, `oracle` context, each language separately. A model passes when every row passes in both languages,
for the merged weights and for each export.

| Metric | Threshold |
|--------|-----------|
| Format valid | >= 0.99 |
| Faithful | >= 0.95 |
| Useful | >= 0.75 |
| Wrongful refusal | <= 0.10 |
| Wrongful refusal, distant | <= 0.15 |
| Out-of-scope refusal | >= 0.95 |
| Near-miss refusal | >= 0.80 |
| Correct source | >= 0.90 |
| Transfer gap: useful on seen domains minus useful on test domains | <= 0.10 |
| Export loss: each export against the merged weights | Useful drop <= 0.05, faithful drop <= 0.02 |
| Sample app | The answer gates of manual.md 8.3 |

Latency, per tier, on the reference hardware listed in `eval.json` (time to last token, p95): browser WebGPU on a
laptop with an integrated GPU <= 6 s; server GPU <= 3 s; server CPU with 8 cores <= 10 s. A tier that misses its
latency target does not block the release; it is disabled in the default profiles and the card says so.

`eval.json` holds the same numbers; a test in `training/tests/` fails when they differ from this table.

### 4.6 Evaluation report

`training/out/assistant/runs/{run_id}/eval-{export}.json`; the recorded one is copied to
`models/assistant/eval/{model_version}.json`.

```json
{
  "schema_version": 1,
  "model": "assistant",
  "model_version": "1.0.0",
  "export": "gguf",
  "suite_version": 1,
  "thresholds_sha256": "{hex of eval.json}",
  "test_set_sha256": "{hex}",
  "judge": { "model": "{id}", "prompt_version": 1, "human_agreement": { "faithful": 0.95, "useful": 0.92 } },
  "hardware": { "device": "gpu", "name": "{device name}" },
  "results": {
    "platform_test": { "en": { "format_valid": 1.0, "faithful": 0.97, "useful": 0.81, "wrongful_refusal": 0.06, "wrongful_refusal_distant": 0.09, "oos_refusal": 0.98, "near_miss_refusal": 0.86, "correct_source": 0.94, "n": 360 }, "it": { } },
    "platform_end_to_end": { "en": { }, "it": { } },
    "sample_app": { "en": { }, "it": { } },
    "seen_domains": { "en": { "useful": 0.84 }, "it": { } },
    "latency": { "p50_ms": 900, "p95_ms": 1800, "ttft_p95_ms": 300 }
  },
  "failures": [],
  "passed": true,
  "items_sha256": "{hex of the per-item results file}"
}
```

The per-item results (`items-{export}.jsonl`: set, id, language, outcome, cited, gold, latency, answer, judge labels) stay in
`training/out/` and are the first thing to read when a threshold is missed (section 10). A real report may also carry
`run_id`, `candidate` and `results.transfer_gap`. The investigator's report has the same envelope with `tier`, `judge: null`
and the suites of operations.md 11.4 as `results`.

---

## 5. Synthetic dataset

### 5.1 Domains and hold-out

- `training/assistant/config/domains.json` lists 40 fictional applications, written by hand and shipped with the
  template: `{ "id": "d01", "sector": "...", "app": "{one-line description}", "users": "{who uses it}", "split": "train|validation|test" }`.
- Split: 30 `train`, 4 `validation`, 6 `test`. The split is by whole domain and by sector: no sector of a
  `validation` or `test` domain appears in `train` (for example, if a veterinary clinic is a test domain, no clinic
  of any kind is a training domain).
- Sectors are varied on purpose (field service, schools, libraries, farms, event venues, warehouses, clinics,
  municipal permits, sports clubs, labs, and so on) and none is the sample app's.
- Names of applications, companies, people and products are invented; `blocklist.txt` lists real brand and product
  names that must not appear (V04).

### 5.2 What a domain contains

For each domain the teacher writes, in the template's formats, a small repository tree under
`training/out/assistant/domains/{id}/`:

| Content | Format | Size |
|---------|--------|------|
| Area registry, area indexes | development.md 3.2, 5 | 4 to 6 areas |
| Stories | development.md 6 (with Questions users ask, both languages) | 12 to 20 stories |
| Manual pages, `en` and `it` | manual.md 2, without images | One per story and area, plus home |
| Questions | 5.3, 5.4 | 10 in-scope per story and language, 6 near-miss per area and language, 15 out-of-scope per domain and language |

Sectioning and ranking run in Python (`sections.py`, `lexical.py`), ports of the frontend code checked against it on golden
lists, so training contexts follow the same rules as at run time. `validate` also runs `npm run manual:check -- --root
{domain dir} --no-images` on each tree when the frontend is installed (V03). Test cases are not generated: the protocol never
sees them.

### 5.3 Example classes

One example = one question, a context of K sections (K uniform in 2..4, built by 3.5 from retrieval), and a
target. Shares of the `train` split, per language:

| Class | Share | Question | Context | Target |
|-------|-------|----------|---------|--------|
| `pos_distant` | 32% | In-scope, overlap <= 0.15 | Retrieved; gold inserted if missing | Answer and source line |
| `pos_mid` | 15% | In-scope, overlap in (0.15, 0.35] | Same | Same |
| `pos_near` | 10% | In-scope, overlap > 0.35 | Same | Same |
| `pos_multi` | 8% | Needs two sections of the domain | Both gold sections present | Answer citing both |
| `ref_near_miss` | 15% | About the domain and its screens, but the domain's docs do not answer it (a missing detail, an absent feature) | Retrieved: similar sections of the same domain | Refusal sentence |
| `ref_removed` | 5% | In-scope question | Retrieved with every gold section removed (next-ranked sections fill the context) | Refusal sentence |
| `ref_oos` | 15% | Unrelated to the application (general knowledge, other software, personal matters) | Retrieved (whatever ranks highest) | Refusal sentence |

- Positives 65%, refusals 35%. `ref_removed` is kept small because it is the class closest to the earlier failure;
  its questions are drawn from all overlap buckets so it cannot teach a wording shortcut.
- The gold section's position is uniform over 1..K (V13): the section order in training examples is shuffled with
  the example seed. At run time sections are in rank order; the model must not rely on position.
- Answers to positives are written by the teacher from the rendered protocol prompt (3.2), then verified (5.6
  stage `verify`). Refusal targets are the fixed sentence; no teacher call.

### 5.4 Lexical distance

`overlap(q, s)` = share of the distinct content stems of the question (manual.md 6.4 analyzer, Python port checked
against the TypeScript one on a golden list) that occur in the title or text of section `s`. For positives `s` is
the gold section (for `pos_multi`, their union); for refusals it is the top-ranked section of the context.

How distance is forced:

- Question styles, assigned by seed per question: `symptom` (what goes wrong), `goal` (what the user wants, no screen
  words), `colleague` (the words a coworker would use, synonyms), `terse` (3 to 6 words), `rambling` (two sentences
  with context), `typo` (lower case, one or two typos). Styles are spread evenly across classes.
- For `pos_distant` the teacher receives a forbidden list: the stems of the section title and the 12 stems of the
  gold section with the highest tf-idf in the domain. V09 rejects a distant question whose measured overlap is
  above 0.15.
- No question may copy four consecutive stems of its gold section (V10); quoting a UI label is allowed only in
  `pos_near`.
- Refusal questions are written with the same styles and are required to share vocabulary with the context:
  `ref_near_miss` questions are written from the most similar existing section ("ask something a user of this
  screen would ask that the page does not answer") so their overlap is often high.
- Dataset rule (V14): in each language and each overlap bucket (<= 0.15, 0.15 to 0.35, > 0.35), the refusal share is
  between 0.25 and 0.45. Wording distance therefore carries no information about the target; only the content
  does.

### 5.5 Languages

Every domain is written in both languages. Stories are in English (the contract), with the Italian block of
questions; manual pages exist in `en` and `it`. Italian questions and pages are written by the teacher directly in
Italian, with Italian personas, not translated from the English ones. The `train` split is 50% `en` and 50% `it`
(V14).

### 5.6 Teacher jobs

Generation uses a larger teacher model served locally through the OpenAI-compatible chat protocol; no cloud AI service is used anywhere in the pipeline. By default the endpoint is `http://localhost:8093/v1`, the `teacher` service of the compose file (a llama.cpp host serving a large open-weight model, the same host family as `src/support/model-host`); any other OpenAI-compatible local server works by changing `base_url`. Config `training/assistant/config/dataset.json`:

```json
{
  "schema_version": 1,
  "master_seed": 20261106,
  "splits": { "train": 30, "validation": 4, "test": 6 },
  "languages": ["en", "it"],
  "per_domain": { "areas": [4, 6], "stories": [12, 20], "in_scope_per_story_lang": 10, "near_miss_per_area_lang": 6, "oos_per_domain_lang": 15 },
  "classes": { "pos_distant": 0.32, "pos_mid": 0.15, "pos_near": 0.10, "pos_multi": 0.08, "ref_near_miss": 0.15, "ref_removed": 0.05, "ref_oos": 0.15 },
  "target_examples": { "train": 20000, "validation": 320, "test": 720 },
  "teacher": {
    "base_url": "http://localhost:8093/v1",
    "model": "qwen3.8-27b",
    "verifier_model": "qwen3.8-27b",
    "api_key_env": null,
    "max_concurrency": 4,
    "tokens_per_second": 0,
    "prompt_tokens_per_second": 0,
    "max_hours": null,
    "weights": {
      "repo": "unsloth/Qwen3.8-27B-GGUF",
      "file": "Qwen3.8-27B-Q4_K_M.gguf",
      "path": "teacher/Qwen3.8-27B-Q4_K_M.gguf",
      "quantization": "Q4_K_M",
      "sha256": null,
      "self_made_from": {"hub_id": "Qwen/Qwen3.8-27B", "revision": "37561816e66b5b9153b1b64c10968c18c1e6f4a0"},
      "note": "Where the host's model file comes from. Either download the 4-bit GGUF from a community conversion (the repository and file name are the expected ones and are UNVERIFIED: confirm them on the Hugging Face page and take the exact file name), or make it with llama.cpp (convert_hf_to_gguf.py, then llama-quantize) from the pinned revision in self_made_from. The file lives outside the repository, in the weights folder (MYAPP_MODELS_DIR, default ~/myapp-models), at `path`. After the download run `python -m training.common.weights FILE` and write the hash into `sha256`; `python -m training.common.weights --check teacher` verifies it later. The same block format serves the investigator's pre-made GGUF (investigator/config/candidates.json)."
    }
  }
}
```

| Key | Meaning |
|-----|---------|
| `base_url` | The endpoint. Requests go only to this host: no redirect is followed and no proxy from the environment is used |
| `model`, `verifier_model` | The model names the host serves. A llama.cpp host serves one model and ignores the name, so `verifier_model` may equal `model` (the default) |
| `api_key_env` | Optional. `null` sends no key. When it names an environment variable and that variable is set, its value is sent as a bearer token (for a local proxy that requires one); a named variable that is not set is not an error. The key is read only from the environment |
| `tokens_per_second` | Generated tokens per second of the whole host at `max_concurrency`, from `generate --measure`. A real run refuses 0 |
| `prompt_tokens_per_second` | Prompt tokens processed per second, from the same command. 0 leaves prompt processing out of the duration |
| `max_hours` | Optional wall-clock limit of one run; the run stops cleanly when it is reached and the same command resumes. `null` means no limit |
| `weights` | Where the host's model file comes from, for the owner: `repo` and `file` (a community 4-bit GGUF), `self_made_from` (the pinned revision to convert yourself), `path` in the weights folder outside the repository (`MYAPP_MODELS_DIR`, default `~/myapp-models`), and `sha256`, recorded after the download (`python -m training.common.weights`). Nothing reads it at run time; the pipeline never downloads it |

**The teacher model.** The teacher must be an open-weight instruct model large enough to write good Italian and English,
to follow the job format of each stage exactly, and to keep answers inside the given sections. A mixture-of-experts model of
about 30B parameters quantized to 4 bits is the intended size class: it fits a workstation with 32 to 64 GB of memory, and
it generates fast enough on a CPU or a modest GPU for the estimate of this section to be measured in hours, not days.
The owner's choice is the Qwen family, served locally: default **Qwen3.8-27B** (dense, 4-bit GGUF, revision
`37561816e66b5b9153b1b64c10968c18c1e6f4a0`), alternative **Qwen3.6-35B-A3B** (mixture of experts, 3B active). The model
name in `dataset.json` and `eval.json` is the name the local host exposes (`qwen3.8-27b`, set in the compose file as
`TEACHER_MODEL_ALIAS`). Both think by default, so the client always sends `chat_template_kwargs {"enable_thinking": false}` and strips any
`<think>` block from the answer; the Qwen3 `/think` and `/nothink` switches are not supported. The teacher is never trained and
never shipped. The licence stays **to be verified** for synthetic data used to train a model that is then shipped.
Check the sample output of `--dry-run` rendered prompts answered by the teacher, in both languages, before the first real run.

The teacher and the students are the same family, so the teacher may be lenient toward Qwen-style answers and share their blind
spots: the human-label calibration of 4.4 (120 items) is the guard against it.

**Warning: a local teacher's quality bounds the dataset's quality.** A smaller local model writes more questions with
outside knowledge, more unnatural Italian and more answers that are not supported by the section. The training set can be no
better than its teacher. What protects the result is not the teacher but the checks after it: the validators of 5.7
(including the verifier stage and V08), the frozen test set of 4.2, and the judge calibration against human labels of 4.4.
Read `rejected.jsonl` and a sample of accepted answers after the first small run, and do not lower a threshold to
make a weaker teacher pass (section 10).

Stages, in order, each a set of jobs: `domain` (areas, roles, screens), `stories`, `pages` (per language),
`questions` (per story, language and style), `near_miss`, `oos`, `answers` (positives), `verify` (verifier model:
for answers "is every statement supported by section [n] and does it answer the question", for near-miss
"is this answered anywhere in these sections": the top 4 retrieved sections of the domain).

Job record (one per request):

```json
{
  "schema_version": 1,
  "stage": "questions",
  "key": "d07/it/questions/US-INV-03/symptom",
  "domain": "d07",
  "lang": "it",
  "template": "questions.v1",
  "template_sha256": "{hex}",
  "vars": { },
  "model": "{teacher model id}",
  "params": { "temperature": 0.8, "max_tokens": 1500 },
  "seed": 1834221
}
```

| Rule | Value |
|------|-------|
| Seeds | `seed = int(sha256("{master_seed}|{key}")[0:8], 16)`; every sampling, quota and shuffle in assembly uses seeds derived the same way |
| Cache and resume | The response is stored at `training/out/assistant/teacher-cache/{h[0:2]}/{h}.json`, `h` = SHA-256 of the canonical job record; a job with a cached response is never sent again. An interrupted run resumes by running the same command |
| Estimate | `--estimate` counts the jobs per stage, estimates input tokens as characters / 3.5 and output as 0.6 x `max_tokens`, converts them to a duration (output tokens / `tokens_per_second` plus input tokens / `prompt_tokens_per_second`), prints a table per stage and the total, and prints `estimate_id` = SHA-256 of (config, job counts of all splits, rates) truncated to 12 characters. Expected order of magnitude for the defaults, not measured: about 80 million input and 10 million output tokens, most of it in `answers` and `verify` |
| Dry run | `--dry-run` renders all jobs and prints three per stage, with no request sent and no file written |
| Approval | A real run requires `--approved-estimate {estimate_id}` equal to the current estimate, which the owner approves (`[COST]`: hours of machine use, not money); a changed config gives a new id and needs a new approval |
| Time limit | With `max_hours` set, the run stops sending new jobs when that time has passed and exits 1; a later run resumes. Every answered job records its duration in the cache |
| Measure | `--measure` sends one long prompt with a one-token answer and `--concurrency` parallel short generations (default `max_concurrency`) to the running host, prints `tokens_per_second` and `prompt_tokens_per_second` and writes nothing; copy the values into the config. It needs the host to report token usage, as llama.cpp does |
| Order | `--splits test,validation` first (frozen for evaluation, 4.2), then `--splits train` |

### 5.7 Validators

`python -m training.assistant.validate` runs every validator; a rejected item goes to `rejected.jsonl` with the
validator id. Dataset-level validators fail the command.

| Id | Level | Rejects or fails when |
|----|-------|-----------------------|
| V01 | Item | The record does not match the example format (5.9) |
| V02 | Domain | A story breaks the story contract: headings, fields, 1 to 8 criteria, 3 to 5 questions per language, at most 80 lines (development.md 6) |
| V03 | Domain | `manual:check --root {domain} --no-images` reports a finding other than drafts |
| V04 | Item | Text contains a `blocklist.txt` name, an e-mail address, a phone number or a URL |
| V05 | Item | Question, page or answer not in the declared language (stopword share of manual.md 2.6 above 0.10) |
| V06 | Item | Target does not parse (3.4), or a refusal target is not exactly the sentence |
| V07 | Item | A positive target does not cite every gold position (`pos_multi`) or at least the gold position (others), or cites a position that does not exist |
| V08 | Item | Grounding: fewer than 90% of the content stems of the answer body occur in the context or the question, or a number or quoted string of the body is not in the context; or the verifier said not supported |
| V09 | Item | The measured overlap does not fall in the class's bucket |
| V10 | Item | The question copies four consecutive stems of a gold section (UI labels in `pos_near` excepted) |
| V11 | Item | A near-miss question the verifier finds answered in the domain |
| V12 | Item | A question whose stem set has Jaccard >= 0.8 with an earlier question of the same domain and language |
| V13 | Dataset | Gold position share differs from 1/K by more than 0.03 for any K and position; the rendered prompt exceeds the context budget |
| V14 | Dataset | Class shares differ from config by more than 0.02; language split outside 0.48 to 0.52; refusal share outside 0.25 to 0.45 in any overlap bucket and language |
| V15 | Dataset | A domain id in two splits; a sector of a test or validation domain in train; any 8-stem sequence of the sample app's `docs/` found in any item |

### 5.8 Statistics

`training/out/assistant/dataset/stats.json`, printed as tables by `validate`:

- counts per split, language, class, style and domain;
- overlap histogram (10 bins) per class and language, and the refusal share per overlap bucket;
- gold position histogram per K; context characters p50, p95, max; answer characters p50, p95, max;
- rejections per validator and per stage;
- teacher tokens and seconds per stage;
- `dataset_sha256`: SHA-256 of the three split files. Running `validate` twice on the same cache gives the same
  value (acceptance check).

### 5.9 Example record

`training/out/assistant/dataset/{split}.jsonl`, one JSON object per line:

```json
{
  "schema_version": 1,
  "id": "d07-it-000123",
  "split": "train",
  "domain": "d07",
  "lang": "it",
  "app_name": "{fictional application name}",
  "class": "pos_distant",
  "style": "symptom",
  "question": "{question}",
  "context": [ { "id": "{section id}", "title": "{title}", "text": "{text}" } ],
  "gold": [2],
  "target": "{answer body}\nSource: [2]",
  "overlap": 0.08,
  "seed": 1834221
}
```

Training renders `system` and `user` from these fields with `protocol.py`; the target is the assistant message.

---

## 6. Training and export

### 6.1 Base model candidates and selection

`training/assistant/config/candidates.json` and `training/investigator/config/candidates.json` list the candidates with `id`,
`hub_id`, `revision` (pinned commit), `parameters`, `license`, `context_length`, `chat_template_kwargs`, `kind`, `verified` and
`checked` (the date and source of the last check against the model card). The investigator's entries also have `tier` and
`tool_role`. `trainable: false` marks an **untrained-only** candidate: `eval` and `export` accept it, `train` refuses it, and the lock
records it (`base_model.trainable`). A real run refuses or skips an entry whose `revision` is not a 40-character commit hash or
whose `verified` is not `true`. The shipped entries are pinned from the model cards read on 2026-11-16, and stay `verified: false` until the owner confirms
them on the Windows PC. Requirements for a candidate:

| Requirement | Check |
|-------------|-------|
| Assistant: at most 2.5 billion parameters. Investigator: the ceiling of its file. Neither applies to an untrained-only candidate, which is limited by the machine that serves it | Config |
| Instruction-tuned, chat template with a system role | Template check (6.6) |
| Licence allows commercial use and redistribution of fine-tuned weights | Recorded in the card; reviewed by the owner |
| Writes Italian | Italian results of `eval --candidates` reported separately |
| Exports to ONNX that runs on WebGPU with transformers.js (assistant only), and to GGUF that runs with llama.cpp | `export --candidate {id}` (9.1 step 9) |
| Context of at least 4,096 tokens | Config |

All Qwen models below are Apache-2.0 and were chosen by the owner on 2026-11-16 (Qwen only, no cloud service). Qwen3.5 and later
are published as multimodal checkpoints (a `...ForConditionalGeneration` class with a vision tower) and are used text-only (6.2).
Thinking is on by default for the 4B, 9B, 27B and 35B-A3B models and off for the 0.8B and 2B; every entry passes
`enable_thinking: false`.

| Role | Primary | Revision | Alternative | Trained? |
|------|---------|----------|-------------|----------|
| Assistant | Qwen/Qwen3.5-0.8B | `2fc06364715b967f1860aea9cf38778875588b17` | Qwen/Qwen3.5-2B (`15852e8c16360a2fea060d615a32b45270f8a8fc`); known-working fallback Qwen/Qwen3-0.6B (`kind: fallback`, never chosen by size) | LoRA, if the untrained candidate fails |
| Investigator, small tier | Qwen/Qwen3.5-4B | `851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a` | Qwen/Qwen3.5-2B | LoRA, if the untrained one fails |
| Investigator, large tier | Qwen/Qwen3.6-35B-A3B (mixture of experts, 3B active), 4-bit GGUF | `345d467b117d1fd5eda9b879a5988de9ae5f47a7` | Qwen/Qwen3.5-9B (`c202236235762e1c871ad0ccb60c8ee5ba337b9a`), trained, if the untrained MoE fails | The MoE never: it is used untrained with the protocol prompt and the report grammar (`trainable: false`); LoRA on its fused expert tensors is not supported by the standard PEFT target modules |
| Teacher and judge | Qwen/Qwen3.8-27B, 4-bit GGUF (`37561816e66b5b9153b1b64c10968c18c1e6f4a0`) | | Qwen/Qwen3.6-35B-A3B | Never trained, never shipped |

The Qwen3.5-2B entry sits in both lists. The `qwen3-0.6b` revision is not pinned: pin it before use.

The investigator's `tool_role` is `user` for every Qwen3.5 and later entry. Their chat template renders a `tool` message inside
`<tool_response>` tags and a tools list in an XML-style `<tool_call><function=NAME><parameter=ARG>` format: that is the model's own tool
format, and the investigator protocol (`training/investigator/protocol.json`, operations.md 11.3) is the only definition of calls
and results. The code never passes `tools` to the chat template, our renderers produce every message as plain text, and with
`tool_role: user` the protocol's result turn travels as a plain user turn in training, in the export template check and in evaluation
(`RoleMappedBackend`). Nothing of the native format enters the prompt.

Selection rule: (1) if an untrained candidate passes 4.5, ship the smallest one that passes; (2) otherwise train the
smallest trainable candidate; (3) if it fails after one diagnostic round (section 10), train the next larger candidate;
(4) ship the smallest trained model that passes. Equal size: higher useful wins. A `fallback` candidate counts last in all of these.

### 6.2 Adapter settings

`training/assistant/config/train.json`, starting values:

| Setting | Value |
|---------|-------|
| Method | LoRA on all attention and MLP projection layers, and on the Gated DeltaNet projections of Qwen3.5 (`in_proj_qkv`, `in_proj_z`, `in_proj_b`, `in_proj_a`, `out_proj`); a name a model does not have is ignored. The vision tower is excluded (`exclude_modules`) |
| Rank, alpha, dropout | 16, 32, 0.05 |
| Learning rate | 1e-4, cosine schedule, 50 warm-up steps, weight decay 0 |
| Batch | Effective 32 (micro-batch and gradient accumulation chosen by `hardware` for the device) |
| Maximum sequence length | 2,048 tokens |
| Loss | Assistant tokens only; prompt tokens masked |
| Epochs | At most 2 (early stopping usually ends earlier) |
| Precision | bf16 on CUDA and on Apple MPS where supported, otherwise fp32 on MPS. The adapter is trained in BF16 |
| Seed | `master_seed` of `dataset.json` |

The `loading` block of `train.json` holds the starting values for the Qwen3.5 dense models: load text-only
(`text_only`), freeze the vision tower (`freeze_vision_tower`), no `flash_attention_2` (`attn_implementation: null`) and no sample packing
(`packing: false`; the configuration check refuses `flash_attention_2` with packing). **These are field reports from secondary
sources, not measurements of ours:** treat them as starting values and write what the first run shows back here. The code
loads the model with the causal-LM class and, when the repository's `architectures` names a `...ForConditionalGeneration` class, with that
class (`training/common/loading.py`); the training log must show the target modules actually found. The shipped
`transformers` (5.22.1) and `peft` (0.21.2) pins support Qwen3.5 (transformers 5.2.0 or later; `exclude_modules` needs peft 0.14 or later). They were chosen from the PyPI release lists on 2026-11-16 and have not yet been installed with the training code: check them when the environment is first built.

### 6.3 Early stopping and checkpoint selection

| Rule | Value |
|------|-------|
| Validation run | Every 250 steps, greedy generation on the 320 validation items, automatic metrics only |
| Validation score | `S = 0.35 x (1 - wrongful_refusal) + 0.20 x oos_refusal + 0.20 x near_miss_refusal + 0.25 x correct_source`, counted only if format valid >= 0.98 |
| Stop | No improvement of S by at least 0.005 for 4 consecutive validation runs |
| Memorisation stop | Training loss (moving average over 50 steps) below 0.05: stop and mark the run `memorisation` in `run.json` |
| Kept checkpoints | The 3 best by S |
| Final selection | The 3 kept checkpoints are evaluated with the judge on the validation items; the one with the highest useful among those with faithful >= 0.95 is selected. Test domains are never used for selection |

The training loss is never a selection criterion.

### 6.4 Hardware

Training must be possible on one consumer machine. `python -m training.common.hardware` refuses to train on CPU.

| Machine | Minimum | Expected training time (0.6B, 20,000 examples) |
|---------|---------|-----------------------------------------------|
| NVIDIA GPU | 12 GB VRAM, 32 GB RAM, 40 GB free disk | 1 to 3 hours |
| Apple Silicon | 16 GB unified memory (32 GB for a 1B model), 40 GB free disk | 6 to 10 hours |
| Evaluation only, GGUF on CPU | 8 cores, 16 GB RAM | 1 to 2 hours for the platform test |

Measured times are written in the model card; the table is updated when they differ by more than half.

The investigator's larger tiers need more: the minimum VRAM, the minimum RAM and the micro-batch of `train` depend on the
model's size (`training/common/hardware.py`, `CUDA_BY_SIZE`: 12 GB VRAM up to 1B parameters, 16 GB up to 2.5B, 24 GB up to
5B, 40 GB up to 10B; estimates, no tier has been measured). `train --micro-batch N` replaces the micro-batch after an
out-of-memory error. On Windows with WSL 2 see 9.4, which lists what differs (hardware check, weights folder, 4-bit GGUF
route, line endings).

### 6.5 Merge and exports

`python -m training.assistant.export` from the selected checkpoint (or the base model for an untrained candidate):

| Output | Path under `training/out/assistant/runs/{run_id}/` | Use |
|--------|------|-----|
| Merged weights, bf16 | `merged/` | Reference evaluation; source of both exports |
| ONNX, 4-bit weights, fp16 activations (`q4f16`) | `onnx/` with `onnx/model_q4f16.onnx`, `config.json`, `generation_config.json`, `tokenizer.json`, `tokenizer_config.json` | Browser, WebGPU, transformers.js |
| GGUF, `Q4_K_M` | `gguf/assistant-{model_version}-Q4_K_M.gguf` | Server, llama.cpp, GPU or CPU |
| Manifest | `exports.json`: every file with SHA-256 and size, and the template check result | Lock |

- Exporter versions (transformers.js conversion script, llama.cpp tag) are pinned in `export.json`, with `tag` null until the
  owner pins them; a real export refuses a null tag. The command templates there are unverified readings of each tool's
  command line. The checkouts are found through `TRANSFORMERS_JS_DIR` and `LLAMA_CPP_DIR` (9.1 step 9).
- **Qwen3.5 exports are unverified.** The models are a hybrid architecture (Gated DeltaNet plus attention). Whether the converters
  export a merged fine-tune of it is the first thing to test on the Windows PC (9.1 step 9), with the untrained candidate and before
  any training: merge, convert to GGUF, convert to ONNX, run the template check. Browser: transformers.js 4.0.0 or later supports
  `qwen3_5_text`, and `onnx-community/Qwen3.5-0.8B-Text-ONNX` shows a text-only `q4f16` build of about 0.5 GB running on WebGPU; the export must be text-only
  (`text_only` in `export.json`). GGUF: community builds exist (ggml-org, unsloth) and llama.cpp supports the architectures; pin a recent tag.
- The investigator exports merged weights and GGUF only, named `investigator-{model_version}-{tier}-{variant}.gguf`.
- No WebAssembly variant is built: devices without a real GPU use the server tier (7.1), which runs the same
  weights faster than WebAssembly would.
- If an export fails the export-loss threshold, the next attempt is `Q8_0` for GGUF and 8-bit weights for ONNX
  (`q8`), with `--variant gguf=Q8_0 --variant onnx=q8`, recorded as the variant; if the browser variant still fails, the
  browser tier is disabled for that model version and the card says so.

### 6.6 Evaluating the exports

- `eval --export merged`, `eval --export onnx` (onnxruntime with the same files, in Python) and `eval --export gguf`
  (llama.cpp server started by the eval command) each produce a report; all three must pass 4.5. The investigator is
  the exception: it is evaluated on `merged` and `gguf`, `gguf` is the gating runtime (decoding under the grammar of
  operations.md 11.3) and its report is the one recorded, while the merged weights run under transformers with no grammar,
  so their report is informative and may fail without blocking the record.
- Template check, part of `export`: the rendered chat prompt of every golden case is produced by the Python tokenizer
  (`apply_chat_template`), by transformers.js (`node training/assistant/js/template_check.mjs`) and by llama.cpp
  (`POST /apply-template`); the strings must be equal byte for byte. The investigator's check has no transformers.js part.
- Browser check: on a machine with a real GPU, 20 fixed items from the sample app bench are asked through the help
  panel (`npm run dev`) after the model is recorded (9.1 step 12); answers must equal the ONNX report's answers for those
  items. The result is written in the card's "Browser check" table.

---

## 7. Runtime ladder

### 7.1 Assistant

| `MyApp:Assistant:Order` | Chain per question |
|-------------------------|--------------------|
| `browser-first` (default) | browser -> server -> retrieval |
| `server-first` (virtual desktops, thin clients) | server -> browser -> retrieval |
| `retrieval-only` | retrieval |

- Retrieval always runs first and its sections are on screen before any model answers (manual.md 7.2).
- A tier is skipped when not eligible (7.2, 7.4, 7.5) and abandoned for the current question on error or timeout;
  the next tier gets the same question and sections.
- The profile (operations.md) sets `MyApp:Assistant:Order` for its environment.

### 7.2 Browser tier: real GPU check

Run once per device and model version, in a Web Worker; the result is kept in `localStorage` key
`help.assistant.webgpu.{model_version}` as `ok` or `fail:{reason}` (read and write inside try/catch; when storage
is unavailable the check runs again).

1. `navigator.gpu` exists.
2. `await navigator.gpu.requestAdapter({ powerPreference: 'high-performance' })` is not null.
3. `adapter.info.isFallbackAdapter` (or the older `adapter.isFallbackAdapter`) is not `true`.
4. None of `adapter.info.vendor`, `architecture`, `device`, `description` matches
   `/swiftshader|llvmpipe|lavapipe|softpipe|warp|basic render|software/i`.
5. `adapter.features.has('shader-f16')` for the `q4f16` variant.
6. `adapter.limits.maxBufferSize >= 268435456` and `adapter.limits.maxStorageBufferBindingSize >= 134217728`.
7. `navigator.deviceMemory`, when present, is at least 4.
8. After the model loads: 16 tokens from a fixed prompt at a rate of at least `MinTokensPerSecond`.

Any failure: the tier is not eligible on this device; the reason is kept for the status line of the panel.

### 7.3 Browser tier: weights

| Rule | Value |
|------|-------|
| Location | `{ModelBaseUrl}{model_version}/` (version from the pack's `assistant.model.version`), same origin, no third-party host |
| HTTP caching | `Cache-Control: public, max-age=31536000, immutable`: the version is in the path, so a file never changes |
| Browser cache | transformers.js with `env.useBrowserCache = true` (Cache API) and `env.allowRemoteModels = false`, `env.localModelPath = ModelBaseUrl`; after the first complete download the page requests `navigator.storage.persist()` |
| Download size | About 0.5 GB for the Qwen3.5-0.8B text-only `q4f16` build (Qwen3-0.6B: about 330 MB); the figure comes from the public onnx-community build, confirm it on the first export. It is above the default threshold of 100 MB, so the consent below is always asked |
| Consent | Before a first download larger than `AskBeforeDownloadMb`, the panel asks "Download the assistant to this device ({size} MB)? Answers will then be generated on this device." with "Download" and "Not now"; "Not now" is remembered like the GPU check and the next tier answers |
| Progress | Shown in the panel; the next tier answers meanwhile (manual.md 7.2) |
| Old versions | Cache entries of other model versions are deleted after the new version loads |

The weights reach the web root through `python -m training.common.fetch --model assistant --runtime browser --to
src/frontend/public/models/assistant/` at build or deploy time.

### 7.4 Server tier

- Model host: llama.cpp `llama-server` with the GGUF export, one process per model, bound to `127.0.0.1`.
  Launchers `src/support/model-host/start.sh` and `start.ps1` are shared by both models.
- GPU, then CPU: with `MODEL_HOST_DEVICE=auto` (default) the launcher starts the GPU build with
  `--n-gpu-layers 999` when a GPU is present (CUDA, Metal or Vulkan device found); it waits for `GET /health` and
  reads the start log for the offloaded layer count; if not every layer is offloaded, or the start fails, it stops
  and starts the CPU build with `--n-gpu-layers 0 --threads {physical cores}`. `gpu` or `cpu` forces a device.
- The flags that change what the model writes (`--jinja`) are in `src/support/model-host/shared-flags.txt`, which both launchers
  and the evaluation's llama.cpp backend read, so that what was evaluated is what runs. The evaluation starts its server with
  `--parallel 1`; with `MaxConcurrent` slots the host's context is divided between them (3.5: 2,048 tokens per slot are enough).
- The launcher passes `--alias {MODEL_HOST_ALIAS}@{gpu|cpu}`, `--ctx-size` (default 4096, shared by the slots) and `--parallel`
  (default 2, set to `MaxConcurrent`); the backend reads model and device from `GET /v1/models`.
- Backend endpoints: `GET /api/help/status` (tier settings for the panel, pack and model versions, server
  availability and device), `POST /api/help/answer` (`lang`, `question`, `section_ids`, `pack_sha256`; streams the
  answer), `POST /api/help/gaps`, `GET /api/help/gaps` (Admin, manual.md 7.4).
- `POST /api/help/answer` checks that `pack_sha256` equals the loaded pack, renders the prompt with the C#
  implementation (3.7) from the sections of the loaded pack, calls the host's `/v1/chat/completions` with the
  decoding of 3.8 and the template kwargs of the lock, streams to the browser, and parses the result (3.4).
- At most `MaxConcurrent` generations at once; a request that waits more than `QueueTimeoutSeconds` gets "busy"
  and the panel moves to the next tier.

### 7.5 Compatibility at run time

Generation tiers are eligible only when all hold; otherwise the panel runs retrieval only and the backend logs the
reason once at start-up.

| Check | Where |
|-------|-------|
| `assistant.compatible` is `true` in the pack (manual.md 6.3) | Browser and backend |
| The protocol SHA-256 of the pack equals `models/model.lock` `assistant.protocol.sha256` | Backend at start-up |
| The host alias carries the lock's `model_version` | Backend |
| The browser weights folder is the pack's `assistant.model.version` | Browser (by URL) |

### 7.6 Investigator

The investigator runs only on the server: GPU, then CPU, then rules only. The profile selects the tier (`small`
for CPU-only virtual machines, `large` where a GPU or enough memory exists); the selection algorithm, device
fallback and configuration keys are in operations.md 11.2. The runtime is the llama.cpp host of 7.4 (same
launcher on port 8092 by convention, GGUF export of the selected tier), with constrained decoding through the request's
`grammar` field. Its protocol and thresholds are in operations.md; its lock entry (one export per tier, 8.1) and card
follow section 8.

### 7.7 Configuration keys

Backend configuration (`appsettings*.json`, environment variables with `__`). The panel receives the
`Assistant` values from `GET /api/help/status`; if that call fails, it runs retrieval only.

| Key | Default | Meaning |
|-----|---------|---------|
| `MyApp:Help:PackDirectory` | `../../frontend/public/help` (Development) | Folder of `manifest.json` and the packs |
| `MyApp:Help:Gaps:Enabled` | `true` | Record documentation gaps (manual.md 7.4) |
| `MyApp:Help:Gaps:RetentionDays` | `90` | Gap retention |
| `MyApp:Models:LockPath` | `../../../models/model.lock` (Development) | Lock file read at start-up |
| `MyApp:Assistant:Order` | `browser-first` | `browser-first`, `server-first`, `retrieval-only` |
| `MyApp:Assistant:Browser:Enabled` | `true` | Allow the browser tier |
| `MyApp:Assistant:Browser:ModelBaseUrl` | `/models/assistant/` | Base URL of the ONNX files |
| `MyApp:Assistant:Browser:MinTokensPerSecond` | `8` | Warm-up rate of 7.2 step 8 |
| `MyApp:Assistant:Browser:AskBeforeDownloadMb` | `100` | Consent threshold of 7.3 |
| `MyApp:Assistant:Browser:TimeoutSeconds` | `20` | Per question |
| `MyApp:Assistant:Server:Enabled` | `false` | Allow the server tier (on when a model host is deployed) |
| `MyApp:Assistant:Server:Endpoint` | `http://127.0.0.1:8091` | Model host |
| `MyApp:Assistant:Server:TimeoutSeconds` | `20` | Per question |
| `MyApp:Assistant:Server:MaxConcurrent` | `2` | Parallel generations |
| `MyApp:Assistant:Server:QueueTimeoutSeconds` | `5` | Wait before "busy" |

Model host environment: `MODEL_HOST_MODEL` (GGUF path), `MODEL_HOST_ALIAS` (`{model id}-{model_version}`), `MODEL_HOST_PORT`
(default `8091`, the assistant; `8092` for the investigator), `MODEL_HOST_DEVICE` (`auto`, `gpu`, `cpu`), `MODEL_HOST_PARALLEL`,
`MODEL_HOST_CTX_SIZE`, `MODEL_HOST_THREADS`; the full list is in `src/support/model-host/README.md`.

### 7.8 Help embedding table

The encoder runs only at build time, once per stem, on the owner's machine; the browser and the backend never load it.
Its output is the table of manual.md 6.7. Status: implemented on 2026-11-16 and tested with a fake encoder; never run with the real model, whose Transformers path (`HFEncoder`) follows the model card and is unverified until the first build.

**Model.** `training/assistant/config/embed.json`, block `model`, pinned like the candidates of 6.1. Values read from the
Hugging Face API and the model card on 2026-11-16:

| Field | Value | Source |
|-------|-------|--------|
| `id` | `qwen3-embedding-0.6b` | |
| `hub_id` | `Qwen/Qwen3-Embedding-0.6B` | API |
| `revision` | `97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3` (last modified 2026-04-20) | API `sha` |
| `license` | `Apache-2.0` | Card metadata. The repository has no separate licence file, and the card's prose calls the model "the latest proprietary model of the Qwen family": the owner reviews the licence before the first build |
| `parameters` | `600000000` (card: 0.6B, 28 layers) | Card, `config.json` |
| `dimensions` | `1024`; the model also supports output dimensions from 32 to 1024 (MRL) | `config.json` `hidden_size`, `1_Pooling/config.json` |
| `pooling` | `last_token`; documents take no prompt, queries an instruction prompt | `1_Pooling/config.json`, `config_sentence_transformers.json` |
| Context | 32k tokens (not relevant: inputs are single words) | Card |
| Languages | "100+"; Italian is not named | Card |
| `min_transformers` | `4.51.0` (older versions fail with `KeyError: 'qwen3'`); `training/requirements.txt` pins 5.22.1 (Qwen3.5 needs 5.2.0 or later) | Card |
| Download | About 1.2 GB of BF16 safetensors (estimate from the parameter count) | |
| `verified` | `false` until the owner confirms the entry on the PC, as in 9.1 step 4; a real build refuses `false` unless `--allow-unverified` (rehearsals and tests only) | |

The model is suitable: Qwen, open weights, a commercial licence, multilingual, with a documented pooling. Closest
alternative: `Qwen/Qwen3-Embedding-4B` (2,560 dimensions, a larger download). Because the encoder runs only at build time,
its size costs build minutes and nothing at run time; try it if the Italian gain is poor. It is not pinned here: read its
card and pin it before use.

`embed.json`, starting values:

```json
{
  "schema_version": 1,
  "model": { "id": "qwen3-embedding-0.6b", "hub_id": "Qwen/Qwen3-Embedding-0.6B", "revision": "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3", "license": "Apache-2.0", "parameters": 600000000, "dimensions": 1024, "pooling": "last_token", "min_transformers": "4.51.0", "verified": false, "checked": { "date": "2026-11-16", "source": "Hugging Face API and model card" } },
  "table": { "dims": 128, "max_stems": 40000, "max_bytes": 8388608, "batch_size": 256, "max_tokens": 16, "min_row_cosine": 0.99 },
  "sources": { "locales": "src/frontend/public/locales/{lang}", "glossary": "docs/manual/glossary.md", "wordlists": { "en": null, "it": null } },
  "build": { "min_vocab_coverage": 0.90 },
  "calibration": { "held_out_share": 0.30, "min_items": 30, "rrf_k": [10, 20, 40, 60], "embedding_weight": [0.5, 0.75, 1.0, 1.25, 1.5, 2.0], "cosine_step": 0.01, "max_veto_loss": 0.02, "target_false_no_match": 0.05 }
}
```

A word list is named as `{ "path": "training/assistant/config/words.it.txt", "license": "{licence}" }`; the builder refuses
one without a licence.

**Distillation**, `python -m training.assistant.embed build`, per language:

1. **Sources.** Read the sources of manual.md 6.7.3. The packs come from `--packs` (default `src/frontend/public/help`, so
   run `help:build` first). The builder refuses a pack whose `analyzer.version` or `stopwords_sha256` differs from the
   Python port (`training/assistant/lexical.py`, whose analyzer version equals the TypeScript one through the lexical
   golden file).
2. **Tokenizer words.** Read `vocab.json` of the pinned revision as JSON; no model library is imported. Accept a token
   that starts with the byte-level space marker `Ġ` and whose rest, decoded with the GPT-2 byte-to-unicode table, has at
   least two characters, each a letter whose Unicode name starts with `LATIN`. Lower-case it and apply NFC. Its rank is
   its position among the accepted tokens in ascending token id (the BPE merge order, which approximates frequency); R
   is the number of accepted tokens.
3. **Stems.** Analyse every surface word with steps 1 to 5 of manual.md 6.4; a word that yields no token, or more than
   one, is skipped. Per stem keep its group (`project`, then `wordlist`, then `tokenizer`), its rank (the lowest rank of
   its words among the tokenizer words; R when it has none) and one representative word: for a `project` stem the
   surface word most frequent in the project sources (ties: lower rank, then code point); otherwise the word of lowest
   rank.
4. **Selection.** Every `project` stem; then `wordlist` stems by (rank, stem); then `tokenizer` stems by (rank, stem);
   until `table.max_stems`.
5. **Encoding.** Each representative word once, as the document side (no prompt), with the Transformers path of the
   model card at the pinned revision: the tokenizer with left padding, `AutoModel`, last-token pooling, L2
   normalisation. Weights and activations in float32 (not BF16), `torch.no_grad()`, batches of `table.batch_size` in
   vocabulary order. A word longer than `table.max_tokens` tokens is skipped and counted. A word that both languages
   need is encoded once per run.
6. **Reduction.** Rows as float64. Subtract the mean row; `numpy.linalg.svd(..., full_matrices=False)`; keep the first
   `table.dims` right singular vectors; flip the sign of each so that its entry of largest absolute value (lowest index
   on ties) is positive; project; L2-normalise each row. A row with a norm below 1e-12 is dropped and counted. Choice:
   centring and PCA rather than keeping the first 128 dimensions (MRL), because removing the direction every word
   vector shares is what lets a mean of static word vectors tell texts apart (model2vec, the reference for this kind of
   distillation, does the same). The PCA is fitted per language on that table's rows.
7. **Quantisation.** `scale_j = max_i |y_ij| / 127` (1.0 when the column is 0), stored as float32;
   `q_ij = rint(y_ij / scale_j)` (numpy, half to even) clipped to -127..127. Only Python writes tables, so rounding is
   defined once.
8. **Weights.** `u = max(1, round(255 * ln(2 + r) / ln(2 + R)))` with r the stem's rank: the most frequent general
   words weigh least (Zipf weighting); a stem without a tokenizer rank gets r = R, so 255.
9. **Write and check.** Write the file of manual.md 6.7.2, read it back with the reader, and fail unless every decoded
   row has a cosine of at least `table.min_row_cosine` with its float row and the file is within `table.max_bytes`.
10. **Report.** `build-{lang}.json`: stems per group, skipped words, dropped rows, the lowest row cosine, bytes, device,
    durations, and the 8 nearest stems of the 10 most frequent project stems, for a person to look at.

**Device.** `--device auto` takes CUDA, then Apple MPS, then the CPU. Any of them works: tens of thousands of one-word
inputs take minutes on a GPU and 10 to 30 minutes on a CPU (estimate, not measured).

**Determinism.** Same inputs, device, model revision and library versions give the same bytes (proved with a fake
encoder in the tests). Across devices the encoder's floats can differ in the last bits and, rarely, a quantised value
by one; the lock records the bytes actually built, so exactly one table per language is ever in use.

**Imports.** `torch` and `transformers` are imported inside the encoding function only: `--dry-run`, source reading,
the tokenizer words (read from the Hugging Face cache or from a folder given as `hub_id`, as in 9.1 "Working offline") and
the tests work with the light dependencies. `--dry-run` prints stems per group, the expected file size and the
estimated duration, and writes nothing.

**Run folder** `training/out/embeddings/runs/{run_id}/`: `run.json` (command, model, revision, `embed.json` SHA-256,
device, library versions, code commit, durations, `status: built`), `{lang}.bin`, `build-{lang}.json`, `exports.json`
(one export of `format` `help-embeddings`, its files with SHA-256 and bytes), and later `calibration.json` (manual.md
6.7.7).

**Probe.** `python -m training.assistant.embed probe --run {run_id} --lang {lang} --question TEXT` prints the query stems
with their weights (stems not in the table marked), the top 5 sections lexical, by cosine and fused, and the decision of
manual.md 6.6, using the Python port and the run's `calibration.json` when present. It is how the owner reads a miss on
the PC. It runs no model.

**Tests** (`training/tests/test_embed.py`, no network, no model):

| Test | Proves |
|------|--------|
| Tokenizer words | A small fake `vocab.json`: the space marker, byte-level decoding of accented letters (`Ġperché`), non-Latin and non-letter tokens refused, ranks in token id order |
| Sources | A fixture pack, glossary and locale files: placeholders and tags removed; the indexed questions used; a word that occurs only in a bench question, the Extra table and an out-of-scope question of the fixture is absent from the table; the builder opens no file under `docs/areas/` and not `docs/manual/bench.md` |
| Stems | Representative word and tie-breaks; group priority; `max_stems` never drops a `project` stem |
| Table | With a fake encoder (vectors from the SHA-256 of the word): the PCA sign rule, normalisation, quantisation, weights, the exact layout; the bytes equal `embeddings.table.golden.json`; two builds give identical bytes |
| Reader | Bad magic, newer version, other language, wrong size, unsorted or duplicate vocabulary, weight 0: each refused with its reason |
| Checks | An injected quantisation error fails the row check; a size above `max_bytes` fails |
| Configuration | `verified: false` refused without `--allow-unverified`; a revision that is not 40 hexadecimal characters refused; a word list without a licence refused; a pack with another analyzer refused |
| Imports | Importing `training.assistant.embed` and running `--dry-run` import neither `torch` nor `transformers` |
| Device | The order CUDA, MPS, CPU with the availability mocked |
| Port | The query vectors, section vectors, cosine order, fused order and decisions of `embeddings.golden.json` (manual.md 6.7.9) |
| Lock | `--record` of a fake run writes the entry of 8.1 and copies the calibration report; it refuses a run without `calibration.json`, a calibration of another table, or a language without parameters; `--verify` reports each case of L09; `fetch --model embeddings` checks the files by SHA-256 |

---

## 8. Lock file and model card

### 8.1 `models/model.lock`

JSON, keys in this order, models sorted by id, written only by `lock --record`. `tiers` exists only on an entry with
tiered exports and comes right after `evaluation`. The `embeddings` entry has its own keys, shown after the example.
`models/model.lock.example` shows an entry with fake values.

```json
{
  "schema_version": 2,
  "models": {
    "assistant": {
      "model_version": "1.0.0",
      "status": "trained",
      "base_model": { "hub_id": "{id}", "revision": "{commit}", "license": "{licence}", "parameters": 600000000, "chat_template_kwargs": { } },
      "adapter": { "method": "lora", "rank": 16, "alpha": 32, "checkpoint": "step-2750" },
      "protocol": { "path": "training/assistant/protocol.json", "sha256": "{hex}" },
      "retrieval": { "path": "training/assistant/retrieval.json", "sha256": "{hex}" },
      "contracts": { "manual-page": 1, "help-pack": 2, "assistant-protocol": 1, "assistant-retrieval": 3 },
      "dataset": { "config_sha256": "{hex}", "dataset_sha256": "{hex}", "domains": { "train": 30, "validation": 4, "test": 6 }, "examples": 20000, "teacher_models": ["{id}"] },
      "training": { "run_id": "2026-11-20-a", "code_commit": "{git commit}", "seed": 20261106, "hardware": "{device name}" },
      "exports": [
        { "format": "onnx", "variant": "q4f16", "runtime": "browser", "url": "{artifact store}/assistant/1.0.0/onnx/", "files": [ { "path": "onnx/model_q4f16.onnx", "sha256": "{hex}", "bytes": 0 } ] },
        { "format": "gguf", "variant": "Q4_K_M", "runtime": "server", "url": "{artifact store}/assistant/1.0.0/gguf/", "files": [ { "path": "assistant-1.0.0-Q4_K_M.gguf", "sha256": "{hex}", "bytes": 0 } ] }
      ],
      "evaluation": { "suite_version": 1, "report": "models/assistant/eval/1.0.0.json", "report_sha256": "{hex}", "passed": true },
      "card": "models/assistant/MODEL_CARD.md",
      "recorded_at": "2026-11-20T18:00:00Z"
    },
    "investigator": {
      "model_version": "1.0.0",
      "status": "trained",
      "base_model": { "hub_id": "{id}", "revision": "{commit}", "license": "{licence}", "parameters": 1700000000, "chat_template_kwargs": { } },
      "adapter": { "method": "lora", "rank": 16, "alpha": 32, "checkpoint": "step-2750" },
      "protocol": { "path": ".github/agents/support.agent.md", "markers": ["<!-- protocol:begin -->", "<!-- protocol:end -->"], "sha256": "{hex}", "format": { "path": "training/investigator/protocol.json", "sha256": "{hex}" } },
      "contracts": { "incident-report": 1, "tool-catalog": 1, "investigator-protocol": 1 },
      "dataset": { "config_sha256": "{hex}", "dataset_sha256": "{hex}", "domains": { "train": 17, "validation": 2, "test": 5 }, "examples": 20000, "teacher_models": [] },
      "training": { "run_id": "2026-11-21-a", "code_commit": "{git commit}", "seed": 20261106, "hardware": "{device name}" },
      "exports": [
        { "format": "gguf", "variant": "Q4_K_M", "runtime": "server", "url": "{artifact store}/investigator/1.0.0/gguf/", "tier": "small", "context_length": 8192, "memory_mb": 2300, "files": [ { "path": "investigator-1.0.0-small-Q4_K_M.gguf", "sha256": "{hex}", "bytes": 0 } ] },
        { "format": "gguf", "variant": "Q4_K_M", "runtime": "server", "url": "{artifact store}/investigator/1.0.0/gguf/", "tier": "large", "context_length": 16384, "memory_mb": 6900, "files": [ { "path": "investigator-1.0.0-large-Q4_K_M.gguf", "sha256": "{hex}", "bytes": 0 } ] }
      ],
      "evaluation": { "suite_version": 1, "report": "models/investigator/eval/1.0.0-small.json", "report_sha256": "{hex}", "passed": true },
      "tiers": {
        "small": { "status": "trained", "base_model": { }, "adapter": { }, "dataset": { }, "training": { }, "evaluation": { } },
        "large": { "status": "trained", "base_model": { }, "adapter": { }, "dataset": { }, "training": { }, "evaluation": { } }
      },
      "card": "models/investigator/MODEL_CARD.md",
      "recorded_at": "2026-11-22T18:00:00Z"
    }
  }
}
```

| Field | Rule |
|-------|------|
| `model_version` | Semantic version: major on a protocol or contract change, minor on a retraining with the same protocol, patch on a re-export |
| `status` | `trained` or `untrained-candidate` (then `adapter` and `dataset` are `null`) |
| `contracts` | The formats the model depends on and their versions at training time. Investigator: `required_contracts` of `training/investigator/config/train.json` |
| `exports[].url` | Where the weights are stored (the organisation's artifact store or release assets); never the repository. `{--artifact-url}/{format}/` unless the run's manifest gives one. A file path that starts with the folder's name loses that prefix when downloaded (9.1 step 11) |
| `exports[].tier`, `context_length`, `memory_mb` | Investigator only: `small` or `large`, the context length the export is run with, and the memory it needs; read by the monitor's tier selection (operations.md 11.2). The assistant omits them |
| `tiers` | Investigator only: per tier its `status`, `base_model`, `adapter`, `dataset`, `training` and `evaluation`. Each tier is trained, exported and evaluated by its own run; recording a run of the same `model_version` for another tier adds it and keeps the first. Recording the same tier again replaces only that tier; a different `model_version` starts a new entry. The top-level fields mirror the smallest tier recorded. The report of a tier is `{model_version}-{tier}.json` |
| `protocol` | Assistant: `training/assistant/protocol.json`. Investigator: the protocol block defined in operations.md 11.3, hashed as the bytes between its `markers`, and `format`, the hash of `training/investigator/protocol.json` |
| `retrieval` | Assistant only |
| `recorded_at` | UTC, seconds |

The `embeddings` entry (7.8), keys in this order:

```json
"embeddings": {
  "model_version": "1.0.0",
  "status": "distilled",
  "base_model": { "hub_id": "Qwen/Qwen3-Embedding-0.6B", "revision": "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3", "license": "Apache-2.0", "parameters": 600000000, "dimensions": 1024 },
  "distillation": {
    "config": { "path": "training/assistant/config/embed.json", "sha256": "{hex}" },
    "dims": 128,
    "languages": { "en": { "stems": 0, "inputs_sha256": "{hex}", "analyzer": { "id": "snowball-en", "version": 2, "stopwords_sha256": "{hex}" } } },
    "run_id": "2026-11-20-a", "code_commit": "{git commit}", "device": "{device name}"
  },
  "contracts": { "help-pack": 2, "assistant-retrieval": 3 },
  "exports": [
    { "format": "help-embeddings", "variant": "int8-128", "runtime": "browser", "url": "{artifact store}/embeddings/1.0.0/", "files": [ { "path": "en.bin", "sha256": "{hex}", "bytes": 0 }, { "path": "it.bin", "sha256": "{hex}", "bytes": 0 } ] }
  ],
  "calibration": {
    "report": "models/embeddings/calibration/1.0.0.json", "report_sha256": "{hex}",
    "params": { "en": { "method": "rrf", "depth": 30, "rrf_k": 60, "embedding_weight": 1.0, "min_cosine": 0.55, "min_cosine_lexical": -1.0 } }
  },
  "card": null,
  "recorded_at": "2026-11-20T18:00:00Z"
}
```

| Field | Rule |
|-------|------|
| `model_version` | Major on a change of the table format (`format_version`) or of the embedding model; minor on a rebuild (new vocabulary, `embed.json` changed). It is written into every table's `meta.model.version`, so it changes only with a rebuild. A recalibration of the same tables records again under the same version and replaces `calibration` |
| `status` | Always `distilled` |
| `base_model` | From the `model` block of `embed.json`, with its licence; the run refuses a model whose `verified` is not `true` |
| `distillation` | From `run.json` and the tables' `meta` |
| `contracts` | The formats the tables depend on: `help-pack` (table file and pack block) and `assistant-retrieval` (fusion and decision) |
| `exports` | One export with a file per language; at `{url}{lang}.bin` |
| `calibration` | The run's `calibration.json`, copied to `models/embeddings/calibration/{model_version}.json`, and the `params` of every language (manual.md 6.7.7) |
| `card` | `null`: no trained weights ship, and the model, revision and licence are in the entry |

`--record --model embeddings` reads `run.json`, `exports.json` and `calibration.json` of
`training/out/embeddings/runs/{run_id}/`. It refuses, writing nothing, when one is missing, when `run.json` is not
`status: built`, when `calibration.json` lacks a language of the exports or names another table's SHA-256, or when no
artifact URL is known.
`--record` reads `training/out/{model}/runs/{run_id}/`: `run.json`, `exports.json` and one `eval-{format}.json` for `merged` and for every
export. It refuses, writing nothing, when one is missing or did not pass, when a report is for another version or is marked `partial` (a
`--suite` or `--limit` run, which is written as `eval-{export}-partial.json` or `eval-{suite}-{export}.json` and never replaces the full
report), when no artifact URL is known (`--artifact-url`, or `artifact_url` in `run.json`), when a run holds two tiers, or when a tier's
protocol or contracts differ from the tiers already recorded. The report of `merged` is the one the assistant's entry records; the
investigator's entry records its `gguf` report, and its `merged` report is copied beside it when present but is neither required nor
required to pass (6.6). The other reports are copied beside the recorded one.

### 8.2 Verification (release)

`python -m training.common.lock --verify`:

| Id | Fails when |
|----|-----------|
| L01 | The lock does not parse or has an unknown `schema_version` (2 since the `embeddings` entry; a version 1 lock is read as version 2 without that entry) |
| L02 | A version in `contracts` differs from `docs/contracts.json` (a retraining trigger, section 11) |
| L03 | `protocol.sha256` or `retrieval.sha256` differs from the current file bytes |
| L04 | `evaluation.passed` is not `true`, the report's SHA-256 differs, or its `thresholds_sha256` differs from the current `eval.json`; checked per tier as well |
| L05 | An export has no file list, SHA-256 or URL; a tiered export has no valid `tier`, positive `context_length` or `memory_mb`; two exports of a format for one tier; a tier with an export and no `tiers` section, or the reverse |
| L06 | The card is missing or lacks a required heading (8.3) |
| L07 | A built pack exists and its `assistant.compatible` is `false`, or its `embeddings` block has `locked: false` |
| L08 | Weights present under `$MYAPP_MODELS_DIR/{id}/` (`models/embeddings/weights/` for `embeddings`) differ from the recorded SHA-256 |
| L09 | `embeddings` entry: `base_model.revision` is not 40 hexadecimal characters or `license` is empty; `distillation.config.sha256` differs from the current `embed.json` (rebuild); a language's `analyzer` differs from the current analyzer version or stopword list (rebuild: `help:build` would refuse the table); the calibration report is missing, its SHA-256 differs, or a language of the exports has no `params` |

With no `models/model.lock`, `--verify` reports L01 and exits 1. A project on retrieval only does not run it (section 12),
unless it records embedding tables. L03, L04 and L06 do not apply to the `embeddings` entry.

A `retrieval.json` change alone fails L03 but needs only a new platform evaluation, not training: run `eval` for
each export and `--record` again with the same weights.

### 8.3 Model card

`models/{id}/MODEL_CARD.md`, exactly these H2 headings in order. `card` fills the tables from the lock and the
reports; it preserves the text of `Limitations` and `Browser check`.

| Heading | Content |
|---------|---------|
| `## Summary` | Model id, version, status, one paragraph on what it does |
| `## Intended use` | The feature, the protocol, the languages |
| `## Out-of-scope use` | Anything outside the protocol; answering without a pack |
| `## Base model and licence` | Hub id, revision, licence, parameters |
| `## Training data` | Synthetic only, number of domains per split, examples, class shares, teacher models; the statement that no project content is in the weights |
| `## Evaluation` | Thresholds and results per suite, language and export; judge agreement; human audit |
| `## Exports` | Formats, variants, sizes, SHA-256 |
| `## Runtime` | Tiers, measured latency per hardware, disabled tiers and why |
| `## Browser check` | The 20-item check of 6.6 |
| `## Limitations` | Written by the owner |
| `## Version history` | One line per recorded version |

The `embeddings` entry has no card (`card: null`); L06 does not apply to it.

---

## 9. First training for an adopting project

**Status of this walkthrough.** It has been rehearsed with fake engines and fake tools
(`training/tests/test_rehearsal.py`: a fake teacher, training engine, exporters, judge and answer backend, and for the
investigator a test double of the monitor's executor), and it has not yet been executed on real models. The rehearsal
proves that the commands fit together: their files, hashes and keys. It proves nothing about a real exporter,
tokenizer or model, so the first real run may need adjustments to the exporter commands in `export.json` and to the
pins in `training/requirements.txt`. The candidate ids, licences, parameter counts and context lengths in both
`candidates.json` files were read from the model cards on 2026-11-16 and are unconfirmed until the owner sets `verified` on the PC. The durations are
estimates, not measurements. Write what the first run teaches you back into this section.

**Decide first: a model is optional.** A project may ship on retrieval only, and that is a valid way to ship: the
assistant panel shows the best sections of the manual with links, and it works without any model. The same holds for
operations: without a model the monitor runs on rules, and every known failure still gets its runbook, incident and
notification. Train when the retrieval bench passes and users still need answers in sentences, or when the residual
incidents justify an investigator.

| Without a model | How | What the user or operator gets |
|-----------------|-----|--------------------------------|
| Assistant | Nothing to do: with no `assistant` entry in `models/model.lock` the pack says `compatible: false`. To state it explicitly, `MyApp:Assistant:Order=retrieval-only` | Retrieval tier of manual.md 7.1 |
| Investigator | Nothing to do: with no lock the monitor runs on rules only and says why on the Sources page. To state it explicitly, profile `investigator.tier: none` | Rule verdicts (operations.md 2) |

The embedding table of 9.5 belongs to the retrieval tier: no model runs at query time. Do it before deciding to train
the assistant, because better retrieval is what step 3 of 9.1 measures, and an assistant only answers from what
retrieval found.

On a Windows PC, set the machine up with 9.4 first: the steps of 9.1 and 9.2 then run inside WSL 2 as written, except
where 9.4 says otherwise.

The two models are independent: train one, both or none. What follows assumes the project has replaced the sample
app's areas with its own stories, and has produced its manual ([manual.md
1.3](manual.md#13-producing-the-manual-the-first-time)).

### 9.1 Assistant

Run from the repository root, inside the environment of step 1. Always pass `--run {run_id}` from step 8 on:
`generate` also creates run folders, so "the latest run" may not be the one you mean.

**Step 1. Environment.** Needs Python 3.11 or 3.12 (`train` refuses another version), Node 22.18 or later, git, and
GPU drivers (CUDA 12) or macOS 14 or later on Apple silicon. On Windows, 9.4 W1 to W4 replace this step. 10 minutes. The teacher and the judge need a local
model host (step 4), and nothing else leaves the machine: with the model files copied in, the whole walkthrough runs
without internet (see "Working offline" after step 4).

```
python3.12 -m venv .venv && . .venv/bin/activate
pip install -r training/requirements.txt
```

The file lists the light dependencies first (all the data side and the tests need) and the heavy ones after them
(`torch`, `transformers`, `peft`, `onnxruntime`, and others). The heavy pins are starting values (torch 2.15.0, transformers 5.22.1, peft 0.21.2, accelerate 1.15.0; chosen on PyPI on 2026-11-16, not yet run with the training code): the pinned
`transformers` may be older than the release that first supported a candidate you choose. If so, raise the pin and
run `python -m pytest training/tests`. If it fails: fix the Python or driver version.

**Step 2. Check the machine.** Prints device, memory, disk and the expected duration. 1 minute.

```
python -m training.common.hardware --parameters 600000000
```

Exit 1 means no CUDA or Apple MPS: use a machine of 6.4. A `Problem:` line with exit 0 means the device exists but is
below 6.4; `train` will refuse it.

**Step 3. Make sure the project's help works without a model.** A model does not fix retrieval. 2 minutes.

```
cd src/frontend && npm ci && npm run manual:check -- --strict && npm run help:build -- --strict && npm run help:bench; cd ../..
```

Produces the packs and the retrieval gates of manual.md 8.3. If it fails: fix pages and questions first (the table of
manual.md 7.4), then consider the embedding table (9.5).

**Step 4. Configure, then estimate.** Finish all of this before step 5: the estimate id hashes `dataset.json`, and the
evaluation reports record the SHA-256 of `eval.json` (L04). 30 minutes for the configuration; the commands take 2
minutes.

| File | What to set |
|------|-------------|
| `training/assistant/config/candidates.json` | For each candidate you will measure: confirm `hub_id`, `parameters`, `context_length` and `chat_template_kwargs` against its model card, set `revision` to the 40-character commit hash, review `license` (6.1), set `verified` to `true` and update `checked`. A real run skips or refuses any other entry; `--allow-unverified` is for rehearsals only |
| `training/assistant/config/dataset.json`, `teacher` | `model` and `verifier_model` (the names the local host serves; the same name is fine), `base_url` if the host is not at `http://localhost:8093/v1`, `max_concurrency` (the host's parallel slots), and after the measurement below `tokens_per_second` and `prompt_tokens_per_second`. Optional: `max_hours`, `api_key_env` (5.6) |
| `training/assistant/config/eval.json`, `judge` | `model`, `base_url`, and after the measurement `tokens_per_second` and `prompt_tokens_per_second`. The judge must not be the model under evaluation (4.4) |
| Environment | Nothing is required. `TEACHER_API_KEY` or `JUDGE_API_KEY` (any name set in `api_key_env`) only if a local proxy in front of the host asks for a key |

**The local teacher and judge host.** On Windows: 9.4 W9. Start it before the commands that need it (`--measure`, step 5 onward, steps 6 and
10). Either start the `teacher` service of the compose file with the model file the owner chose (the `teacher` profile of
`compose.yaml`; the file is copied in by hand, nothing is downloaded by the pipeline), or run any
OpenAI-compatible local server (for example `llama-server -m {file}.gguf --port 8093 --parallel 4`) and set `base_url`.
Choose and confirm the model as 5.6 and 4.4 describe: its quality bounds the dataset's quality. Check that the host
answers, and measure its speed, before estimating:

```
python -m training.assistant.generate --measure
python -m training.assistant.eval --measure
```

Each sends a few short requests to the running host and prints the two values to copy into `dataset.json` and `eval.json`.
Run `generate --measure` with the concurrency the real run will use (`--concurrency`). Then:

```
python -m training.assistant.generate --estimate
python -m training.assistant.generate --dry-run
```

`--estimate` prints requests, tokens and the estimated duration per stage and the `estimate_id`, which covers all splits, so
the same id serves steps 5 and 7. `--dry-run` renders every request and prints three per stage. Neither sends a request nor
writes anything, and neither needs the host. A warning that `tokens_per_second` is not set means the duration shown is 0 and
meaningless; a real run refuses it. The duration is an estimate from your measured speed: the real time varies with the
length of the answers and the load on the machine.

**Working offline.** The base models of `candidates.json` come from a model hub by default. To run without internet, copy
each model's folder (weights, tokenizer and `config.json`) onto the machine, put the folder path in that candidate's `hub_id`
(an absolute path works: `from_pretrained` reads a folder as readily as a hub name), keep `revision` as the 40-character
commit hash of the copy, which is only a record for a local folder, and set `HF_HUB_OFFLINE=1`. The licence review of 6.1 and
`verified` apply as before. The weights of a released model are fetched from the recorded URLs (`fetch`); with `--to` and
a local web root that is a folder copy as well. With the teacher model file and the base model folder in place, nothing in
the pipeline needs the network.

**Step 5. Owner approves the estimate. Generate and freeze the evaluation sets.** `[COST]` machine time: read it from the estimate, which was measured on your host. Needs the teacher host running.

```
python -m training.assistant.generate --splits test,validation --approved-estimate {estimate_id}
python -m training.assistant.validate
python -m training.assistant.eval --freeze-sets
```

`generate` fills `training/out/assistant/teacher-cache/` and writes the domain trees to
`training/out/assistant/domains/`. If it fails with "is the teacher host running", start the host (4) and run it again.
When it stops on the time limit or on failed jobs it exits 1: run the same command again, a cached job is never sent
again.

`validate` writes `training/out/assistant/dataset/{train,validation,test,seen}.jsonl`, `rejected.jsonl` and
`stats.json`. Here it exits 1 with exactly one dataset failure, `V14: the train split is empty`, and warns that the
cache is incomplete; both are expected until step 7. Any other dataset failure is real (section 10). `--tools require`
makes it fail when the frontend `manual:check` cannot run on the trees; the default `auto` skips V03's external part
and says so.

`--freeze-sets` writes `test_set_sha256` and `validation_set_sha256` into `eval.json`. A real evaluation refuses a
null or a different value. If rejections exceed 30% in a stage: read `rejected.jsonl`, fix the teacher template, run
`generate` again.

**Step 6. Label by hand, then measure the untrained candidates.** `[COST]` for the judge run, which needs the judge host running. 1 to 2 hours, plus the
labelling.

The judge is calibrated on `training/assistant/eval/human-labels.jsonl`: 120 items labelled by a person (4.4). A
report without the file fails with `calibration labels missing`, so no result counts until it exists. The `labels`
command builds it from the frozen test set, with no judge call. First real answers, so that both labels occur (this
downloads the base model and writes `training/out/assistant/candidates/items-{id}.jsonl`):

```
python -m training.assistant.eval --candidates --no-judge --no-end-to-end --only {smallest candidate id}
python -m training.assistant.labels select --answers training/out/assistant/candidates/items-{id}.jsonl
python -m training.assistant.labels label      # in the terminal; quit with q, run again to continue
python -m training.assistant.labels finish
```

`select` takes 60 in-scope items per language from `test.jsonl` (the classes in turn, in a seeded order) into
`training/out/assistant/labels/todo.jsonl`, and alternates the model's answer with the teacher's reference answer.
`label` shows the numbered sections, the gold section, the reference, the question and the answer, and asks `faithful`
and `useful` (4.3); every answer is saved at once. The file can also be completed in an editor (`faithful` and `useful` to
`true` or `false` on every line). `finish` checks it (counts, ids in the frozen test set, booleans) and writes
`human-labels.jsonl`, one JSON object per line with exactly the keys `id`, `question`, `context`, `gold`, `reference`,
`answer`, `faithful`, `useful`. Without `--answers` every answer is the reference and the calibration proves little;
`select` says so.

The judged run:

```
python -m training.assistant.eval --candidates                            # refused: prints the estimate and its id, runs nothing
python -m training.assistant.eval --candidates --approved-estimate {id}   # after the owner approves that id
```

It writes `models/assistant/eval/candidates/{id}.json` for each candidate and
`training/out/assistant/candidates/summary.json`. The id differs from the one of `eval --estimate` when more than one
candidate runs. Read the `failures` of each report:

| Failure | Meaning | Do |
|---------|---------|----|
| `judge`, `human_agreement.*` | The judge agrees with the person below 0.90 | Revise `eval/judge.v1.txt` and run again before believing any result |
| Any model threshold of 4.5 | The candidate does not qualify | Train (6.1 rule 2) |

An untrained candidate has no transfer gap (4.1), so it can pass here, before the training set exists. If one passes,
skip steps 7 and 8 and go to step 9 with `--candidate {id}`: no training runs, no teacher call is made for the train
split, and the lock entry gets `status: untrained-candidate`. Evaluating its run in step 10 uses the same estimate id
(`eval --estimate --run {run_id}` prints it) and the judged answers are cached.

**Step 7. Generate the training set.** `[COST]` the largest part of the estimate (hours on a CPU host), resumable. The owner approved the id in step 5. Needs the teacher host running.

```
python -m training.assistant.generate --splits train --approved-estimate {estimate_id}
python -m training.assistant.validate
```

`validate` now exits 0, with V13, V14 and V15 passing, and `test.jsonl` keeps its frozen hash. It also writes
`seen.jsonl`, so the judge estimate id changes: take a new one for later steps. If a dataset validator fails: section
10.

**Step 8. Owner approves the plan. Train.** `[COST]` 1 to 10 hours.

```
python -m training.assistant.train --dry-run [--candidate {id}] [--model-version 1.0.0]
python -m training.assistant.train --approved-plan {plan_id} [--candidate {id}] [--model-version 1.0.0]
```

The dry run prints the plan and its `plan_id`, and imports no torch. Run it on the training machine, after pinning:
the id hashes `train.json`, the example count, the seed, the base model and revision, the dataset and the version.

A real run needs that id, CUDA or MPS meeting 6.4, a pinned and verified candidate (default: the smallest),
`judge.model` and the judge host running: the final selection of 6.3 calls the judge. It
downloads the base weights, creates `training/out/assistant/runs/{run_id}/` and writes checkpoints under
`train/checkpoints/step-{n}/`. On success `run.json` has `status: trained` and the selected checkpoint.

To resume, run the same command with `--run {run_id}`; without it a new run starts. `--no-resume` starts over. Exit 1
means no kept checkpoint met `faithful >= 0.95`; a `memorisation` warning means the training loss fell below the
limit. In both cases: section 10.

**Step 9. Export.** 20 to 40 minutes. These tools are outside pip (on Windows: 9.4 W6 and W7):

| Needs | How |
|-------|-----|
| Pinned tags | `llama_cpp.tag` and `transformers_js.tag` in `training/assistant/config/export.json`. A real export refuses a null tag |
| llama.cpp | A checkout at that tag built so that `build/bin/llama-quantize` exists; `export LLAMA_CPP_DIR={checkout}`; `llama-server` on the `PATH` (`export PATH="$LLAMA_CPP_DIR/build/bin:$PATH"`); the Python requirements of its `convert_hf_to_gguf.py` in this environment |
| transformers.js | A checkout at that tag; `export TRANSFORMERS_JS_DIR={checkout}`; the Python requirements of its `scripts/convert.py` in this environment |
| Template check | Node 22.18 or later and the package where the script finds it: `npm install --prefix training/assistant/js --no-save --no-package-lock @huggingface/transformers@{version in src/frontend/package.json}` |

```
python -m training.assistant.export --dry-run --run {run_id}
python -m training.assistant.export --run {run_id}
```

An untrained candidate uses `--candidate {id}` instead of `--run`; it creates a new run with `status:
untrained-candidate`. The dry run prints every external command and what a real export would refuse. The real one
writes `merged/`, `onnx/`, `gguf/` and `exports.json` under the run folder (6.5), and is resumable stage by stage.

| Failure | Do |
|---------|----|
| Exit 2, `refused:` | Pin the tag or set the variable it names |
| An exporter command fails | The commands in `export.json` are unverified readings of each tool's command line: correct them for the pinned tags and record the correction here |
| Exit 1, `TEMPLATE CHECK FAILED` | Fix `chat_template_kwargs` in `candidates.json` and export again |
| Export loss too high at step 10 | `--variant gguf=Q8_0 --variant onnx=q8` (6.5) |
| No browser tier wanted | `--formats gguf` |

**Step 10. Owner approves the estimate. Evaluate every output.** `[COST]` for the judge run, which needs the judge host running. 1 to 3 hours. Merged first:
the exports are compared with its report.

```
(cd src/frontend && npm run help:bench -- --export ../../training/out/assistant/bench.jsonl)
python -m training.assistant.eval --estimate
python -m training.assistant.eval --export merged --run {run_id} --approved-estimate {id} --items training/out/assistant/bench.jsonl
python -m training.assistant.eval --export onnx --run {run_id} --approved-estimate {id} --items training/out/assistant/bench.jsonl
python -m training.assistant.eval --export gguf --run {run_id} --approved-estimate {id} --items training/out/assistant/bench.jsonl --latency-tier server-gpu
```

A partial run (`--suite`) writes `eval-{suite}-{export}.json` and is marked `partial`: it never replaces
`eval-{export}.json`, and `lock --record` refuses it. Without `--items` the report fails on `sample_app`, because the project bench of 4.2 is part of the gate.
`--latency-tier` compares the p95 with its target (`server-cpu` with `--device cpu`). Each command writes
`eval-{export}.json` and `items-{export}.jsonl` in the run folder and exits 1 when a threshold is missed: read the
items, then section 10. A host that stops answering ends the command with a message; start it and run the same command again: it resumes, and judged answers stay cached.

**Step 11. Upload, record, card, verify.** 15 minutes.

Upload the weights to the artifact store. For `--artifact-url {store}/assistant/1.0.0` the exports must answer at
`{url}/gguf/{gguf file}` and at `{url}/onnx/{file}`. An ONNX path that starts with `onnx/` loses that prefix there:
`onnx/model_q4f16.onnx` of the run folder is served as `{url}/onnx/model_q4f16.onnx`.

```
python -m training.common.lock --record --model assistant --run {run_id} --artifact-url {url}
python -m training.common.card --model assistant
python -m training.common.lock --verify --model assistant
python -m training.common.fetch --model assistant --to {temporary folder}
```

`--record` writes the entry in `models/model.lock` and copies the reports to `models/assistant/eval/`. It refuses,
writing nothing, when `eval-merged.json` or the report of an export is missing or did not pass, when the versions
differ, or without a URL. `--verify` names what to fix (8.2). `fetch` to a temporary folder proves that the upload
matches the recorded SHA-256.

**Step 12. Browser check on a real GPU.** 20 minutes. It comes after the record because the pack is `compatible` only
once the lock has the entry.

```
python -m training.common.fetch --model assistant --runtime browser --to src/frontend/public/models/assistant/
cd src/frontend && npm run help:build && npm run dev
```

With the backend running (README), ask the first 10 in-scope items per language of `bench.jsonl` in the help panel.
Each answer must equal the answer of that item in `items-onnx.jsonl` (set `sample_app`). Write the result in the
card's "Browser check" table; `card` keeps that text. If it fails: `MyApp:Assistant:Browser:Enabled=false` for this
version, and say so in the card.

**Step 13. Commit and deploy.** Committing needs the owner's approval. Commit `models/model.lock`, the card and the
reports under `models/assistant/`, the edited config files and `human-labels.jsonl`. On each target:

```
python -m training.common.fetch --model assistant --runtime server
MODEL_HOST_MODEL="${MYAPP_MODELS_DIR:-$HOME/myapp-models}/assistant/assistant.gguf" MODEL_HOST_ALIAS=assistant-1.0.0 src/support/model-host/start.sh
```

Set `MyApp:Assistant:Server:Enabled=true`. A person verifies it in the panel: `GET /api/help/status` reports the model
and the device, and an answer ends with "Answered by the server" or "Answered on this device". If only "Best matches
in the manual" appears, the backend logged why at start-up (7.5).

### 9.2 Investigator

A separate walkthrough: no teacher, no judge and no model host. Tool results come from the monitor's own executor, so the
machine needs the .NET 10 SDK. It trains one tier at a time; start with `small`. The environment and step 2 of 9.1
apply.

**Step 1. Set the masking key.** Every tool call goes through the monitor's masking, and without the key every call
fails. Keep the same value for generation and evaluation.

```
export Monitor__Masking__HashKey='{at least 32 bytes}'
dotnet build src/support/MyApp.Monitor
```

**Step 2. Write the project's bench scenarios.** The suite `sample-app` reads
`docs/support/bench/investigator/{scenario id}.json` and `worlds/{scenario id}/` beside them. They are written by hand
(operations.md 11.4) and they describe the sample app: replace them with scenarios of the project's own log keys and
stories, in the same shape. `BenchScenarioTests` checks them. Never use them for training.

**Step 3. Generate the synthetic incidents.** No model host; many hours, since each tool call starts one `dotnet run`
process. Resumable.

```
python -m training.investigator.generate --dry-run
python -m training.investigator.generate --split holdout --limit 2      # a small check; the full run starts over
python -m training.investigator.generate --split all
```

It writes `training/out/investigator/raw/{train,validation,holdout}.jsonl`, and for the held-out split `scenarios/`
and `worlds/`. Exit 1 prints `stopped:` with the reason; `the monitor's tool command did not print an envelope`
usually means the key of step 1 is missing. A changed world or protocol starts that split over.

**Step 4. Validate and freeze.**

```
python -m training.investigator.validate
python -m training.investigator.eval --freeze-sets
```

`validate` writes `dataset/{train,validation,holdout}.jsonl`, `rejected.jsonl` and `stats.json`, and exits 1 on IV07
or IV08. `--freeze-sets` writes `holdout_set_sha256` into `training/investigator/config/eval.json`; do this before any
report, because reports record that file's SHA-256 (L04).

**Step 5. Pin the candidates and measure them.** No model host, long compute. In
`training/investigator/config/candidates.json` confirm each entry, set `revision` to a 40-character commit hash,
review `license`, set `verified` to `true` and update `checked`. `tool_role` is `user` for Qwen3.5 and later (6.1). The large tier's Qwen3.6-35B-A3B is untrained-only (`trainable: false`): measure it here, export it, and train the Qwen3.5-9B only if it fails.

```
python -m training.investigator.eval --candidates --tier small
```

It writes `models/investigator/eval/candidates/{id}.json`. Candidates run under transformers, without the constrained
decoding the runtime uses (operations.md 11.5), so read `report_valid` as a lower bound. If one passes, go to step 7
with `--candidate {id} --tier small`, and pass `--tier small` to `eval` in step 8.

**Step 6. Owner approves the plan. Train the tier.** `[COST]` long run.

```
python -m training.investigator.train --tier small --dry-run
python -m training.investigator.train --tier small --approved-plan {plan_id}
```

Always pass `--tier`: without it the run records no tier. To resume, add `--run {run_id}`. The hardware check is the
table of 6.4, which was written for the assistant's size: a large tier may need more.

**Step 7. Export the tier.** Needs `llama_cpp.tag` in `training/investigator/config/export.json` and the llama.cpp
setup of 9.1 step 9; no ONNX and no transformers.js. 20 to 40 minutes.

```
python -m training.investigator.export --dry-run --run {run_id}
python -m training.investigator.export --run {run_id}
```

It writes `merged/`, `gguf/investigator-{version}-{tier}-Q4_K_M.gguf` and `exports.json`, whose entry carries `tier`,
`context_length` and `memory_mb`.

**Step 8. Evaluate.** No judge and no model host; long compute. `gguf` is the gating runtime: `eval` starts its own
`llama-server` and drives the closed loop through the monitor's executor.

```
python -m training.investigator.eval --export merged --run {run_id}
python -m training.investigator.eval --export gguf --run {run_id}
```

It writes `artifacts/eval/investigator/{suite}-{tier}.json` and, in the run folder, `eval-{export}.json` and
`items-{export}.jsonl`. Exit 1 means a threshold of operations.md 11.4 was missed. `--suite` and `--limit` write
`eval-{export}-partial.json` and `items-{export}-partial.jsonl` instead, marked `partial`: they never replace the full
report, and `lock --record` refuses a partial one.

`lock --record` records the `gguf` report and requires it to pass: the merged weights run under transformers without
constrained decoding, so `eval-merged.json` is informative. It is copied beside the recorded report when present, and
it may fail (typically on `report_valid`) without blocking the record. Never lower a threshold or edit a report.

**Step 9. Upload and record the tier.** Upload the GGUF so that it answers at `{url}/gguf/{file}`.

```
python -m training.common.lock --record --model investigator --run {run_id} --artifact-url {url}
python -m training.common.card --model investigator
python -m training.common.lock --verify --model investigator
```

**Step 10. Optionally repeat steps 6 to 9 for `large`,** with `--tier large` and the same `--model-version`. The
second `--record` adds the tier to the entry (8.1); a different version starts a new entry and loses the first tier.
It refuses when the protocol or contracts differ.

**Step 11. Start the model host.** Fetch has no tier filter: it downloads every recorded tier into `$MYAPP_MODELS_DIR/investigator/`,
as `investigator.gguf` (small) and `investigator-large.gguf` (large), the names compose expects.

```
python -m training.common.fetch --model investigator --runtime server
MODEL_HOST_MODEL="${MYAPP_MODELS_DIR:-$HOME/myapp-models}/investigator/investigator.gguf" \
MODEL_HOST_ALIAS=investigator-1.0.0 MODEL_HOST_PORT=8092 MODEL_HOST_PARALLEL=1 MODEL_HOST_CTX_SIZE=8192 \
src/support/model-host/start.sh
```

`MODEL_HOST_ALIAS` is `investigator-{version}` for every tier, and the launcher adds `@gpu` or `@cpu`.
`MODEL_HOST_PORT` must be given, since the launcher defaults to the assistant's 8091. `MODEL_HOST_CTX_SIZE` is the
export's `context_length` (`small` 8192, `large` 16384), and `MODEL_HOST_PARALLEL` is 1 so the one slot gets all of
it. For both tiers, start a second host on another port (`large`: 8094) with its own file and context. The launcher
stays in the foreground and falls back from GPU to CPU by itself (7.4). `start.ps1` has the same logic but has not
been run (9.4 W11). `curl -s http://127.0.0.1:8092/v1/models` shows the alias.

**Step 12. Start the monitor and verify on the Sources page.**

```
export Monitor__Masking__HashKey='{at least 32 bytes}'
export Monitor__ContentRoot="$PWD/src/support/MyApp.Monitor"
export Monitor__Investigator__ModelLock="$PWD/models/model.lock"
export Monitor__Investigator__Endpoint=http://127.0.0.1:8092
export Monitor__Investigator__Endpoints__large=http://127.0.0.1:8094     # only with a second host
dotnet run --project src/support/MyApp.Monitor -- run --profile onprem
```

Set `investigator.tier` in the profile to the tier you want to use; `large` tries `small` next. The alias does not
name the tier, so the endpoint decides which host answers for it. Open `http://localhost:5310/sources`. The monitor
checks the host at start and every `Monitor:Investigator:ProbeSeconds` (30), so the order of starting does not matter.
The card "Investigator" should read `State ready`, the tier, the device and the model version. Otherwise its text
gives the reason:

| The card says | Do |
|---------------|----|
| `the model lock was not found` | Set `Monitor:Investigator:ModelLock` |
| `has no investigator entry`, or `no server export with a tier` | Step 9 has not been done for this lock |
| `the protocol the model was trained with is not the protocol of the loaded pack` | `support.agent.md` changed after training: section 11 |
| `no host answers at` | Step 11, or a wrong endpoint |
| `the host serves 'x', the lock has investigator-1.0.0` | `MODEL_HOST_ALIAS` differs from the lock's version |
| `the host runs on the cpu, the profile requires the gpu` | The launcher fell back: read its output; or set `Monitor:Investigator:Device` |
| `the {n} MB the export needs are more than this machine has` | Use `small`, or a larger machine |
| `State disabled` | Three investigations failed in a row: rules only until restart. Read the incident's `investigation` and the host output |

**Step 13. Verify an investigation.** A wiring check, not a measure. Create an empty test file, point
`Monitor:Settings:LogGlob` at it, start the monitor, and append one line it has never seen, with the current time:

```json
{"Timestamp":"{now, UTC}","Level":"Error","Category":"MyApp.Check","Message":"Model host wiring check, this message is new","Service":"myapp-api"}
```

An unknown line at level `Error` is a residual case (operations.md 6.7). An incident appears at `/`; its page shows
the rule and model verdicts side by side, and the model block names tier, device and version. `Investigate again`
repeats it. With no database or backend connected the tools answer `error`, so an `abstain` is a correct result here,
and the health source opens its own incident after three failed polls.

**Step 14. Measure the tier on the project's bench.** It runs on the run's export, not on the host.

```
python -m training.investigator.eval --export gguf --suite sample-app --tier small --run {run_id}
```

Read `artifacts/eval/investigator/sample-app-small.json`: `metrics`, `failures`, `passed`. `--export` is required:
without it the command prints the usage. This is a partial run: it writes `eval-gguf-partial.json` and leaves `eval-gguf.json`
of the run as it was.

**Step 15. Commit.** With the owner's approval: `models/model.lock`, the card and reports under
`models/investigator/`, the two edited config files, and the bench scenarios. Weights never.

### 9.3 When the project only needs the template's model

A project may adopt the model recorded by the template's owner (same contracts, same protocol): copy the lock entry
and card, fetch the weights, and run step 3 of 9.1 and the project bench of manual.md 8.4. If L02 or L03 fails, the
project's contracts differ and it trains.

### 9.4 On a Windows PC with an NVIDIA GPU

**Status.** Written on 2026-11-16 from a reading of the pipeline's code and of the tools' documentation. **Nothing in this
subsection has been run: every command is untested, and every size, memory figure and duration is an estimate.** Correct
it with what the first session on the PC shows.

This subsection replaces only what differs on Windows: the machine setup (W1 to W8), the teacher and judge host (W9), the
order of the steps on one GPU (W10) and the native Windows model host (W11). Every step of 9.1 and 9.2 not named here runs
unchanged, command for command, in the WSL terminal.

#### Decision: WSL 2 with Ubuntu 24.04 is the supported path

**Reason in one line:** the pipeline, its launcher and its tests are written and rehearsed for Linux and macOS, and in WSL 2 they
run as they are, with CUDA, while native Windows would need code changes and has no fast kernels for the Qwen3.5 architecture.

| Concern | WSL 2 (Ubuntu 24.04) | Native Windows Python |
|---------|----------------------|-----------------------|
| PyTorch with CUDA | The Linux wheels on PyPI are CUDA builds; the Windows driver serves CUDA inside WSL | The PyPI wheels for Windows are CPU-only: the PyTorch index URL is needed, or torch finds no GPU |
| Qwen3.5 linear-attention kernels (`flash-linear-attention`, `causal-conv1d`, both on Triton) | Installable (reported, not verified by us); without them transformers falls back to a slower implementation | Triton has no official Windows build |
| PEFT, transformers, onnx, onnxruntime | Yes | Yes |
| bitsandbytes | Not used: the adapters are trained in BF16 (6.2) | Not used |
| llama.cpp `convert_hf_to_gguf.py`, transformers.js `scripts/convert.py` | Python (the interpreter running the pipeline, or the one named by `python_env` in `export.json`) | Python; `export` finds `llama-quantize` under `build/bin` and `build/bin/Release`, with or without `.exe` |
| Our code | As rehearsed: `start.sh`, `npm`, `dotnet`, `nvidia-smi` | `npm` is started by its full path (`npm.cmd`), the memory check uses the Windows API instead of `wmic`, `start.ps1` has never run |
| File system | ext4 inside WSL: fast | NTFS: fast. But a repository under `/mnt/c` used from WSL is very slow (the teacher cache writes hundreds of thousands of small files; the investigator starts one `dotnet run` per tool call) |
| Line endings | Clone inside WSL: LF, as on macOS | Git for Windows may check out CRLF, which changes the SHA-256 of `protocol.json`, of the teacher templates and of the protocol block of `support.agent.md` (estimate ids, cache keys, L03). The repository's `.gitattributes` sets LF for every text file and for `*.sh`; the executable bit of the `.sh` files is set when committing |

Native Windows Python is **not supported** for the pipeline. Native Windows **is** supported for the model host alone (W11),
because that is what a project's Windows servers run.

#### Decision: the teacher and judge host runs inside WSL

It runs in the same WSL distribution, from the CUDA build of llama.cpp at the pinned tag (W6), started by `start.sh` on port
8093. One llama.cpp version then serves the teacher, the export, the template check and the GGUF evaluation;
`http://localhost:8093/v1` works with no network setting; the GGUF file is read from ext4.

| Alternative | Status | Note |
|-------------|--------|------|
| Docker Desktop (WSL 2 back end) with `compose.gpu.yaml`, profile `teacher` | Best effort | The image's llama.cpp version is not the pinned one (acceptable for the teacher, not for the evaluation). Whether the port published on Windows' 127.0.0.1 answers inside the WSL distribution is not checked: test with `curl` first. Set `TEACHER_PARALLEL` and `TEACHER_CTX_SIZE` in `.env` as in W9 (the defaults are 1 slot and 8,192 tokens) |
| Native Windows release of llama.cpp with `start.ps1` (W11) | Best effort | Inside WSL, `localhost` reaches Windows only with mirrored networking (`networkingMode=mirrored` in `.wslconfig`, Windows 11 22H2 or later). Never bind the host to `0.0.0.0` to work around it |

#### One GPU, one job at a time

The teacher (27B, 4-bit) and a training run do not fit together on one consumer GPU, and they compete for it even when they
do. They never run at the same time. The order is:

| Phase (9.1 / 9.2 step) | On the GPU | What to do |
|------------------------|------------|------------|
| `--measure`, generation (9.1 steps 4, 5, 7) | The teacher host alone | W9 |
| Untrained candidates, judged (9.1 step 6) | The judge host and the candidate (transformers, BF16) | Start the judge with one slot (below); with it, the 0.8B and the 2B fit on 24 GB (estimate) |
| Training loop (9.1 step 8, 9.2 step 6) | The training alone | Stop the host (Ctrl+C in its terminal) before `train` |
| Final selection, at the end of `train` (6.3) | The judge host and the student | When the loop ends, the first judge call fails with `is the teacher host running`. Start the host and run the same `train` command with `--run {run_id}`: `state.json` already holds the stop reason, so the run goes straight to the final selection. (Read from the code, not tested) |
| Export (9.1 step 9, 9.2 step 7) | Nothing; the template check starts its own `llama-server` on the CPU | Stop the host if it is running |
| Judged evaluation (9.1 step 10) | The judge host and the evaluated output | As the final selection |
| Investigator evaluation (9.2 steps 5, 8) | The candidate, or the `llama-server` that `eval` starts for the export | Stop the teacher host |

If the judge and the student do not fit together, start the judge with one slot (`MODEL_HOST_PARALLEL=1`,
`MODEL_HOST_CTX_SIZE=8192`); if that is still too much, run that model's final selection and judged evaluation on a machine
with more VRAM. The Windows desktop also takes 0.5 to 1.5 GB of the GPU it is connected to (estimate).

#### Hardware per step (estimates)

All figures are **estimates** from parameter counts and file sizes, not measurements. Write measured values back here.

| Step | VRAM, minimum | VRAM, comfortable | RAM (Windows total) |
|------|---------------|-------------------|---------------------|
| Teacher Qwen3.8-27B `Q4_K_M` (about 17 GB of weights) at 4 slots x 8,192 tokens | 24 GB at 2 slots | 32 GB | 32 GB |
| Embedding table, Qwen3-Embedding-0.6B in float32, one-word batches of 256 (9.5) | 4 GB, or none on the CPU | 8 GB | 16 GB |
| LoRA, Qwen3.5-0.8B, BF16, 2,048 tokens | 12 GB | 16 GB | 32 GB |
| LoRA, Qwen3.5-2B | 16 GB | 24 GB | 32 GB |
| LoRA, Qwen3.5-4B | 24 GB | 32 GB | 48 GB |
| Final selection or judged evaluation: judge at 1 slot plus student | 24 GB with the 0.8B | 32 GB with the 4B | 48 GB |
| Qwen3.6-35B-A3B untrained, `Q4_K_M` GGUF (about 21 GB), 16,384 tokens, through `eval --export gguf` | 24 GB (tight) | 32 GB | 32 GB; 48 GB for `--device cpu` |
| Making that GGUF with today's `export --candidate` (BF16 merge on the CPU) | none | none | About 80 GB including WSL swap |

Under `eval --candidates` the 35B-A3B would load whole in BF16 (about 70 GB) onto one device: it cannot be measured that way on
a consumer GPU, and the investigator's `eval --candidates` **refuses** a candidate whose BF16 weights do not fit the device,
naming the route below. Making the GGUF from BF16 (`export --candidate ... --from-bf16`) needs about 80 GB of RAM and 165 GB of
disk. The route that needs neither is an **existing 4-bit GGUF**:

1. The `gguf` block of `qwen3.6-35b-a3b` in `training/investigator/config/candidates.json` names the expected source
   (`unsloth/Qwen3.6-35B-A3B-GGUF`, `Qwen3.6-35B-A3B-Q4_K_M.gguf`: unverified, take the exact name from the Hugging Face page) or a
   file made by the owner from the pinned revision. Download it into `~/myapp-models/investigator/`.
2. `python -m training.common.weights {file}` prints its SHA-256; write it into `sha256` of the block. The file is pinned by that
   hash: `eval` and `export` refuse it while the hash is empty or different.
3. `eval --candidates --tier large --only qwen3.6-35b-a3b` serves the file with the local llama.cpp host (`--gguf {file}` if it is
   elsewhere). The report names the repository, file, quantisation and hash.
4. `export --candidate qwen3.6-35b-a3b --tier large` links the file into the run (no merge, no conversion), runs the template check
   against the pinned revision's tokenizer, and the lock records the file's hash in its `files` list (9.2 steps 7 and 8).

The hardware check of 9.1 step 2 takes the model size: `python -m training.common.hardware --parameters 4000000000` applies the
minimums of "Hardware per step" and a smaller micro-batch for larger models. If training still runs out of memory, pass
`--micro-batch N` to `train`.

| Disk item | Size (estimate) |
|-----------|-----------------|
| Ubuntu, build tools, CUDA toolkit, Node, .NET SDK | 15 GB |
| Python environment with CUDA PyTorch | 10 GB |
| llama.cpp build, transformers.js checkout, Node packages | 3 GB |
| Teacher GGUF | 17 GB |
| Base models in the Hugging Face cache: 0.8B 2 GB, 2B 5 GB, 4B 9 GB, 9B 19 GB, 35B-A3B 70 GB; Qwen3-Embedding-0.6B 1.2 GB | 106 GB for all |
| Teacher cache, domain trees, datasets; investigator raw data and datasets | 7 GB |
| Checkpoints of one training run (adapters and the resume state) | 2 GB |
| Exports of one run (merged BF16, temporary BF16 GGUF, `Q4_K_M`, ONNX) | 0.8B 8 GB, 2B 15 GB, 4B 25 GB |
| Untrained 35B-A3B export with today's code: merged 70, BF16 GGUF 70 (deleted after quantising), `Q4_K_M` 21 | 165 GB at peak |

Plan for **300 GB** free for the assistant and the small investigator tier and **500 GB** with the large tier, on an NVMe
drive: the drive that holds the WSL virtual disk (`ext4.vhdx`, on `C:` by default).

#### W1. Windows (PowerShell as administrator, once)

```powershell
nvidia-smi                      # the Windows driver; it also provides CUDA inside WSL
wsl --install -d Ubuntu-24.04
wsl --update
wsl --version                   # WSL 2
```

Install no NVIDIA driver inside Linux. Create `%UserProfile%\.wslconfig`, then run `wsl --shutdown` to apply it:

```
[wsl2]
memory=48GB
swap=32GB
```

`memory` caps the RAM WSL may use (default: half of Windows' RAM); leave Windows about a quarter. Raise `swap` for the BF16
merge of the 35B-A3B. Add `networkingMode=mirrored` only for a host on the Windows side (W11). During `[COST]` runs, switch
sleep off and pause Windows Update restarts: a reboot stops the run (every step resumes, 9.1).

#### W2. Ubuntu (once)

```bash
sudo apt update && sudo apt install -y build-essential cmake git curl tmux python3.12 python3.12-venv python3.12-dev libcurl4-openssl-dev
wget https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb
sudo dpkg -i cuda-keyring_1.1-1_all.deb && sudo apt update && sudo apt install -y cuda-toolkit-12-8
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash - && sudo apt install -y nodejs
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
cat >> ~/.bashrc << 'EOF'
export PATH=/usr/local/cuda/bin:$HOME/.dotnet:$PATH
export DOTNET_ROOT=$HOME/.dotnet
EOF
. ~/.bashrc
git config --global core.autocrlf input
nvidia-smi && nvcc --version && node --version && dotnet --version
```

The CUDA toolkit is only for building llama.cpp (W6). Install the `cuda-toolkit-*` package of the **WSL-Ubuntu** repository,
never `cuda` or `cuda-drivers`, which bring a Linux driver. Take 12.8 or later for an RTX 50 series GPU. The .NET SDK is for
the investigator (9.2).

#### W3. The repository in the Linux file system

```bash
mkdir -p ~/src && cd ~/src && git clone {repository url} stdd && cd stdd && git checkout {branch}
file src/support/model-host/start.sh       # must not say "CRLF"
test -x src/support/model-host/start.sh || chmod +x src/support/model-host/start.sh
```

Work in `~/src/stdd`, never under `/mnt/c`. Windows Explorer and editors reach it at `\\wsl.localhost\Ubuntu-24.04\home\{user}\src\stdd`.
Do not copy `.venv`, `node_modules`, `bin` or `obj` from another machine: they hold binaries for that machine.

#### W4. Python environment (9.1 step 1)

```bash
python3.12 -m venv .venv && . .venv/bin/activate
pip install -r training/requirements.txt
python -c "import torch; print(torch.__version__, torch.version.cuda, torch.cuda.is_available(), torch.cuda.get_device_name(0))"
python -m pytest training/tests
```

The last line of the check must print `True` and the GPU's name. Raise the pins as 9.1 step 1 and 6.2 say. The pinned
`torch==2.15.0` comes from the default index: the Linux wheel is built for CUDA 13 and needs a recent NVIDIA driver, the
Windows wheel is CPU only. For another CUDA version, or an RTX 50 series GPU that needs CUDA 12.8 or later, install a release
from the matching index (`pip install torch=={version} --index-url https://download.pytorch.org/whl/cu128`) and write the pin
into `requirements.txt`. `transformers` 5.22 needs Python 3.11 or later. If the
training log of 9.1 step 8 says the fast path of the linear attention is not available, try `pip install
flash-linear-attention causal-conv1d` (they need `nvcc` from W2) and compare the speed.

#### W5. Check the machine (9.1 step 2)

`python -m training.common.hardware --parameters 600000000`, as in 9.1. Under WSL it says so, finds `nvidia-smi` at
`/usr/lib/wsl/lib/nvidia-smi` when it is not on the `PATH`, and prints the RAM of the WSL limit of W1. The free disk is the
virtual disk's maximum, not the free space of the Windows drive (check that in PowerShell with `Get-PSDrive C`); a working
folder under `/mnt/c` reports that drive's free space, with a note to leave it.

#### W6. llama.cpp at the pinned tag, CUDA build

```bash
git clone https://github.com/ggml-org/llama.cpp ~/tools/llama.cpp
cd ~/tools/llama.cpp && git checkout {llama_cpp.tag}
cmake -B build -DGGML_CUDA=ON -DCMAKE_BUILD_TYPE=Release && cmake --build build -j
cd ~/src/stdd
cat >> ~/.bashrc << 'EOF'
export LLAMA_CPP_DIR=$HOME/tools/llama.cpp
export PATH="$LLAMA_CPP_DIR/build/bin:$PATH"
EOF
. ~/.bashrc && . .venv/bin/activate
pip install sentencepiece protobuf
```

This is the llama.cpp row of 9.1 step 9 and the server of the teacher (W9) and of the GGUF evaluation. The converter imports
`gguf` from its own checkout. Do not install the checkout's `requirements*.txt` into `.venv` without reading them: they may pin a
CPU build of torch. After any install, run the `torch` check of W4 again.

#### W7. transformers.js and the template check (9.1 step 9)

```bash
git clone https://github.com/huggingface/transformers.js ~/tools/transformers.js
git -C ~/tools/transformers.js checkout {transformers_js.tag}
echo 'export TRANSFORMERS_JS_DIR=$HOME/tools/transformers.js' >> ~/.bashrc && . ~/.bashrc && . .venv/bin/activate
pip install --dry-run -r "$TRANSFORMERS_JS_DIR/scripts/requirements.txt"
npm install --prefix training/assistant/js --no-save --no-package-lock @huggingface/transformers@{version in src/frontend/package.json}
```

`export` starts the converter with the interpreter that runs the pipeline (`sys.executable`, the training environment). Install
the converter's requirements there only if the dry run changes none of `torch`, `transformers` or `peft`. Otherwise give the
converter its own environment (`python3.12 -m venv ~/tools/tjs-venv`, install its requirements there) and export
`TRANSFORMERS_JS_PYTHON=~/tools/tjs-venv/bin/python` (the variable named by `python_env` in `export.json`; the same for
llama.cpp with `LLAMA_CPP_PYTHON`).

#### W8. Model files

- Weights folder: model files that this pipeline does not make live **outside the repository**, in `$MYAPP_MODELS_DIR`
  (default `~/myapp-models`; create it, and keep it in the Linux file system). The git-ignore of `weights/` folders is only a
  safety net.
- Teacher: the `Q4_K_M` GGUF of Qwen3.8-27B at `~/myapp-models/teacher/Qwen3.8-27B-Q4_K_M.gguf`. Its source is the
  `teacher.weights` block of `training/assistant/config/dataset.json`: a community conversion (`unsloth/Qwen3.8-27B-GGUF`, file
  `Qwen3.8-27B-Q4_K_M.gguf`; repository and file name unverified until the owner opens the Hugging Face page) or one made with
  llama.cpp from `Qwen/Qwen3.8-27B` at revision `37561816e66b5b9153b1b64c10968c18c1e6f4a0`. After the download run
  `python -m training.common.weights {file}` and write the hash into `sha256` of that block; `python -m training.common.weights
  --check teacher` verifies it later. The compose `teacher` profile mounts `MODELS_DIR`: point it at the same folder.
- Base models: downloaded into `~/.cache/huggingface` by the first command that needs them, or copied in (9.1 step 4,
  "Working offline").
- A file downloaded with a Windows browser is in `/mnt/c/Users/{name}/Downloads/`: copy it into the Linux file system with `cp`
  before use.

#### W9. The teacher and judge host (replaces the host paragraph of 9.1 step 4)

In a terminal of its own (`tmux new -s teacher`):

```bash
cd ~/src/stdd
MODEL_HOST_MODEL=$HOME/myapp-models/teacher/Qwen3.8-27B-Q4_K_M.gguf MODEL_HOST_ALIAS=qwen3.8-27b \
MODEL_HOST_PORT=8093 MODEL_HOST_DEVICE=gpu MODEL_HOST_PARALLEL=4 MODEL_HOST_CTX_SIZE=32768 \
MODEL_HOST_START_TIMEOUT=600 src/support/model-host/start.sh
```

In another: `curl -s http://localhost:8093/v1/models` names `qwen3.8-27b@gpu`. Then set `max_concurrency` in `dataset.json`
to `MODEL_HOST_PARALLEL` and run the `--measure` commands of 9.1 step 4.

- `gpu`, not `auto`: `auto` falls back to the CPU when a layer stays off the GPU, and a 27B teacher on the CPU turns
  generation into weeks. `gpu` keeps it on the GPU and warns.
- 8,192 tokens per slot (`CTX_SIZE` = slots x 8,192) leaves room for the `domain` stage, which asks for up to 4,000 output
  tokens (estimate of the need).
- If the start fails for memory: `MODEL_HOST_PARALLEL=2 MODEL_HOST_CTX_SIZE=16384`, then measure again.
- Keep the WSL terminal open for the whole run: closing the last one may stop the distribution. `tmux attach -t teacher`
  returns to the host.

Expected order of magnitude for the default dataset (estimate, not measured): 10 million output tokens at about 100 tokens
per second over 4 slots, plus 80 million input tokens at about 2,000 per second, is 1.5 to 2 days. `--estimate` gives the
figure from the measured speed.

#### W10. The rest of the walkthrough

9.1 steps 3 to 13 and 9.2 steps 1 to 15 run as written, in the WSL terminal with `.venv` active, in the order of the table "One
GPU, one job at a time". Where they differ:

| Step | On Windows |
|------|------------|
| 9.1 step 8 | Stop the host first; resume the final selection with the host running (table above) |
| 9.2 step 5, large tier | `eval --candidates` from the pre-made 4-bit GGUF (above); BF16 is refused |
| 9.1 step 12, browser check | Untested: a Windows browser reaches `npm run dev` in WSL at `localhost`, but the HTTPS development certificate made inside WSL is not trusted by Windows. Doing this step on any machine with a real GPU and the repository is simpler |
| 9.1 step 13 and 9.2 step 11 on a Windows target | `start.ps1` (W11) |

#### W11. The model host natively on Windows (`start.ps1`)

For a project's Windows servers, and once on the training PC to prove the launcher. Not used for training. PowerShell 7:

```powershell
# From the llama.cpp release of the pinned tag: the win-cuda asset and the matching cudart asset, unzipped into one folder
$env:PATH = "C:\tools\llama.cpp;$env:PATH"
$env:MODEL_HOST_MODEL = 'D:\models\assistant\weights\assistant-1.0.0-Q4_K_M.gguf'
$env:MODEL_HOST_ALIAS = 'assistant-1.0.0'
pwsh -NoProfile -ExecutionPolicy Bypass -File src\support\model-host\start.ps1
Invoke-RestMethod http://127.0.0.1:8091/v1/models          # in a second window: the alias ends in @gpu
```

Then the fallback: `$env:MODEL_HOST_DEVICE = 'cpu'` (alias `@cpu`), and `auto` with `$env:MODEL_HOST_GPU_PRESENT = '0'`.
The other variables are those of 7.7. Asset names change between releases; take the `win-cuda` build that matches the driver.

#### W12. Checklist before the first session

- [ ] Windows 11 up to date; virtualisation on in the firmware; sleep off and update restarts paused for the run window.
- [ ] Recent NVIDIA driver on Windows; `nvidia-smi` in PowerShell shows the GPU and its VRAM; that VRAM read against "Hardware per step".
- [ ] RAM: 48 GB or more (64 GB for the 35B-A3B merge), estimate.
- [ ] Free space on the drive of the WSL disk: 300 GB, or 500 GB with the large tier, estimate.
- [ ] WSL 2 and Ubuntu 24.04 (W1); `.wslconfig` written; `wsl --shutdown` after editing it.
- [ ] Inside Ubuntu: `nvidia-smi`, `nvcc`, Node 22.18 or later, .NET 10 SDK, tmux (W2).
- [ ] Repository cloned to `~/src/stdd`; `start.sh` with LF line endings and executable (W3).
- [ ] `.venv` built; `torch.cuda.is_available()` is `True`; `pytest training/tests` passes; hardware check read (W4, W5).
- [ ] Tags chosen for llama.cpp and transformers.js and written in both `export.json` files; llama.cpp built with CUDA;
      `LLAMA_CPP_DIR`, `TRANSFORMERS_JS_DIR` and the `PATH` in `~/.bashrc`; `@huggingface/transformers` in `training/assistant/js` (W6, W7).
- [ ] Teacher GGUF in `~/myapp-models/teacher/` (outside the repository), its hash recorded in `dataset.json` (W8); access to the base models at the revisions of both `candidates.json` files, or local copies (W8).
- [ ] Licences reviewed and `verified` set in `candidates.json` (9.1 step 4, 9.2 step 5).
- [ ] Teacher host up on the GPU at port 8093; both `--measure` values written in `dataset.json` and `eval.json` (W9).
- [ ] The owner has said that training may start; each `[COST]` step is approved with its estimate or plan id.

### 9.5 Help embedding table

**Status.** Specified and implemented on 2026-11-16 (manual.md 6.7, section 7.8) and tested with a fake encoder; no step has run with the real model. Durations are estimates.

Independent of the two models: a project on retrieval only can do it, and it needs neither the teacher nor the judge.
Steps 3 and 7 need the owner's approval; subagents never run them.

**Step 1. Prerequisites.** The environment of 9.1 step 1 (on Windows: 9.4 W4) with `transformers` 4.51.0 or later; the
manual produced (manual.md 1.3); the packs built:

```
cd src/frontend && npm run help:build && npm run help:bench -- --compare; cd ../..
```

The lexical figures printed here are the baseline.

**Step 2. Confirm the model.** Open the model card of `Qwen/Qwen3-Embedding-0.6B` at the revision of `embed.json`,
review the licence (7.8), set `verified` to `true` and update `checked`. 10 minutes.

**Step 3. Owner approves. Build the tables.** The first run downloads about 1.2 GB into the Hugging Face cache (offline:
the model folder as `hub_id`, as in 9.1 "Working offline"). Minutes on a GPU, up to half an hour on a CPU (estimate).

```
python -m training.assistant.embed build --dry-run
python -m training.assistant.embed build --run {run_id}
```

Read `build-{lang}.json`: the nearest stems of the probe stems should look related (`task`: `todo`, `job`, `assign`...).
Unrelated neighbours mean a wrong pooling or prompt: stop and compare with the model card.

**Step 4. Calibrate and compare.** No approval. 5 minutes.

```
cd src/frontend
npm run help:build -- --embeddings ../../training/out/embeddings/runs/{run_id} --unlocked
npm run help:bench -- --calibrate --embeddings ../../training/out/embeddings/runs/{run_id} --unlocked
npm run help:bench -- --embeddings ../../training/out/embeddings/runs/{run_id} --unlocked --compare
cd ../..
```

Read a miss with `python -m training.assistant.embed probe --run {run_id} --lang {lang} --question "..."`.

**Step 5. Decide.** Record the tables when, in both languages, fused Recall@3 is at least 0.05 above lexical Recall@3
and no gate that passes lexically fails fused. This is one yes-or-no reading of the bench, never a search over
parameters: the parameters come only from step 4's calibration. Otherwise record nothing: the help stays lexical, and
the summary of `calibration.json` and section 10 of this document guide the next round (a larger Qwen embedding model,
a word list for the weaker language, a richer glossary or more indexed questions).

**Step 6. Upload.** Upload `en.bin` and `it.bin` of the run folder so that they answer at `{url}/en.bin` and
`{url}/it.bin`, with `{url}` = `{artifact store}/embeddings/{model_version}`.

**Step 7. Owner approves. Record, verify, use.**

```
python -m training.common.lock --record --model embeddings --run {run_id} --artifact-url {url}
python -m training.common.lock --verify --model embeddings
python -m training.common.fetch --model embeddings
cd src/frontend && npm run help:build -- --strict && npm run help:bench -- --compare; cd ../..
```

`fetch` writes the tables to `models/embeddings/weights/`, where `help:build` finds them. Every build that produces the
help (a pipeline, release, deploy) runs the same `fetch` before `help:build`; without it the help is lexical, and `--strict` does
not fail for a missing table. Commit `models/model.lock` and `models/embeddings/calibration/` with approval.

**Later.** `help:build` warns when the manual's vocabulary is no longer covered (manual.md 6.7.4): rebuild from step 3
under a new minor version. A change of the analyzer makes the tables unusable until rebuilt (section 11).

---

## 10. When a threshold is missed

Read `items-{export}.jsonl` of the failing report first, filtered by the failing metric and language. The table is written for the
assistant; for the investigator the same reading applies to the metrics of operations.md 11.4.

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Wrongful refusal high, worse on the distant subset | Wording shortcut | Check the refusal share per overlap bucket in `stats.json`; raise `pos_distant`, lower `ref_removed`; regenerate questions with stricter forbidden lists |
| Wrongful refusal high on every bucket | Too many refusals, or long contexts confuse the model | Lower refusal shares to 30%; check gold position spread; try K up to 3 |
| Near-miss refusal low | Near-miss questions too easy to tell apart | More `ref_near_miss` written from the closest section; check V11 rejections |
| Faithful low | Teacher answers carry outside knowledge (more likely with a small local teacher, 5.6); learning rate too high | Tighten V08 to 95%; use a stronger teacher or verifier; lower the learning rate; train fewer steps |
| Useful low with high faithful | Answers too short or vague | Inspect teacher answers; require the needed step or fact in the answer template; raise `pos_multi` |
| Correct source low | Model cites position 1 by habit | Check V13; more examples with gold at positions 3 and 4 |
| Italian much worse than English | Base model's Italian, weak Italian data, or a teacher whose Italian is poor | Check Italian rejections and read Italian samples; try a candidate or a teacher stronger in Italian |
| Transfer gap above 0.10 | Memorisation of training domains | Fewer epochs, more domains, lower rank (8) |
| Export loss above the limit | Quantisation | `Q8_0` or `q8` variants (6.5) |
| Still failing after one round | Model too small | Next candidate size (6.1) |
| `BPE pre-tokenizer not recognized` (llama.cpp `convert_hf_to_gguf.py`) on a fine-tuned Qwen3.5 | Reported for a fine-tuned Qwen3.5-4B (secondary source; the cause is not verified by us): the converter identifies the pre-tokenizer from the tokenizer files, and it does not know the ones in the merged folder (a llama.cpp tag older than the model, or tokenizer files changed on save) | Pin a newer llama.cpp tag; copy `tokenizer.json` and `tokenizer_config.json` from the base repository's pinned revision over the ones in `merged/` and convert again; as a last resort map the unknown hash to the base model's pre-tokenizer in the converter's `get_vocab_base_pre` and say so in the card. Test this on the untrained candidate first |
| Export fails on a Qwen3.5 merged folder (ONNX or GGUF), or the converted model answers differently | Hybrid architecture or vision tower in the checkpoint; converters written for plain transformers | Export text-only; use the converter versions pinned in `export.json`; compare with the template check; the first-test list in `export.json` (`first_test_on_pc`) |
| `the chat template is not prefix-stable across turns` on Qwen3.5 | The template drops or adds empty think tags depending on the turn, so the masked prefix differs | Keep `enable_thinking: false` in `chat_template_kwargs`; if it persists, the template of this revision needs a fixed copy recorded in the card |
| `$'\r': command not found` from `start.sh`, or hashes (L03, estimate ids) that differ from another machine | CRLF line endings from a Windows checkout | Clone inside WSL with `core.autocrlf input` (9.4 W3) |
| `CUDA out of memory` in training, the final selection or a judged evaluation, on one GPU | The teacher or judge host holds the GPU, or the micro-batch is too large for the model | Follow "One GPU, one job at a time" of 9.4 |
| Help bench: fused retrieval no better than lexical, or the nearest stems of `build-{lang}.json` unrelated | Pooling or prompt not as the model card says; a vocabulary without the users' words; weights of the wrong sign | Compare the encoding with the card's example at the pinned revision; read misses with `embed probe`; check the `wordlist` and `tokenizer` stem counts of `build-{lang}.json` (9.5 step 5) |
| Help bench: false no-match passes but out-of-scope rejection falls with fusion | `min_cosine` too low (`target_missed` in `calibration.json`) | More indexed questions in the weak stories; calibrate again; the bench is never used to set the threshold |
| `is the teacher host running` inside WSL while the host runs on Windows | WSL's `localhost` is not Windows' in the default network mode | Run the host inside WSL (9.4 W9), or mirrored networking (9.4 W1) |
| Generation, validation or the investigator's generation is very slow on Windows | The repository is under `/mnt/c` | Move it into the Linux file system (9.4 W3) |

Each round is a new `run_id`; the previous reports stay for comparison. Thresholds are not lowered to make a run
pass.

---

## 11. Retraining triggers

| Trigger | Detected by | Action |
|---------|-------------|--------|
| Version change of `manual-page`, `help-pack` or `assistant-protocol` | L02 or L03 at release | Regenerate the dataset (formats changed), train, full evaluation, new major or minor version |
| Version change of `assistant-retrieval` only | L03 | Platform evaluation of the existing exports; train only if it fails |
| A language is added to the application | The language list in `protocol.json` and the frontend | Add the language to domains, dataset and thresholds; train |
| The base model changes (new candidate, licence or security reason) | Owner | Steps 4 to 13 of 9.1, or steps 5 to 9 of 9.2 |
| The project bench fails systematically on new features | Two consecutive releases fail the answer gates of manual.md 8.3 while the retrieval gates pass, or useful on stories added since `recorded_at` is at least 0.15 below the other stories | Diagnose with section 10 against the project items; add the missing pattern to the synthetic generator (never the project's content); train |
| Version change of `incident-report`, `tool-catalog` or `investigator-protocol`, or a change to the protocol block of `support.agent.md` | L02 or L03 at release; the monitor falls back to rules only when the protocol differs (operations.md 11.2) | Investigator only: generate again, train, full evaluation (9.2 steps 3 to 9) |
| A tool family is added | Change of `docs/support/tools.json` with a new tool family | Investigator only: add the family to the worlds and the oracle, then as the row above |
| `help:build` reports the analyzer mismatch of the embedding tables (analyzer version or stopword list changed) | `help:build`, L09 | Rebuild the tables, calibrate, record (9.5 steps 3 to 7), new minor version |
| `vocab_coverage` of a pack below `build.min_vocab_coverage` | `help:build` warning; `--strict` fails | Rebuild the tables (new minor version) |
| The embedding model, the table format or `embed.json` changes | Owner; L09 | Rebuild the tables; a new model or format is a new major version |
| The help bench fails false no-match or out-of-scope rejection with a locked table | `help:bench` | Calibrate again (manual.md 6.7.7) and record under the same version; rebuild only if that does not help |
| Open weights of a **Qwen 4** appear in the Qwen organisation | Owner | Add it as a candidate: read its model card, pin the revision, add it to `candidates.json` with `verified: false`, measure it untrained before any training (6.1). On 2026-11-16 there are none: only a hosted preview. **Third-party repositories named "Qwen4" are forbidden**: only repositories of the Qwen organisation are used |

Not triggers: new stories, new pages, a new area, new questions, new runbooks and log keys. They change the pack, not the model.
For the embedding tables they are not triggers either, until the vocabulary coverage falls below its gate.

---

## 12. Release checklist (models)

A project on retrieval only runs items 2 and 3, and item 10 when it uses embedding tables; a project with rules only for
operations skips item 8.

1. `python -m training.common.lock --verify` passes.
2. `npm run help:build -- --strict` passes (pack compatible with the lock).
3. `npm run help:bench` passes (retrieval gates, both languages).
4. When a generation tier is enabled in a shipped profile: `npm run help:bench -- --export {jsonl}`, then
   `python -m training.assistant.eval --suite project --items {jsonl} --export gguf --run {run_id} --approved-estimate {id}`
   (the refused run prints the id; approval for the judge run), then
   `npm run help:bench -- --answers training/out/assistant/runs/{run_id}/eval-project-gguf.json --require-answers` passes the
   answer gates of manual.md 8.3.
5. The weights at every export URL match the lock's SHA-256 (`fetch` on the target).
6. Each shipped profile's `MyApp:Assistant:Order` names only tiers that passed their latency target (4.5).
7. The model card is current (`card` run after the last `--record`).
8. Investigator: `dotnet run --project src/support/MyApp.Monitor -- pack build --profile {name}` passes for each shipped profile; both
   suites of operations.md 11.4 passed for the tier the profile sets; the Sources page on the target reads `State ready` with that
   tier (9.2 step 12).
9. On a Windows target, the model host was started there with `start.ps1` and its GPU start and CPU fallback were seen (9.4 W11).
10. When the help uses embedding tables: `fetch --model embeddings` ran before `help:build -- --strict` (item 2 then refuses
    an unlocked table and a coverage below its gate); `lock --verify` passes L09; `help:bench -- --compare` shows the
    fused configuration passing the gates of item 3.

---

## 13. Formats defined here

| Format key | Section |
|------------|---------|
| `assistant-protocol` | 3 (`training/assistant/protocol.json`, rendering, answer grammar, context budget) |
| `assistant-example` | 5.9 (with the teacher job record of 5.6) |
| `eval-report` | 4.6 |
| `model-lock` | 8.1, 8.2 (with `tiers` and the `embeddings` entry; version 2 since 2026-11-16) |
| `model-card` | 8.3 |

A change follows development.md section 13. A change to `assistant-protocol` or `assistant-example` is a
retraining trigger. The embedding table file and the calibration file are part of `help-pack` (manual.md 6.7); the
settings of `embed.json` are configuration, recorded by hash in the lock (L09), not a contract.
