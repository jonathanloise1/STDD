"""Calls to the frontend tooling (``npm run manual:check``) on a synthetic domain tree (models.md 5.2, V03).

The tooling is written by another work item. When it is not installed or the script does not exist, the functions
return ``None`` and ``validate`` reports that the external part of V03 was not run.
"""
from __future__ import annotations

import json
import re
import shutil
import subprocess
from pathlib import Path
from typing import Callable

from training.common.config import REPO_ROOT

FRONTEND = REPO_ROOT / "src" / "frontend"
Runner = Callable[..., "subprocess.CompletedProcess[str]"]


def npm_path() -> str | None:
    """Full path of npm. On Windows npm is ``npm.cmd``, which a process cannot start under the bare name ``npm``; ``which`` honours PATHEXT."""
    return shutil.which("npm") or shutil.which("npm.cmd")


def script_available(name: str, frontend: Path = FRONTEND) -> bool:
    pkg = frontend / "package.json"
    if not pkg.exists() or npm_path() is None:
        return False
    try:
        return name in json.loads(pkg.read_text(encoding="utf-8")).get("scripts", {})
    except (OSError, json.JSONDecodeError):
        return False


_FINDING = re.compile(r"^(M\d{2}) (\S+): (.*)$")


def manual_check(tree: Path, frontend: Path = FRONTEND, runner: Runner = subprocess.run) -> list[dict[str, str]] | None:
    """Findings of ``npm run manual:check -- --root {tree} --no-images``, drafts excluded (M06); ``None`` when unavailable."""
    if not script_available("manual:check", frontend):
        return None
    res = runner([npm_path() or "npm", "run", "--silent", "manual:check", "--", "--root", str(tree), "--no-images"], cwd=frontend,
                 capture_output=True, text=True, timeout=300)
    findings = []
    for line in (res.stdout + res.stderr).splitlines():
        m = _FINDING.match(line.strip())
        if m and m.group(1) != "M06":
            findings.append({"check": m.group(1), "path": m.group(2), "message": m.group(3)})
    if res.returncode != 0 and not findings:
        findings.append({"check": "M??", "path": str(tree), "message": f"manual:check failed with exit code {res.returncode}"})
    return findings


def help_build(tree: Path, out: Path, frontend: Path = FRONTEND, runner: Runner = subprocess.run) -> bool | None:
    """``npm run help:build -- --root {tree} --out {out}``; ``None`` when unavailable."""
    if not script_available("help:build", frontend):
        return None
    res = runner([npm_path() or "npm", "run", "--silent", "help:build", "--", "--root", str(tree), "--out", str(out)], cwd=frontend,
                 capture_output=True, text=True, timeout=300)
    return res.returncode == 0
