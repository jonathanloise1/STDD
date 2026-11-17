"""Hashing and deterministic seeding (models.md 5.6)."""
from __future__ import annotations

import hashlib
import random
from pathlib import Path
from typing import Any, Sequence, TypeVar

from .jsonio import canonical_json

T = TypeVar("T")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_text(text: str) -> str:
    return sha256_bytes(text.encode("utf-8"))


def sha256_file(path: str | Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def canonical_sha256(obj: Any) -> str:
    return sha256_text(canonical_json(obj))


def git_blob_id(data: bytes) -> str:
    """Git blob id: SHA-1 of ``blob {length}\\0{bytes}`` (manual.md 2.3)."""
    return hashlib.sha1(b"blob %d\x00" % len(data) + data).hexdigest()


def derive_seed(master_seed: int, key: str) -> int:
    """``seed = int(sha256("{master_seed}|{key}")[0:8], 16)``."""
    return int(sha256_text(f"{master_seed}|{key}")[0:8], 16)


def rng(master_seed: int, key: str) -> random.Random:
    return random.Random(derive_seed(master_seed, key))


def shuffled(items: Sequence[T], master_seed: int, key: str) -> list[T]:
    out = list(items)
    rng(master_seed, key).shuffle(out)
    return out
