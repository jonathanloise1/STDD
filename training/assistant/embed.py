"""The help embedding table: build and probe (models.md 7.8, manual.md 6.7).

    python -m training.assistant.embed build [--dry-run] [--langs en,it] [--packs DIR] [--run ID] [--model-version V]
                                             [--device auto|cuda|mps|cpu] [--allow-unverified]
    python -m training.assistant.embed probe --run ID --lang LANG --question TEXT

``build`` distils, once per stem, a local Qwen embedding model into a static table (one int8 vector per stem, 128 dimensions by
default) and writes ``{lang}.bin`` (manual.md 6.7.2) into ``training/out/embeddings/runs/{run_id}/`` with ``build-{lang}.json``,
``run.json`` and ``exports.json``. ``probe`` explains a retrieval with the Python port; it runs no model.

``torch`` and ``transformers`` are imported inside ``HFEncoder`` only: ``--dry-run``, source reading, the tokenizer words and
the tests need the light dependencies (numpy, snowballstemmer). The Transformers path of ``HFEncoder`` follows the model card of
the pinned revision and has not run against the real weights yet (models.md 7.8 status).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
import time
import unicodedata
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping, Protocol, Sequence

import numpy as np

from training.common.config import REPO_ROOT, next_run_id
from training.common.hashing import sha256_bytes, sha256_file
from training.common.jsonio import load_json, write_json
from training.common.runs import RunRecorder, code_commit

from . import embtable
from . import lexical as L

CONFIG_REL = "training/assistant/config/embed.json"
RETRIEVAL_REL = "training/assistant/retrieval.json"
DEFAULT_PACKS = "src/frontend/public/help"
LANGS = ("en", "it")
REVISION_RE = re.compile(r"^[0-9a-f]{40}$")
SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+$")
GROUPS = ("project", "wordlist", "tokenizer")
ENCODE_RATE = {"cuda": 220.0, "mps": 120.0, "cpu": 33.0}          # words per second: estimates, not measured (models.md 7.8)


class BuildError(RuntimeError):
    """The build refuses; the message says what to fix."""


# ---------------------------------------------------------------- configuration

def config_path(root: Path = REPO_ROOT) -> Path:
    return root / CONFIG_REL


def load_config(root: Path = REPO_ROOT) -> dict[str, Any]:
    return load_json(config_path(root))


def check_config(cfg: Mapping[str, Any], allow_unverified: bool = False) -> None:
    """The refusals of models.md 7.8 "Configuration"."""
    model = cfg["model"]
    if not REVISION_RE.match(str(model.get("revision", ""))):
        raise BuildError(f"model.revision {model.get('revision')!r} is not a 40-character hexadecimal commit")
    if not model.get("license"):
        raise BuildError("model.license is empty")
    if model.get("verified") is not True and not allow_unverified:
        raise BuildError("model.verified is not true: open the model card at the pinned revision, review the licence, set verified to "
                         "true and update checked (models.md 9.5 step 2); rehearsals use --allow-unverified")
    for lang, entry in (cfg.get("sources", {}).get("wordlists") or {}).items():
        if entry is not None and (not isinstance(entry, dict) or not entry.get("path") or not entry.get("license")):
            raise BuildError(f"the word list of {lang} needs a path and a licence: refused")
    table = cfg["table"]
    if table["dims"] < 1 or table["max_stems"] < 1 or table["max_tokens"] < 1:
        raise BuildError("table.dims, table.max_stems and table.max_tokens must be positive")


# ---------------------------------------------------------------- tokenizer words (models.md 7.8 step 2)

def _byte_decoder() -> dict[str, int]:
    """The inverse of the GPT-2 byte-to-unicode table used by byte-level BPE vocabularies."""
    bs = list(range(ord("!"), ord("~") + 1)) + list(range(ord("¡"), ord("¬") + 1)) + list(range(ord("®"), ord("ÿ") + 1))
    cs = bs[:]
    n = 0
    for b in range(256):
        if b not in bs:
            bs.append(b)
            cs.append(256 + n)
            n += 1
    return {chr(c): b for b, c in zip(bs, cs)}


SPACE_MARKER = "Ġ"                                              # the byte-level image of a space: ``Ġ``


def tokenizer_words(vocab: Mapping[str, int]) -> list[str]:
    """Whole-word tokens of a byte-level vocabulary, lower-cased and NFC, in ascending token id: a token that starts with the
    space marker and whose rest decodes to at least two characters, each a letter whose Unicode name starts with ``LATIN``."""
    decoder = _byte_decoder()
    out: list[str] = []
    for token, _tid in sorted(vocab.items(), key=lambda kv: (kv[1], kv[0])):
        if not token.startswith(SPACE_MARKER) or len(token) < 2:
            continue
        try:
            raw = bytes(decoder[c] for c in token[1:]).decode("utf-8")
        except (KeyError, UnicodeDecodeError):
            continue
        if len(raw) >= 2 and all(c.isalpha() and unicodedata.name(c, "").startswith("LATIN") for c in raw):
            out.append(unicodedata.normalize("NFC", raw.lower()))
    return out


def find_vocab(hub_id: str, revision: str, env: Mapping[str, str] | None = None) -> Path | None:
    """``vocab.json`` of the model: in the folder named by ``hub_id``, or in the Hugging Face cache at the pinned revision."""
    env = os.environ if env is None else env
    folder = Path(hub_id).expanduser()
    if folder.is_dir():
        return folder / "vocab.json" if (folder / "vocab.json").is_file() else None
    hub = Path(env.get("HF_HUB_CACHE") or (Path(env.get("HF_HOME") or "~/.cache/huggingface").expanduser() / "hub")).expanduser()
    p = hub / ("models--" + hub_id.replace("/", "--")) / "snapshots" / revision / "vocab.json"
    return p if p.is_file() else None


# ---------------------------------------------------------------- sources (manual.md 6.7.3)

@dataclass
class Sources:
    lang: str
    pack: dict[str, Any]
    texts: list[str]                                   # project texts: pack titles, texts, indexed questions, glossary, locale strings
    wordlist: list[str]
    files: list[tuple[str, str]]                       # (repository-relative path, SHA-256 of its bytes) of the non-pack source files
    analyzer: dict[str, Any]


def analyzer_block(lang: str) -> dict[str, Any]:
    return {"id": f"snowball-{lang}", "version": L.ANALYZER_VERSION, "stopwords_sha256": sha256_file(L.stopwords_path(lang))}


def parse_glossary(text: str, lang: str) -> list[dict[str, Any]]:
    """Rows ``{"term", "aliases"}`` of the ``## {lang}`` section of ``docs/manual/glossary.md`` (manual.md 6.9)."""
    rows: list[dict[str, Any]] = []
    section = None
    for line in text.splitlines():
        if line.startswith("## "):
            section = line[3:].strip()
            continue
        if section != lang or not line.strip().startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2 or cells[0] == "Term" or set(cells[0]) <= set("-: "):
            continue
        aliases = [a.strip() for a in cells[1].split(",") if a.strip()]
        if cells[0] and aliases:
            rows.append({"term": unicodedata.normalize("NFC", cells[0]), "aliases": [unicodedata.normalize("NFC", a) for a in aliases]})
    return rows


_PLACEHOLDER = re.compile(r"\{\{.*?\}\}")
_TAG = re.compile(r"<[^>]*>")


def locale_strings(value: Any) -> list[str]:
    """Every string value, recursively, with ``{{...}}`` placeholders and ``<...>`` tags removed."""
    if isinstance(value, str):
        return [_TAG.sub(" ", _PLACEHOLDER.sub(" ", value))]
    if isinstance(value, dict):
        return [s for v in value.values() for s in locale_strings(v)]
    if isinstance(value, list):
        return [s for v in value for s in locale_strings(v)]
    return []


def read_sources(root: Path, lang: str, packs: Path, cfg: Mapping[str, Any]) -> Sources:
    """Reads the sources of 6.7.3 for one language. Never opens ``docs/areas/`` or ``docs/manual/bench.md``."""
    pack_file = packs / lang / "pack.json"
    if not pack_file.is_file():
        raise BuildError(f"{pack_file} does not exist: run `npm run help:build` in src/frontend first")
    pack = json.loads(pack_file.read_text(encoding="utf-8"))
    want = analyzer_block(lang)
    if pack.get("analyzer") != want:
        raise BuildError(f"the {lang} pack was built with another analyzer or stopword list ({pack.get('analyzer')} instead of {want}): "
                         "run `npm run help:build` again")
    texts: list[str] = []
    for s in pack["sections"]:
        texts += [s["title"], s["text"]]
    texts += [q["text"] for q in pack["questions"]]
    files: list[tuple[str, str]] = []

    def rel(p: Path) -> str:
        try:
            return p.relative_to(root).as_posix()
        except ValueError:
            return p.as_posix()

    glossary = root / cfg["sources"]["glossary"]
    if glossary.is_file():
        data = glossary.read_bytes()
        files.append((rel(glossary), sha256_bytes(data)))
        for row in parse_glossary(data.decode("utf-8"), lang):
            texts += [row["term"], *row["aliases"]]
    locales = root / cfg["sources"]["locales"].format(lang=lang)
    if locales.is_dir():
        for f in sorted(locales.glob("*.json")):
            data = f.read_bytes()
            files.append((rel(f), sha256_bytes(data)))
            texts += locale_strings(json.loads(data.decode("utf-8")))
    wordlist: list[str] = []
    entry = (cfg["sources"].get("wordlists") or {}).get(lang)
    if entry:
        wl = root / entry["path"]
        if not wl.is_file():
            raise BuildError(f"the word list {entry['path']} does not exist")
        data = wl.read_bytes()
        files.append((rel(wl), sha256_bytes(data)))
        wordlist = [ln.strip() for ln in data.decode("utf-8").splitlines() if ln.strip()]
    return Sources(lang, pack, texts, wordlist, files, want)


def inputs_sha256(src: Sources) -> str:
    """SHA-256 of the lines ``pack {build.inputs_sha256}`` and ``{path} {SHA-256}`` of every other source file, sorted, joined with LF."""
    lines = [f"pack {src.pack['build']['inputs_sha256']}"] + [f"{p} {h}" for p, h in src.files]
    return sha256_bytes("\n".join(sorted(lines)).encode("utf-8"))


# ---------------------------------------------------------------- stems (models.md 7.8 steps 3 and 4)

@dataclass
class StemInfo:
    stem: str
    group: str
    rank: int
    word: str


@dataclass
class StemPlan:
    stems: list[StemInfo]                              # selected, sorted by code point of the stem
    counts: Counter                                    # project stem -> occurrences in the project sources
    ranked_total: int                                  # R: number of accepted tokenizer words
    available: dict[str, int] = field(default_factory=dict)   # candidate stems per group before the limit


def _single_stem(word: str, lang: str) -> str | None:
    """The stem of a word that yields exactly one token under the analyzer; None otherwise (skipped)."""
    toks = L.tokens(word, lang)
    if len(toks) != 1:
        return None
    stem = L.stem_token(toks[0], lang)
    return stem or None


def plan_stems(src: Sources, tok_words: Sequence[str], max_stems: int) -> StemPlan:
    lang = src.lang
    ranked_total = len(tok_words)
    word_rank: dict[str, int] = {}
    for i, w in enumerate(tok_words, 1):
        word_rank.setdefault(w, i)
    # words of the tokenizer by stem
    tok_stem_words: dict[str, list[str]] = {}
    for w in word_rank:
        s = _single_stem(w, lang)
        if s is not None:
            tok_stem_words.setdefault(s, []).append(w)

    def stem_rank(stem: str) -> int:
        ws = tok_stem_words.get(stem)
        return min(word_rank[w] for w in ws) if ws else ranked_total

    # project stems and their surface words
    project: dict[str, Counter] = {}
    counts: Counter = Counter()
    for text in src.texts:
        for tok in L.tokens(text, lang):
            stem = L.stem_token(tok, lang)
            if not stem:
                continue
            project.setdefault(stem, Counter())[tok] += 1
            counts[stem] += 1
    wordlist_words: dict[str, list[str]] = {}
    for raw in src.wordlist:
        s = _single_stem(raw, lang)
        if s is not None:
            wordlist_words.setdefault(s, []).append(L.tokens(raw, lang)[0])

    def own_rank(word: str) -> int:
        return word_rank.get(word, ranked_total)

    chosen: dict[str, StemInfo] = {}
    for stem in project:
        best = min(project[stem].items(), key=lambda kv: (-kv[1], own_rank(kv[0]), kv[0]))[0]
        chosen[stem] = StemInfo(stem, "project", stem_rank(stem), best)
    n_project = len(chosen)
    avail = {"project": n_project, "wordlist": 0, "tokenizer": 0}
    for group, words_by_stem in (("wordlist", wordlist_words), ("tokenizer", tok_stem_words)):
        pool = sorted((s for s in words_by_stem if s not in chosen), key=lambda s: (stem_rank(s), s))
        avail[group] = len(pool)
        for s in pool:
            if len(chosen) >= max_stems:
                break
            best = min(words_by_stem[s], key=lambda w: (own_rank(w), w))
            chosen[s] = StemInfo(s, group, stem_rank(s), best)
    for s in chosen:
        if any(ord(c) > 0xFFFF for c in s) or "\n" in s or not s:
            raise BuildError(f"the stem {s!r} has a character outside the Basic Multilingual Plane, a line feed or is empty: "
                             "the table format cannot hold it (manual.md 6.7.2)")
    return StemPlan([chosen[s] for s in sorted(chosen)], counts, ranked_total, avail)


# ---------------------------------------------------------------- encoders

class Encoder(Protocol):
    name: str
    device: str

    def token_counts(self, words: Sequence[str]) -> list[int]: ...

    def encode(self, words: Sequence[str]) -> np.ndarray:
        """L2-normalised document-side vectors, float32, shape (len(words), dimensions)."""


def _hash_vector(key: str, dim: int) -> np.ndarray:
    out = bytearray()
    k = 0
    while len(out) < dim:
        out += hashlib.sha256(f"{key}|{k}".encode("utf-8")).digest()
        k += 1
    return (np.frombuffer(bytes(out[:dim]), dtype=np.uint8).astype(np.float64) - 127.5) / 127.5


class FakeEncoder:
    """Vectors from the SHA-256 of the word, for tests and fixtures; words of one cluster share a base vector, so they are near each
    other. Deterministic, no model, no network."""

    name = "fake-encoder"
    device = "cpu"

    def __init__(self, dim: int = 32, clusters: Sequence[Sequence[str]] = ()):
        self.dim = dim
        self.cluster_of = {w: i for i, c in enumerate(clusters) for w in c}

    def token_counts(self, words: Sequence[str]) -> list[int]:
        return [max(1, math.ceil(len(w) / 4)) for w in words]

    def encode(self, words: Sequence[str]) -> np.ndarray:
        rows = []
        for w in words:
            v = 0.5 * _hash_vector(f"word:{w}", self.dim)
            if w in self.cluster_of:
                v = v + 2.0 * _hash_vector(f"cluster:{self.cluster_of[w]}", self.dim)
            rows.append((v / np.linalg.norm(v)).astype(np.float32))
        return np.stack(rows) if rows else np.zeros((0, self.dim), dtype=np.float32)


def version_tuple(v: str) -> tuple[int, ...]:
    return tuple(int(x) for x in re.findall(r"\d+", v)[:3])


class HFEncoder:
    """The pinned Qwen embedding model through Transformers, as its model card shows: the tokenizer with left padding, ``AutoModel``,
    last-token pooling, L2 normalisation; float32, ``torch.no_grad()``; documents take no prompt (models.md 7.8 step 5).
    Heavy imports happen here and nowhere else."""

    name = "hf"

    def __init__(self, cfg: Mapping[str, Any], device: str):
        import torch                                                # noqa: PLC0415 (lazy on purpose)
        import transformers                                         # noqa: PLC0415
        from transformers import AutoModel, AutoTokenizer           # noqa: PLC0415
        m = cfg["model"]
        if version_tuple(transformers.__version__) < version_tuple(m["min_transformers"]):
            raise BuildError(f"transformers {transformers.__version__} is older than {m['min_transformers']}: "
                             "pip install -r training/requirements.txt")
        kw: dict[str, Any] = {} if Path(m["hub_id"]).expanduser().is_dir() else {"revision": m["revision"]}
        self.torch = torch
        self.device = device
        self.tokenizer = AutoTokenizer.from_pretrained(m["hub_id"], padding_side="left", **kw)
        self.model = AutoModel.from_pretrained(m["hub_id"], torch_dtype=torch.float32, **kw).to(device).eval()
        self.batch = int(cfg["table"]["batch_size"])

    def token_counts(self, words: Sequence[str]) -> list[int]:
        return [len(ids) for ids in self.tokenizer(list(words), add_special_tokens=False)["input_ids"]]

    def encode(self, words: Sequence[str]) -> np.ndarray:
        torch = self.torch
        out = []
        with torch.no_grad():
            for i in range(0, len(words), self.batch):
                batch = self.tokenizer(list(words[i:i + self.batch]), padding=True, truncation=True, max_length=64, return_tensors="pt")
                batch = {k: v.to(self.device) for k, v in batch.items()}
                hidden = self.model(**batch).last_hidden_state
                vec = torch.nn.functional.normalize(hidden[:, -1], p=2, dim=1)       # left padding: the last position is the last token
                out.append(vec.float().cpu().numpy())
        return np.concatenate(out) if out else np.zeros((0, 0), dtype=np.float32)


def choose_device(requested: str = "auto", cuda: bool | None = None, mps: bool | None = None) -> str:
    """``auto`` takes CUDA, then Apple MPS, then the CPU; availability is probed with torch unless given (tests)."""
    if requested != "auto":
        return requested
    if cuda is None or mps is None:
        try:
            import torch                                            # noqa: PLC0415
            cuda = torch.cuda.is_available() if cuda is None else cuda
            mps = bool(getattr(torch.backends, "mps", None) and torch.backends.mps.is_available()) if mps is None else mps
        except ImportError:
            cuda, mps = bool(cuda), bool(mps)
    return "cuda" if cuda else "mps" if mps else "cpu"


# ---------------------------------------------------------------- reduction, quantisation, weights (steps 6 to 8)

def reduce_rows(x: np.ndarray, dims: int) -> tuple[np.ndarray, np.ndarray]:
    """Centre, PCA by SVD, keep ``dims`` components with a fixed sign, project, L2-normalise. Returns the rows and the mask of rows
    kept (a row with a norm below 1e-12 is dropped)."""
    x = np.asarray(x, dtype=np.float64)
    n, d = x.shape
    if dims > min(n, d):
        raise BuildError(f"table.dims is {dims} but there are only {n} vectors of {d} dimensions to reduce")
    xc = x - x.mean(axis=0)
    _u, _s, vt = np.linalg.svd(xc, full_matrices=False)
    comps = vt[:dims].copy()
    for k in range(dims):
        j = int(np.argmax(np.abs(comps[k])))                       # the lowest index on ties
        if comps[k, j] < 0:
            comps[k] = -comps[k]
    y = xc @ comps.T
    norms = np.linalg.norm(y, axis=1)
    keep = norms >= 1e-12
    return y[keep] / norms[keep, None], keep


def quantise(y: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """``scale_j = max_i |y_ij| / 127`` (1.0 for a zero column) stored as float32; ``q = rint(y / scale)`` clipped to -127..127."""
    amax = np.abs(y).max(axis=0) if len(y) else np.zeros(y.shape[1])
    scales = np.where(amax > 0, amax / 127.0, 1.0).astype(np.float32)
    q = np.clip(np.rint(y / scales.astype(np.float64)[None, :]), -127, 127).astype(np.int8)
    return scales, q


def weight_of(rank: int, ranked_total: int) -> int:
    """``u = max(1, round(255 x ln(2 + r) / ln(2 + R)))`` (half up): frequent general words weigh least."""
    return max(1, min(255, int(math.floor(255.0 * math.log(2 + rank) / math.log(2 + ranked_total) + 0.5))))


# ---------------------------------------------------------------- one language

@dataclass
class LangBuild:
    lang: str
    data: bytes
    sha256: str
    report: dict[str, Any]
    stems: int
    inputs_sha256: str
    analyzer: dict[str, Any]


def table_meta(lang: str, cfg: Mapping[str, Any], cfg_sha: str, version: str, analyzer: dict[str, Any], inputs: str,
               by_group: Mapping[str, int]) -> dict[str, Any]:
    m = cfg["model"]
    return {"kind": "help-embeddings", "format_version": embtable.FORMAT_VERSION, "lang": lang,
            "model": {"id": "embeddings", "version": version, "hub_id": m["hub_id"], "revision": m["revision"]},
            "analyzer": analyzer, "config_sha256": cfg_sha, "inputs_sha256": inputs,
            "sources": {g: int(by_group.get(g, 0)) for g in GROUPS}}


def build_language(src: Sources, plan: StemPlan, encoder: Encoder, cfg: Mapping[str, Any], cfg_sha: str, version: str,
                   cache: dict[str, np.ndarray | None], device: str,
                   progress: Callable[[str], None] = lambda m: None) -> LangBuild:
    """Steps 5 to 10 of models.md 7.8 for one language. ``cache`` holds word -> (vector, tokens) so that a word both languages need is
    encoded once per run."""
    t0 = time.monotonic()
    table_cfg = cfg["table"]
    max_tokens = int(table_cfg["max_tokens"])
    todo = [si.word for si in plan.stems if si.word not in cache]
    todo = list(dict.fromkeys(todo))
    counts = encoder.token_counts(todo) if todo else []
    fit = [w for w, n in zip(todo, counts) if n <= max_tokens]
    for w, n in zip(todo, counts):
        if n > max_tokens:
            cache[w] = None
    progress(f"{src.lang}: encoding {len(fit)} words ({len(todo) - len(fit)} too long)")
    if fit:
        vecs = encoder.encode(fit)
        for w, v in zip(fit, vecs):
            cache[w] = v
    kept = [si for si in plan.stems if cache.get(si.word) is not None]
    skipped = len(plan.stems) - len(kept)
    if not kept:
        raise BuildError(f"{src.lang}: no stem could be encoded")
    raw = np.stack([cache[si.word] for si in kept]).astype(np.float64)
    y, mask = reduce_rows(raw, int(table_cfg["dims"]))
    dropped = int((~mask).sum())
    final = [si for si, k in zip(kept, mask) if k]
    scales, q = quantise(y)
    weights = np.array([weight_of(si.rank, plan.ranked_total) for si in final], dtype=np.uint8)
    by_group = Counter(si.group for si in final)
    meta = table_meta(src.lang, cfg, cfg_sha, version, src.analyzer, inputs_sha256(src), by_group)
    data = embtable.encode_table(src.lang, scales, meta, [si.stem for si in final], weights, q)

    # step 9: read it back and check every row
    table = embtable.read_table(data, src.lang)
    dec = table.decoded()
    denom = np.linalg.norm(dec, axis=1) * np.linalg.norm(y, axis=1)
    cos = np.where(denom > 0, (dec * y).sum(axis=1) / np.where(denom > 0, denom, 1.0), 0.0)          # a row that decodes to zero has cosine 0
    lowest = float(cos.min())
    if lowest < float(table_cfg["min_row_cosine"]):
        raise BuildError(f"{src.lang}: a quantised row has cosine {lowest:.4f} with its float row, below {table_cfg['min_row_cosine']}")
    if len(data) > int(table_cfg["max_bytes"]):
        raise BuildError(f"{src.lang}: the table has {len(data)} bytes, above table.max_bytes {table_cfg['max_bytes']}")

    # step 10: the report, with the nearest stems of the 10 most frequent project stems for a person to read
    unit = dec / np.maximum(np.linalg.norm(dec, axis=1, keepdims=True), 1e-300)
    neighbours: dict[str, list[str]] = {}
    for stem, _n in sorted(plan.counts.items(), key=lambda kv: (-kv[1], kv[0])):
        if len(neighbours) == 10:
            break
        row = table.index.get(stem)
        if row is None:
            continue
        sims = unit @ unit[row]
        order = sorted((i for i in range(len(final)) if i != row), key=lambda i: (-sims[i], table.vocab[i]))
        neighbours[stem] = [table.vocab[i] for i in order[:8]]
    report = {
        "lang": src.lang, "stems": {g: int(by_group.get(g, 0)) for g in GROUPS}, "candidates": dict(plan.available),
        "skipped_words": skipped, "dropped_rows": dropped, "lowest_row_cosine": round(lowest, 6), "bytes": len(data),
        "dims": table.dims, "device": device, "duration_s": round(time.monotonic() - t0, 3), "table_sha256": sha256_bytes(data),
        "nearest": neighbours,
    }
    return LangBuild(src.lang, data, sha256_bytes(data), report, len(final), meta["inputs_sha256"], src.analyzer)


# ---------------------------------------------------------------- build command

def _langs(value: str) -> list[str]:
    langs = [x.strip() for x in value.split(",") if x.strip()]
    bad = [x for x in langs if x not in LANGS]
    if bad or not langs:
        raise BuildError(f"--langs takes {', '.join(LANGS)}; got {value!r}")
    return langs


def _write_bytes(path: Path, data: bytes) -> None:
    import tempfile
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(dir=str(path.parent), prefix=path.name + ".", suffix=".tmp")
    try:
        with os.fdopen(fd, "wb") as fh:
            fh.write(data)
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.unlink(tmp)
        raise


def _library_versions() -> dict[str, str]:
    out = {"python": sys.version.split()[0], "numpy": np.__version__}
    for mod in ("torch", "transformers"):
        m = sys.modules.get(mod)
        if m is not None:
            out[mod] = str(getattr(m, "__version__", "?"))
    return out


def dry_run(root: Path, langs: Sequence[str], packs: Path, cfg: Mapping[str, Any], cfg_sha: str, version: str, device: str,
            tok_words: Sequence[str] | None, out: Callable[[str], None]) -> int:
    """Stems per group, the expected file size and the estimated duration; writes nothing."""
    code = 0
    if tok_words is None:
        out("note: the tokenizer's vocab.json is not in the Hugging Face cache or the model folder: the `tokenizer` group is counted as 0")
    for lang in langs:
        src = read_sources(root, lang, packs, cfg)
        plan = plan_stems(src, tok_words or [], int(cfg["table"]["max_stems"]))
        by_group = Counter(si.group for si in plan.stems)
        meta_b = embtable.meta_json(table_meta(lang, cfg, cfg_sha, version, src.analyzer, inputs_sha256(src), by_group))
        vocab_b = len(embtable.vocab_bytes([si.stem for si in plan.stems]))
        size = embtable.table_size(int(cfg["table"]["dims"]), len(meta_b), vocab_b, len(plan.stems))
        rate = ENCODE_RATE.get(device, ENCODE_RATE["cpu"])
        out(f"{lang}: stems project {by_group['project']}, wordlist {by_group['wordlist']}, tokenizer {by_group['tokenizer']} "
            f"(total {len(plan.stems)}); expected file size {size} bytes (limit {cfg['table']['max_bytes']}); "
            f"estimated encoding {len(plan.stems) / rate / 60:.1f} minutes on {device} (an estimate, not measured)")
        if size > int(cfg["table"]["max_bytes"]):
            out(f"{lang}: the table would exceed table.max_bytes: lower table.max_stems or table.dims")
            code = 1
    return code


def run_build(root: Path, langs: Sequence[str], packs: Path, run_id: str | None, version: str, device_arg: str,
              allow_unverified: bool, dry: bool, encoder_factory: Callable[[Mapping[str, Any], str], Encoder] | None = None,
              vocab_path: Path | None = None, out: Callable[[str], None] = print, argv: list[str] | None = None) -> int:
    cfg = load_config(root)
    check_config(cfg, allow_unverified)
    if not SEMVER_RE.match(version):
        raise BuildError(f"--model-version {version!r} is not a semantic version")
    cfg_sha = sha256_file(config_path(root))
    device = choose_device(device_arg)
    m = cfg["model"]
    vocab = vocab_path or find_vocab(m["hub_id"], m["revision"])
    if dry:
        words = tokenizer_words(json.loads(vocab.read_text(encoding="utf-8"))) if vocab else None
        return dry_run(root, langs, packs, cfg, cfg_sha, version, device, words, out)

    sources = {lang: read_sources(root, lang, packs, cfg) for lang in langs}       # fail on the sources before any download
    encoder = (encoder_factory or (lambda c, d: HFEncoder(c, d)))(cfg, device)
    vocab = vocab_path or find_vocab(m["hub_id"], m["revision"])
    if vocab is None:
        raise BuildError(f"vocab.json of {m['hub_id']} at revision {m['revision']} is not in the Hugging Face cache or the model folder")
    tok_words = tokenizer_words(json.loads(vocab.read_text(encoding="utf-8")))

    training = root / "training"
    run_id = run_id or next_run_id("embeddings", training)
    run_dir = training / "out" / "embeddings" / "runs" / run_id
    cache: dict[str, np.ndarray | None] = {}
    with RunRecorder("embeddings", "build", {"embed.json": cfg_sha}, run_id, training, argv or []) as rec:
        results: dict[str, LangBuild] = {}
        for lang in langs:
            plan = plan_stems(sources[lang], tok_words, int(cfg["table"]["max_stems"]))
            res = build_language(sources[lang], plan, encoder, cfg, cfg_sha, version, cache, device, out)
            _write_bytes(run_dir / f"{lang}.bin", res.data)
            write_json(run_dir / f"build-{lang}.json", res.report)
            out(f"{lang}: {res.stems} stems, {len(res.data)} bytes, sha256 {res.sha256}")
            results[lang] = res
        dims = int(cfg["table"]["dims"])
        exports = {"exports": [{"format": "help-embeddings", "variant": f"int8-{dims}", "runtime": "browser",
                                "files": [{"path": f"{lang}.bin", "sha256": r.sha256, "bytes": len(r.data)} for lang, r in results.items()]}]}
        write_json(run_dir / "exports.json", exports)
        libs = _library_versions()
        rec.extra.update({"model": m["hub_id"], "revision": m["revision"], "device": device, "libraries": libs})
        top = load_json(run_dir / "run.json") if (run_dir / "run.json").exists() else {"schema_version": 1, "model": "embeddings", "run_id": run_id, "commands": []}
        top.update({
            "model_version": version, "status": "built",
            "base_model": {"hub_id": m["hub_id"], "revision": m["revision"], "license": m["license"], "parameters": m["parameters"], "dimensions": m["dimensions"]},
            "verified": m.get("verified") is True,
            "distillation": {"config": {"path": CONFIG_REL, "sha256": cfg_sha}, "dims": dims,
                             "languages": {lang: {"stems": r.stems, "inputs_sha256": r.inputs_sha256, "analyzer": r.analyzer} for lang, r in results.items()},
                             "run_id": run_id, "code_commit": code_commit(root), "device": device},
        })
        write_json(run_dir / "run.json", top)
    out(f"built run {run_id}: {run_dir}")
    return 0


# ---------------------------------------------------------------- probe

def index_from_pack(pack: Mapping[str, Any], lang: str, glossary_rows: Sequence[Mapping[str, Any]]) -> L.Index:
    """The Python ``Index`` of a pack, rebuilt from its sections and indexed questions as ``help:build`` indexes them."""
    by_story: dict[str, list[str]] = {}
    for q in pack["questions"]:
        by_story.setdefault(q["story"], []).append(q["text"])
    sections = [{"id": s["id"], "title": s["title"], "text": s["text"], "questions": by_story.get(s["story"], []) if s["story"] else []}
                for s in pack["sections"]]
    return L.Index(sections, lang, glossary_rows)


def probe(root: Path, run: str, lang: str, question: str, packs: Path, out: Callable[[str], None] = print) -> int:
    run_dir = root / "training" / "out" / "embeddings" / "runs" / run
    table_file = run_dir / f"{lang}.bin"
    if not table_file.is_file():
        raise BuildError(f"{table_file} does not exist")
    table = embtable.read_table(table_file.read_bytes(), lang)
    pack_file = packs / lang / "pack.json"
    if not pack_file.is_file():
        raise BuildError(f"{pack_file} does not exist: run `npm run help:build` first")
    pack = json.loads(pack_file.read_text(encoding="utf-8"))
    cfg = load_config(root)
    glossary = root / cfg["sources"]["glossary"]
    rows = parse_glossary(glossary.read_text(encoding="utf-8"), lang) if glossary.is_file() else []
    index = index_from_pack(pack, lang, rows)
    params = None
    cal = run_dir / "calibration.json"
    if cal.is_file():
        params = (load_json(cal).get("languages", {}).get(lang) or {}).get("params")
    retrieval = L.effective_retrieval(load_json(root / RETRIEVAL_REL), params)
    scorer = L.EmbeddingScorer(table, index, retrieval["bm25"]["field_weights"])

    stems = sorted(L.stem_set(question, lang))
    out(f"question: {question}")
    out("query stems (weight u, 255 = full):")
    for s in stems:
        row = table.index.get(s)
        out(f"  {s}  " + (f"u={int(table.weights[row])}/255" if row is not None else "NOT IN THE TABLE"))
    out(f"calibration: {'run calibration.json (' + json.dumps(params) + ')' if params else 'none: defaults of retrieval.json'}")

    def show(title: str, items: Sequence[tuple[str, str]]) -> None:
        out(title)
        for sid, extra in items[:5]:
            out(f"  {sid}  {extra}")
        if not items:
            out("  (none)")

    lex_retrieval = L.effective_retrieval(retrieval, {"method": "none"})
    show("top 5 lexical:", [(s["id"], f"score {sc:.4f}") for s, sc in index.search(question, lex_retrieval)])
    show("top 5 by cosine:", [(sid, f"cosine {c:.4f}") for sid, _i, c in scorer.rank(stems, retrieval["fusion"]["depth"])])
    fused = index.search(question, retrieval, scorer)
    show("top 5 fused:", [(s["id"], f"score {sc:.6f}") for s, sc in fused])
    d = index.decide(question, retrieval, scorer, fused)
    cos = "null" if d["cosine"] is None else f"{d['cosine']:.4f}"
    oos = retrieval["out_of_scope"]
    out(f"decision: {'in scope' if d['in_scope'] else 'OUT OF SCOPE'}; top {d['top_id']}; coverage {d['coverage']:.4f} (min {oos['min_coverage']}); "
        f"cosine {cos} (min_cosine {oos['min_cosine']}, min_cosine_lexical {oos['min_cosine_lexical']})")
    return 0


# ---------------------------------------------------------------- CLI

def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.assistant.embed", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="command", required=True)
    b = sub.add_parser("build", help="distil the embedding model into the tables of a run")
    b.add_argument("--dry-run", action="store_true", help="print stems per group, expected size and duration; writes nothing")
    b.add_argument("--langs", default=",".join(LANGS))
    b.add_argument("--packs", type=Path, help=f"folder with {{lang}}/pack.json (default {DEFAULT_PACKS})")
    b.add_argument("--run", help="run id {yyyy-mm-dd}-{letter} (default: the next one of the day)")
    b.add_argument("--model-version", default="1.0.0", help="the table version recorded in meta and in the lock")
    b.add_argument("--device", choices=["auto", "cuda", "mps", "cpu"], default="auto")
    b.add_argument("--allow-unverified", action="store_true", help="rehearsals and tests only: accept model.verified false")
    b.add_argument("--root", type=Path, default=REPO_ROOT, help="repository root (tests)")
    p = sub.add_parser("probe", help="explain a retrieval with the Python port (no model runs)")
    p.add_argument("--run", required=True)
    p.add_argument("--lang", required=True, choices=list(LANGS))
    p.add_argument("--question", required=True)
    p.add_argument("--packs", type=Path)
    p.add_argument("--root", type=Path, default=REPO_ROOT, help="repository root (tests)")
    args = ap.parse_args(argv)
    try:
        packs = args.packs or args.root / DEFAULT_PACKS
        if args.command == "build":
            return run_build(args.root, _langs(args.langs), packs, args.run, args.model_version, args.device, args.allow_unverified,
                             args.dry_run, argv=list(sys.argv[1:] if argv is None else argv))
        return probe(args.root, args.run, args.lang, args.question, packs)
    except (BuildError, embtable.TableError) as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
