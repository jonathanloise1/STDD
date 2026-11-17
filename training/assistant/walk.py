"""Walk the stages of a pipeline against the teacher cache or the offline teacher."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Callable

from training.assistant.fake_teacher import FakeTeacher
from training.assistant.jobs import STAGES, Pipeline
from training.common.teacher import JobCache


@dataclass
class StageUsage:
    jobs: int = 0
    found: int = 0
    input_tokens: int = 0
    output_tokens: int = 0
    seconds: float = 0.0


def placeholder_walk(pipe: Pipeline, on_stage: Callable[[str, list[dict[str, Any]]], None] | None = None) -> dict[str, list[dict[str, Any]]]:
    """Derive every job of every stage, standing in for upstream outputs with the offline teacher. No cache, no network."""
    fake = FakeTeacher(pipe)
    out: dict[str, list[dict[str, Any]]] = {}
    for stage in STAGES:
        jobs = pipe.jobs(stage)
        out[stage] = jobs
        if on_stage:
            on_stage(stage, jobs)
        pipe.ingest(stage, jobs, {j["key"]: fake.respond(j) for j in jobs})
    return out


def cache_walk(pipe: Pipeline, cache: JobCache, stop_when_incomplete: bool = False,
               on_stage: Callable[[str, list[dict[str, Any]], StageUsage], None] | None = None) -> dict[str, StageUsage]:
    """Replay the stages from the teacher cache; jobs without a cached response are treated as missing."""
    usage: dict[str, StageUsage] = {}
    for stage in STAGES:
        jobs = pipe.jobs(stage)
        u = StageUsage(jobs=len(jobs))
        texts: dict[str, str | None] = {}
        for job in jobs:
            entry = cache.get(job)
            texts[job["key"]] = entry["response"] if entry else None
            if entry:
                u.found += 1
                u.input_tokens += entry["usage"]["input_tokens"]
                u.output_tokens += entry["usage"]["output_tokens"]
                u.seconds += entry.get("seconds", 0.0)
        pipe.ingest(stage, jobs, texts)
        usage[stage] = u
        if on_stage:
            on_stage(stage, jobs, u)
        if stop_when_incomplete and u.found < u.jobs:
            break
    return usage
