"""``run.json`` of every command (models.md 2.2)."""
from __future__ import annotations

import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .config import REPO_ROOT, next_run_id, out_dir, TRAINING_DIR
from .jsonio import load_json, write_json


def code_commit(root: Path = REPO_ROOT) -> str | None:
    """Current git commit, read-only; ``None`` outside a repository."""
    try:
        res = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, capture_output=True, text=True, timeout=10)
    except (OSError, subprocess.SubprocessError):
        return None
    return res.stdout.strip() or None if res.returncode == 0 else None


class RunRecorder:
    """Context manager that writes ``training/out/{model}/runs/{run_id}/run.json`` for a command.

    ``run.json`` may be written by several commands of the same run (generate, train, export); each command adds an
    entry under ``commands`` and keeps the keys other commands wrote (for example ``status``, ``adapter``).
    """

    def __init__(self, model: str, command: str, config_hashes: dict[str, str] | None = None,
                 run_id: str | None = None, root: Path = TRAINING_DIR, argv: list[str] | None = None,
                 model_out: Path | None = None):
        """``model_out`` overrides ``training/out/{model}`` (tests, custom output folders)."""
        self.model, self.command, self.root = model, command, root
        self.base = model_out if model_out is not None else out_dir(model, root)
        self.run_id = run_id or next_run_id(model, root, runs=self.base / "runs")
        self.config_hashes = config_hashes or {}
        self.argv = argv if argv is not None else sys.argv[1:]
        self.path = self.base / "runs" / self.run_id / "run.json"
        self.extra: dict[str, Any] = {}

    def __enter__(self) -> "RunRecorder":
        self._start = time.monotonic()
        self._started_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        data: dict[str, Any] = load_json(self.path) if self.path.exists() else {"schema_version": 1, "model": self.model, "run_id": self.run_id, "commands": []}
        data.setdefault("commands", []).append({
            "command": self.command,
            "argv": self.argv,
            "config_sha256": self.config_hashes,
            "code_commit": code_commit(),
            "started_at": self._started_at,
            "duration_s": round(time.monotonic() - self._start, 3),
            "ok": exc_type is None,
            **self.extra,
        })
        write_json(self.path, data)
