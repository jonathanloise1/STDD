"""Run-time contexts of the platform test items (models.md 4.2, "end to end"): retrieval exactly as at run time, with no
gold section inserted. Rebuilt from the teacher cache with the same pipeline ``validate`` uses; nothing is called."""
from __future__ import annotations

from pathlib import Path
from typing import Any

from training.assistant import protocol as P
from training.assistant.dataset import assemble
from training.assistant.generate import ASSISTANT, check_config, load_all
from training.assistant.jobs import Pipeline, load_templates
from training.assistant.walk import cache_walk
from training.common.teacher import JobCache


def runtime_contexts(out_dir: Path, config_dir: Path = ASSISTANT / "config", split: str = "test") -> dict[str, dict[str, Any]]:
    """``{example id: {"context": [sections], "gold_ids": [section ids]}}`` for the examples of ``split``."""
    cfg, domains, retrieval = load_all(config_dir)
    check_config(cfg, domains)
    pipe = Pipeline(cfg, domains, load_templates(ASSISTANT / "teacher"), retrieval)
    cache_walk(pipe, JobCache(out_dir / "teacher-cache"))
    out: dict[str, dict[str, Any]] = {}
    for ex in assemble(pipe).examples.get(split, []):
        index = pipe.state[ex.domain].index[ex.lang]
        ranked = [s for s, _score in index.rank(ex.question, pipe.retrieval)]
        selected = P.select_sections(pipe.protocol, ranked)
        out[ex.id] = {"context": [{"id": s["id"], "title": s["title"], "text": s["text"]} for s in selected],
                      "gold_ids": [ex.context[g - 1]["id"] for g in ex.gold]}
    return out
