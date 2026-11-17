"""Shared tools of the model pipeline: config, hashing, JSONL, teacher client, estimate, hardware, lock, card, fetch.

Module boundaries (models.md 2.1):

- ``jsonio``, ``hashing``, ``config``: no dependencies, used by everything.
- ``templates``, ``teacher``, ``estimate``: the teacher job format, cache, resume, cost estimate and approval.
- ``runs``: ``run.json`` of every command and the run id.
- ``hardware``: device detection and the batch plan for training.
- ``lock``, ``card``, ``fetch``: models/model.lock, the model card and the weights download.

Heavy dependencies (torch, transformers, peft, onnx tooling) are imported lazily inside the functions that need
them, never at module level.
"""
