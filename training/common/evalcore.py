"""Pieces shared by the evaluations of both models: threshold rules, run folders, report writing (models.md 4.5, 4.6)."""
from __future__ import annotations

import operator
from pathlib import Path
from typing import Any, Mapping

from .config import TRAINING_DIR, out_dir
from .hashing import sha256_file
from .jsonio import load_json, write_json, write_jsonl

OPS = {">=": operator.ge, "<=": operator.le, ">": operator.gt, "<": operator.lt, "==": operator.eq}
EXPORTS = ("merged", "onnx", "gguf")


def holds(value: float | None, rule: Mapping[str, Any]) -> bool:
    """``rule`` is ``{"op": ">=", "value": 0.99}``. A metric that was not measured (``None``) never passes."""
    return value is not None and OPS[rule["op"]](round(value, 6), rule["value"])


def check_rules(metrics: Mapping[str, Any], rules: Mapping[str, Mapping[str, Any]], where: str, only: set[str] | None = None) -> list[dict[str, Any]]:
    """Failures of ``metrics`` against ``rules``: ``{"where", "metric", "value", "threshold"}``."""
    fails = []
    for name, rule in rules.items():
        if only is not None and name not in only:
            continue
        v = metrics.get(name)
        if not holds(v, rule):
            fails.append({"where": where, "metric": name, "value": v, "threshold": f"{rule['op']} {rule['value']}"})
    return fails


def runs_dir(model: str, root: Path = TRAINING_DIR, model_out: Path | None = None) -> Path:
    return (model_out if model_out is not None else out_dir(model, root)) / "runs"


def latest_run(model: str, root: Path = TRAINING_DIR, model_out: Path | None = None) -> str | None:
    d = runs_dir(model, root, model_out)
    runs = sorted(p.name for p in d.iterdir() if p.is_dir()) if d.exists() else []
    return runs[-1] if runs else None


def thresholds_sha256(model: str, root: Path = TRAINING_DIR) -> str:
    """SHA-256 of ``training/{model}/config/eval.json``: what ``lock --verify`` compares (L04)."""
    return sha256_file(root / model / "config" / "eval.json")


def write_report(run_dir: Path, export: str, report: dict[str, Any], items: list[dict[str, Any]]) -> Path:
    """Write ``items-{export}.jsonl`` first so the report carries its SHA-256, then ``eval-{export}.json``."""
    items_path = run_dir / f"items-{export}.jsonl"
    write_jsonl(items_path, items)
    report["items_sha256"] = sha256_file(items_path)
    path = run_dir / f"eval-{export}.json"
    write_json(path, report)
    return path


def format_failures(failures: list[dict[str, Any]], limit: int = 30) -> str:
    lines = [f"  {f.get('where', '-')}: {f['metric']} = {f['value']} (needs {f['threshold']})" for f in failures[:limit]]
    if len(failures) > limit:
        lines.append(f"  ... {len(failures) - limit} more")
    return "\n".join(lines)
