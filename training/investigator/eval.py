"""``python -m training.investigator.eval``: evaluation of the investigator (operations.md 11.4, 11.5).

    eval --freeze-sets                                  pin the held-out scenarios by SHA-256 in config/eval.json
    eval --suite synthetic-holdout|sample-app|all --tier small|large [--export gguf|merged] [--run ID]
    eval --candidates --tier small                      the untrained baseline under the same grammar and suites (11.5)
    eval --candidates --tier large [--gguf FILE]        a candidate with a pre-made 4-bit GGUF (``gguf`` block in candidates.json) is served by llama.cpp from
                                                        that file, its hash checked; any other candidate must fit the device in BF16 or is refused

Metrics are computed deterministically from the reports, no judge. Each scenario is run as a closed loop (``host.py``): the model
chooses tools, the monitor's executor (``dotnet run --project src/support/MyApp.Monitor -- tool ...``) answers over the scenario's world,
the stop rules apply. Writes ``artifacts/eval/investigator/{suite}-{tier}.json`` (operations.md 11.4) and, for a run, the report
``training/out/investigator/runs/{run_id}/eval-{export}.json`` that ``lock --record`` reads. Nothing here calls a cloud service.

The gating runtime is the llama.cpp server (``--export gguf``), which constrains decoding with the grammar of 11.3. The merged weights run
under transformers without constrained decoding: their report is informative and cannot stand in for the grammar.
"""
from __future__ import annotations

import argparse
import statistics
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from training.common import evalcore as E, hardware as HW, train_core as T, weights as WT
from training.common.backends import AnswerBackend, LlamaServerBackend, RoleMappedBackend, make_backend
from training.common.config import REPO_ROOT, TRAINING_DIR, out_dir
from training.common.hashing import canonical_sha256, sha256_bytes, sha256_file
from training.common.jsonio import load_json, write_json
from training.common.runs import RunRecorder
from training.investigator import host as H, protocol as P, tools as TL
from training.investigator.masking import Masker  # noqa: F401  (re-exported for tests)

SUITES = ("synthetic-holdout", "sample-app")


class EvalError(RuntimeError):
    pass


@dataclass
class Env:
    training_dir: Path = TRAINING_DIR
    repo_root: Path = REPO_ROOT
    backend_factory: Callable[..., AnswerBackend] | None = None
    runner: TL.ToolRunner | None = None
    clock: Callable[[], float] | None = None
    log: Callable[[str], None] = print
    counter: Any = None
    hardware: HW.HardwareInfo | None = None               # injected by tests; a real run detects it
    environ: Mapping[str, str] | None = None

    @property
    def inv(self) -> Path:
        return self.training_dir / "investigator"

    @property
    def out(self) -> Path:
        return out_dir("investigator", self.training_dir)

    def next_tag(self) -> str:
        """A tag unique to one evaluation in this process: the executor keeps state per incident, so two evaluations never share an incident id."""
        if self.counter is None:
            import itertools
            self.counter = itertools.count(1)
        return f"eval{next(self.counter)}"

    def tool_runner(self) -> TL.ToolRunner:
        return self.runner or TL.DotnetToolRunner(self.repo_root, self.out / "tool-cache")


# ---------------------------------------------------------------- scenarios

def scenario_files(env: Env, suite: str) -> list[Path]:
    if suite == "synthetic-holdout":
        d = env.out / "scenarios"
        return sorted(p for p in d.glob("*.json") if load_json(p)["split"] == "holdout") if d.exists() else []
    d = env.repo_root / "docs" / "support" / "bench" / "investigator"
    return sorted(d.glob("*.json")) if d.exists() else []


def holdout_hash(files: Sequence[Path]) -> str:
    return sha256_bytes("\n".join(f"{p.name} {sha256_file(p)}" for p in files).encode("utf-8"))


def freeze_sets(env: Env) -> str:
    files = scenario_files(env, "synthetic-holdout")
    if not files:
        raise EvalError("no held-out scenarios: run `generate --split holdout` first")
    path = env.inv / "config" / "eval.json"
    cfg = load_json(path)
    cfg["holdout_set_sha256"] = holdout_hash(files)
    write_json(path, cfg)
    return cfg["holdout_set_sha256"]


# ---------------------------------------------------------------- metrics (11.4)

def _stopped_by_budget(scn: Mapping[str, Any], inv: H.Investigation) -> bool:
    """Stopped by the call budget (11.4): the budget reached 0 and the report abstains with ``budget_exhausted``, unless that is the gold
    answer (a scenario built so that the budget is too small): then the stop is the expected outcome, not a miss."""
    rep = inv.report
    return bool(inv.budget_exhausted and rep and rep["abstain_reason"] == "budget_exhausted" and scn["gold"]["abstain_reason"] != "budget_exhausted")


def investigation_row(scn: Mapping[str, Any], inv: H.Investigation) -> dict[str, Any]:
    return {"id": scn["id"], "domain": scn["domain"], "gold": scn["gold"], "status": inv.status, "report": inv.report, "calls": inv.calls, "valid_first_time": inv.report_valid_first_time,
            "budget_exhausted": _stopped_by_budget(scn, inv), "duration_s": round(inv.duration_s, 3), "error": inv.error}


def compute_metrics(rows: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    n = len(rows)
    answerable = [r for r in rows if r["gold"]["verdict"] == "explained"]
    abstain = [r for r in rows if r["gold"]["verdict"] == "abstain"]
    with_story = [r for r in rows if r["gold"]["user_story"] is not None]
    explained = [r for r in rows if r["report"] and r["report"]["verdict"] == "explained"]
    share = lambda a, b: round(a / b, 4) if b else None   # noqa: E731

    def correct(r: Mapping[str, Any]) -> bool:
        rep, g = r["report"], r["gold"]
        return bool(rep and rep["verdict"] == "explained" and rep["cause_category"] == g["cause_category"] and rep["area"] == g["area"])

    def grounded(r: Mapping[str, Any]) -> bool:
        want = {(s["tool"], canonical_sha256(s["arguments"])) for s in r["gold"]["supporting_evidence"]}
        ids = {c["evidence_id"] for c in r["calls"] if c["status"] in ("ok", "empty") and (c["tool"], canonical_sha256(c["arguments"])) in want}
        return bool(ids & set(r["report"]["evidence_refs"]))

    calls = [c for r in rows for c in r["calls"]]
    extra = [len(r["calls"]) - r["gold"]["min_calls"] for r in rows]
    m = {
        "report_valid": share(sum(r["valid_first_time"] for r in rows), n),
        "cause_correct": share(sum(correct(r) for r in answerable), len(answerable)),
        "story_correct": share(sum(bool(r["report"]) and r["report"]["user_story"] == r["gold"]["user_story"] for r in with_story), len(with_story)),
        "grounded": 1.0 if not explained else share(sum(grounded(r) for r in explained), len(explained)),
        "abstain_correct": share(sum(bool(r["report"]) and r["report"]["verdict"] == "abstain" for r in abstain), len(abstain)),
        "wrongful_abstain": share(sum(bool(r["report"]) and r["report"]["verdict"] == "abstain" for r in answerable), len(answerable)),
        "denied_calls": share(sum(c["status"] == "denied" for c in calls), len(calls)),
        "budget_exhausted": share(sum(r["budget_exhausted"] for r in rows), n),
        "extra_calls": statistics.median(extra) if extra else None,
        "latency_p95": round(sorted(r["duration_s"] for r in rows)[max(0, -(-95 * n // 100) - 1)], 3) if n else None,
        "n": n, "n_answerable": len(answerable), "n_abstain": len(abstain), "failed": sum(r["status"] == "failed" for r in rows)}
    return m


def run_suite(backend: AnswerBackend, env: Env, suite: str, tier: str, files: Sequence[Path], limit: int | None, context_tokens: int | None, tag: str = "") -> tuple[dict[str, Any], list[dict[str, Any]]]:
    inv_dir = env.inv
    protocol = P.load_protocol(inv_dir / "protocol.json")
    system = P.system_text(protocol, env.repo_root)
    catalog = load_json(env.repo_root / "docs" / "support" / "tools.json")
    constraints = [{"id": c["id"], "text": c["text"]} for c in load_json(env.repo_root / "docs" / "support" / "constraints.json")["constraints"]]
    schema = P.investigation_schema(env.repo_root, protocol)
    thresholds = load_json(inv_dir / "thresholds.json")
    runner = env.tool_runner()
    rows = []
    for k, f in enumerate(files[:limit] if limit else files, 1):
        scn = load_json(f)
        budget = scn.get("budget_override") or protocol["budget"][tier]
        res = H.investigate(backend, scn, world_dir=(f.parent / scn["world_dir"]).resolve(), runner=runner, protocol=protocol, system=system, catalog=catalog,
                            constraints=constraints, schema=schema, budget=budget, timeout_s=load_json(inv_dir / "config" / "eval.json")["timeout_seconds"],
                            context_tokens=context_tokens, incident_id=f"{scn['incident_id']}:{tag}:{suite}", **({"clock": env.clock} if env.clock else {}))
        rows.append(investigation_row(scn, res))
        if k % 25 == 0:
            env.log(f"{suite}: {k}/{len(files)}")
    metrics = compute_metrics(rows)
    failures = E.check_rules(metrics, thresholds["gated"], suite)
    return {"metrics": metrics, "failures": failures, "passed": not failures}, rows


# ---------------------------------------------------------------- command

def _tier_context(env: Env, tier: str) -> int:
    return load_json(env.inv / "config" / "export.json")["tiers"][tier]["context_length"]


def _backend(env: Env, export: str, run_dir: Path, run: Mapping[str, Any], tier: str, device: str, candidate: Mapping[str, Any] | None = None) -> AnswerBackend:
    kwargs = (candidate or run["base_model"]).get("chat_template_kwargs", {})
    if env.backend_factory:
        return env.backend_factory(export=export, run_dir=run_dir, kwargs=kwargs, candidate=candidate, tier=tier)
    manifest = load_json(run_dir / "exports.json") if (run_dir / "exports.json").exists() else None
    tools = load_json(env.inv / "config" / "export.json").get("llama_cpp", {})
    return make_backend(export, run_dir, kwargs=kwargs, manifest=manifest, model="investigator", version=run.get("model_version", ""), base=candidate, tools=tools,
                        device=device, ctx_size=_tier_context(env, tier), tool_role=(candidate or run["base_model"]).get("tool_role"))


BF16_OVERHEAD = 1.15            # activations, the KV cache and the framework on top of 2 bytes per parameter (ESTIMATE)


def check_fits_bf16(c: Mapping[str, Any], info: HW.HardwareInfo, device: str = "auto") -> None:
    """``EvalError`` when the candidate's BF16 weights do not fit the device ``eval --candidates`` would load them on, pointing to the pre-made GGUF route."""
    need = c["parameters"] * 2 * BF16_OVERHEAD / 2**30
    if device == "auto":
        device = info.device
    have = info.vram_gb if device == "cuda" and info.vram_gb is not None else info.ram_gb
    if need > have:
        raise EvalError(f"{c['id']} needs about {need:.0f} GB in BF16 ({c['parameters']:,} parameters) and the {'GPU' if device == 'cuda' else 'machine'} has {have} GB: "
                        "it cannot be evaluated through `eval --candidates` here. Evaluate it from a 4-bit GGUF: add a `gguf` block (repo, file, path, quantization, sha256) to "
                        "its entry in candidates.json, download the file into the weights folder (MYAPP_MODELS_DIR, default ~/myapp-models), record its hash "
                        "(`python -m training.common.weights FILE`) and run `eval --candidates` again (models.md 9.4)")


def _candidate_backend(env: Env, c: Mapping[str, Any], tier: str, device: str, gguf_override: str | None, cdir: Path) -> tuple[AnswerBackend, str]:
    """``(backend, export)`` of an untrained candidate: llama.cpp on its pre-made GGUF when the entry has one, else the BF16 weights through transformers."""
    block = c.get("gguf")
    kwargs = c.get("chat_template_kwargs", {})
    if env.backend_factory:
        return env.backend_factory(export="gguf" if block else "merged", run_dir=cdir, kwargs=kwargs, candidate=c, tier=tier), ("gguf" if block else "merged")
    if block:
        path = WT.weights_path(block, gguf_override, env.environ)
        WT.check_weights(block, path, c["id"])
        tools = load_json(env.inv / "config" / "export.json").get("llama_cpp", {})
        inner = LlamaServerBackend(path, tools.get("server_bin", "llama-server"), kwargs, _tier_context(env, tier), 0 if device == "cpu" else 999)
        return (RoleMappedBackend(inner) if c.get("tool_role") == "user" else inner), "gguf"
    check_fits_bf16(c, env.hardware or HW.detect(), device)
    return make_backend("merged", cdir, kwargs=kwargs, manifest=None, model="investigator", version="", base=c, device=device, tool_role=c.get("tool_role")), "merged"


def partial_marker(suite: str, limit: int | None) -> dict[str, Any] | None:
    """``{"suite": ..., "limit": ...}`` for a run that is not the whole evaluation (``--suite`` or ``--limit``), else ``None``. Such a report is
    written beside the full one, never over it, and ``lock --record`` refuses it."""
    marker = {k: v for k, v in (("suite", suite if suite != "all" else None), ("limit", limit)) if v}
    return marker or None


def evaluate(backend: AnswerBackend, env: Env, suites: Sequence[str], tier: str, export: str, run_id: str | None, model_version: str | None,
             limit: int | None = None, candidate: Mapping[str, Any] | None = None, partial: Mapping[str, Any] | None = None) -> tuple[dict[str, Any], dict[str, list[dict[str, Any]]]]:
    cfg = load_json(env.inv / "config" / "eval.json")
    results: dict[str, Any] = {}
    all_rows: dict[str, list[dict[str, Any]]] = {}
    failures: list[dict[str, Any]] = []
    tag = env.next_tag()
    for suite in suites:
        files = scenario_files(env, suite)
        if not files:
            results[suite] = None
            failures.append({"where": suite, "metric": "evaluated", "value": None, "threshold": f"no scenarios found for {suite}"})
            continue
        if suite == "synthetic-holdout":
            want = cfg.get("holdout_set_sha256")
            if not want:
                raise EvalError("the held-out scenarios are not frozen: run `python -m training.investigator.eval --freeze-sets`")
            if holdout_hash(files) != want:
                raise EvalError("the held-out scenarios differ from holdout_set_sha256 in config/eval.json (a change bumps the suite version)")
        env.log(f"{suite}: {len(files)} scenarios, tier {tier}")
        res, rows = run_suite(backend, env, suite, tier, files, limit, _tier_context(env, tier), tag)
        results[suite] = res
        all_rows[suite] = rows
        failures += res["failures"]
    report = {"schema_version": 1, "model": "investigator", "model_version": model_version, "export": export, "suite_version": cfg["suite_version"], "tier": tier,
              "thresholds_sha256": sha256_file(env.inv / "config" / "eval.json"), "test_set_sha256": cfg.get("holdout_set_sha256"),
              "judge": None, "hardware": backend.info(), "results": results, "failures": failures, "passed": not failures}
    if run_id:
        report["run_id"] = run_id
    if partial:
        report["partial"] = dict(partial)
    if candidate:
        report["candidate"] = {k: candidate[k] for k in ("id", "hub_id", "revision", "parameters")}
        if candidate.get("gguf"):
            report["candidate"]["gguf"] = {k: candidate["gguf"].get(k) for k in ("repo", "file", "quantization", "sha256")}
    return report, all_rows


def write_outputs(env: Env, report: Mapping[str, Any], rows: Mapping[str, Sequence[Mapping[str, Any]]], artifacts_dir: Path, run_dir: Path | None, export: str, tier: str) -> list[Path]:
    paths = []
    limited = bool((report.get("partial") or {}).get("limit"))                  # a suite cut short never replaces the full result of that suite
    for suite, res in report["results"].items():
        if res is None:
            continue
        p = artifacts_dir / f"{suite}-{tier}{'-partial' if limited else ''}.json"
        write_json(p, {"suite": suite, "tier": tier, "model_version": report["model_version"], "export": export, **res})
        paths.append(p)
    if run_dir is not None:
        items = [{"suite": s, **r} for s, rs in rows.items() for r in rs]
        rep = dict(report)
        paths.append(E.write_report(run_dir, f"{export}-partial" if report.get("partial") else export, rep, items))
    return paths


def main(argv: list[str] | None = None, env: Env | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.investigator.eval", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--candidates", action="store_true", help="measure the untrained candidates of the tier (11.5)")
    mode.add_argument("--export", choices=["merged", "gguf"], help="evaluate the merged weights or the GGUF export of a run")
    mode.add_argument("--freeze-sets", action="store_true", help="write the SHA-256 of the held-out scenarios into config/eval.json")
    ap.add_argument("--suite", choices=[*SUITES, "all"], default="all")
    ap.add_argument("--tier", choices=["small", "large"], help="default: the tier recorded in the run")
    ap.add_argument("--run", help="run id (default: the latest run)")
    ap.add_argument("--only", help="with --candidates: one candidate id")
    ap.add_argument("--limit", type=int, help="evaluate only the first N scenarios of each suite (checks, not results)")
    ap.add_argument("--gguf", metavar="FILE", help="with --candidates --only ID: the path of that candidate's pre-made GGUF, when it is not in the weights folder; its hash is checked")
    ap.add_argument("--artifacts-dir", type=Path, help="where {suite}-{tier}.json is written (default artifacts/eval/investigator)")
    ap.add_argument("--device", choices=["auto", "cpu", "cuda", "mps"], default="auto")
    ap.add_argument("--approved-estimate", help="accepted and ignored: the investigator's evaluation has no judge calls")
    ap.add_argument("--allow-unverified", action="store_true", help="with --candidates: accept entries that are not pinned and verified (rehearsals only)")
    args = ap.parse_args(argv)
    env = env or Env()
    suites = list(SUITES) if args.suite == "all" else [args.suite]
    art = args.artifacts_dir or env.repo_root / "artifacts" / "eval" / "investigator"
    real = env.backend_factory is None
    try:
        if args.freeze_sets:
            env.log(f"holdout_set_sha256: {freeze_sets(env)}")
            return 0
        if args.candidates:
            if not args.tier:
                ap.error("--candidates needs --tier")
            cands = T.load_candidates(env.inv / "config" / "candidates.json")
            pool = [c for c in cands if c.get("tier") == args.tier and (not args.only or c["id"] == args.only)]
            usable = [c for c in pool if not T.check_candidate(c, None, real_run=real, allow_unverified=args.allow_unverified) or not real]
            if not usable:
                raise EvalError("no usable candidate: pin revisions and verify entries in candidates.json")
            cdir = env.out / "candidates"
            winners = []
            for c in sorted(usable, key=lambda x: (x["parameters"], x["id"])):
                env.log(f"candidate {c['id']} ({c['hub_id']})")
                backend, cexport = _candidate_backend(env, c, args.tier, args.device, args.gguf if args.only == c["id"] else None, cdir)
                try:
                    report, rows = evaluate(backend, env, suites, args.tier, cexport, None, f"candidate:{c['id']}", args.limit, c, partial_marker(args.suite, args.limit))
                finally:
                    backend.close()
                write_outputs(env, report, rows, art / "candidates" / c["id"], None, cexport, args.tier)
                dest = env.repo_root / "models" / "investigator" / "eval" / "candidates"
                dest.mkdir(parents=True, exist_ok=True)
                write_json(dest / (f"{c['id']}-partial.json" if report.get("partial") else f"{c['id']}.json"), report)
                env.log(f"  {c['id']}: {'PASSED' if report['passed'] else 'FAILED'} ({len(report['failures'])} failures)")
                if report["passed"] and not report.get("partial"):
                    winners.append(c["id"])
            if args.suite != "all" or args.limit:
                env.log("a partial run (--suite or --limit) decides nothing: run the whole baseline before choosing between training and a candidate")
            env.log(f"smallest passing candidate: {winners[0]}" if winners else "no candidate passes: train (operations.md 11.5)")
            return 0
        if not args.export:
            ap.print_usage()
            return 2
        run_id = args.run or E.latest_run("investigator", model_out=env.out)
        if run_id is None:
            raise EvalError("no run found: pass --run")
        run_dir = env.out / "runs" / run_id
        run = load_json(run_dir / "run.json")
        tier = args.tier or run.get("tier")
        if tier is None:
            raise EvalError("the run records no tier: pass --tier")
        backend = _backend(env, args.export, run_dir, run, tier, args.device)
        try:
            with RunRecorder("investigator", f"eval --export {args.export}", {"eval": sha256_file(env.inv / "config" / "eval.json")}, run_id, root=env.training_dir, model_out=env.out) as rec:
                report, rows = evaluate(backend, env, suites, tier, args.export, run_id, run.get("model_version"), args.limit, None, partial_marker(args.suite, args.limit))
                paths = write_outputs(env, report, rows, art, run_dir, args.export, tier)
                rec.extra.update(passed=report["passed"], tier=tier)
        finally:
            backend.close()
        for p in paths:
            env.log(f"report: {p}")
        if report.get("partial"):
            env.log(f"partial run ({report['partial']}): written as eval-{args.export}-partial.json; eval-{args.export}.json is untouched and `lock --record` refuses this one")
        if report["failures"]:
            env.log("thresholds missed (read items-" + args.export + ("-partial" if report.get("partial") else "") + ".jsonl first):\n" + E.format_failures(report["failures"]))
        env.log("PASSED" if report["passed"] else "FAILED")
        return 0 if report["passed"] else 1
    except (EvalError, T.TrainError, WT.WeightsError, FileNotFoundError, TL.ToolRunnerError) as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
