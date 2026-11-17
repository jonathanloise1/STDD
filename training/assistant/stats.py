"""Dataset statistics of models.md 5.8."""
from __future__ import annotations

import math
from collections import Counter, defaultdict
from typing import Any, Iterable, Mapping, Sequence

from training.assistant.validators import refusal_share_by_bucket


def percentile(values: Sequence[float], p: float) -> float:
    """Nearest-rank percentile; 0 for an empty list."""
    if not values:
        return 0
    s = sorted(values)
    return s[max(0, math.ceil(p / 100 * len(s)) - 1)]


def _dist(values: Sequence[float]) -> dict[str, float]:
    return {"p50": percentile(values, 50), "p95": percentile(values, 95), "max": max(values) if values else 0}


def histogram(values: Iterable[float], bins: int = 10) -> list[int]:
    out = [0] * bins
    for v in values:
        out[min(bins - 1, int(v * bins))] += 1
    return out


def compute(records_by_split: Mapping[str, Sequence[Mapping[str, Any]]], rejections: Sequence[Mapping[str, Any]],
            usage: Mapping[str, Any], dataset_sha256: str, extra: Mapping[str, Any] | None = None) -> dict[str, Any]:
    allr = [r for s in ("train", "validation", "test") for r in records_by_split.get(s, [])]
    by = lambda key: dict(sorted(Counter(key(r) for r in allr).items()))   # noqa: E731
    hist: dict[str, dict[str, list[int]]] = defaultdict(dict)
    groups: dict[tuple[str, str], list[float]] = defaultdict(list)
    for r in allr:
        groups[(r["class"], r["lang"])].append(r["overlap"])
    for (cls, lang), vals in sorted(groups.items()):
        hist[cls][lang] = histogram(vals)
    gold_pos: dict[str, dict[str, int]] = defaultdict(lambda: defaultdict(int))
    for r in allr:
        for g in r["gold"]:
            gold_pos[f"K={len(r['context'])}"][str(g)] += 1
    ctx_chars = [sum(len(c["text"]) for c in r["context"]) for r in allr]
    ans_chars = [len(r["target"]) for r in allr]
    by_validator = Counter(r["validator"] for r in rejections)
    by_stage = Counter(r["stage"] for r in rejections)
    stats = {
        "counts": {"per_split": {s: len(records_by_split.get(s, [])) for s in ("train", "validation", "test")},
                   "per_split_language": {s: dict(sorted(Counter(r["lang"] for r in records_by_split.get(s, [])).items())) for s in ("train", "validation", "test")},
                   "per_class": by(lambda r: r["class"]), "per_style": by(lambda r: r["style"]), "per_domain": by(lambda r: r["domain"]),
                   "per_split_class": {s: dict(sorted(Counter(r["class"] for r in records_by_split.get(s, [])).items())) for s in ("train", "validation", "test")}},
        "overlap_histogram_10_bins": {k: dict(v) for k, v in sorted(hist.items())},
        "refusal_share_per_overlap_bucket": {s: refusal_share_by_bucket(records_by_split.get(s, [])) for s in ("train", "validation", "test")},
        "gold_position_histogram": {k: dict(sorted(v.items())) for k, v in sorted(gold_pos.items())},
        "context_chars": _dist(ctx_chars),
        "answer_chars": _dist(ans_chars),
        "rejections": {"per_validator": dict(sorted(by_validator.items())), "per_stage": dict(sorted(by_stage.items())), "total": len(rejections)},
        "teacher": {s: {"jobs": u.jobs, "found": u.found, "input_tokens": u.input_tokens, "output_tokens": u.output_tokens, "seconds": round(u.seconds, 1)} for s, u in usage.items()},
        "dataset_sha256": dataset_sha256,
    }
    if extra:
        stats.update(extra)
    return stats


def format_tables(stats: Mapping[str, Any]) -> str:
    def table(title: str, rows: list[tuple[Any, ...]], header: tuple[str, ...]) -> str:
        data = [header] + [tuple(str(c) for c in r) for r in rows]
        w = [max(len(r[i]) for r in data) for i in range(len(header))]
        lines = [title, "  ".join(h.ljust(w[i]) for i, h in enumerate(header)), "  ".join("-" * x for x in w)]
        lines += ["  ".join(c.ljust(w[i]) for i, c in enumerate(r)) for r in data[1:]]
        return "\n".join(lines)

    c = stats["counts"]
    parts = [table("Examples per split and language", [(s, n, c["per_split_language"][s].get("en", 0), c["per_split_language"][s].get("it", 0)) for s, n in c["per_split"].items()], ("split", "total", "en", "it")),
             table("Examples per class", list(c["per_class"].items()), ("class", "count")),
             table("Examples per style", list(c["per_style"].items()), ("style", "count"))]
    rows = [(s, lang, b, v["n"], v["refusals"], v["refusal_share"]) for s, langs in stats["refusal_share_per_overlap_bucket"].items() for lang, bs in langs.items() for b, v in bs.items()]
    parts.append(table("Refusal share per overlap bucket", rows, ("split", "lang", "bucket", "n", "refusals", "share")))
    parts.append(table("Gold position histogram", [(k, ", ".join(f"{p}:{n}" for p, n in v.items())) for k, v in stats["gold_position_histogram"].items()], ("K", "position:count")))
    parts.append(table("Characters", [("context", *stats["context_chars"].values()), ("answer", *stats["answer_chars"].values())], ("what", "p50", "p95", "max")))
    r = stats["rejections"]
    parts.append(table("Rejections", [(f"validator {k}", v) for k, v in r["per_validator"].items()] + [(f"stage {k}", v) for k, v in r["per_stage"].items()] + [("total", r["total"])], ("where", "count")))
    parts.append(table("Teacher usage per stage", [(s, u["jobs"], u["found"], u["input_tokens"], u["output_tokens"], f"{u['seconds']:.0f}") for s, u in stats["teacher"].items()], ("stage", "jobs", "cached", "input tokens", "output tokens", "seconds")))
    parts.append(f"dataset_sha256: {stats['dataset_sha256']}")
    return "\n\n".join(parts)
