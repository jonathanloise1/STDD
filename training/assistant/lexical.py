"""Python port of the help analyzer, of BM25F ranking and of the glossary (manual.md 6.4, 6.5, 6.9), and the lexical
distance of 5.4.

The TypeScript implementation under ``src/frontend/src/help/lexical/`` is the reference at run time. This port serves
the synthetic dataset (overlap, forbidden lists, V05, V08 to V12, retrieval for training contexts). It needs the
pure-Python ``snowballstemmer`` package (imported lazily) and the stopword lists: the frontend files when they exist,
otherwise the copies in ``config/``.
"""
from __future__ import annotations

import math
import re
import unicodedata
from collections import Counter
from functools import lru_cache
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

from training.common.config import REPO_ROOT

FRONTEND_STOPWORDS = REPO_ROOT / "src" / "frontend" / "src" / "help" / "lexical"
LOCAL_STOPWORDS = Path(__file__).with_name("config")
_SPLIT = re.compile(r"[\W_]+", re.UNICODE)
SNOWBALL_NAMES = {"en": "english", "it": "italian"}

# Changes with any change to the analyzer steps or the stopword lists (manual.md 6.4); equal to ANALYZER_VERSION of
# src/frontend/src/help/lexical/analyzer.ts, which tests/test_lexical_golden.py checks through the golden file.
ANALYZER_VERSION = 2


def stopwords_path(lang: str) -> Path:
    front = FRONTEND_STOPWORDS / f"stopwords.{lang}.txt"
    return front if front.exists() else LOCAL_STOPWORDS / f"stopwords.{lang}.txt"


@lru_cache(maxsize=None)
def stopwords(lang: str) -> frozenset[str]:
    return frozenset(w.strip() for w in stopwords_path(lang).read_text(encoding="utf-8").splitlines() if w.strip())


@lru_cache(maxsize=None)
def _stemmer(lang: str):
    try:
        import snowballstemmer  # type: ignore
    except ImportError as exc:  # pragma: no cover
        raise RuntimeError("snowballstemmer is required for the analyzer: pip install -r training/requirements.txt") from exc
    return snowballstemmer.stemmer(SNOWBALL_NAMES[lang])


def strip_diacritics(token: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", token) if not unicodedata.combining(c))


def tokens(text: str, lang: str, *, keep_stopwords: bool = False) -> list[str]:
    """Steps 1 to 3 of 6.4: NFC, lower case, split on non letters or digits, drop one-character tokens and stopwords."""
    raw = _SPLIT.split(unicodedata.normalize("NFC", text).lower())
    sw = stopwords(lang)
    return [t for t in raw if len(t) > 1 and (keep_stopwords or t not in sw)]


@lru_cache(maxsize=200_000)
def _stem_cached(token: str, lang: str) -> str:
    return strip_diacritics(_stemmer(lang).stemWord(token))


def stem_token(token: str, lang: str) -> str:
    """Steps 4 and 5 of 6.4 for one token that already passed steps 1 to 3."""
    return _stem_cached(token, lang)


def analyze(text: str, lang: str) -> list[str]:
    """Full analyzer of 6.4: the list of stems (with repetitions, in order)."""
    return [_stem_cached(t, lang) for t in tokens(text, lang)]


def stem_set(text: str, lang: str) -> set[str]:
    return set(analyze(text, lang))


def stopword_share(text: str, lang_other: str, lang_own: str) -> float:
    """Share of tokens that are stopwords of ``lang_other`` (words in both lists ignored), outside double quotes (manual.md 2.6)."""
    outside = re.sub(r"\"[^\"]*\"", " ", unicodedata.normalize("NFC", text))
    other, own = stopwords(lang_other), stopwords(lang_own)
    toks = [t for t in _SPLIT.split(outside.lower()) if len(t) > 0 and t not in (other & own)]
    if not toks:
        return 0.0
    return sum(1 for t in toks if t in other) / len(toks)


# ---------------------------------------------------------------- lexical distance (5.4)

def overlap(question: str, sections: Sequence[Mapping[str, Any]], lang: str) -> float:
    """Share of the distinct content stems of the question that occur in the title or text of the section
    (for several sections, their union). A question without content stems has overlap 0."""
    q = stem_set(question, lang)
    if not q:
        return 0.0
    found: set[str] = set()
    for s in sections:
        found |= stem_set(f"{s['title']}\n{s['text']}", lang)
    return round(len(q & found) / len(q), 4)


def overlap_bucket(value: float) -> str:
    """``distant`` (<= 0.15), ``mid`` (0.15, 0.35], ``near`` (> 0.35)."""
    return "distant" if value <= 0.15 else "mid" if value <= 0.35 else "near"


def consecutive_stem_copy(question: str, section_text: str, lang: str, run: int = 4) -> bool:
    """True when ``run`` consecutive stems of the question appear consecutively in the section (V10)."""
    q = analyze(question, lang)
    s = analyze(section_text, lang)
    if len(q) < run:
        return False
    grams = {tuple(s[i:i + run]) for i in range(len(s) - run + 1)}
    return any(tuple(q[i:i + run]) in grams for i in range(len(q) - run + 1))


def stem_sequences(text: str, lang: str, n: int) -> Iterable[tuple[str, ...]]:
    st = analyze(text, lang)
    return (tuple(st[i:i + n]) for i in range(len(st) - n + 1))


def jaccard(a: set[str], b: set[str]) -> float:
    if not a and not b:
        return 1.0
    return len(a & b) / len(a | b)


def top_tfidf_stems(section: Mapping[str, Any], domain_sections: Sequence[Mapping[str, Any]], lang: str, n: int = 12) -> list[str]:
    """The ``n`` stems of the section with the highest tf-idf in the domain (forbidden list of 5.4); ties by stem."""
    docs = [Counter(analyze(f"{s['title']}\n{s['text']}", lang)) for s in domain_sections]
    df: Counter[str] = Counter()
    for d in docs:
        df.update(d.keys())
    tf = Counter(analyze(f"{section['title']}\n{section['text']}", lang))
    n_docs = max(1, len(docs))
    scored = sorted(((-(c * math.log(1 + n_docs / max(1, df.get(t, 1)))), t) for t, c in tf.items()))
    return [t for _s, t in scored[:n]]


# ---------------------------------------------------------------- glossary (6.9)

def compile_glossary(rows: Sequence[Mapping[str, Any]], lang: str) -> list[tuple[list[str], list[str]]]:
    """``rows`` of ``{"term": str, "aliases": [str]}`` to ``(term stems, added stems)``; a row without stems is dropped."""
    out: list[tuple[list[str], list[str]]] = []
    for r in rows:
        term = analyze(r["term"], lang)
        add: list[str] = []
        for a in r["aliases"]:
            for s in analyze(a, lang):
                if s not in term and s not in add:
                    add.append(s)
        if term and add:
            out.append((term, add))
    return out


def _contains_run(stems: Sequence[str], run: Sequence[str]) -> bool:
    n = len(run)
    return any(list(stems[i:i + n]) == list(run) for i in range(len(stems) - n + 1))


def glossary_stems(stems: Sequence[str], glossary: Sequence[tuple[list[str], list[str]]]) -> list[str]:
    """Stems a field gains: the added stems of every entry whose term occurs in it, each once."""
    out: list[str] = []
    for term, add in glossary:
        if _contains_run(stems, term):
            out.extend(s for s in add if s not in out)
    return out


def page_of(section_id: str) -> str:
    return section_id.split("#", 1)[0]


def pages_first(ranked: Sequence[Any], key=lambda h: h) -> list[Any]:
    """``diversify`` = ``page`` (6.5): the best section of each page in order, then the others in order."""
    seen: set[str] = set()
    first: list[Any] = []
    rest: list[Any] = []
    for h in ranked:
        page = page_of(key(h))
        (rest if page in seen else first).append(h)
        seen.add(page)
    return first + rest


# ---------------------------------------------------------------- BM25F (6.5)

class Index:
    """BM25F index over sections with fields ``title``, ``text``, ``questions``; ``glossary`` rows as in 6.9."""

    FIELDS = ("title", "text", "questions")
    GLOSSARY_FIELDS = (0, 1)

    def __init__(self, sections: Sequence[Mapping[str, Any]], lang: str, glossary: Sequence[Mapping[str, Any]] = ()):
        self.sections = list(sections)
        self.lang = lang
        self.glossary = compile_glossary(glossary, lang)
        self.tf: list[list[Counter[str]]] = []
        self.len: list[list[int]] = []
        self.df: Counter[str] = Counter()
        for s in self.sections:
            fields: list[Counter[str]] = []
            lengths: list[int] = []
            for i, f in enumerate(self.FIELDS):
                value = s.get(f, "")
                stems = analyze(value if isinstance(value, str) else "\n".join(value), lang)
                gained = glossary_stems(stems, self.glossary) if i in self.GLOSSARY_FIELDS else []
                fields.append(Counter(stems + gained))
                lengths.append(len(stems))
            self.tf.append(fields)
            self.len.append(lengths)
            terms: set[str] = set()
            for c in fields:
                terms |= set(c)
            self.df.update(terms)
        n = len(self.sections)
        self.avg = [(sum(l[i] for l in self.len) / n) if n else 0.0 for i in range(3)]

    @property
    def n(self) -> int:
        return len(self.sections)

    def idf(self, term: str) -> float:
        df = self.df.get(term, 0)
        return math.log(1 + (self.n - df + 0.5) / (df + 0.5))

    def lexical_ranked(self, query: str, retrieval: Mapping[str, Any], exclude: set[str] | None = None) -> list[tuple[float, str, int]]:
        """All sections with a positive BM25F score as ``(-score, id, index)``, best first, ties by section id in code point order."""
        bm = retrieval["bm25"]
        k1, b, w = bm["k1"], bm["b"], [bm["field_weights"][f] for f in self.FIELDS]
        q = sorted(stem_set(query, self.lang))
        scored: list[tuple[float, str, int]] = []
        for i, s in enumerate(self.sections):
            if exclude and s["id"] in exclude:
                continue
            score = 0.0
            for t in q:
                tfp = 0.0
                for f in range(3):
                    tf = self.tf[i][f].get(t, 0)
                    if tf:
                        avg = self.avg[f] or 1.0
                        tfp += w[f] * tf / (1 - b + b * self.len[i][f] / avg)
                if tfp:
                    score += self.idf(t) * tfp * (k1 + 1) / (tfp + k1)
            if score > 0:
                scored.append((-score, s["id"], i))
        scored.sort()
        return scored

    def rank(self, query: str, retrieval: Mapping[str, Any], exclude: set[str] | None = None) -> list[tuple[Mapping[str, Any], float]]:
        """Top ``candidates`` sections by score, ties by section id in code point order; score 0 is not returned. Lexical only."""
        scored = self.lexical_ranked(query, retrieval, exclude)
        if retrieval.get("diversify", "none") == "page":
            scored = pages_first(scored, key=lambda x: x[1])
        return [(self.sections[i], -neg) for neg, _sid, i in scored[: retrieval["candidates"]]]

    def search(self, query: str, retrieval: Mapping[str, Any], scorer: "EmbeddingScorer | None" = None,
               exclude: set[str] | None = None) -> list[tuple[Mapping[str, Any], float]]:
        """Retrieval of 6.5: lexical ranking, or reciprocal rank fusion when ``scorer`` is given and ``fusion.method`` is ``rrf``.

        ``retrieval`` holds the effective values (``effective_retrieval``)."""
        if scorer is None or retrieval["fusion"]["method"] != "rrf":
            return self.rank(query, retrieval, exclude)
        depth = retrieval["fusion"].get("depth", DEFAULT_DEPTH)
        lex = self.lexical_ranked(query, retrieval, exclude)[:depth]
        emb = scorer.rank(sorted(stem_set(query, self.lang)), depth)
        fused = fuse_rrf([(sid, i) for _neg, sid, i in lex], emb, retrieval["fusion"]["rrf_k"], retrieval["fusion"]["embedding_weight"],
                         [s["id"] for s in self.sections])
        if retrieval.get("diversify", "none") == "page":
            fused = pages_first(fused, key=lambda x: x[1])
        return [(self.sections[i], score) for score, _sid, i in fused[: retrieval["candidates"]]]

    def decide(self, query: str, retrieval: Mapping[str, Any], scorer: "EmbeddingScorer | None" = None,
               hits: Sequence[tuple[Mapping[str, Any], float]] | None = None) -> dict[str, Any]:
        """The out-of-scope decision of 6.6: ``{"in_scope", "coverage", "cosine", "top_id"}``."""
        stems = sorted(stem_set(query, self.lang))
        if hits is None:
            hits = self.search(query, retrieval, scorer)
        fusion_on = scorer is not None and retrieval["fusion"]["method"] == "rrf"
        top_index = None
        if hits:
            top_id = hits[0][0]["id"]
            top_index = next(i for i, s in enumerate(self.sections) if s["id"] == top_id)
        coverage = self.coverage(query, top_index) if top_index is not None else 0.0
        cosine = scorer.cosine(stems, top_index) if fusion_on and top_index is not None else None
        oos = retrieval["out_of_scope"]
        enough = len(stems) >= oos["min_query_terms"]
        if not fusion_on:
            in_scope = enough and coverage >= oos["min_coverage"]
        else:
            lexical_ok = coverage >= oos["min_coverage"] and (cosine is None or cosine >= oos.get("min_cosine_lexical", -1.0))
            in_scope = enough and ((cosine is not None and cosine >= oos["min_cosine"]) or lexical_ok)
        return {"in_scope": in_scope, "coverage": coverage, "cosine": cosine, "top_id": hits[0][0]["id"] if hits else None}

    def coverage(self, query: str, section_index: int) -> float:
        """Out-of-scope coverage of 6.6 for the given section."""
        q = stem_set(query, self.lang)
        if not q:
            return 0.0
        unknown = math.log(1 + (self.n + 0.5) / 0.5)
        total = sum(self.idf(t) if t in self.df else unknown for t in q)
        found = sum(self.idf(t) for t in q if any(self.tf[section_index][f].get(t) for f in range(3)))
        return found / total if total else 0.0


# ---------------------------------------------------------------- static embeddings (6.5 fusion, 6.6, 6.7.6)

DEFAULT_DEPTH = 30


def effective_retrieval(retrieval: Mapping[str, Any], params: Mapping[str, Any] | None) -> dict[str, Any]:
    """The retrieval parameters with the pack's ``embeddings.params`` laid over the defaults (6.5): ``rrf_k``, ``embedding_weight``,
    ``min_cosine`` and ``min_cosine_lexical``; ``method`` ``none`` turns fusion off. ``depth`` always stays the file's."""
    import copy
    out = copy.deepcopy(dict(retrieval))
    out.setdefault("fusion", {"method": "none"})
    out["fusion"].setdefault("depth", DEFAULT_DEPTH)
    out["out_of_scope"].setdefault("min_cosine_lexical", -1.0)
    if params:
        out["fusion"]["method"] = params.get("method", out["fusion"]["method"])
        for key in ("rrf_k", "embedding_weight"):
            if key in params:
                out["fusion"][key] = params[key]
        for key in ("min_cosine", "min_cosine_lexical"):
            if key in params:
                out["out_of_scope"][key] = params[key]
    return out


def fuse_rrf(lex: Sequence[tuple[str, int]], emb: Sequence[tuple[str, int, float]], rrf_k: float, weight: float,
             ids: Sequence[str]) -> list[tuple[float, str, int]]:
    """Reciprocal rank fusion (6.5 step 3): ``(score, id, index)`` best first, ties by section id. ``lex`` is ``(id, index)`` and ``emb``
    ``(id, index, cosine)``, both already cut to ``depth``."""
    score: dict[int, float] = {}
    for rank, (_sid, i) in enumerate(lex, 1):
        score[i] = score.get(i, 0.0) + 1 / (rrf_k + rank)
    for rank, (_sid, i, _c) in enumerate(emb, 1):
        score[i] = score.get(i, 0.0) + weight / (rrf_k + rank)
    return sorted(((s, ids[i], i) for i, s in score.items()), key=lambda x: (-x[0], x[1]))


def _normalise(x):
    import numpy as np
    n = float(np.sqrt(float(np.dot(x, x))))
    return None if n == 0.0 else x / n


class EmbeddingScorer:
    """Section vectors and query vectors of 6.7.6 over a table (``embtable.Table``) and an ``Index``.

    ``s_d = normalise(sum over stems t in the table of (sum over fields f of w_f x tf(t,d,f)) x u_t x v_t)``;
    ``q = normalise(sum over the distinct query stems t in the table of u_t x v_t)``. All arithmetic is float64."""

    def __init__(self, table, index: "Index", field_weights: Mapping[str, float]):
        import numpy as np
        self.table = table
        self.index = index
        self._rows = table.decoded() * table.u()[:, None]            # u_t x v_t
        w = [field_weights[f] for f in Index.FIELDS]
        self.section_vectors: list[Any] = []
        for i in range(index.n):
            weight: dict[int, float] = {}
            for f in range(3):
                for stem, tf in index.tf[i][f].items():
                    row = table.index.get(stem)
                    if row is not None:
                        weight[row] = weight.get(row, 0.0) + w[f] * tf
            if not weight:
                self.section_vectors.append(None)
                continue
            acc = np.zeros(table.dims, dtype=np.float64)
            for r in sorted(weight):
                acc += weight[r] * self._rows[r]
            self.section_vectors.append(_normalise(acc))

    def query_vector(self, stems: Sequence[str]):
        import numpy as np
        rows = sorted({self.table.index[s] for s in stems if s in self.table.index})
        if not rows:
            return None
        acc = np.zeros(self.table.dims, dtype=np.float64)
        for r in rows:
            acc += self._rows[r]
        return _normalise(acc)

    def cosines(self, stems: Sequence[str]) -> list[float | None] | None:
        """Cosine of the query to every section (None for a section without a vector); None when the query has no vector."""
        import numpy as np
        q = self.query_vector(stems)
        if q is None:
            return None
        return [None if s is None else float(np.dot(q, s)) for s in self.section_vectors]

    def rank(self, stems: Sequence[str], depth: int) -> list[tuple[str, int, float]]:
        """The embedding list of 6.5 step 2: cosine greater than 0 only, ties by section id, cut to ``depth``."""
        cos = self.cosines(stems)
        if cos is None:
            return []
        ids = [s["id"] for s in self.index.sections]
        hits = [(ids[i], i, c) for i, c in enumerate(cos) if c is not None and c > 0]
        hits.sort(key=lambda x: (-x[2], x[0]))
        return hits[:depth]

    def cosine(self, stems: Sequence[str], section_index: int) -> float | None:
        """Cosine of the query and one section (6.6): None without a query vector, 0 when the section has no vector."""
        cos = self.cosines(stems)
        if cos is None:
            return None
        c = cos[section_index]
        return 0.0 if c is None else c
