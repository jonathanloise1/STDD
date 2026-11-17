"""Download the weights of a recorded model and check them by SHA-256 (models.md 2.2, 7.3, 8.1).

    python -m training.common.fetch --model assistant|investigator|embeddings [--to DIR] [--runtime browser|server] [--format onnx|gguf]

For ``embeddings`` the files are the tables ``{lang}.bin`` and land in ``models/embeddings/weights/``, where ``help:build`` looks for them.

Default destination for the assistant and the investigator: ``$MYAPP_MODELS_DIR/{model}/`` (default ``~/myapp-models``), outside the
repository and the folder the compose model hosts mount. A server GGUF is stored under the name compose expects,
``{model}.gguf`` (the investigator's large tier ``investigator-large.gguf``); every other file lands at ``{dest}/{path}`` as recorded in
the lock. With ``--to`` the destination is ``{to}/{model_version}/`` (the web root layout of 7.3) and every file keeps its recorded path. Files that already exist with the
right SHA-256 are skipped. A download that fails the check is deleted and the command exits with 1.
"""
from __future__ import annotations

import argparse
import os
import sys
import tempfile
import urllib.request
from pathlib import Path
from typing import Any, Callable, Iterator

from .config import REPO_ROOT
from .hashing import sha256_file
from .lock import LockError, lock_path, read_lock
from .weights import local_path, weights_dir

Downloader = Callable[[str, Path], None]


def file_url(export_url: str, file_path: str) -> str:
    """URL of one file: the export URL names the export folder (``.../onnx/``); a file path that starts with that
    folder name (``onnx/model.onnx``) is not repeated."""
    base = export_url.rstrip("/")
    last = base.rsplit("/", 1)[-1]
    parts = file_path.split("/", 1)
    rel = parts[1] if len(parts) == 2 and parts[0] == last else file_path
    return f"{base}/{rel}"


def default_download(url: str, dest: Path) -> None:
    with urllib.request.urlopen(url, timeout=300) as resp, open(dest, "wb") as out:   # noqa: S310 (recorded artifact URL)
        while chunk := resp.read(1 << 20):
            out.write(chunk)


def plan(entry: dict[str, Any], runtime: str | None = None, fmt: str | None = None) -> Iterator[tuple[str, str, dict[str, Any], dict[str, Any]]]:
    """``(url, recorded path, file entry, export)`` for the selected exports."""
    for x in entry["exports"]:
        if runtime and x["runtime"] != runtime or fmt and x["format"] != fmt:
            continue
        for fl in x["files"]:
            yield file_url(x["url"], fl["path"]), fl["path"], fl, x


def fetch(model: str, root: Path = REPO_ROOT, to: Path | None = None, runtime: str | None = None, fmt: str | None = None,
          downloader: Downloader = default_download, out: Callable[[str], None] = print) -> int:
    data = read_lock(lock_path(root))
    if model not in data["models"]:
        raise LockError(f"no {model} entry in the lock")
    entry = data["models"][model]
    dest = (to / entry["model_version"]) if to else weights_dir(model, root)
    failures = 0
    for url, recorded, fl, export in plan(entry, runtime, fmt):
        rel = recorded if to else local_path(model, export, fl)
        target = dest / rel
        if target.is_file() and sha256_file(target) == fl["sha256"]:
            out(f"ok (present) {rel}")
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        fd, tmp = tempfile.mkstemp(dir=str(target.parent), prefix=target.name + ".", suffix=".part")
        os.close(fd)
        tmp_path = Path(tmp)
        try:
            downloader(url, tmp_path)
            if tmp_path.stat().st_size != fl["bytes"] or sha256_file(tmp_path) != fl["sha256"]:
                out(f"FAILED {rel}: size or SHA-256 differs from the lock")
                failures += 1
                continue
            os.replace(tmp_path, target)
            out(f"ok (downloaded) {rel}")
        except Exception as exc:  # noqa: BLE001 - one failed file must not hide the others
            out(f"FAILED {rel}: {type(exc).__name__}: {exc}")
            failures += 1
        finally:
            if tmp_path.exists():
                tmp_path.unlink()
    return 1 if failures else 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.common.fetch", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--model", required=True)
    ap.add_argument("--to", type=Path, help="destination base (the files go to {to}/{model_version}/)")
    ap.add_argument("--runtime", choices=["browser", "server"])
    ap.add_argument("--format", dest="fmt", choices=["onnx", "gguf"])
    ap.add_argument("--root", type=Path, default=REPO_ROOT, help="repository root (tests)")
    args = ap.parse_args(argv)
    try:
        return fetch(args.model, args.root, args.to, args.runtime, args.fmt)
    except LockError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
