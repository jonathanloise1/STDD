"""Binary file of the help embedding table (manual.md 6.7.2): the writer and the reader of the Python side.

Little-endian, one file per language, written by the Python builder only; TypeScript reads it with the same checks
(``src/frontend/src/help/lexical/embeddings.ts``). The layout, offsets and checks are exactly those of 6.7.2 and 6.7.5.

``numpy`` is a light dependency (``training/requirements.txt``); nothing here imports a model library.
"""
from __future__ import annotations

import json
import struct
from dataclasses import dataclass, field
from typing import Any, Sequence

import numpy as np

MAGIC = b"HELPEMB\x00"
FORMAT_VERSION = 1
HEADER_BYTES = 32
_HEADER = struct.Struct("<8sH2sHHIIII")            # magic, version, lang, dims, reserved, count, meta, vocab, reserved
assert _HEADER.size == HEADER_BYTES

META_KEYS = ["kind", "format_version", "lang", "model", "analyzer", "config_sha256", "inputs_sha256", "sources"]


class TableError(ValueError):
    """The file is not a usable table; the message is the reason of 6.7.5."""


def meta_json(meta: dict[str, Any]) -> bytes:
    """Compact JSON, keys in the order of 6.7.2, UTF-8 (the writer keeps the order it is given)."""
    return json.dumps(meta, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def table_size(dims: int, meta_bytes: int, vocab_bytes: int, count: int) -> int:
    """Exact file size of 6.7.2: ``32 + 4d + M + V + N + N x d``."""
    return HEADER_BYTES + 4 * dims + meta_bytes + vocab_bytes + count + count * dims


def vocab_bytes(vocab: Sequence[str]) -> bytes:
    return b"".join(s.encode("utf-8") + b"\n" for s in vocab)


@dataclass
class Table:
    lang: str
    dims: int
    count: int
    scales: np.ndarray                       # float64 view of the float32 scales, shape (d,)
    meta: dict[str, Any]
    vocab: list[str]
    weights: np.ndarray                      # uint8, shape (N,)
    vectors: np.ndarray                      # int8, shape (N, d)
    index: dict[str, int] = field(default_factory=dict)
    _decoded: np.ndarray | None = None

    def decoded(self) -> np.ndarray:
        """``v_t[j] = vector[t][j] x scales[j]`` in float64, not renormalised."""
        if self._decoded is None:
            self._decoded = self.vectors.astype(np.float64) * self.scales[None, :]
        return self._decoded

    def u(self) -> np.ndarray:
        """Weight of every stem divided by 255."""
        return self.weights.astype(np.float64) / 255.0


def encode_table(lang: str, scales: np.ndarray, meta: dict[str, Any], vocab: Sequence[str], weights: np.ndarray,
                 vectors: np.ndarray) -> bytes:
    """The bytes of a table. ``vocab`` must already be sorted by code point and unique; ``read_table`` checks it."""
    dims = int(scales.shape[0])
    count = len(vocab)
    if len(lang.encode("ascii")) != 2:
        raise TableError(f"language {lang!r} is not two ASCII characters")
    if vectors.shape != (count, dims) or weights.shape != (count,):
        raise TableError("vectors and weights do not match the vocabulary")
    meta_b = meta_json(meta)
    vocab_b = vocab_bytes(vocab)
    parts = [
        _HEADER.pack(MAGIC, FORMAT_VERSION, lang.encode("ascii"), dims, 0, count, len(meta_b), len(vocab_b), 0),
        np.asarray(scales, dtype="<f4").tobytes(),
        meta_b,
        vocab_b,
        np.asarray(weights, dtype=np.uint8).tobytes(),
        np.asarray(vectors, dtype=np.int8).tobytes(),
    ]
    return b"".join(parts)


def read_table(data: bytes, expect_lang: str | None = None) -> Table:
    """Parse and check a table: the checks 2 and 4 of 6.7.5 (the hash is the caller's, the pack comparisons too)."""
    if len(data) < HEADER_BYTES:
        raise TableError("the file is shorter than its 32-byte header")
    magic, version, lang_b, dims, r1, count, meta_n, vocab_n, r2 = _HEADER.unpack_from(data, 0)
    if magic != MAGIC:
        raise TableError("bad magic: not a help embedding table")
    if version < 1 or version > FORMAT_VERSION:
        raise TableError(f"format_version {version} is not supported (this reader knows {FORMAT_VERSION})")
    try:
        lang = lang_b.decode("ascii")
    except UnicodeDecodeError as exc:
        raise TableError("the language is not ASCII") from exc
    if expect_lang is not None and lang != expect_lang:
        raise TableError(f"the table is for language {lang!r}, expected {expect_lang!r}")
    if dims < 1 or r1 != 0 or r2 != 0:
        raise TableError("dims is zero or a reserved field is not zero")
    want = table_size(dims, meta_n, vocab_n, count)
    if len(data) != want:
        raise TableError(f"the file has {len(data)} bytes, its header says {want}")
    off = HEADER_BYTES
    scales = np.frombuffer(data, dtype="<f4", count=dims, offset=off).astype(np.float64)
    if not np.all(scales > 0):
        raise TableError("a quantisation scale is not greater than 0")
    off += 4 * dims
    try:
        meta = json.loads(data[off:off + meta_n].decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise TableError("meta is not UTF-8 JSON") from exc
    if not isinstance(meta, dict):
        raise TableError("meta is not a JSON object")
    off += meta_n
    try:
        text = data[off:off + vocab_n].decode("utf-8")
    except UnicodeDecodeError as exc:
        raise TableError("the vocabulary is not UTF-8") from exc
    off += vocab_n
    if count == 0:
        vocab: list[str] = []
        if text:
            raise TableError("the vocabulary has bytes but count is 0")
    else:
        if not text.endswith("\n"):
            raise TableError("the vocabulary does not end with a line feed")
        vocab = text[:-1].split("\n")
    if len(vocab) != count:
        raise TableError(f"the vocabulary has {len(vocab)} stems, count says {count}")
    for a, b in zip(vocab, vocab[1:]):
        if not a < b:
            raise TableError("the vocabulary is not sorted by code point and unique")
    if vocab and vocab[0] == "":
        raise TableError("the vocabulary has an empty stem")
    weights = np.frombuffer(data, dtype=np.uint8, count=count, offset=off).copy()
    off += count
    if count and int(weights.min()) < 1:
        raise TableError("a weight is 0")
    vectors = np.frombuffer(data, dtype=np.int8, count=count * dims, offset=off).reshape(count, dims).copy()
    return Table(lang, dims, count, scales, meta, vocab, weights, vectors, {s: i for i, s in enumerate(vocab)})
