"""Assistant evaluation: running items through a backend and computing the metrics of models.md 4.3.

Pure functions over result records, so every metric and threshold decision is tested with ``FakeBackend``.

A result record: ``{id, lang, class, domain, outcome, cited, answer, gold, overlap, k, latency_ms, ttft_ms, source}``
(``outcome`` is ``refused``, ``answered`` or ``invalid``; ``source`` is ``model`` or ``no-match`` for bench items the
out-of-scope decision rejected before the model).
"""
from __future__ import annotations

from typing import Any, Callable, Mapping, Sequence

from training.assistant import protocol as P
from training.common.backends import AnswerBackend
from training.common.judge import Verdict

IN_SCOPE_CLASSES = ("pos_distant", "pos_mid", "pos_near", "pos_multi", "bench_pos")
DISTANT = 0.15
METRIC_KEYS = ["format_valid", "faithful", "useful", "wrongful_refusal", "wrongful_refusal_distant", "oos_refusal", "near_miss_refusal", "correct_source"]


def is_in_scope(r: Mapping[str, Any]) -> bool:
    return r["class"] in IN_SCOPE_CLASSES


def run_items(backend: AnswerBackend, protocol: Mapping[str, Any], items: Sequence[Mapping[str, Any]],
              context_of: Callable[[Mapping[str, Any]], Sequence[Mapping[str, str]] | None] | None = None,
              max_new_tokens: int | None = None, progress: Callable[[str], None] | None = None) -> list[dict[str, Any]]:
    """Render each item through the single protocol (3.2), generate, parse (3.4). ``context_of(item)`` returns the context
    to use (default: the item's own ``context``); ``None`` means no model call (the out-of-scope decision said no)."""
    out: list[dict[str, Any]] = []
    max_new = max_new_tokens or protocol["decoding"]["max_new_tokens"]
    for n, it in enumerate(items, 1):
        ctx = it["context"] if context_of is None else context_of(it)
        base = {"id": it["id"], "lang": it["lang"], "class": it["class"], "domain": it.get("domain"), "gold": list(it.get("gold", [])),
                "overlap": it.get("overlap"), "k": len(ctx or [])}
        if ctx is None:
            out.append({**base, "outcome": "refused", "cited": [], "answer": protocol["languages"][it["lang"]]["refusal"], "latency_ms": 0.0,
                        "ttft_ms": None, "source": "no-match"})
            continue
        system, user = P.render_messages(protocol, it["lang"], it["app_name"], ctx, it["question"])
        res = backend.generate([{"role": "system", "content": system}, {"role": "user", "content": user}], max_new_tokens=max_new)
        parsed = P.parse_answer(protocol, it["lang"], res.text, len(ctx))
        out.append({**base, "outcome": parsed.outcome, "cited": parsed.cited, "answer": res.text, "latency_ms": res.total_ms,
                    "ttft_ms": res.ttft_ms, "source": "model"})
        if progress and n % 50 == 0:
            progress(f"{n}/{len(items)} items")
    return out


def judge_items(results: Sequence[dict[str, Any]], items_by_id: Mapping[str, Mapping[str, Any]], contexts: Mapping[str, Sequence[Mapping[str, str]]]
                ) -> list[tuple[dict[str, Any], str]]:
    """The ``(judge item, answer)`` pairs of the answered results (only answered items are judged, 4.4)."""
    pairs = []
    for r in results:
        if r["outcome"] != "answered":
            continue
        it = items_by_id[r["id"]]
        pairs.append(({"id": r["id"], "question": it["question"], "context": list(contexts.get(r["id"], it["context"])), "gold": r["gold"],
                       "reference": it.get("target") if it["class"] in IN_SCOPE_CLASSES else None}, r["answer"]))
    return pairs


def _share(num: int, den: int) -> float | None:
    return None if den == 0 else round(num / den, 4)


def compute_metrics(results: Sequence[Mapping[str, Any]], verdicts: Mapping[str, Verdict] | None = None) -> dict[str, Any]:
    """The metrics of 4.3 over one language. ``verdicts`` (by item id) are the judge's labels; without them faithful and
    useful are ``None`` (not measured). A population that is empty gives ``None``, except faithful, which is 1.0 when
    nothing was answered (nothing unfaithful was said) and is reported with ``answered``."""
    ins = [r for r in results if is_in_scope(r)]
    oos = [r for r in results if r["class"] == "ref_oos"]
    near = [r for r in results if r["class"] == "ref_near_miss"]
    answered = [r for r in results if r["outcome"] == "answered"]
    ins_answered = [r for r in ins if r["outcome"] == "answered"]
    distant = [r for r in ins if r.get("overlap") is not None and r["overlap"] <= DISTANT]
    wrongful = lambda rs: _share(sum(r["outcome"] != "answered" for r in rs), len(rs))   # noqa: E731
    m: dict[str, Any] = {
        "format_valid": _share(sum(r["outcome"] != "invalid" for r in results), len(results)),
        "wrongful_refusal": wrongful(ins),
        "wrongful_refusal_distant": wrongful(distant),
        "oos_refusal": _share(sum(r["outcome"] == "refused" for r in oos), len(oos)),
        "near_miss_refusal": _share(sum(r["outcome"] == "refused" for r in near), len(near)),
        "correct_source": _share(sum(bool(set(r["cited"]) & set(r["gold"])) for r in ins_answered), len(ins_answered)),
    }
    if verdicts is None:
        m["faithful"], m["useful"] = None, None
    else:
        judged = [verdicts.get(r["id"]) for r in answered]
        m["faithful"] = 1.0 if not answered else _share(sum(bool(v and v.faithful) for v in judged), len(answered))
        m["useful"] = _share(sum(r["outcome"] == "answered" and bool(verdicts.get(r["id"]) and verdicts[r["id"]].useful) for r in ins), len(ins))
    m.update(n=len(results), n_in_scope=len(ins), answered=len(answered))
    return {k: m[k] for k in METRIC_KEYS + ["n", "n_in_scope", "answered"]}


def by_language(results: Sequence[Mapping[str, Any]]) -> dict[str, list[Mapping[str, Any]]]:
    out: dict[str, list[Mapping[str, Any]]] = {}
    for r in results:
        out.setdefault(r["lang"], []).append(r)
    return dict(sorted(out.items()))


def percentile(values: Sequence[float], p: float) -> float:
    if not values:
        return 0.0
    s = sorted(values)
    return s[max(0, -(-int(p * len(s)) // 100) - 1)] if p < 100 else s[-1]


def latency_summary(results: Sequence[Mapping[str, Any]]) -> dict[str, float]:
    total = [r["latency_ms"] for r in results if r["source"] == "model"]
    first = [r["ttft_ms"] for r in results if r["source"] == "model" and r["ttft_ms"] is not None]
    return {"p50_ms": round(percentile(total, 50), 1), "p95_ms": round(percentile(total, 95), 1), "ttft_p95_ms": round(percentile(first, 95), 1), "n": len(total)}
