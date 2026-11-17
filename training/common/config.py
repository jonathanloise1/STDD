"""Configuration loading and repository paths."""
from __future__ import annotations

import re
from datetime import date
from pathlib import Path
from typing import Any

from .hashing import sha256_file
from .jsonio import load_json

REPO_ROOT = Path(__file__).resolve().parents[2]
TRAINING_DIR = REPO_ROOT / "training"
MODELS_DIR = REPO_ROOT / "models"


def model_dir(model: str, root: Path = TRAINING_DIR) -> Path:
    """Code and config of a model: ``training/{model}``."""
    return root / model


def out_dir(model: str, root: Path = TRAINING_DIR) -> Path:
    """Git-ignored outputs of a model: ``training/out/{model}``."""
    return root / "out" / model


def load_config(model: str, name: str, root: Path = TRAINING_DIR) -> dict[str, Any]:
    """Load ``training/{model}/config/{name}.json``."""
    return load_json(model_dir(model, root) / "config" / f"{name}.json")


def config_path(model: str, name: str, root: Path = TRAINING_DIR) -> Path:
    return model_dir(model, root) / "config" / f"{name}.json"


def config_sha256(model: str, name: str, root: Path = TRAINING_DIR) -> str:
    return sha256_file(config_path(model, name, root))


RUN_ID_RE = re.compile(r"^\d{4}-\d{2}-\d{2}-[a-z]$")


def next_run_id(model: str, root: Path = TRAINING_DIR, today: date | None = None, runs: Path | None = None) -> str:
    """``{yyyy-mm-dd}-{letter}``: the first unused letter of the day."""
    day = (today or date.today()).isoformat()
    runs = runs if runs is not None else out_dir(model, root) / "runs"
    for letter in "abcdefghijklmnopqrstuvwxyz":
        if not (runs / f"{day}-{letter}").exists():
            return f"{day}-{letter}"
    raise RuntimeError(f"no free run id for {day}")
