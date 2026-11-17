"""``python -m training.assistant.eval``: the evaluation harness of models.md section 4.

    eval --estimate                         judge calls, tokens and duration of a full evaluation; no model run, no call
    eval --measure [--concurrency N]        speed of the judge host (a few short requests); prints the values for eval.json
    eval --freeze-sets                      write the SHA-256 of the test and validation sets into config/eval.json
    eval --candidates   --approved-estimate ID    measure every untrained candidate (4.1)             [COST]
    eval --export merged|onnx|gguf --run RUN_ID --approved-estimate ID    the platform evaluation (4.2, 4.5, 4.6)   [COST]
    eval --suite project --items bench.jsonl --export gguf --approved-estimate ID   the project bench answers (manual.md 8.3)

The backend that produces answers (transformers, onnxruntime, a llama.cpp server) is behind ``AnswerBackend``; the judge
is behind ``TeacherClient``. Both are injected by tests. Without ``--approved-estimate`` no long run starts.
"""
from __future__ import annotations

import argparse
import copy
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from training.assistant import metrics as M, protocol as P
from training.common import evalcore as E, judge as J
from training.common.backends import AnswerBackend, make_backend
from training.common.config import REPO_ROOT, TRAINING_DIR, out_dir
from training.common.estimate import ApprovalError, format_duration, format_measurement, measure_throughput, require_approval
from training.common.hashing import sha256_file
from training.common.jsonio import dumps_pretty, load_json, read_jsonl, write_json
from training.common.runs import RunRecorder
from training.common.teacher import HttpTeacherClient, JobCache, TeacherError
from training.common.templates import Template
from training.common import train_core as T

ASSISTANT = Path(__file__).resolve().parent
LANGS = ("en", "it")


class EvalError(RuntimeError):
    pass


@dataclass
class Env:
    """Where things are and what is injected. Tests pass fakes; a real run leaves the factories ``None``."""
    training_dir: Path = TRAINING_DIR
    repo_root: Path = REPO_ROOT
    backend_factory: Callable[..., AnswerBackend] | None = None
    client_factory: Callable[[Mapping[str, Any]], Any] | None = None
    log: Callable[[str], None] = print

    @property
    def config_dir(self) -> Path:
        return self.training_dir / "assistant" / "config"

    @property
    def out(self) -> Path:
        return out_dir("assistant", self.training_dir)

    @property
    def protocol_path(self) -> Path:
        return self.training_dir / "assistant" / "protocol.json"


# ---------------------------------------------------------------- configuration

def load_eval_config(env: Env) -> dict[str, Any]:
    cfg = load_json(env.config_dir / "eval.json")
    for key in ("model_thresholds", "transfer_gap", "export_loss", "sample_app", "latency", "judge"):
        if key not in cfg:
            raise EvalError(f"eval.json lacks {key}")
    return cfg


def freeze_sets(env: Env) -> dict[str, str]:
    """Write the SHA-256 of ``test.jsonl`` and ``validation.jsonl`` into ``eval.json`` (models.md 4.2, 9.1 step 5)."""
    path = env.config_dir / "eval.json"
    cfg = load_json(path)
    ds = env.out / "dataset"
    cfg["test_set_sha256"] = sha256_file(ds / "test.jsonl")
    cfg["validation_set_sha256"] = sha256_file(ds / "validation.jsonl")
    path.write_text(dumps_pretty(cfg), encoding="utf-8")
    return {"test_set_sha256": cfg["test_set_sha256"], "validation_set_sha256": cfg["validation_set_sha256"]}


def check_frozen(env: Env, cfg: Mapping[str, Any]) -> str:
    """The test set must equal the frozen hash; returns it. Raises ``EvalError`` otherwise."""
    want = cfg.get("test_set_sha256")
    if not want:
        raise EvalError("the test set is not frozen: run `python -m training.assistant.eval --freeze-sets` after validate")
    have = sha256_file(env.out / "dataset" / "test.jsonl")
    if have != want:
        raise EvalError("test.jsonl differs from test_set_sha256 in eval.json: changing the set bumps the suite version (models.md 4.2)")
    return want


def build_judge(env: Env, cfg: Mapping[str, Any], real: bool) -> J.Judge:
    jc = cfg["judge"]
    if real:
        if not jc.get("model"):
            raise EvalError("eval.json judge.model is not set")
    client = env.client_factory(jc) if env.client_factory else HttpTeacherClient(jc.get("base_url"), jc.get("api_key_env"))
    tpl = Template.load(ASSISTANT / "eval", jc["template"])
    return J.Judge(client, jc.get("model") or "judge", tpl, jc["prompt_version"], env.out / "judge-cache", jc)


def check_judge_differs(judge: J.Judge | None, *names: str | None) -> None:
    clash = J.same_model(judge.model, names) if judge is not None else None
    if clash:
        raise EvalError(f"the judge model {judge.model!r} is the model under evaluation ({clash}): the judge must differ from the model it judges (models.md 4.4)")


def load_labels(env: Env, cfg: Mapping[str, Any]) -> list[dict[str, Any]]:
    p = env.repo_root / cfg["judge"]["human_labels"]
    return read_jsonl(p) if p.exists() else []


# ---------------------------------------------------------------- items

def load_items(env: Env, split: str) -> list[dict[str, Any]]:
    p = env.out / "dataset" / f"{split}.jsonl"
    if not p.exists():
        raise EvalError(f"{p} does not exist: run validate first")
    return read_jsonl(p)


def runtime_items(items: Sequence[Mapping[str, Any]], rc: Mapping[str, Mapping[str, Any]]) -> list[dict[str, Any]]:
    """Items with the run-time context; those whose gold section is not in it no longer count as in scope (class ``nohit``)."""
    out = []
    for it in items:
        r = rc.get(it["id"])
        if r is None:
            continue
        ctx = r["context"]
        ids = [s["id"] for s in ctx]
        gold = [ids.index(g) + 1 for g in r["gold_ids"] if g in ids]
        cls = it["class"]
        if cls.startswith("pos_") and not gold:
            cls = "nohit"
        out.append({**it, "context": ctx, "gold": gold, "class": cls})
    return out


def bench_items(rows: Sequence[Mapping[str, Any]], packs: Mapping[str, Mapping[str, Any]]) -> list[dict[str, Any]]:
    """Items of the project bench export (manual.md 8.4) with the section texts of the built packs."""
    sections = {lang: {s["id"]: s for s in p["sections"]} for lang, p in packs.items()}
    out = []
    for r in rows:
        lang = r["lang"]
        if lang not in packs:
            raise EvalError(f"no built pack for language {lang}: run `npm run help:build` first")
        decided = r.get("in_scope_decision", True)
        ctx = [sections[lang][i] for i in r.get("context", []) if i in sections[lang]] if decided else None
        context = None if ctx is None else [{"id": s["id"], "title": s["title"], "text": s["text"]} for s in ctx]
        gold = [i + 1 for i, s in enumerate(ctx or []) if r.get("story") and s.get("story") == r["story"]]
        if r["kind"] == "out_of_scope":
            cls = "ref_oos"
        else:
            cls = "bench_pos" if gold else "nohit"
        out.append({"id": r["id"], "lang": lang, "class": cls, "domain": "sample-app", "app_name": packs[lang]["project"]["name"], "question": r["question"],
                    "context": context, "gold": gold, "overlap": None, "target": None})
    return out


def load_packs(pack_dir: Path) -> dict[str, dict[str, Any]]:
    packs = {}
    for lang in LANGS:
        p = pack_dir / lang / "pack.json"
        if p.exists():
            packs[lang] = load_json(p)
    if not packs:
        raise EvalError(f"no pack.json under {pack_dir}: build the packs with `npm run help:build` (manual.md 6)")
    return packs


# ---------------------------------------------------------------- the evaluation

@dataclass
class Options:
    export: str
    suite: str = "all"                        # all | platform | project
    judge_on: bool = True
    run_id: str | None = None
    model_version: str | None = None
    bench_rows: list[dict[str, Any]] | None = None
    pack_dir: Path | None = None
    latency_tier: str | None = None
    end_to_end: bool = True
    candidate: Mapping[str, Any] | None = None
    untrained: bool = False                   # an untrained candidate: nothing was trained, so there is no seen-domain set and no transfer gap (4.1)

    @property
    def never_trained(self) -> bool:
        return self.untrained or self.candidate is not None


def _verdicts(judge: J.Judge | None, pairs: Sequence[tuple[dict[str, Any], str]]) -> dict[str, J.Verdict] | None:
    if judge is None:
        return None
    return {it["id"]: judge.judge(it, ans) for it, ans in pairs}


def _lang_metrics(results: Sequence[Mapping[str, Any]], verdicts: Mapping[str, J.Verdict] | None) -> dict[str, dict[str, Any]]:
    return {lang: M.compute_metrics(rs, verdicts) for lang, rs in M.by_language(results).items()}


def evaluate(backend: AnswerBackend, env: Env, cfg: Mapping[str, Any], opts: Options, judge: J.Judge | None,
             merged_report: Mapping[str, Any] | None = None) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Run the suites of 4.2 through ``backend`` and build the report of 4.6 with its failures. Returns ``(report, items)``."""
    protocol = P.load_protocol(env.protocol_path)
    failures: list[dict[str, Any]] = []
    items_out: list[dict[str, Any]] = []
    results: dict[str, Any] = {}
    log = env.log
    judge_items: dict[str, list[tuple[dict[str, Any], str]]] = {}
    sets: dict[str, tuple[list[dict[str, Any]], list[dict[str, Any]]]] = {}      # suite -> (items, results)

    def run(name: str, items: list[dict[str, Any]], max_new: int | None = None) -> list[dict[str, Any]]:
        log(f"{name}: {len(items)} items")
        res = M.run_items(backend, protocol, items, max_new_tokens=max_new, progress=log)
        sets[name] = (items, res)
        by_id = {i["id"]: i for i in items}
        judge_items[name] = M.judge_items(res, by_id, {i["id"]: i["context"] or [] for i in items})
        return res

    test_sha = None
    if opts.suite in ("all", "platform"):
        test_sha = check_frozen(env, cfg)
        test = load_items(env, "test")
        run("platform_test", test)
        if not opts.never_trained:
            run("seen_domains", load_items(env, "seen"))
        if opts.end_to_end:
            try:
                from training.assistant.runtime_context import runtime_contexts
                rc = runtime_contexts(env.out, env.config_dir)
                run("platform_end_to_end", runtime_items(test, rc))
            except Exception as exc:  # noqa: BLE001 - reported, not gated (4.2)
                log(f"end-to-end suite skipped: {type(exc).__name__}: {exc}")
                results["platform_end_to_end"] = None
    if opts.suite in ("all", "project"):
        if opts.bench_rows is not None:
            run("sample_app", bench_items(opts.bench_rows, load_packs(opts.pack_dir or env.repo_root / "src" / "frontend" / "public" / "help")))
        else:
            results["sample_app"] = None
            failures.append({"where": "sample_app", "metric": "evaluated", "value": None, "threshold": "pass --items (the project bench export, manual.md 8.4)"})

    agreement = None
    if judge is not None:
        jcfg = cfg["judge"]
        labels = load_labels(env, cfg)
        if labels:
            agreement = J.agreement(judge, labels)
            for k, v in agreement.items():
                if v < jcfg["min_human_agreement"]:
                    failures.append({"where": "judge", "metric": f"human_agreement.{k}", "value": v, "threshold": f">= {jcfg['min_human_agreement']}"})
        else:
            failures.append({"where": "judge", "metric": "human_agreement", "value": None,
                             "threshold": f"calibration labels missing: {jcfg['human_labels']} (models.md 4.4)"})
    verdicts = {name: _verdicts(judge, pairs) for name, pairs in judge_items.items()}

    for name, (items, res) in sets.items():
        results[name] = _lang_metrics(res, verdicts.get(name))
        v = verdicts.get(name) or {}
        for r in res:
            row = {"set": name, **{k: r[k] for k in ("id", "lang", "outcome", "cited", "gold", "latency_ms", "ttft_ms", "source")}, "answer": r["answer"]}
            row["faithful"], row["useful"] = (v[r["id"]].faithful, v[r["id"]].useful) if r["id"] in v else (None, None)
            items_out.append(row)

    # thresholds of 4.5, platform test, oracle context, each language
    if "platform_test" in results:
        for lang in LANGS:
            m = results["platform_test"].get(lang)
            if m is None:
                failures.append({"where": f"platform_test/{lang}", "metric": "items", "value": 0, "threshold": "at least one item"})
                continue
            failures += E.check_rules(m, cfg["model_thresholds"], f"platform_test/{lang}")
            if opts.never_trained:
                continue                      # an untrained model has nothing to transfer: the gap is not measured and does not gate (4.1)
            seen = results["seen_domains"].get(lang, {})
            gap = None if seen.get("useful") is None or m.get("useful") is None else round(seen["useful"] - m["useful"], 4)
            results.setdefault("transfer_gap", {})[lang] = gap
            failures += E.check_rules({"transfer_gap": gap}, {"transfer_gap": cfg["transfer_gap"]}, f"transfer_gap/{lang}")
        if opts.never_trained:
            results["transfer_gap"] = None
        else:
            results["seen_domains"] = {lang: {"useful": m.get("useful"), "n": m.get("n")} for lang, m in results["seen_domains"].items()}
        if opts.export != "merged":
            failures += _export_loss(results["platform_test"], merged_report, cfg["export_loss"], opts.export)
    if results.get("sample_app"):
        sa = cfg["sample_app"]
        for lang, m in results["sample_app"].items():
            gated = m["n_in_scope"] >= sa["min_in_scope_items_to_gate"]
            m["gated"] = gated
            if gated:
                failures += E.check_rules(m, sa["gates"], f"sample_app/{lang}")

    if opts.suite in ("all", "platform"):
        results["latency"] = _latency(backend, protocol, cfg["latency"], sets["platform_test"][0], opts.latency_tier)

    run_dir_name = opts.run_id
    report: dict[str, Any] = {
        "schema_version": 1, "model": "assistant", "model_version": opts.model_version, "export": opts.export,
        "suite_version": cfg["suite_version"], "thresholds_sha256": E.thresholds_sha256("assistant", env.training_dir),
        "test_set_sha256": test_sha,
        "judge": {"model": judge.model if judge else None, "prompt_version": cfg["judge"]["prompt_version"], "human_agreement": agreement},
        "hardware": backend.info(), "results": results, "failures": failures, "passed": not failures,
    }
    if opts.candidate:
        report["candidate"] = {k: opts.candidate[k] for k in ("id", "hub_id", "revision", "parameters")}
    if opts.never_trained:
        report["untrained"] = True
    if opts.suite != "all" and not opts.candidate:
        report["partial"] = {"suite": opts.suite}          # `lock --record` refuses it: the platform test and the project bench are one gate (4.2)
    if run_dir_name:
        report["run_id"] = run_dir_name
    return report, items_out


def _export_loss(test: Mapping[str, Any], merged: Mapping[str, Any] | None, rule: Mapping[str, Any], export: str) -> list[dict[str, Any]]:
    if merged is None:
        return [{"where": f"export_loss/{export}", "metric": "merged report", "value": None, "threshold": "eval-merged.json of the same run is needed"}]
    fails = []
    for lang in LANGS:
        base, now = merged["results"]["platform_test"].get(lang, {}), test.get(lang, {})
        for metric, key in (("useful", "useful_drop_max"), ("faithful", "faithful_drop_max")):
            b, n = base.get(metric), now.get(metric)
            drop = None if b is None or n is None else round(b - n, 4)
            if drop is None or drop > rule[key]:
                fails.append({"where": f"export_loss/{export}/{lang}", "metric": f"{metric}_drop", "value": drop, "threshold": f"<= {rule[key]}"})
    return fails


def _latency(backend: AnswerBackend, protocol: Mapping[str, Any], lc: Mapping[str, Any], test: Sequence[Mapping[str, Any]], tier: str | None) -> dict[str, Any]:
    ins = [i for i in test if i["class"].startswith("pos_")]
    by_lang = {lang: [i for i in ins if i["lang"] == lang] for lang in LANGS}
    chosen: list[dict[str, Any]] = []
    for k in range(lc["items"]):
        pool = by_lang[LANGS[k % 2]] or ins
        if pool:
            chosen.append(pool[(k // 2) % len(pool)])
    res = M.run_items(backend, protocol, chosen, max_new_tokens=lc["max_new_tokens"])
    out: dict[str, Any] = M.latency_summary(res)
    target = lc["targets"].get(tier) if tier else None
    out["tier"] = tier
    out["target_p95_ms"] = target["p95_ms"] if target else None
    out["meets_target"] = None if target is None else out["p95_ms"] <= target["p95_ms"]
    out["note"] = "a tier that misses its target does not block the release; it is disabled in the default profiles (models.md 4.5)"
    return out


# ---------------------------------------------------------------- estimate (the approval of a [COST] run)

def judge_estimate(env: Env, cfg: Mapping[str, Any], judge: J.Judge, runs: int = 1, untrained: bool = False) -> dict[str, Any]:
    """Upper bound without running any model: every item of every suite is assumed answered with its reference answer.
    The id depends on the config and counts only, so it is stable while a run is in progress."""
    pairs = []
    for split in ("test",) if untrained else ("test", "seen"):          # an untrained model has no seen-domain set (4.1)
        pairs += [(_judge_item(i), i["target"]) for i in load_items(env, split)]
    pairs += pairs[: len(load_items(env, "test"))]                      # end-to-end: the test items again
    labels = load_labels(env, cfg)
    pairs += [(_judge_item(l), l["answer"]) for l in labels]
    est = J.estimate(pairs, judge)
    est["calls"] *= runs
    est["input_tokens"] *= runs
    est["output_tokens"] *= runs
    est["seconds"] = round(est["seconds"] * runs, 1)
    est["id"] = J.canonical_sha256({"base": est["id"], "runs": runs, "sample_app": "excluded"})[:12]
    est["note"] = "upper bound: the project bench items are not counted; items already judged are free (cached)"
    return est


def _judge_item(i: Mapping[str, Any]) -> dict[str, Any]:
    return {"id": i["id"], "question": i["question"], "context": i["context"], "gold": i.get("gold", []), "reference": i.get("reference") or i.get("target")}


def _require(est: Mapping[str, Any], approved: str | None) -> None:
    try:
        require_approval(est["id"], approved)
    except ApprovalError as exc:
        raise EvalError(str(exc)) from exc


# ---------------------------------------------------------------- run folders and reports

def _run_info(env: Env, run_id: str) -> tuple[Path, dict[str, Any]]:
    run_dir = env.out / "runs" / run_id
    rj = run_dir / "run.json"
    if not rj.exists():
        raise EvalError(f"{rj} does not exist: train and export first (or pass --run)")
    return run_dir, load_json(rj)


def _backend(env: Env, export: str, run_dir: Path, run: Mapping[str, Any], device: str, candidate: Mapping[str, Any] | None = None) -> AnswerBackend:
    kwargs = (candidate or run["base_model"]).get("chat_template_kwargs", {})
    if env.backend_factory:
        return env.backend_factory(export=export, run_dir=run_dir, kwargs=kwargs, candidate=candidate)
    manifest = load_json(run_dir / "exports.json") if (run_dir / "exports.json").exists() else None
    tools = load_json(env.config_dir / "export.json").get("llama_cpp", {})
    return make_backend(export, run_dir, kwargs=kwargs, manifest=manifest, model="assistant", version=run.get("model_version", ""),
                        base=candidate, tools=tools, device=device)


def run_export_eval(env: Env, args: argparse.Namespace) -> int:
    cfg = load_eval_config(env)
    real = env.backend_factory is None
    use_judge = not args.no_judge
    run_id = args.run or E.latest_run("assistant", model_out=env.out)
    if run_id is None:
        raise EvalError("no run found under training/out/assistant/runs: pass --run")
    run_dir, run = _run_info(env, run_id)
    untrained = run.get("status") == "untrained-candidate"
    judge = build_judge(env, cfg, real) if use_judge else None
    check_judge_differs(judge, run["base_model"].get("hub_id"))
    if judge is not None:
        est = judge_estimate(env, cfg, judge, untrained=untrained) if args.suite != "project" else _project_estimate(env, cfg, judge, args)
        env.log(f"judge estimate: {est['calls']} calls, {est['input_tokens']} input and {est['output_tokens']} output tokens, about {format_duration(est['seconds'])}, id {est['id']}")
        _require(est, args.approved_estimate)
    bench = read_jsonl(args.items) if args.items else None
    merged_report = load_json(run_dir / "eval-merged.json") if args.export != "merged" and (run_dir / "eval-merged.json").exists() else None
    hashes = {"eval": E.thresholds_sha256("assistant", env.training_dir), "protocol": P.protocol_sha256(env.protocol_path)}
    opts = Options(args.export, args.suite, use_judge, run_id, run["model_version"], bench, args.pack_dir, args.latency_tier, not args.no_end_to_end, None, untrained)
    backend = _backend(env, args.export, run_dir, run, args.device)
    try:
        with RunRecorder("assistant", f"eval --export {args.export}", hashes, run_id, root=env.training_dir, model_out=env.out) as rec:
            report, items = evaluate(backend, env, cfg, opts, judge, merged_report)
            name = args.export if args.suite == "all" else f"{args.suite}-{args.export}"      # a partial run never replaces eval-{export}.json (lock --record reads that)
            path = E.write_report(run_dir, name, report, items)
            rec.extra.update(report=path.name, passed=report["passed"], judge_calls=judge.calls if judge else 0)
    finally:
        backend.close()
    env.log(f"report: {path}")
    if report["failures"]:
        env.log("thresholds missed (models.md section 10; read items-" + name + ".jsonl first):\n" + E.format_failures(report["failures"]))
    env.log("PASSED" if report["passed"] else "FAILED")
    return 0 if report["passed"] else 1


def _project_estimate(env: Env, cfg: Mapping[str, Any], judge: J.Judge, args: argparse.Namespace) -> dict[str, Any]:
    rows = read_jsonl(args.items) if args.items else []
    pairs = [({"id": r["id"], "question": r["question"], "context": [{"title": "t", "text": "x" * 300}] * 3, "gold": [1], "reference": None}, "x" * 200) for r in rows]
    labels = load_labels(env, cfg)
    pairs += [(_judge_item(l), l["answer"]) for l in labels]
    est = J.estimate(pairs, judge)
    return est


def run_candidates(env: Env, args: argparse.Namespace) -> int:
    cfg = load_eval_config(env)
    real = env.backend_factory is None
    cands = T.load_candidates(env.config_dir / "candidates.json")
    wanted = [c for c in cands if not args.only or c["id"] == args.only]
    if not wanted:
        raise EvalError("no candidate selected")
    usable = []
    for c in wanted:
        problems = T.check_candidate(c, T.MAX_PARAMETERS, real_run=real, allow_unverified=args.allow_unverified)
        if problems and real:
            env.log("skipped: " + "; ".join(problems))
            continue
        usable.append(c)
    if not usable:
        raise EvalError("no usable candidate: pin revisions and verify entries in candidates.json (or --allow-unverified for a rehearsal)")
    judge = build_judge(env, cfg, real) if not args.no_judge else None
    if judge is not None:
        est = judge_estimate(env, cfg, judge, runs=len(usable), untrained=True)
        env.log(f"judge estimate for {len(usable)} candidate(s): {est['calls']} calls, about {format_duration(est['seconds'])}, id {est['id']}")
        _require(est, args.approved_estimate)
    check_frozen(env, cfg)
    cdir = env.out / "candidates"
    results = []
    for c in sorted(usable, key=lambda x: (x["parameters"], x["id"])):
        env.log(f"candidate {c['id']} ({c['hub_id']})")
        check_judge_differs(judge, c["id"], c["hub_id"])
        backend = (env.backend_factory(export="merged", run_dir=cdir, kwargs=c.get("chat_template_kwargs", {}), candidate=c) if env.backend_factory
                   else make_backend("merged", cdir, kwargs=c.get("chat_template_kwargs", {}), manifest=None, model="assistant", version="", base=c, device=args.device))
        try:
            opts = Options("merged", "platform", judge is not None, None, f"candidate:{c['id']}", None, None, None, not args.no_end_to_end, c)
            report, items = evaluate(backend, env, cfg, opts, judge)
        finally:
            backend.close()
        path = E.write_report(cdir, c["id"], report, items)
        (path.parent / f"eval-{c['id']}.json").replace(path.parent / f"{c['id']}.json")
        dest = env.repo_root / "models" / "assistant" / "eval" / "candidates"
        dest.mkdir(parents=True, exist_ok=True)
        write_json(dest / f"{c['id']}.json", report)
        results.append((c, report))
        env.log(f"  {c['id']}: {'PASSED' if report['passed'] else 'FAILED'} ({len(report['failures'])} failures)")
    passing = [(c, r) for c, r in results if r["passed"]]
    winner = sorted(passing, key=lambda cr: (cr[0].get("kind") == "fallback", cr[0]["parameters"], -(cr[1]["results"]["platform_test"]["en"].get("useful") or 0)))[0][0] if passing else None
    write_json(cdir / "summary.json", {"winner": winner["id"] if winner else None, "candidates": {c["id"]: {"passed": r["passed"], "failures": len(r["failures"])} for c, r in results}})
    if winner:
        env.log(f"smallest passing candidate: {winner['id']}: export it without training (`export --candidate {winner['id']}`, status untrained-candidate; models.md 4.1)")
    else:
        env.log("no candidate passes: train (models.md 6.1 rule 2)")
    return 0


# ---------------------------------------------------------------- CLI

def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog="python -m training.assistant.eval", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--candidates", action="store_true", help="measure the untrained candidates of candidates.json (4.1) [COST]")
    mode.add_argument("--export", choices=list(E.EXPORTS), help="evaluate the merged weights or one export of a run (6.6) [COST]")
    mode.add_argument("--estimate", action="store_true", help="print the judge estimate and its id; runs no model and calls nothing")
    mode.add_argument("--measure", action="store_true", help="measure the speed of the judge host with a few short requests; prints the values for eval.json")
    mode.add_argument("--freeze-sets", action="store_true", help="write the SHA-256 of the test and validation sets into config/eval.json")
    ap.add_argument("--suite", choices=["all", "platform", "project"], default="all", help="project: only the project bench (manual.md 8.3)")
    ap.add_argument("--items", type=Path, help="project bench export (npm run help:bench -- --export)")
    ap.add_argument("--pack-dir", type=Path, help="folder with {lang}/pack.json (default src/frontend/public/help)")
    ap.add_argument("--run", help="run id (default: the latest run)")
    ap.add_argument("--approved-estimate", metavar="ID", help="the estimate id the owner approved [COST]")
    ap.add_argument("--concurrency", type=int, default=4, help="with --measure: parallel requests (the value the judge will use)")
    ap.add_argument("--only", help="with --candidates: one candidate id")
    ap.add_argument("--allow-unverified", action="store_true", help="with --candidates: accept entries that are not verified or pinned (rehearsals only)")
    ap.add_argument("--device", choices=["auto", "cpu", "cuda", "mps"], default="auto")
    ap.add_argument("--latency-tier", choices=["browser-webgpu", "server-gpu", "server-cpu"], help="compare the p95 with the tier's target")
    ap.add_argument("--no-judge", action="store_true", help="automatic metrics only; faithful and useful are not measured and the report cannot pass")
    ap.add_argument("--no-end-to-end", action="store_true", help="skip the run-time-context suite")
    return ap


def main(argv: list[str] | None = None, env: Env | None = None) -> int:
    args = build_parser().parse_args(argv)
    env = env or Env()
    if args.suite == "project" and not (args.export or args.candidates or args.estimate or args.freeze_sets or args.measure):
        args.export = "gguf"                      # `eval --suite project --items {jsonl}` of models.md 2.2: the shipped server tier (models.md 12)
    try:
        if args.freeze_sets:
            for k, v in freeze_sets(env).items():
                env.log(f"{k}: {v}")
            return 0
        if args.measure:
            jc = load_eval_config(env)["judge"]
            if not jc.get("model"):
                raise EvalError("eval.json judge.model is not set")
            client = env.client_factory(jc) if env.client_factory else HttpTeacherClient(jc.get("base_url"), jc.get("api_key_env"))
            try:
                env.log(format_measurement(measure_throughput(client, jc["model"], args.concurrency)))
            except TeacherError as exc:
                raise EvalError(f"measurement failed: {exc}") from exc
            return 0
        if args.estimate:
            cfg = load_eval_config(env)
            untrained = False
            if args.run:                                                     # the estimate of an untrained candidate's run has no seen-domain items
                untrained = _run_info(env, args.run)[1].get("status") == "untrained-candidate"
            est = judge_estimate(env, cfg, build_judge(env, cfg, False), untrained=untrained)
            env.log(dumps_pretty(est))
            return 0
        if args.candidates:
            return run_candidates(env, args)
        if args.export:
            return run_export_eval(env, args)
    except (EvalError, T.TrainError, FileNotFoundError) as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2
    build_parser().print_usage()
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
