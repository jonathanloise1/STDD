"""JSON and JSONL helpers with deterministic output."""
from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any, Iterable, Iterator


def canonical_json(obj: Any) -> str:
    """Canonical form used for hashing: sorted keys, no whitespace, UTF-8 characters kept."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def dumps_line(obj: Any) -> str:
    """One JSONL line: key order preserved, no whitespace."""
    return json.dumps(obj, separators=(",", ":"), ensure_ascii=False)


def dumps_pretty(obj: Any) -> str:
    return json.dumps(obj, indent=2, ensure_ascii=False) + "\n"


def load_json(path: str | Path) -> Any:
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def atomic_write_text(path: str | Path, text: str) -> None:
    """Write through a temporary file in the same folder, then rename."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(dir=str(path.parent), prefix=path.name + ".", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.unlink(tmp)
        raise


def write_json(path: str | Path, obj: Any) -> None:
    atomic_write_text(path, dumps_pretty(obj))


def read_jsonl(path: str | Path) -> list[Any]:
    return list(iter_jsonl(path))


def iter_jsonl(path: str | Path) -> Iterator[Any]:
    with open(path, "r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line:
                yield json.loads(line)


def write_jsonl(path: str | Path, rows: Iterable[Any]) -> None:
    atomic_write_text(path, "".join(dumps_line(r) + "\n" for r in rows))


def append_jsonl(path: str | Path, row: Any) -> None:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "a", encoding="utf-8", newline="\n") as fh:
        fh.write(dumps_line(row) + "\n")
