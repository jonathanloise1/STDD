"""``python -m training.assistant.generate``: synthetic dataset generation (models.md 5, 2.2).

    --estimate                       request counts, tokens, duration and the estimate_id; calls nothing, writes nothing
    --measure [--concurrency N]      speed of the local host: a few short requests; prints tokens_per_second for dataset.json
    --dry-run                        renders every request, prints three per stage; calls nothing, writes nothing
    --splits test,validation|train   real run (long, occupies the host [COST]); needs --approved-estimate {estimate_id}
    --approved-estimate {id}         must equal the current estimate_id

A real run resumes by running the same command again: a job with a cached response is never sent again.
"""
from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path
from typing import Any

from training.assistant import protocol as P
from training.assistant.domain import write_tree
from training.assistant.jobs import STAGES, Pipeline, load_templates
from training.assistant.walk import cache_walk, placeholder_walk
from training.common import config as C
from training.common.estimate import (ApprovalError, estimate_id, estimate_stages, format_duration, format_measurement, format_table, measure_throughput, rates_of, require_approval)
from training.common.hashing import sha256_file
from training.common.jsonio import load_json
from training.common.runs import RunRecorder
from training.common.teacher import HttpTeacherClient, JobCache, JobRunner, TeacherError

ASSISTANT = Path(__file__).resolve().parent
ALL_SPLITS = ["train", "validation", "test"]


def default_out() -> Path:
    return C.out_dir("assistant")


def load_all(config_dir: Path = ASSISTANT / "config") -> tuple[dict[str, Any], list[dict[str, Any]], dict[str, Any]]:
    cfg = load_json(config_dir / "dataset.json")
    domains = load_json(config_dir / "domains.json")
    retrieval = load_json(ASSISTANT / "retrieval.json")
    return cfg, domains, retrieval


def check_config(cfg: dict[str, Any], domains: list[dict[str, Any]]) -> None:
    counts = {s: sum(1 for d in domains if d["split"] == s) for s in ALL_SPLITS}
    if counts != cfg["splits"]:
        raise SystemExit(f"domains.json splits {counts} differ from dataset.json {cfg['splits']}")
    if abs(sum(cfg["classes"].values()) - 1.0) > 1e-9:
        raise SystemExit("class shares do not sum to 1")


def build_estimate(cfg: dict[str, Any], domains: list[dict[str, Any]], retrieval: dict[str, Any], splits: list[str] | None = None) -> dict[str, Any]:
    """Plan every job (offline teacher stands in for upstream outputs). ``estimate_id`` covers all splits."""
    pipe = Pipeline(cfg, domains, load_templates(ASSISTANT / "teacher"), retrieval)
    by_stage = placeholder_walk(pipe)
    pipe_for_render = pipe

    def rows(sel: set[str] | None):
        for stage in STAGES:
            for job in by_stage[stage]:
                if sel and next(d for d in domains if d["id"] == job["domain"])["split"] not in sel:
                    continue
                system, user = pipe_for_render.render(job)
                yield (stage, job["model"], len(system) + len(user), job["params"]["max_tokens"])

    all_stages = estimate_stages(rows(None), cfg["teacher"])
    shown = estimate_stages(rows(set(splits)), cfg["teacher"]) if splits and set(splits) != set(ALL_SPLITS) else all_stages
    return {"id": estimate_id(cfg, all_stages, rates_of(cfg["teacher"])), "stages": shown, "all_stages": all_stages, "by_stage": by_stage, "pipe": pipe}


def parse_splits(value: str | None) -> list[str]:
    if not value:
        return list(ALL_SPLITS)
    parts = [p.strip() for p in value.split(",") if p.strip()]
    bad = [p for p in parts if p not in ALL_SPLITS]
    if bad:
        raise SystemExit(f"unknown split: {', '.join(bad)} (use train, validation, test)")
    return parts


def cmd_estimate(cfg, domains, retrieval, splits, out=print) -> int:
    est = build_estimate(cfg, domains, retrieval, splits)
    out(f"Estimate for splits: {', '.join(splits)}\n")
    out(format_table(est["stages"]))
    if not rates_of(cfg["teacher"])["tokens_per_second"]:
        out("\nWarning: teacher.tokens_per_second is not set in dataset.json, so no duration can be estimated. "
            "Start the host and run `python -m training.assistant.generate --measure`, then put the values into dataset.json.")
    else:
        out("\nThe duration is an estimate from the configured speed of the host; the output length is assumed to be 60% of max_tokens.")
    out(f"\nestimate_id: {est['id']}")
    out("Approve it with the owner, then: python -m training.assistant.generate --splits test,validation --approved-estimate " + est["id"])
    return 0


def cmd_measure(cfg, concurrency: int | None, client=None, out=print) -> int:
    """Quick speed test of the configured host: ``concurrency + 1`` short requests, nothing is written."""
    t = cfg["teacher"]
    if not t.get("model") or t["model"].startswith("{"):
        out("refused: dataset.json teacher.model is not set")
        return 2
    client = client or HttpTeacherClient(t.get("base_url"), t.get("api_key_env"))
    try:
        m = measure_throughput(client, t["model"], concurrency or t.get("max_concurrency", 8))
    except TeacherError as exc:
        out(f"failed: {exc}")
        return 1
    out(format_measurement(m))
    return 0


def cmd_dry_run(cfg, domains, retrieval, out=print) -> int:
    est = build_estimate(cfg, domains, retrieval)
    pipe = est["pipe"]
    for stage in STAGES:
        jobs = est["by_stage"][stage]
        out(f"=== stage {stage}: {len(jobs)} jobs ===")
        rendered = [pipe.render(j) for j in jobs]       # every request is rendered
        picks = sorted({0, len(jobs) // 2, len(jobs) - 1}) if jobs else []
        for i in picks:
            j = jobs[i]
            out(f"--- {j['key']}  template={j['template']}  model={j['model']}  params={j['params']}  seed={j['seed']}")
            system, user = rendered[i]
            if system:
                out("[system]\n" + _clip(system))
            out("[user]\n" + _clip(user))
    out(f"\nDry run complete: {sum(len(v) for v in est['by_stage'].values())} requests rendered, nothing sent, nothing written.")
    return 0


def _clip(text: str, limit: int = 1800) -> str:
    return text if len(text) <= limit else text[:limit] + f"\n[... {len(text) - limit} more characters]"


def check_real_run_config(cfg: dict[str, Any]) -> None:
    t = cfg["teacher"]
    for key in ("model", "verifier_model"):
        if not t.get(key) or t[key].startswith("{"):
            raise SystemExit(f"dataset.json teacher.{key} is not set")
    if not rates_of(t)["tokens_per_second"]:
        raise SystemExit("dataset.json teacher.tokens_per_second must be set (run --measure against the running host) before a real run")


def cmd_run(cfg, domains, retrieval, splits, approved, out_dir: Path, client_factory=None, out=print, config_dir: Path = ASSISTANT / "config") -> int:
    est = build_estimate(cfg, domains, retrieval)
    try:
        require_approval(est["id"], approved)
    except ApprovalError as exc:
        out(f"refused: {exc}")
        return 2
    check_real_run_config(cfg)
    t = cfg["teacher"]
    cache = JobCache(out_dir / "teacher-cache")
    pipe = Pipeline(cfg, domains, load_templates(ASSISTANT / "teacher"), retrieval, set(splits))
    client = client_factory(pipe) if client_factory else HttpTeacherClient(t.get("base_url"), t.get("api_key_env"))
    max_hours = t.get("max_hours")
    runner = JobRunner(cache, client, pipe.render, max_hours * 3600 if max_hours else None, t.get("max_concurrency", 8),
                       on_progress=lambda m: out(f"done {m}"))
    hashes = {"dataset": sha256_file(config_dir / "dataset.json"), "domains": sha256_file(config_dir / "domains.json"),
              "protocol": P.protocol_sha256(), "retrieval": sha256_file(ASSISTANT / "retrieval.json")}
    code = 0
    with RunRecorder("assistant", "generate", hashes, model_out=out_dir) as rec:
        for stage in STAGES:
            jobs = pipe.jobs(stage)
            res = runner.run(jobs)
            texts = {j["key"]: (cache.get(j) or {}).get("response") for j in jobs}
            pipe.ingest(stage, jobs, texts)
            out(f"stage {stage}: {len(jobs)} jobs, {res.cached} cached, {res.sent} sent, {len(res.failed)} failed, took {format_duration(res.elapsed_seconds)}")
            rec.extra.setdefault("stages", {})[stage] = {"jobs": len(jobs), "cached": res.cached, "sent": res.sent, "failed": len(res.failed)}
            for key, why in res.failed[:10]:
                out(f"  failed {key}: {why}")
            if stage == "oos":                       # the trees are complete once the out-of-scope questions exist
                for st in pipe.alive():
                    write_tree(st.domain, out_dir / "domains" / st.id)
            if not res.complete:
                out("stopped: " + ("time limit (max_hours) reached; run the same command again to resume" if res.stopped_for_time else "some jobs failed; run the same command again to retry"))
                code = 1
                break
        rec.extra["splits"] = splits
        rec.extra["rejections"] = len(pipe.rejections)
    return code


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.assistant.generate", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--estimate", action="store_true", help="count requests, tokens and duration; print the estimate_id")
    mode.add_argument("--measure", action="store_true", help="measure the speed of the local host with a few short requests")
    mode.add_argument("--dry-run", action="store_true", help="render every request and print three samples per stage")
    ap.add_argument("--splits", help="comma separated: test,validation first, then train (default for --estimate: all)")
    ap.add_argument("--approved-estimate", metavar="ID", help="the estimate_id the owner approved [COST]")
    ap.add_argument("--concurrency", type=int, default=None, help="with --measure: parallel requests (default teacher.max_concurrency)")
    ap.add_argument("--out", type=Path, default=None, help="output folder (default training/out/assistant)")
    ap.add_argument("--config-dir", type=Path, default=ASSISTANT / "config", help="folder of dataset.json and domains.json")
    args = ap.parse_args(argv)
    cfg, domains, retrieval = load_all(args.config_dir)
    check_config(cfg, domains)
    if args.estimate:
        return cmd_estimate(cfg, domains, retrieval, parse_splits(args.splits))
    if args.measure:
        return cmd_measure(cfg, args.concurrency)
    if args.dry_run:
        return cmd_dry_run(cfg, domains, retrieval)
    if not args.splits:
        ap.error("a real run needs --splits (test,validation first, then train); use --estimate or --dry-run to preview")
    return cmd_run(cfg, domains, retrieval, parse_splits(args.splits), args.approved_estimate, args.out or default_out(), config_dir=args.config_dir)


if __name__ == "__main__":
    raise SystemExit(main())
