"""Where model weights that are not made by this pipeline live, and how they are checked (models.md 6.1, 9.4 W8).

Two kinds of file come from outside: the teacher and judge GGUF (``assistant/config/dataset.json``, key ``teacher.weights``) and an existing
4-bit GGUF of an untrained candidate (``investigator/config/candidates.json``, key ``gguf``), used when the BF16 weights do not fit the machine.
Both are described by the same block::

    {"repo": "owner/name-GGUF", "file": "name-Q4_K_M.gguf", "path": "teacher/name-Q4_K_M.gguf", "sha256": null, "quantization": "Q4_K_M", ...}

``path`` is relative to the weights folder. The folder is **outside the repository**: the environment variable ``MYAPP_MODELS_DIR``, default
``~/myapp-models``. ``sha256`` is ``null`` until the owner has downloaded the file and recorded its hash; a file whose hash is not recorded, or does
not match, is refused. ``python -m training.common.weights FILE`` prints the hash to record. Nothing here downloads anything.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import Any, Mapping

from .hashing import sha256_file

MODELS_DIR_ENV = "MYAPP_MODELS_DIR"
DEFAULT_MODELS_DIR = "~/myapp-models"


class WeightsError(RuntimeError):
    pass


def models_dir(env: Mapping[str, str] | None = None) -> Path:
    env = os.environ if env is None else env
    return Path(env.get(MODELS_DIR_ENV) or DEFAULT_MODELS_DIR).expanduser()


def weights_dir(model: str, root: Path, env: Mapping[str, str] | None = None) -> Path:
    """Where the fetched files of a locked model live. ``embeddings`` (small tables that ``help:build`` reads) stay in the
    repository, ``models/embeddings/weights/``; the assistant and the investigator go to ``$MYAPP_MODELS_DIR/{model}/``, the folder
    the compose model hosts mount (``MODELS_DIR``), so one folder serves the scripts and Docker."""
    if model == "embeddings":
        return root / "models" / "embeddings" / "weights"
    return models_dir(env) / model


def local_path(model: str, export: Mapping[str, Any], file_entry: Mapping[str, Any]) -> str:
    """Path of a locked file below ``weights_dir``. A server GGUF gets the name compose expects: ``{model}.gguf`` (the investigator's
    ``large`` tier ``{model}-large.gguf``); every other file keeps the path recorded in the lock."""
    if model in ("assistant", "investigator") and export.get("format") == "gguf":
        tier = export.get("tier")
        return f"{model}.gguf" if tier in (None, "small") else f"{model}-{tier}.gguf"
    return file_entry.get("path", "")


def weights_path(block: Mapping[str, Any], override: str | Path | None = None, env: Mapping[str, str] | None = None) -> Path:
    if override:
        return Path(override).expanduser()
    return models_dir(env) / block.get("path", block["file"])


def check_weights(block: Mapping[str, Any], path: Path, label: str = "weights") -> str:
    """The file must exist and its SHA-256 must equal the one pinned in the configuration. Returns the hash."""
    if not path.is_file():
        raise WeightsError(f"{label}: {path} does not exist. Download {block['file']} from {block['repo']} into the weights folder "
                           f"({MODELS_DIR_ENV}, default {DEFAULT_MODELS_DIR}) or pass its path")
    want = block.get("sha256")
    if not want:
        raise WeightsError(f"{label}: the hash of {block['file']} is not recorded. Run `python -m training.common.weights {path}` and write the result "
                           "into the sha256 key of the block in the configuration, then run again")
    have = sha256_file(path)
    if have != want:
        raise WeightsError(f"{label}: {path} has SHA-256 {have}, the configuration pins {want}: a different file or a damaged download")
    return have


def configured_block(name: str, training_dir: Path | None = None) -> Mapping[str, Any]:
    """``teacher`` (dataset.json of the assistant) or the id of an investigator candidate that has a ``gguf`` block."""
    from .config import TRAINING_DIR
    from .jsonio import load_json
    base = training_dir or TRAINING_DIR
    if name == "teacher":
        return load_json(base / "assistant" / "config" / "dataset.json")["teacher"]["weights"]
    for c in load_json(base / "investigator" / "config" / "candidates.json")["candidates"]:
        if c["id"] == name and c.get("gguf"):
            return c["gguf"]
    raise WeightsError(f"{name!r} is not `teacher` or an investigator candidate with a gguf block")


def main(argv: list[str] | None = None) -> int:
    args = sys.argv[1:] if argv is None else argv
    try:
        if len(args) == 1 and not args[0].startswith("-"):
            print(sha256_file(args[0]))
            return 0
        if len(args) in (2, 4) and args[0] == "--check":
            block = configured_block(args[1])
            path = weights_path(block, args[3] if len(args) == 4 and args[2] == "--file" else None)
            print(f"{path}: SHA-256 matches the configuration ({check_weights(block, path, args[1])})")
            return 0
    except WeightsError as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2
    print("usage: python -m training.common.weights FILE                    print the SHA-256 to record in the configuration\n"
          "       python -m training.common.weights --check NAME [--file F]  NAME is `teacher` or an investigator candidate id", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
