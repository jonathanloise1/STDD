"""Token and time estimate, approval, and the throughput measurement of the local host (models.md 5.6).

Nothing here costs money: the teacher and the judge run on a local host. A long run still needs the owner's approval
because it occupies the machine for hours; the estimate says how many tokens and how long.
"""
from __future__ import annotations

import time
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from typing import Any, Callable, Iterable, Mapping

from .hashing import canonical_sha256

CHARS_PER_TOKEN = 3.5
OUTPUT_FRACTION = 0.6
RATE_KEYS = ("tokens_per_second", "prompt_tokens_per_second")


class ApprovalError(RuntimeError):
    pass


@dataclass
class StageEstimate:
    stage: str
    requests: int
    input_tokens: int
    output_tokens: int
    seconds: float


def input_tokens_for(chars: int) -> float:
    return chars / CHARS_PER_TOKEN


def output_tokens_for(max_tokens: int) -> float:
    return OUTPUT_FRACTION * max_tokens


def rates_of(cfg: Mapping[str, Any]) -> dict[str, float]:
    """The configured speed of the host: generated tokens per second and prompt tokens per second, both for the whole
    host with the configured concurrency (what ``--measure`` reports). A missing or non-positive value is 0 (unknown)."""
    out = {}
    for k in RATE_KEYS:
        try:
            out[k] = max(0.0, float(cfg.get(k) or 0.0))
        except (TypeError, ValueError):
            out[k] = 0.0
    return out


def seconds_for(input_tokens: float, output_tokens: float, rates: Mapping[str, float]) -> float:
    """Duration at the given rates; 0 when the generation rate is unknown (the caller warns)."""
    gen, prompt = rates.get("tokens_per_second", 0.0), rates.get("prompt_tokens_per_second", 0.0)
    if gen <= 0:
        return 0.0
    return output_tokens / gen + (input_tokens / prompt if prompt > 0 else 0.0)


def estimate_stages(rendered: Iterable[tuple[str, str, int, int]], teacher_cfg: Mapping[str, Any]) -> list[StageEstimate]:
    """``rendered`` rows are ``(stage, model, prompt_chars, max_tokens)``; stages keep first-seen order."""
    rates = rates_of(teacher_cfg)
    order: list[str] = []
    acc: dict[str, list[float]] = {}
    for stage, _model, chars, max_tokens in rendered:
        if stage not in acc:
            order.append(stage)
            acc[stage] = [0, 0.0, 0.0]
        a = acc[stage]
        a[0] += 1
        a[1] += input_tokens_for(chars)
        a[2] += output_tokens_for(max_tokens)
    return [StageEstimate(s, int(acc[s][0]), round(acc[s][1]), round(acc[s][2]), round(seconds_for(acc[s][1], acc[s][2], rates), 1)) for s in order]


def estimate_id(config: dict[str, Any], stages: list[StageEstimate], rates: dict[str, Any]) -> str:
    """SHA-256 of (config, job counts, rates), truncated to 12 characters."""
    counts = {s.stage: s.requests for s in stages}
    return canonical_sha256({"config": config, "job_counts": counts, "rates": rates})[0:12]


def format_duration(seconds: float) -> str:
    s = int(round(seconds))
    h, rem = divmod(s, 3600)
    m, sec = divmod(rem, 60)
    return f"{h}h {m:02d}m" if h else (f"{m}m {sec:02d}s" if m else f"{sec}s")


def format_table(stages: list[StageEstimate]) -> str:
    rows = [("stage", "requests", "input tokens", "output tokens", "duration")]
    for s in stages:
        rows.append((s.stage, f"{s.requests:,}", f"{s.input_tokens:,}", f"{s.output_tokens:,}", format_duration(s.seconds)))
    rows.append(("total", f"{sum(s.requests for s in stages):,}", f"{sum(s.input_tokens for s in stages):,}",
                 f"{sum(s.output_tokens for s in stages):,}", format_duration(sum(s.seconds for s in stages))))
    widths = [max(len(r[i]) for r in rows) for i in range(5)]
    lines = []
    for n, r in enumerate(rows):
        lines.append("  ".join(c.ljust(widths[i]) if i == 0 else c.rjust(widths[i]) for i, c in enumerate(r)))
        if n == 0 or n == len(rows) - 2:
            lines.append("  ".join("-" * w for w in widths))
    return "\n".join(lines)


def require_approval(current_id: str, approved_id: str | None) -> None:
    """A real run needs ``--approved-estimate`` equal to the current estimate id."""
    if not approved_id:
        raise ApprovalError(f"this run occupies the local model host for a long time [COST]: run --estimate, have the owner approve it, then pass --approved-estimate {current_id}")
    if approved_id != current_id:
        raise ApprovalError(f"approved estimate {approved_id} does not match the current estimate {current_id}: the configuration, job counts or rates changed and need a new approval")


MEASURE_PROMPT = ("Write a numbered list of the first twenty prime numbers, then describe in about two hundred words "
                  "how a sieve finds them. Keep going until you are stopped.")
PREFILL_FILLER = "The quick brown fox jumps over the lazy dog. " * 60


def measure_throughput(client: Any, model: str, concurrency: int = 4, max_tokens: int = 128,
                       clock: Callable[[], float] = time.monotonic) -> dict[str, Any]:
    """Quick measurement against the running host: one prompt-processing probe (long prompt, one output token) and
    ``concurrency`` parallel generation probes. Returns the values to put in the config. Sends ``concurrency + 1``
    short requests and nothing else."""
    t0 = clock()
    pre = client.complete(model=model, system="", user=PREFILL_FILLER, params={"max_tokens": 1, "temperature": 0.0}, seed=0)
    pre_s = max(clock() - t0, 1e-9)

    def one(i: int):
        return client.complete(model=model, system="", user=MEASURE_PROMPT, params={"max_tokens": max_tokens, "temperature": 0.0}, seed=i)

    t1 = clock()
    with ThreadPoolExecutor(max_workers=max(1, concurrency)) as pool:
        comps = list(pool.map(one, range(max(1, concurrency))))
    gen_s = max(clock() - t1, 1e-9)
    gen_tokens = sum(c.output_tokens for c in comps)
    return {"model": model, "concurrency": max(1, concurrency),
            "tokens_per_second": round(gen_tokens / gen_s, 2) if gen_tokens else 0.0,
            "prompt_tokens_per_second": round(pre.input_tokens / pre_s, 2) if pre.input_tokens else 0.0,
            "generated_tokens": gen_tokens, "generation_seconds": round(gen_s, 2), "prompt_tokens": pre.input_tokens}


def format_measurement(m: Mapping[str, Any]) -> str:
    lines = [f"Measured on model {m['model']} with {m['concurrency']} parallel request(s):",
             f"  tokens_per_second:        {m['tokens_per_second']}   ({m['generated_tokens']} tokens in {m['generation_seconds']} s)",
             f"  prompt_tokens_per_second: {m['prompt_tokens_per_second']}   ({m['prompt_tokens']} prompt tokens)"]
    if not m["tokens_per_second"]:
        lines.append("  The host did not report token usage: set the values by hand.")
    lines.append("Put both values into the config (they describe the whole host at this concurrency), then run --estimate again.")
    return "\n".join(lines)
