"""The judge of models.md 4.4: a larger local model through an OpenAI-compatible endpoint, temperature 0, schema-checked JSON.

- Only ``answered`` items are sent (refusals are scored automatically by the caller).
- An invalid output is retried once, then the item counts as not faithful and not useful.
- Results are cached by SHA-256 of (judge prompt version, item, answer): an unchanged answer is never judged twice.
- ``estimate`` prints the number of calls, tokens and duration before a run, with an ``estimate_id`` the owner approves.

The client is any ``TeacherClient`` (``HttpTeacherClient`` against the local host for a real run; a fake in tests).
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

from .hashing import canonical_sha256
from .jsonio import atomic_write_text, dumps_pretty
from .estimate import rates_of, seconds_for
from .teacher import TeacherClient
from .templates import Template

JUDGE_PARAMS = {"temperature": 0.0, "max_tokens": 400}


def _norm(name: str) -> str:
    return re.sub(r"[^a-z0-9]", "", name.rsplit("/", 1)[-1].lower())


def same_model(judge_model: str | None, evaluated: Sequence[str | None]) -> str | None:
    """The first of ``evaluated`` that names the judge's model (case, organisation prefix, punctuation and file-name suffixes ignored;
    one name containing the other counts), or ``None``. A judge must differ from the model it judges (models.md 4.4)."""
    j = _norm(judge_model or "")
    if not j:
        return None
    for name in evaluated:
        n = _norm(name or "")
        if n and (n in j or j in n):
            return name
    return None


@dataclass
class Verdict:
    faithful: bool
    useful: bool
    unsupported: list[str]
    valid: bool = True
    cached: bool = False


def parse_verdict(text: str) -> Verdict | None:
    """Extract and check ``{"faithful": bool, "useful": bool, "unsupported": [str]}``; ``None`` when invalid."""
    m = re.search(r"\{.*\}", text, re.S)
    if not m:
        return None
    try:
        data = json.loads(m.group(0))
    except json.JSONDecodeError:
        return None
    if not isinstance(data, dict) or set(data) - {"faithful", "useful", "unsupported"}:
        return None
    if not isinstance(data.get("faithful"), bool) or not isinstance(data.get("useful"), bool):
        return None
    uns = data.get("unsupported", [])
    if not isinstance(uns, list) or not all(isinstance(x, str) for x in uns):
        return None
    return Verdict(data["faithful"], data["useful"], uns)


def render_sections(context: Sequence[Mapping[str, str]]) -> str:
    return "\n\n".join(f"[{i}] {s['title']}\n{s['text']}" for i, s in enumerate(context, 1))


class Judge:
    def __init__(self, client: TeacherClient, model: str, template: Template, prompt_version: int, cache_dir: Path,
                 rates: Mapping[str, Any] | None = None, params: Mapping[str, Any] | None = None):
        self.client, self.model, self.template, self.prompt_version = client, model, template, prompt_version
        self.cache_dir, self.rates = Path(cache_dir), rates_of(rates or {})
        self.params = dict(params or JUDGE_PARAMS)
        self.calls = 0

    def key(self, item: Mapping[str, Any], answer: str) -> str:
        return canonical_sha256({"judge_prompt_version": self.prompt_version, "model": self.model, "template": self.template.sha256,
                                 "item": {k: item[k] for k in ("id", "question", "context", "gold", "reference")}, "answer": answer})

    def _path(self, h: str) -> Path:
        return self.cache_dir / h[:2] / f"{h}.json"

    def is_cached(self, item: Mapping[str, Any], answer: str) -> bool:
        return self._path(self.key(item, answer)).exists()

    def judge(self, item: Mapping[str, Any], answer: str) -> Verdict:
        h = self.key(item, answer)
        p = self._path(h)
        if p.exists():
            try:
                d = json.loads(p.read_text(encoding="utf-8"))
                return Verdict(d["faithful"], d["useful"], d.get("unsupported", []), d.get("valid", True), True)
            except (json.JSONDecodeError, KeyError):
                pass
        system, user = self.template.render({"sections": render_sections(item["context"]), "question": item["question"],
                                             "gold": ", ".join(f"[{g}]" for g in item["gold"]) or "none",
                                             "reference": item.get("reference") or "(none)", "answer": answer})
        verdict: Verdict | None = None
        for attempt in range(2):
            comp = self.client.complete(model=self.model, system=system, user=user, params=self.params, seed=0)
            self.calls += 1
            verdict = parse_verdict(comp.text)
            if verdict:
                break
        if verdict is None:
            verdict = Verdict(False, False, [], valid=False)
        atomic_write_text(p, dumps_pretty({"faithful": verdict.faithful, "useful": verdict.useful, "unsupported": verdict.unsupported,
                                           "valid": verdict.valid, "judge_prompt_version": self.prompt_version, "model": self.model}))
        return verdict


def estimate(items_with_answers: Sequence[tuple[Mapping[str, Any], str]], judge: Judge, template_chars: int | None = None) -> dict[str, Any]:
    """Calls still to make (cache misses), tokens (characters / 3.5 in, 60 out), duration at the judge rates and ``estimate_id``. The id depends on the number of
    items, not on the cache, so it stays the same while a run is in progress and after a resume."""
    todo = [(it, a) for it, a in items_with_answers if not judge.is_cached(it, a)]
    overhead = template_chars if template_chars is not None else len(judge.template.text)
    chars = sum(overhead + len(render_sections(it["context"])) + len(it["question"]) + len(it.get("reference") or "") + len(a) for it, a in todo)
    tin, tout = round(chars / 3.5), 60 * len(todo)
    seconds = seconds_for(tin, tout, judge.rates)
    return {"calls": len(todo), "cached": len(items_with_answers) - len(todo), "input_tokens": tin, "output_tokens": tout,
            "seconds": round(seconds, 1),
            "id": canonical_sha256({"model": judge.model, "prompt_version": judge.prompt_version, "items": len(items_with_answers), "rates": judge.rates})[:12]}


def agreement(judge: Judge, labelled: Sequence[Mapping[str, Any]]) -> dict[str, float]:
    """Share of human-labelled items on which the judge agrees, per metric (4.4 calibration)."""
    if not labelled:
        return {"faithful": 0.0, "useful": 0.0}
    ok = {"faithful": 0, "useful": 0}
    for it in labelled:
        v = judge.judge(it, it["answer"])
        ok["faithful"] += v.faithful == it["faithful"]
        ok["useful"] += v.useful == it["useful"]
    return {k: round(v / len(labelled), 4) for k, v in ok.items()}
