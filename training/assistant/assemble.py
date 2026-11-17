"""Assembly of examples from the questions of the domains (models.md 5.3, 5.4).

Deterministic given the teacher cache: the same questions give the same selection, contexts, ids and seeds.
Positive targets (teacher answers) and verifier decisions are attached later by ``validate``.
"""
from __future__ import annotations

import math
import re
from dataclasses import dataclass, field
from typing import Any, Mapping

from training.assistant import lexical, protocol as P
from training.assistant.plan import largest_remainder
from training.common.hashing import derive_seed, rng

POS_CLASSES = ["pos_distant", "pos_mid", "pos_near", "pos_multi"]
REF_CLASSES = ["ref_near_miss", "ref_removed", "ref_oos"]
ALL_CLASSES = POS_CLASSES + REF_CLASSES
SEEN_SHARE = 0.05
# Composition of one domain and language in the evaluation splits (4.2). Validation is not specified by the document:
# 40 items per domain and language, split like the platform test.
EVAL_COMPOSITION = {
    "test": {"distant": 18, "mid": 10, "near": 8, "near_miss": 12, "oos": 12},
    "validation": {"distant": 12, "mid": 7, "near": 5, "near_miss": 8, "oos": 8},
}


@dataclass
class QRec:
    key: str                 # key of the teacher job that wrote it
    idx: int                 # position inside the job's answer
    domain: str
    lang: str
    kind: str                # story | multi | near_miss | oos
    text: str
    style: str
    story: str | None = None
    area: str | None = None
    gold: list[str] = field(default_factory=list)
    overlap: float = 0.0

    @property
    def qid(self) -> str:
        return f"{self.key}#{self.idx}"

    @property
    def bucket(self) -> str:
        return lexical.overlap_bucket(self.overlap)


@dataclass
class Example:
    id: str
    split: str
    domain: str
    lang: str
    app_name: str
    cls: str
    style: str
    question: str
    context: list[dict[str, str]]
    gold: list[int]
    overlap: float
    seed: int
    qkey: str
    target: str | None = None

    def record(self) -> dict[str, Any]:
        """The example format of 5.9, key order of the document."""
        return {"schema_version": 1, "id": self.id, "split": self.split, "domain": self.domain, "lang": self.lang,
                "app_name": self.app_name, "class": self.cls, "style": self.style, "question": self.question,
                "context": self.context, "gold": self.gold, "target": self.target, "overlap": self.overlap, "seed": self.seed}


# ---------------------------------------------------------------- context construction

def _fill(selected: list[Mapping[str, Any]], sections: list[Mapping[str, Any]], need: int, exclude: set[str],
          r) -> list[Mapping[str, Any]]:
    chosen = {s["id"] for s in selected}
    rest = [s for s in sections if s["id"] not in chosen and s["id"] not in exclude]
    r.shuffle(rest)
    out = list(selected)
    for s in rest:
        if len(out) >= need:
            break
        out.append(s)
    return out


def build_context(*, protocol: Mapping[str, Any], retrieval: Mapping[str, Any], index: lexical.Index, question: str,
                  gold: list[str], k: int | None, cls: str, master_seed: int, example_id: str, train_order: bool
                  ) -> tuple[list[Mapping[str, Any]], list[int], Mapping[str, Any]] | None:
    """Return ``(sections in context order, 1-based gold positions, top-ranked section)`` or ``None`` when the budget
    cannot be met."""
    by_id = {s["id"]: s for s in index.sections}
    r = rng(master_seed, f"{example_id}/context")
    exclude = set(gold) if cls == "ref_removed" else set()
    ranked = [s for s, _score in index.rank(question, retrieval, exclude=exclude)]
    max_k = protocol["context"]["max_sections"]
    limit = k if k is not None else max_k
    selected = list(P.select_sections(protocol, ranked, limit))
    if cls.startswith("pos_"):
        for g in gold:
            if g in {s["id"] for s in selected}:
                continue
            if len(selected) < limit:
                selected.append(by_id[g])
            else:
                for i in range(len(selected) - 1, -1, -1):          # replace the lowest-ranked non-gold section
                    if selected[i]["id"] not in gold:
                        selected[i] = by_id[g]
                        break
    need = max(2, len(gold)) if k is None else k
    if len(selected) < need:
        selected = _fill(selected, index.sections, need, set(gold) if not cls.startswith("pos_") else set(), r)
    while P.context_chars(selected) > protocol["context"]["max_context_chars"]:
        drop = next((i for i in range(len(selected) - 1, -1, -1) if selected[i]["id"] not in gold), None)
        if drop is None or len(selected) <= 2:
            return None
        selected.pop(drop)
    if len(selected) < 2 and len(index.sections) >= 2:
        return None
    top = selected[0]
    if train_order:
        order = list(range(len(selected)))
        rng(master_seed, f"{example_id}/shuffle").shuffle(order)
        selected = [selected[i] for i in order]
    positions = [i + 1 for i, s in enumerate(selected) if s["id"] in gold]
    return selected, positions, top


# ---------------------------------------------------------------- question pools

def prepare_pool(*, questions: list[QRec], index: lexical.Index, sections_by_id: Mapping[str, Mapping[str, Any]], lang: str,
                 retrieval: Mapping[str, Any], blocklist: list[str], reject) -> list[QRec]:
    """Question-level filters (V04, V05, V10, V12) and the measurement of the overlap. ``reject(validator, qrec, reason)``
    logs the rejections. Returns the accepted questions in order."""
    from training.assistant import textchecks

    kept: list[QRec] = []
    seen_sets: list[set[str]] = []
    for q in questions:
        problem = textchecks.text_problem(q.text, blocklist)
        if problem:
            reject("V04", q, problem)
            continue
        other = "it" if lang == "en" else "en"
        if lexical.stopword_share(q.text, other, lang) > 0.10:
            reject("V05", q, f"stopword share of {other} above 0.10")
            continue
        stems = lexical.stem_set(q.text, lang)
        if not stems:
            reject("V09", q, "question has no content stems")
            continue
        if q.kind in ("story", "multi"):
            gold_secs = [sections_by_id[g] for g in q.gold]
            q.overlap = lexical.overlap(q.text, gold_secs, lang)
            check_text = q.text
            if lexical.overlap_bucket(q.overlap) == "near":
                check_text = re.sub(r"\"[^\"]*\"", " ", q.text)     # UI labels may be quoted in pos_near
            if any(lexical.consecutive_stem_copy(check_text, f"{g['title']}\n{g['text']}", lang) for g in gold_secs):
                reject("V10", q, "copies four consecutive stems of a gold section")
                continue
        if any(lexical.jaccard(stems, prev) >= 0.8 for prev in seen_sets):
            reject("V12", q, "stem set too close to an earlier question")
            continue
        seen_sets.append(stems)
        kept.append(q)
    return kept


# ---------------------------------------------------------------- quotas

def train_counts(pools: Mapping[str, int], story_total: int, shares: Mapping[str, float], target: int) -> dict[str, int]:
    """Largest total N <= target such that every class count (by ``shares``) fits its pool. ``ref_removed`` draws from the
    in-scope questions not used by the single-gold positive classes."""
    if target <= 0:
        return {c: 0 for c in shares}
    caps = {c: pools.get(c, 0) for c in shares if c != "ref_removed"}
    bound = min((caps[c] / shares[c] for c in caps if shares[c] > 0), default=0)
    n = min(target, int(math.floor(bound + 1e-9)))
    while n > 0:
        counts = largest_remainder(n, dict(shares))
        single = counts["pos_distant"] + counts["pos_mid"] + counts["pos_near"]
        if all(counts[c] <= caps[c] for c in caps) and counts["ref_removed"] <= story_total - single:
            return counts
        n -= 1
    return {c: 0 for c in shares}


def hold_out_seen(story_q: list[QRec], master_seed: int, domain: str, lang: str) -> tuple[list[QRec], list[QRec]]:
    """Remove 5% of the in-scope questions of a training domain and language (seen-domain check, 4.2)."""
    ordered = sorted(story_q, key=lambda q: (q.key, q.idx))
    shuffled = list(ordered)
    rng(master_seed, f"{domain}/{lang}/seen").shuffle(shuffled)
    n = math.ceil(SEEN_SHARE * len(shuffled)) if shuffled else 0
    seen_ids = {q.qid for q in shuffled[:n]}
    return [q for q in ordered if q.qid not in seen_ids], [q for q in ordered if q.qid in seen_ids]


def eval_selection(split: str, story_q: list[QRec], near_q: list[QRec], oos_q: list[QRec], master_seed: int,
                   domain: str, lang: str) -> tuple[list[tuple[str, QRec]], dict[str, int]]:
    """Items of one domain and language in ``test`` or ``validation`` (4.2) with the shortfall per part."""
    comp = EVAL_COMPOSITION[split]
    order = ("distant", "mid", "near")
    buckets = {b: [q for q in sorted(story_q, key=lambda x: (x.key, x.idx)) if q.bucket == b] for b in order}
    for b in order:
        rng(master_seed, f"{domain}/{lang}/{split}/{b}").shuffle(buckets[b])
    take = {b: min(len(buckets[b]), comp[b]) for b in order}
    deficit = sum(comp[b] for b in order) - sum(take.values())
    for b in order:
        extra = min(deficit, len(buckets[b]) - take[b])
        take[b] += extra
        deficit -= extra
    out: list[tuple[str, QRec]] = []
    for b in order:
        out += [(f"pos_{b}", q) for q in buckets[b][: take[b]]]
    short = {"in_scope": deficit}
    for name, pool, cls in (("near_miss", near_q, "ref_near_miss"), ("oos", oos_q, "ref_oos")):
        pool = sorted(pool, key=lambda x: (x.key, x.idx))
        rng(master_seed, f"{domain}/{lang}/{split}/{name}").shuffle(pool)
        chosen = pool[: comp[name]]
        short[name] = comp[name] - len(chosen)
        out += [(cls, q) for q in chosen]
    return out, short
