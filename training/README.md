# training

The model pipeline of the template. The specification is [docs/guidelines/models.md](../docs/guidelines/models.md);
this folder implements it. Read the section of the step you run; an adopting project follows section 9 (first training: 9.1 the
assistant, 9.2 the investigator). That walkthrough has been rehearsed with fakes only (`tests/test_rehearsal.py`), not yet on real models.

Run every command from the repository root, inside the Python environment of models.md 9.1 (Python 3.11 or 3.12 for
training; the data side and the tests run on 3.11 and later).

To run only the tests and the data-side commands (Python 3.11 or 3.12 recommended; no GPU, no model library):

```
python -m venv .venv && . .venv/bin/activate
pip install -r training/requirements-light.txt
python -m pytest training/tests
```

For training, export and evaluation install the full set instead (`pip install -r training/requirements.txt`, which includes
the light one).

Heavy dependencies (torch, transformers, peft, onnx tooling) are imported lazily; the data-side commands, the tests and
`--help` work without them. No cloud AI service is used: the teacher (synthetic data) and the judge (evaluation) run on a
local OpenAI-compatible host, by default `http://localhost:8093/v1` (the `teacher` service of the compose file, a llama.cpp host
serving a large open-weight model, or any other local server). A command marked `[COST]` occupies the machine for a long time
(a local model host or the training GPU), not money, and is started only by the owner (CLAUDE.md).

**Teacher and judge host.** Start it before `--measure`, a real `generate` and a judged `eval`. Set `model` (the name the host
serves; the shipped default is `qwen3.8-27b`, the compose `teacher` alias) in `assistant/config/dataset.json` (`teacher`) and `eval.json` (`judge`); `base_url` defaults to
`http://localhost:8093/v1`. No API key is needed: `api_key_env` is `null`, and when it names an environment variable that is
set, its value is sent as a bearer token (only for a local proxy that asks for one). If the host is not running, the command
fails with "is the teacher host running at {url}" and nothing is lost: start it and run the same command again, because every
answered job and judged item is cached. The quality of the local teacher bounds the quality of the dataset (models.md 5.6): choose
it as models.md 5.6 and 4.4 say, and rely on the validators and the human labels, not on the teacher.

**No internet.** Besides the teacher model file, the base models of `candidates.json` can come from a local folder: put the
folder path in `hub_id` (keep `revision` as the commit hash of the copy) and set `HF_HUB_OFFLINE=1` (models.md 9.1 step 4).

## Layout

| Path | Content |
|------|---------|
| `common/` | Shared: config, hashing and seeds, JSONL, templates, teacher client and cache, estimate, hardware, lock, card, fetch |
| `assistant/protocol.json` | The single definition of the assistant protocol (models.md 3.1) |
| `assistant/protocol.golden.json` | Language-neutral golden cases for the Python, TypeScript and C# implementations |
| `assistant/protocol.py` | Python renderer, parser and context selection |
| `assistant/config/` | `dataset.json`, `domains.json` (40 domains and their split), `blocklist.txt`, stopword lists |
| `assistant/teacher/` | Teacher prompt templates, one file per stage, version in the name |
| `assistant/generate.py`, `validate.py` | Synthetic dataset generation and validation (models.md 5) |
| `assistant/embed.py`, `embtable.py`, `config/embed.json` | The help embedding table: `embed build` and `embed probe`, the binary file format and its reader, the pin of the Qwen embedding model (models.md 7.8, manual.md 6.7). `lexical.py` holds the Python port of the section and query vectors, the fusion and the decision |
| `assistant/labels.py` | Builds the judge's 120 hand-labelled items from the frozen test set (models.md 4.4, 9.1 step 6) |
| `assistant/eval.py`, `train.py`, `export.py`, `adapter.py`, `metrics.py` | Evaluation harness, adapter training, merge and exports, the assistant's side of them (models.md 4, 6) |
| `assistant/config/` (continued) | `eval.json` (thresholds, judge), `train.json`, `candidates.json`, `export.json` (starting values; the Qwen candidates are pinned from the model cards of 2026-11-16 and stay `verified: false` until the owner confirms them; exporter tags are not pinned yet) |
| `assistant/eval/judge.v1.txt`, `assistant/js/template_check.mjs` | Judge prompt; chat-template rendering with transformers.js |
| `common/backends.py`, `judge.py`, `train_core.py`, `train_cli.py`, `export_core.py`, `export_cli.py`, `evalcore.py`, `schema.py` | Shared by both models: answer backends (transformers, onnxruntime, llama.cpp server, fake), judge, training loop, export orchestration, report helpers |
| `investigator/` | The operations investigator (operations.md 11): `protocol.json` and `protocol.golden.json`, 24 world specs in `worlds/`, `generate`, `validate`, `eval`, `train`, `export`, `thresholds.json`, `config/` |
| `tests/` | pytest |
| `out/` | Git-ignored: teacher cache, domain trees, datasets, checkpoints, exports, runs |

## Commands (data side)

| Command | What it does | Approval |
|---------|--------------|----------|
| `python -m training.common.hardware` | Device, memory, disk, expected durations; exit 1 without CUDA or Apple MPS | No |
| `python -m training.assistant.generate --measure [--concurrency N]` | A few short requests to the running local host; prints `tokens_per_second` and `prompt_tokens_per_second` for `dataset.json`; writes nothing | No |
| `python -m training.assistant.generate --estimate [--splits ...]` | Requests, tokens, estimated duration per stage and the `estimate_id`; calls nothing, writes nothing | No |
| `python -m training.assistant.generate --dry-run` | Renders every request, prints three per stage; sends nothing, writes nothing | No |
| `python -m training.assistant.generate --splits test,validation --approved-estimate {id}` | Teacher outputs into `out/assistant/teacher-cache/`, domain trees into `out/assistant/domains/`; resumable: run the same command again | Yes `[COST]` |
| `python -m training.assistant.generate --splits train --approved-estimate {id}` | Same, for the training domains | Yes `[COST]` |
| `python -m training.assistant.validate [--tools auto\|off\|require]` | Assembles the dataset from the cache, runs V01 to V15, writes `out/assistant/dataset/{train,validation,test,seen}.jsonl`, `rejected.jsonl`, `stats.json` | No |
| `python -m training.assistant.golden --write \| --check` | Regenerates or checks `protocol.golden.json` | No |
| `python -m training.common.lock --record --model {m} --run {run_id} --artifact-url {url}` | Records a run that passed in `models/model.lock`; one run per investigator tier | No |
| `python -m training.common.lock --verify` | Checks L01 to L09; exit 1 on any failure | No |
| `python -m training.assistant.embed build --dry-run [--langs en,it] [--packs DIR]` | Stems per group, expected file size and duration of the help embedding table; imports no model library, writes nothing (needs the packs: `npm run help:build` in `src/frontend`) | No |
| `python -m training.assistant.embed probe --run {run_id} --lang {lang} --question TEXT` | Query stems and weights, the top 5 sections lexical, by cosine and fused, and the decision of manual.md 6.6; runs no model | No |
| `python -m training.common.card --model {m}` | Refreshes `models/{m}/MODEL_CARD.md` | No |
| `python -m training.common.fetch --model {m} [--runtime browser\|server] [--format onnx\|gguf] [--to DIR]` | Downloads the recorded weights and checks their SHA-256; files go to `$MYAPP_MODELS_DIR/{m}/` (default `~/myapp-models`, the folder compose mounts; a server GGUF is named `{m}.gguf`); `{m}` can be `embeddings` (the tables go to `models/embeddings/weights/`) | No |

## Commands (model side)

A command marked `[COST]` is started only by the owner, with the approval id it prints (`--approved-estimate` for the judge, `--approved-plan` for
training). Pass `--run` explicitly: `generate` also creates run folders.

| Command | What it does | Approval |
|---------|--------------|----------|
| `python -m training.assistant.embed build [--run {run_id}] [--model-version V] [--device auto\|cuda\|mps\|cpu]` | Distils the pinned Qwen embedding model into `{lang}.bin` of a run (models.md 7.8). It downloads about 1.2 GB the first time; minutes, not `[COST]`. Then calibrate with `npm run help:bench -- --calibrate --embeddings {run folder} --unlocked` and record with `lock --record --model embeddings` (models.md 9.5) | Yes (downloads and runs a model) |
| `python -m training.assistant.eval --measure [--concurrency N]` | Speed of the judge host (a few short requests); prints the values for `eval.json` | No |
| `python -m training.assistant.eval --estimate` | Judge calls, tokens, estimated duration and the estimate id; runs no model | No |
| `python -m training.assistant.labels select\|label\|status\|finish` | Builds `eval/human-labels.jsonl` (select items, label them in the terminal or an editor, check and write) | No |
| `python -m training.assistant.eval --freeze-sets` | Pins the test and validation sets by SHA-256 in `config/eval.json` (after `validate`) | No |
| `python -m training.assistant.eval --candidates --approved-estimate ID` | Measures the untrained candidates (models.md 4.1) | Yes `[COST]` |
| `python -m training.assistant.train --dry-run` | Validates configuration and dataset, prints the plan and plan id; imports no torch | No |
| `python -m training.assistant.train --approved-plan ID` | LoRA training with validation, early stopping, checkpoint comparison and resume | Yes `[COST]` |
| `python -m training.assistant.export --dry-run` | Prints every export step and command, and what would be refused | No |
| `python -m training.assistant.export [--run ID \| --candidate ID]` | Merge, ONNX, GGUF, manifest, template check | No |
| `python -m training.assistant.eval --export merged\|onnx\|gguf --run ID --approved-estimate ID --items bench.jsonl` | Platform evaluation of one output, report of models.md 4.6; without `--items` the report fails | Yes `[COST]` |
| `python -m training.assistant.eval --suite project --items bench.jsonl --run ID --approved-estimate ID` | Project bench answers (manual.md 8.3) | Yes `[COST]` |
| `python -m training.investigator.generate --dry-run \| --split train\|validation\|holdout\|all` | Synthetic incidents; a real run needs `dotnet`, the built monitor and `Monitor__Masking__HashKey` | No |
| `python -m training.investigator.validate` | Validators IV01 to IV08, dataset files, statistics | No |
| `python -m training.investigator.eval --freeze-sets \| --candidates --tier T \| --export merged\|gguf --run ID` | Closed-loop evaluation on both suites; needs the monitor, `Monitor__Masking__HashKey` and, for gguf, `llama-server` | Long compute; no model host |
| `python -m training.investigator.train --tier T [--dry-run \| --approved-plan ID]` | As the assistant's, shared implementation | Yes `[COST]` |
| `python -m training.investigator.export [--dry-run] --run ID` | Merge, GGUF of the run's tier, manifest, template check | No |

Heavy tools outside pip: llama.cpp (pinned tag, `LLAMA_CPP_DIR`, `llama-server` on the `PATH`), the transformers.js checkout (`TRANSFORMERS_JS_DIR`), each
with the Python requirements of its conversion script, and Node 22.18 or later with `@huggingface/transformers` installed where `assistant/js/template_check.mjs`
finds it (models.md 9.1 step 9). `export.json` holds their pins (empty until the owner sets them).

## Tests

```
python -m pytest training/tests
```

The offline rehearsal (`tests/test_rehearsal.py`) runs every command of a model in a temporary repository with fakes, through `lock --verify`.
Besides the data-side tests, they cover the renderer against the golden cases, answer parsing, context selection, the question split and seeds,
every validator with a passing and a failing example, the estimate arithmetic (tokens and duration) and approval, the optional key, the default URL, the throughput measurement and the guarantee that no request leaves the configured host (against a stand-in HTTP server in the test process), cache and resume with a
fake teacher, and the lock, card and fetch tools with fake artifacts. No test uses the network beyond the loopback stand-in server.
