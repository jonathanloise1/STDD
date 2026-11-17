# models

Recorded models of the template. The lifecycle is in [docs/guidelines/models.md](../docs/guidelines/models.md); an adopting project
following the first training reads its section 9 ("First training for an adopting project": 9.1 the assistant, 9.2 the investigator) and runs the
commands it lists from the repository root. Without a recorded model the assistant works on retrieval and the monitor on rules.

| Path | What it is | Written by |
|------|------------|------------|
| `model.lock` | The recorded models: version, base model, adapter, protocol hash, dataset, exports with SHA-256, evaluation report hash (models.md 8.1). It does not exist until a model is recorded | `python -m training.common.lock --record --model {model} --run {run_id} --artifact-url {url}` only |
| `model.lock.example` | The fields of an entry with obviously fake values, for reading only. It is not a lock and nothing reads it | By hand |
| `{model}/MODEL_CARD.md` | The model card: the 11 headings of models.md 8.3 | `python -m training.common.card --model {model}` (the text of `Limitations` and `Browser check` is kept) |
| `{model}/eval/{model_version}.json` | The evaluation report of the recorded version (models.md 4.6), with `{model_version}-onnx.json` and `-gguf.json` beside it. Investigator: `{model_version}-{tier}.json` and `{model_version}-{tier}-gguf.json` | Copied by `lock --record` |
| `{model}/eval/candidates/{id}.json` | The untrained candidates' reports, the baseline a trained model must beat (models.md 4.1) | `eval --candidates` |
| `embeddings/weights/` | The help tables, git-ignored | `python -m training.common.fetch --model embeddings` |

`{model}` is `assistant` or `investigator`; their weights are not here: `fetch` writes them to `$MYAPP_MODELS_DIR/{model}/` (default `~/myapp-models`), the folder compose mounts; `lock --record`, `lock --verify` and `fetch` also take `embeddings` (the help embedding table: it has no card, and its tables `{lang}.bin` go to `models/embeddings/weights/`, with the calibration report in `models/embeddings/calibration/`; models.md 7.8, 9.5). `python -m training.common.lock --verify` checks the lock (L01 to L09, models.md 8.2) and is part of
the release checklist (models.md 12). Weights, datasets and teacher outputs are never committed. An untrained-only candidate (`trainable: false`, models.md 6.1) is recorded in the lock with `status: untrained-candidate` and `base_model.trainable: false`. The teacher and the judge that produce the training data and the evaluation run on a local model host (models.md 5.6, 4.4): no cloud AI service is involved, and with the model files copied in, the whole pipeline runs without internet (models.md 9.1 step 4).
