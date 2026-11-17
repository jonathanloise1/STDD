"""``python -m training.investigator.generate``: the synthetic incident generator (operations.md 11.6).

    generate --dry-run                       plan and three rendered samples with the executor's test double; writes nothing
    generate --split train|holdout|all       simulate worlds, run the oracle through the monitor's real executor, write raw examples

No teacher model and no cloud service: the data are simulated worlds (``worlds/{domain}.json``, at least 24 fictional applications,
at least 4 held out whole), a scripted oracle per fault and tool results produced by the monitor's executor
(``dotnet run --project src/support/MyApp.Monitor -- tool ...``). A real run needs `dotnet` and a built monitor. Deterministic and
resumable: running the same command again continues where it stopped.
"""
from __future__ import annotations

import argparse
import shutil
import sys
import tempfile
from pathlib import Path
from typing import Any, Callable, Mapping

from training.common.config import REPO_ROOT, TRAINING_DIR, out_dir
from training.common.hashing import canonical_sha256, rng, sha256_file
from training.common.jsonio import append_jsonl, dumps_line, load_json, read_jsonl, write_json
from training.common.runs import RunRecorder
from training.investigator import protocol as P, scenarios as SC, tools as TL, world as W

INV = Path(__file__).resolve().parent


class GenerateError(RuntimeError):
    pass


def load_config(inv: Path = INV) -> dict[str, Any]:
    cfg = load_json(inv / "config.json")
    for k in ("seed", "train_examples", "holdout_examples_per_domain", "holdout_domains", "max_chars"):
        if k not in cfg:
            raise GenerateError(f"config.json lacks {k}")
    return cfg


def load_worlds(inv: Path = INV) -> dict[str, dict[str, Any]]:
    return {p.stem: load_json(p) for p in sorted((inv / "worlds").glob("*.json"))}


def check_worlds(cfg: Mapping[str, Any], specs: Mapping[str, Mapping[str, Any]]) -> list[str]:
    """Problems with the world files: at least 24 domains, at least 4 held out whole, specs agree with the config, sectors do not cross the split."""
    problems = []
    if len(specs) < 24:
        problems.append(f"{len(specs)} world specs: at least 24 are required")
    hold, val = set(cfg["holdout_domains"]), set(cfg.get("validation_domains", []))
    if len(hold) < 4:
        problems.append("at least 4 domains must be held out")
    for d in hold:
        if d not in specs:
            problems.append(f"holdout domain {d} has no world spec")
    if hold & val:
        problems.append(f"domains in both holdout and validation: {sorted(hold & val)}")
    for d in val:
        if d not in specs:
            problems.append(f"validation domain {d} has no world spec")
    for sid, s in specs.items():
        want = "holdout" if sid in hold else "validation" if sid in val else "train"
        if s["split"] != want:
            problems.append(f"{sid}: split {s['split']} disagrees with the config ({want})")
    train_sectors = {s["sector"] for sid, s in specs.items() if sid not in hold and sid not in val}
    for d in hold | val:
        if d in specs and specs[d]["sector"] in train_sectors:
            problems.append(f"{d}: sector {specs[d]['sector']} also appears in a training domain")
    return problems


def default_out(training_dir: Path = TRAINING_DIR) -> Path:
    return out_dir("investigator", training_dir)


def domains_for(cfg: Mapping[str, Any], specs: Mapping[str, Mapping[str, Any]], split: str) -> list[str]:
    hold, val = set(cfg["holdout_domains"]), set(cfg.get("validation_domains", []))
    want = {"holdout": hold, "validation": val}.get(split)
    return sorted(d for d in specs if (d in want if want is not None else d not in hold | val))


def plan_ids(cfg: Mapping[str, Any], specs: Mapping[str, Mapping[str, Any]], split: str, limit: int | None = None) -> list[tuple[str, int]]:
    """``(domain, index)`` of every scenario of a split: training round-robin over the training domains, holdout per domain."""
    doms = domains_for(cfg, specs, split)
    if split in ("holdout", "validation"):
        per = cfg[f"{split}_examples_per_domain"] if limit is None else limit
        return [(d, i) for d in doms for i in range(per)]
    n = cfg["train_examples"] if limit is None else limit
    return [(doms[i % len(doms)], i // len(doms)) for i in range(n)]


def conversation(built: SC.Built, runner: TL.ToolRunner, world_dir: Path, protocol: Mapping[str, Any], system: str, tools_catalog: Mapping[str, Any],
                 constraints: list[Mapping[str, str]], budget: int, master_seed: int) -> dict[str, Any]:
    """Run the oracle's calls through the executor and assemble the conversation of 11.3 and the gold report."""
    plan, spec = built.plan, built.world.spec
    entities = [{"name": e["name"], "description": e["description"]} for e in spec["entities"]]
    user = P.render_user(protocol, built.incident, built.pack, tools_catalog["tools"], entities, constraints, budget)
    messages = [{"role": "system", "content": system}, {"role": "user", "content": user}]
    calls_left, evidence, results, n = budget, {}, [], 0
    wanted = {(s["tool"], canonical_sha256(s["arguments"])) for s in built.gold["supporting_evidence"]}
    for st in built.steps:
        if calls_left <= 0:
            break
        n += 1
        env = runner.call(world_dir, st.tool, st.arguments, built.incident_id, n)
        calls_left -= 1
        messages.append({"role": "assistant", "content": P.render_call(protocol, st.tool, st.arguments)})
        messages.append({"role": protocol["result_role"], "content": P.render_result(protocol, env, calls_left)})
        results.append({"tool": st.tool, "arguments": st.arguments, "status": env["status"], "evidence_id": env["evidence_id"]})
        if (st.tool, canonical_sha256(st.arguments)) in wanted and env["status"] in ("ok", "empty"):
            evidence[f"{st.tool}|{canonical_sha256(st.arguments)}"] = env["evidence_id"]
    r = rng(master_seed, f"{plan.id}/report")
    report = SC.make_report(plan, built, evidence, r)
    messages.append({"role": "assistant", "content": P.render_report(protocol, report)})
    return {"messages": messages, "calls": results, "report": report, "evidence_ids": sorted(evidence.values())}


def make_example(plan: SC.Plan, spec: Mapping[str, Any], runner: TL.ToolRunner, cfg: Mapping[str, Any], protocol: Mapping[str, Any], system: str,
                 catalog: Mapping[str, Any], constraints: list[Mapping[str, str]], world_dir: Path, repo_root: Path, tier_budget: int | None = None) -> tuple[dict[str, Any], SC.Built]:
    built = SC.build_scenario(plan, spec, cfg["seed"], repo_root)
    W.write_world(built.world, world_dir, repo_root)
    budget = built.budget_override if built.budget_override is not None else (tier_budget or plan.budget_choice)
    conv = conversation(built, runner, world_dir, protocol, system, catalog, constraints, budget, cfg["seed"])
    rec = {"schema_version": 1, "id": plan.id, "split": plan.split, "domain": plan.domain, "class": plan.klass, "fault": plan.fault, "trigger": plan.trigger,
           "variant": plan.variant, "budget": budget, "messages": conv["messages"], "gold": built.gold, "calls": conv["calls"], "evidence_ids": conv["evidence_ids"]}
    return rec, built


def scenario_record(built: SC.Built, world_rel: str) -> dict[str, Any]:
    """The scenario file used by the evaluation: everything needed to run the investigation again against the world.
    ``world_rel`` is the world directory relative to the scenario file."""
    spec = built.world.spec
    return {"schema_version": 1, "id": built.plan.id, "domain": built.plan.domain, "split": built.plan.split, "fault": built.plan.fault, "trigger": built.plan.trigger,
            "class": built.plan.klass, "world_dir": world_rel, "incident_id": built.incident_id, "budget_override": built.budget_override,
            "prompt": {"incident": built.incident, "pack": built.pack, "entities": [{"name": e["name"], "description": e["description"]} for e in spec["entities"]]},
            "gold": built.gold}


def inputs_hash(cfg: Mapping[str, Any], specs: Mapping[str, Any], inv: Path, repo_root: Path, system_sha: str) -> str:
    return canonical_sha256({"config": cfg, "specs": {k: canonical_sha256(v) for k, v in specs.items()}, "protocol": sha256_file(inv / "protocol.json"),
                             "system": system_sha, "tools": sha256_file(repo_root / "docs" / "support" / "tools.json"),
                             "constraints": sha256_file(repo_root / "docs" / "support" / "constraints.json")})


def generate_split(split: str, out: Path, runner: TL.ToolRunner, *, inv: Path = INV, repo_root: Path = REPO_ROOT, limit: int | None = None,
                   log: Callable[[str], None] = print) -> dict[str, Any]:
    cfg, specs = load_config(inv), load_worlds(inv)
    problems = check_worlds(cfg, specs)
    if problems:
        raise GenerateError("; ".join(problems))
    protocol = P.load_protocol(inv / "protocol.json")
    system = P.system_text(protocol, repo_root)
    catalog = load_json(repo_root / "docs" / "support" / "tools.json")
    constraints = load_json(repo_root / "docs" / "support" / "constraints.json")["constraints"]
    constraints = [{"id": c["id"], "text": c["text"]} for c in constraints]
    ih = inputs_hash(cfg, specs, inv, repo_root, P.block_sha256(protocol, repo_root))
    raw = out / "raw" / f"{split}.jsonl"
    meta = out / "raw" / f"{split}.meta.json"
    ids = plan_ids(cfg, specs, split, limit)
    done: set[str] = set()
    if raw.exists():
        if meta.exists() and load_json(meta).get("inputs_sha256") == ih and load_json(meta).get("limit") == limit:
            done = {r["id"] for r in read_jsonl(raw)}
        else:
            log(f"{raw.name}: inputs changed, starting over")
            raw.unlink()
    write_json(meta, {"inputs_sha256": ih, "split": split, "limit": limit, "planned": len(ids)})
    keep_worlds = split == "holdout" and cfg.get("keep_holdout_worlds", True)
    generated = 0
    for domain, idx in ids:
        plan = SC.plan_scenario(cfg["seed"], specs[domain], idx, split)
        if plan.id in done:
            continue
        if keep_worlds:
            world_dir = out / "worlds" / plan.id
            if world_dir.exists():
                shutil.rmtree(world_dir)
            rec, built = make_example(plan, specs[domain], runner, cfg, protocol, system, catalog, constraints, world_dir, repo_root)
            write_json(out / "scenarios" / f"{plan.id}.json", scenario_record(built, f"../worlds/{plan.id}"))
        else:
            with tempfile.TemporaryDirectory() as tmp:
                rec, _ = make_example(plan, specs[domain], runner, cfg, protocol, system, catalog, constraints, Path(tmp) / "world", repo_root)
        append_jsonl(raw, rec)
        generated += 1
        if generated % 200 == 0:
            log(f"{split}: {generated + len(done)}/{len(ids)}")
    return {"split": split, "planned": len(ids), "generated": generated, "skipped": len(done)}


def cmd_dry_run(out: Callable[[str], None] = print, inv: Path = INV, repo_root: Path = REPO_ROOT) -> int:
    cfg, specs = load_config(inv), load_worlds(inv)
    problems = check_worlds(cfg, specs)
    out(f"Investigator generator (dry run: nothing written; tool results come from the executor's TEST DOUBLE, the real run uses the monitor)")
    out(f"  worlds: {len(specs)} ({len(domains_for(cfg, specs, 'train'))} training, {len(domains_for(cfg, specs, 'validation'))} validation: {', '.join(cfg['validation_domains'])}; "
        f"{len(domains_for(cfg, specs, 'holdout'))} held out whole: {', '.join(cfg['holdout_domains'])})")
    out(f"  sizes:  {cfg['train_examples']} training examples, {cfg['validation_examples_per_domain']} per validation domain, {cfg['holdout_examples_per_domain']} per held-out domain, seed {cfg['seed']}")
    for p in problems:
        out(f"  PROBLEM: {p}")
    if problems:
        return 1
    protocol = P.load_protocol(inv / "protocol.json")
    system = P.system_text(protocol, repo_root)
    catalog = load_json(repo_root / "docs" / "support" / "tools.json")
    constraints = [{"id": c["id"], "text": c["text"]} for c in load_json(repo_root / "docs" / "support" / "constraints.json")["constraints"]]
    runner = TL.FakeToolRunner(repo_root)
    for k, domain in enumerate(list(specs)[:3]):
        plan = SC.plan_scenario(cfg["seed"], specs[domain], k, specs[domain]["split"])
        with tempfile.TemporaryDirectory() as tmp:
            rec, built = make_example(plan, specs[domain], runner, cfg, protocol, system, catalog, constraints, Path(tmp) / "world", repo_root)
        out(f"  sample {rec['id']}: class {rec['class']}, fault {rec['fault']}, trigger {rec['trigger']}, variant {rec['variant']}, budget {rec['budget']}, "
            f"{len(rec['calls'])} calls, gold {rec['gold']['verdict']}/{rec['gold']['cause_category']}")
        out("    last assistant turn: " + rec["messages"][-1]["content"][:200])
    return 0


def main(argv: list[str] | None = None, *, training_dir: Path = TRAINING_DIR, runner: TL.ToolRunner | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.investigator.generate", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="plan and print samples; writes nothing, runs no executor")
    ap.add_argument("--split", choices=["train", "validation", "holdout", "all"], help="which examples to generate")
    ap.add_argument("--limit", type=int, help="number of examples (train: total, validation and holdout: per domain); a smaller run for checks")
    ap.add_argument("--out", type=Path, help="output folder (default training/out/investigator)")
    args = ap.parse_args(argv)
    if args.dry_run:
        return cmd_dry_run()
    if not args.split:
        ap.error("a real run needs --split train|holdout|all (or use --dry-run)")
    out = args.out or default_out(training_dir)
    runner = runner or TL.DotnetToolRunner(cache_dir=out / "tool-cache")
    try:
        with RunRecorder("investigator", "generate", {"config": sha256_file(INV / "config.json"), "protocol": sha256_file(INV / "protocol.json")}, root=training_dir, model_out=out) as rec:
            for split in (["train", "validation", "holdout"] if args.split == "all" else [args.split]):
                res = generate_split(split, out, runner, limit=args.limit)
                print(f"{split}: planned {res['planned']}, generated {res['generated']}, already present {res['skipped']}")
                rec.extra.setdefault("splits", {})[split] = res
    except (GenerateError, TL.ToolRunnerError, ValueError) as exc:
        print(f"stopped: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
