"""The validators of models.md 5.7. Item validators take one example record (5.9) and return a list of problems (empty
means pass). Dataset validators take lists of records and return a list of failures."""
from __future__ import annotations

import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

from training.assistant import lexical, protocol as P, textchecks
from training.assistant.assemble import ALL_CLASSES, POS_CLASSES
from training.assistant.domain import check_page, check_story  # noqa: F401  (V02 and V03 live in domain.py)
from training.common.config import REPO_ROOT

KEYS = ["schema_version", "id", "split", "domain", "lang", "app_name", "class", "style", "question", "context", "gold", "target", "overlap", "seed"]
ID_RE = re.compile(r"^d\d{2,}-(en|it)-\d{6}$")
STYLES = ["symptom", "goal", "colleague", "terse", "rambling", "typo", "multi"]
ITEM_VALIDATORS = ["V01", "V04", "V05", "V06", "V07", "V08", "V09", "V10", "V11"]
BUCKETS = {"pos_distant": (-1.0, 0.15), "pos_mid": (0.15, 0.35), "pos_near": (0.35, 2.0)}


# ---------------------------------------------------------------- item validators

def v01(rec: Mapping[str, Any]) -> list[str]:
    """The record matches the example format of 5.9."""
    p: list[str] = []
    if list(rec.keys()) != KEYS:
        return [f"keys are {list(rec.keys())}, expected {KEYS}"]
    if rec["schema_version"] != 1:
        p.append("schema_version must be 1")
    if not isinstance(rec["id"], str) or not ID_RE.match(rec["id"]):
        p.append("id format")
    if rec["split"] not in ("train", "validation", "test"):
        p.append("split")
    if rec["lang"] not in ("en", "it"):
        p.append("lang")
    if rec["class"] not in ALL_CLASSES:
        p.append("class")
    for k in ("domain", "app_name", "question"):
        if not isinstance(rec[k], str) or not rec[k].strip():
            p.append(f"{k} must be a non-empty string")
    if not isinstance(rec["style"], str) or rec["style"] not in STYLES:
        p.append("style")
    ctx = rec["context"]
    if not isinstance(ctx, list) or not 1 <= len(ctx) <= 4 or not all(
            isinstance(c, dict) and list(c.keys()) == ["id", "title", "text"] and all(isinstance(v, str) and v for v in c.values()) for c in ctx):
        p.append("context must be 1 to 4 objects with id, title, text")
        return p
    g = rec["gold"]
    if not isinstance(g, list) or not all(isinstance(x, int) and not isinstance(x, bool) and 1 <= x <= len(ctx) for x in g) or len(set(g)) != len(g) or g != sorted(g):
        p.append("gold must be distinct ascending positions inside the context")
    elif (rec["class"] in POS_CLASSES) != bool(g):
        p.append("gold must be non-empty exactly for positive classes")
    elif rec["class"] == "pos_multi" and len(g) != 2:
        p.append("pos_multi needs two gold positions")
    elif rec["class"] in ("pos_distant", "pos_mid", "pos_near") and len(g) != 1:
        p.append("single-gold class needs one gold position")
    if not isinstance(rec["target"], str) or not rec["target"]:
        p.append("target must be a non-empty string")
    if not isinstance(rec["overlap"], (int, float)) or isinstance(rec["overlap"], bool) or not 0 <= rec["overlap"] <= 1:
        p.append("overlap must be a number from 0 to 1")
    if not isinstance(rec["seed"], int) or isinstance(rec["seed"], bool):
        p.append("seed must be an integer")
    return p


def v04(rec: Mapping[str, Any], blocklist: Sequence[str]) -> list[str]:
    """No blocklisted name, e-mail address, phone number or URL in the question, the context or the target."""
    texts = [("question", rec["question"]), ("target", rec["target"] or "")] + [(f"context[{i}]", f"{c['title']}\n{c['text']}") for i, c in enumerate(rec["context"])]
    return [f"{where}: {why}" for where, t in texts if (why := textchecks.text_problem(t, blocklist))]


def v05(rec: Mapping[str, Any]) -> list[str]:
    """Question, pages and answer are in the declared language (other-language stopword share at most 0.10)."""
    lang = rec["lang"]
    other = "it" if lang == "en" else "en"
    parts = [("question", rec["question"])] + [(f"context[{i}]", c["text"]) for i, c in enumerate(rec["context"])]
    if rec["class"] in POS_CLASSES and rec["target"]:
        parts.append(("answer", rec["target"].rsplit("\n", 1)[0]))
    return [f"{w}: stopword share of {other} above 0.10" for w, t in parts if lexical.stopword_share(t, other, lang) > 0.10]


def v06(rec: Mapping[str, Any], protocol: Mapping[str, Any]) -> list[str]:
    """The target parses (3.4); a refusal target is exactly the sentence."""
    lang = rec["lang"]
    r = P.parse_answer(protocol, lang, rec["target"] or "", len(rec["context"]))
    if rec["class"].startswith("ref_"):
        return [] if rec["target"] == protocol["languages"][lang]["refusal"] else ["refusal target is not exactly the refusal sentence"]
    return [] if r.outcome == "answered" else [f"positive target does not parse as answered ({r.outcome}: {r.reason})"]


def v07(rec: Mapping[str, Any], protocol: Mapping[str, Any]) -> list[str]:
    """A positive cites every gold position (pos_multi) or at least the gold position (others) and nothing that does not exist."""
    if rec["class"] not in POS_CLASSES:
        return []
    r = P.parse_answer(protocol, rec["lang"], rec["target"] or "", len(rec["context"]))
    if r.outcome != "answered":
        return []                                   # V06 reports it
    if any(c < 1 or c > len(rec["context"]) for c in r.cited):
        return ["cites a position that does not exist"]
    gold = rec["gold"]
    ok = set(gold) <= set(r.cited) if rec["class"] == "pos_multi" else bool(set(gold) & set(r.cited))
    return [] if ok else [f"cited {r.cited} does not cover gold {gold}"]


_NUM = re.compile(r"\d+(?:[.,]\d+)?")
_QUOTED = re.compile(r"\"([^\"]+)\"")


def v08(rec: Mapping[str, Any], protocol: Mapping[str, Any], verdict: Mapping[str, Any] | None) -> list[str]:
    """Grounding: at least 90% of the content stems of the answer body occur in the context or the question; numbers and
    quoted strings of the body are in the context; the verifier did not say unsupported."""
    if rec["class"] not in POS_CLASSES:
        return []
    r = P.parse_answer(protocol, rec["lang"], rec["target"] or "", len(rec["context"]))
    if r.outcome != "answered" or r.body is None:
        return []
    lang = rec["lang"]
    context_text = "\n".join(f"{c['title']}\n{c['text']}" for c in rec["context"])
    problems: list[str] = []
    body_stems = lexical.analyze(r.body, lang)
    if body_stems:
        known = lexical.stem_set(context_text, lang) | lexical.stem_set(rec["question"], lang)
        share = sum(1 for s in body_stems if s in known) / len(body_stems)
        if share < 0.90:
            problems.append(f"only {share:.2f} of the body stems occur in the context or the question")
    for n in _NUM.findall(r.body):
        if n not in context_text:
            problems.append(f"number {n} is not in the context")
    for q in _QUOTED.findall(r.body):
        if q not in context_text:
            problems.append(f"quoted string \"{q}\" is not in the context")
    if verdict is None:
        problems.append("no verifier decision")
    elif verdict.get("supported") is not True or verdict.get("answers_question") is not True:
        problems.append(f"verifier: supported={verdict.get('supported')} answers_question={verdict.get('answers_question')}")
    return problems


def v09(rec: Mapping[str, Any]) -> list[str]:
    """The measured overlap falls in the bucket of the class (pos_multi and refusals have no bucket)."""
    b = BUCKETS.get(rec["class"])
    if not b:
        return []
    lo, hi = b
    return [] if lo < rec["overlap"] <= hi else [f"overlap {rec['overlap']} outside ({max(lo, 0)}, {hi}] of {rec['class']}"]


def v10(rec: Mapping[str, Any]) -> list[str]:
    """The question does not copy four consecutive stems of a gold section (quoted UI labels excepted in pos_near)."""
    if rec["class"] not in POS_CLASSES:
        return []
    q = rec["question"]
    if rec["class"] == "pos_near":
        q = re.sub(r"\"[^\"]*\"", " ", q)
    for pos in rec["gold"]:
        c = rec["context"][pos - 1]
        if lexical.consecutive_stem_copy(q, f"{c['title']}\n{c['text']}", rec["lang"]):
            return [f"copies four consecutive stems of the gold section at position {pos}"]
    return []


def v11(rec: Mapping[str, Any], verdict: Mapping[str, Any] | None) -> list[str]:
    """A near-miss question the verifier finds answered in the domain."""
    if rec["class"] != "ref_near_miss":
        return []
    if verdict is None:
        return ["no verifier decision"]
    return ["the verifier finds the question answered in the domain"] if verdict.get("answered") is not False else []


def v12(records: Iterable[Mapping[str, Any]]) -> dict[str, str]:
    """Question whose stem set has Jaccard >= 0.8 with an earlier question of the same domain and language.
    Returns ``{id: reason}`` for the later items."""
    seen: dict[tuple[str, str], list[set[str]]] = defaultdict(list)
    out: dict[str, str] = {}
    for rec in records:
        stems = lexical.stem_set(rec["question"], rec["lang"])
        key = (rec["domain"], rec["lang"])
        if any(lexical.jaccard(stems, prev) >= 0.8 for prev in seen[key]):
            out[rec["id"]] = "stem set too close to an earlier question"
        else:
            seen[key].append(stems)
    return out


def run_item_validators(rec: Mapping[str, Any], protocol: Mapping[str, Any], blocklist: Sequence[str],
                        verdict: Mapping[str, Any] | None) -> dict[str, list[str]]:
    """All item validators; returns ``{validator id: problems}`` for the ones that fail. V01 failing stops the others."""
    failed: dict[str, list[str]] = {}
    p = v01(rec)
    if p:
        return {"V01": p}
    for vid, fn in (("V04", lambda: v04(rec, blocklist)), ("V05", lambda: v05(rec)), ("V06", lambda: v06(rec, protocol)),
                    ("V07", lambda: v07(rec, protocol)), ("V08", lambda: v08(rec, protocol, verdict)), ("V09", lambda: v09(rec)),
                    ("V10", lambda: v10(rec)), ("V11", lambda: v11(rec, verdict))):
        p = fn()
        if p:
            failed[vid] = p
    return failed


# ---------------------------------------------------------------- dataset validators

def v13(train: Sequence[Mapping[str, Any]], all_records: Sequence[Mapping[str, Any]], protocol: Mapping[str, Any],
        min_sample: int = 200, tolerance: float = 0.03) -> list[str]:
    """Gold position share is 1/K within 0.03 for every K and position (single-gold positives of the train split; a K
    with fewer than ``min_sample`` such examples is not tested, the sample is too small to mean anything); and no
    rendered prompt exceeds the context budget."""
    fails: list[str] = []
    by_k: dict[int, Counter[int]] = defaultdict(Counter)
    for r in train:
        if r["class"] in ("pos_distant", "pos_mid", "pos_near") and len(r["gold"]) == 1:
            by_k[len(r["context"])][r["gold"][0]] += 1
    for k, c in sorted(by_k.items()):
        n = sum(c.values())
        if n < min_sample:
            continue
        for pos in range(1, k + 1):
            share = c[pos] / n
            if abs(share - 1 / k) > tolerance:
                fails.append(f"K={k} position {pos}: share {share:.3f}, expected {1 / k:.3f} +- {tolerance}")
    ctx = protocol["context"]
    for r in all_records:
        chars = sum(len(c["text"]) for c in r["context"])
        if (len(r["context"]) > ctx["max_sections"] or chars > ctx["max_context_chars"]
                or any(len(c["text"]) > ctx["max_section_chars"] for c in r["context"]) or len(P.normalise_question(protocol, r["question"])) > ctx["max_question_chars"]):
            fails.append(f"{r['id']}: prompt exceeds the context budget")
    return fails


def refusal_share_by_bucket(records: Iterable[Mapping[str, Any]]) -> dict[str, dict[str, dict[str, float | int]]]:
    out: dict[str, dict[str, dict[str, float | int]]] = {}
    tally: dict[tuple[str, str], list[int]] = defaultdict(lambda: [0, 0])
    for r in records:
        t = tally[(r["lang"], lexical.overlap_bucket(r["overlap"]))]
        t[0] += 1
        t[1] += 1 if r["class"].startswith("ref_") else 0
    for (lang, b), (n, refs) in sorted(tally.items()):
        out.setdefault(lang, {})[b] = {"n": n, "refusals": refs, "refusal_share": round(refs / n, 4) if n else 0.0}
    return out


def v14(train: Sequence[Mapping[str, Any]], class_shares: Mapping[str, float]) -> list[str]:
    """Class shares within 0.02 of the config; language split 0.48 to 0.52; refusal share 0.25 to 0.45 in every overlap
    bucket and language."""
    if not train:
        return ["the train split is empty"]
    fails: list[str] = []
    n = len(train)
    counts = Counter(r["class"] for r in train)
    for cls, want in class_shares.items():
        got = counts.get(cls, 0) / n
        if abs(got - want) > 0.02:
            fails.append(f"class {cls}: share {got:.3f}, config {want:.3f}")
    langs = Counter(r["lang"] for r in train)
    for lang in ("en", "it"):
        if not 0.48 <= langs.get(lang, 0) / n <= 0.52:
            fails.append(f"language {lang}: share {langs.get(lang, 0) / n:.3f} outside 0.48 to 0.52")
    for lang, buckets in refusal_share_by_bucket(train).items():
        for b, v in buckets.items():
            if not 0.25 <= v["refusal_share"] <= 0.45:
                fails.append(f"{lang} overlap bucket {b}: refusal share {v['refusal_share']:.3f} outside 0.25 to 0.45 (n={v['n']})")
    return fails


def sample_app_sequences(docs_root: Path, n: int = 8) -> dict[str, set[tuple[str, ...]]]:
    """8-stem sequences of the sample application's documentation (areas, manual, overview), by language."""
    res: dict[str, set[tuple[str, ...]]] = {"en": set(), "it": set()}
    files: list[Path] = []
    for sub in ("areas", "manual"):
        files += sorted((docs_root / sub).rglob("*.md")) if (docs_root / sub).exists() else []
    if (docs_root / "overview.md").exists():
        files.append(docs_root / "overview.md")
    for f in files:
        lang = "it" if "/manual/it/" in f.as_posix() else "en"
        res[lang] |= set(lexical.stem_sequences(f.read_text(encoding="utf-8", errors="replace"), lang, n))
        if lang == "en":
            continue
    return res


def v15(records: Sequence[Mapping[str, Any]], domains: Sequence[Mapping[str, Any]], docs_root: Path = REPO_ROOT / "docs",
        sequences: dict[str, set[tuple[str, ...]]] | None = None) -> list[str]:
    """A domain id in two splits; a sector of a test or validation domain in train; any 8-stem sequence of the sample
    app's docs in any item."""
    fails: list[str] = []
    splits_of: dict[str, set[str]] = defaultdict(set)
    for r in records:
        splits_of[r["domain"]].add(r["split"])
    fails += [f"domain {d} appears in splits {sorted(s)}" for d, s in sorted(splits_of.items()) if len(s) > 1]
    cfg_split = {d["id"]: d["split"] for d in domains}
    fails += [f"domain {r_dom} has split {sorted(s)} but domains.json says {cfg_split.get(r_dom)}" for r_dom, s in sorted(splits_of.items()) if cfg_split.get(r_dom) not in s]
    train_sectors = {d["sector"].strip().lower() for d in domains if d["split"] == "train"}
    fails += [f"sector '{d['sector']}' of {d['split']} domain {d['id']} is also a train sector" for d in domains if d["split"] != "train" and d["sector"].strip().lower() in train_sectors]
    seqs = sequences if sequences is not None else sample_app_sequences(docs_root)
    for r in records:
        lang = r["lang"]
        if not seqs.get(lang):
            continue
        text = "\n".join([r["question"], r["target"] or ""] + [f"{c['title']}\n{c['text']}" for c in r["context"]])
        for tup in lexical.stem_sequences(text, lang, 8):
            if tup in seqs[lang]:
                fails.append(f"{r['id']}: shares an 8-stem sequence with the sample app docs")
                break
    return fails
