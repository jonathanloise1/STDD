"""The ``train`` command, shared by ``training.assistant.train`` and ``training.investigator.train`` (models.md 6.1 to 6.4).

    train --dry-run                        validate configuration and dataset, print the plan; imports no torch
    train [--candidate ID] --approved-plan ID     the long run [COST]

The model-specific parts come from a ``setup`` object (``AssistantSetup`` or ``InvestigatorSetup``): where its files are,
its dataset, its adapter (examples, validation scoring, final selection), its protocol and contracts for the lock.
A real run needs the plan id the owner approved, a CUDA or Apple MPS device (CPU is refused), a pinned and verified base
model and, when the final selection uses a judge, a configured judge model. ``engine_factory`` and ``hardware`` are injected by tests.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any, Callable, Mapping

from . import hardware as H, train_core as T
from .config import next_run_id
from .estimate import ApprovalError, require_approval
from .jsonio import load_json, write_json
from .runs import RunRecorder, code_commit
from .hashing import sha256_file


def set_run_keys(path: Path, keys: Mapping[str, Any]) -> None:
    """Merge top-level keys into ``run.json`` (the keys ``lock --record`` reads), keeping everything else."""
    data = load_json(path) if path.exists() else {"schema_version": 1, "commands": []}
    data.update(keys)
    write_json(path, data)


def build_parser(prog: str, description: str) -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog=prog, description=description, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="validate configuration and dataset and print the plan; imports no torch, writes nothing")
    ap.add_argument("--candidate", help="base model id from candidates.json (default: the smallest, models.md 6.1 rule 2)")
    ap.add_argument("--tier", choices=["small", "large"], help="investigator: the tier whose candidates are used")
    ap.add_argument("--model-version", default="1.0.0", help="semantic version recorded for this run (default 1.0.0)")
    ap.add_argument("--run", help="run id to use or resume ({yyyy-mm-dd}-{letter}); default: a new one")
    ap.add_argument("--no-resume", action="store_true", help="start over even when the run has a saved state")
    ap.add_argument("--device", choices=["auto", "cuda", "mps"], default="auto", help="training device (CPU is refused)")
    ap.add_argument("--micro-batch", type=int, metavar="N", help="micro-batch size, replacing the one chosen from the VRAM and the model size (use a smaller one after an out-of-memory error); "
                    "the accumulation follows, the effective batch is unchanged")
    ap.add_argument("--approved-plan", metavar="ID", help="the plan id the owner approved [COST]")
    ap.add_argument("--allow-unverified", action="store_true", help="accept candidates that are not pinned and verified (rehearsals only)")
    return ap


def _hardware(args: argparse.Namespace, hw: H.HardwareInfo | None) -> H.HardwareInfo:
    info = hw or H.detect()
    if args.device != "auto" and info.device != args.device:
        raise T.TrainError(f"--device {args.device} requested but the machine reports {info.device}")
    return info


def main(setup: Any, argv: list[str] | None = None, *, prog: str, engine_factory: Callable[..., Any] | None = None,
         judge_factory: Callable[[bool], Any] | None = None, hardware: H.HardwareInfo | None = None, out: Callable[[str], None] = print,
         evaluate: Callable[[H.HardwareInfo, int], tuple[bool, list[str]]] = H.evaluate) -> int:
    args = build_parser(prog, __doc__ or "").parse_args(argv)
    real = not args.dry_run
    try:
        cfg = load_json(setup.config_dir / "train.json")
        validate_config(cfg)
        cands = T.load_candidates(setup.config_dir / "candidates.json")
        base = T.select_base_model(cands, args.candidate or cfg["base_model"].get("candidate"), max_parameters=setup.max_parameters,
                                   real_run=real, allow_unverified=args.allow_unverified, tier=args.tier or getattr(setup, "tier", None), purpose="train")
        if hasattr(setup, "tool_role"):
            setup.tool_role = base.get("tool_role")
        adapter_noj = setup.adapter(cfg, None)
        examples = adapter_noj.train_examples()
        validation = adapter_noj.validation_items()
        if not examples or not validation:
            raise T.TrainError("the dataset has no training or validation examples: run validate first")
        ds = setup.dataset_block()
        hw = _hardware(args, hardware) if real else _hardware_info_for_plan(args, hardware)
        plan = T.build_plan(cfg, len(examples), hw, setup.seed(), {"base": [base["hub_id"], base.get("revision")], "dataset_sha256": ds["dataset_sha256"],
                                                                  "model_version": args.model_version, "model": setup.model},
                           base["parameters"], args.micro_batch)
        if args.dry_run:
            return _print_plan(setup, cfg, base, plan, ds, len(validation), hw, out, evaluate)
        if hw.device == "cpu":
            raise T.TrainError("training on CPU is refused (models.md 6.4); use CUDA or Apple MPS")
        ok, problems = evaluate(hw, base["parameters"])
        if not ok:
            raise T.TrainError("this machine does not meet models.md 6.4: " + "; ".join(problems))
        require_approval(plan.plan_id, args.approved_plan)
        problems = setup.preflight(True)
        if problems:
            raise T.TrainError("; ".join(problems))
    except (T.TrainError, ApprovalError, FileNotFoundError) as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2

    run_id = args.run or next_run_id(setup.model, setup.training_dir, runs=setup.out / "runs")
    run_dir = setup.out / "runs" / run_id
    judge = judge_factory(True) if judge_factory else None
    adapter = setup.adapter(cfg, judge)
    template_kwargs = base.get("chat_template_kwargs", {})
    engine = (engine_factory or T.TorchEngine)(base, cfg, plan, examples, template_kwargs, out)
    hashes = {"train": sha256_file(setup.config_dir / "train.json"), "candidates": sha256_file(setup.config_dir / "candidates.json"), "dataset": ds["dataset_sha256"]}
    try:
        with RunRecorder(setup.model, "train", hashes, run_id, root=setup.training_dir, model_out=setup.out) as rec:
            outcome = T.run_training(engine, adapter, plan, cfg, run_dir / "train", out, resume=not args.no_resume)
            rec.extra.update(plan=plan.as_dict(), stop_reason=outcome.stop_reason, steps=outcome.steps)
            keys: dict[str, Any] = {"selection": {"kept": outcome.kept, "final": outcome.final_records, "selected": outcome.selected, "history": outcome.history},
                                    "stop_reason": outcome.stop_reason, "memorisation": outcome.memorisation}
            if outcome.selected is not None:
                a = cfg["adapter"]
                keys.update(model_version=args.model_version, status="trained",
                            base_model={"hub_id": base["hub_id"], "revision": base["revision"], "license": base["license"], "parameters": base["parameters"],
                                        "chat_template_kwargs": template_kwargs,
                                        **({"tool_role": base["tool_role"]} if base.get("tool_role") else {})},
                            adapter={"method": a["method"], "rank": a["rank"], "alpha": a["alpha"], "checkpoint": f"step-{outcome.selected['step']}"},
                            dataset=ds, training={"run_id": run_id, "code_commit": code_commit(), "seed": plan.seed, "hardware": hw.name})
                if setup.protocol_ref() is not None:
                    keys["protocol"] = setup.protocol_ref()
                if setup.contracts() is not None:
                    keys["contracts"] = setup.contracts()
                if args.tier or getattr(setup, "tier", None):
                    keys["tier"] = args.tier or setup.tier
            set_run_keys(rec.path, keys)
    except T.TrainError as exc:                      # for example the judge failed in the final selection: the run is saved and resumes
        print(f"refused: {exc}", file=sys.stderr)
        return 2
    if outcome.memorisation:
        out("WARNING: the run stopped on memorisation (training loss below the limit): see models.md section 10")
    if outcome.selected is None:
        out("no kept checkpoint met the selection rule (faithful >= the minimum): nothing is recorded as trained; see models.md section 10")
        return 1
    out(f"selected step-{outcome.selected['step']} ({outcome.stop_reason}); run {run_id}")
    return 0


def _hardware_info_for_plan(args: argparse.Namespace, hw: H.HardwareInfo | None) -> H.HardwareInfo:
    return hw or H.detect()


def validate_config(cfg: Mapping[str, Any]) -> None:
    need = ["adapter", "optimizer", "batch", "max_seq_len", "epochs", "precision", "validation", "score_weights", "format_valid_min", "early_stopping",
            "memorisation", "keep_checkpoints", "selection", "checkpoint_every_steps", "log_every_steps"]
    missing = [k for k in need if k not in cfg]
    if missing:
        raise T.TrainError(f"train.json lacks: {', '.join(missing)}")
    if cfg["adapter"]["method"] != "lora":
        raise T.TrainError("adapter.method must be lora")
    ld = cfg.get("loading", {})
    if ld.get("attn_implementation") == "flash_attention_2" and ld.get("packing"):
        raise T.TrainError("loading: flash_attention_2 with sample packing is not supported (field reports advise against the combination for Qwen3.5; models.md 6.2)")
    if cfg["batch"]["effective"] < 1 or cfg["epochs"] < 1 or cfg["keep_checkpoints"] < 1:
        raise T.TrainError("batch.effective, epochs and keep_checkpoints must be at least 1")


def _print_plan(setup: Any, cfg: Mapping[str, Any], base: Mapping[str, Any], plan: T.Plan, ds: Mapping[str, Any], n_val: int, hw: H.HardwareInfo,
                out: Callable[[str], None], evaluate: Callable[[H.HardwareInfo, int], tuple[bool, list[str]]] = H.evaluate) -> int:
    ok, problems = evaluate(hw, base["parameters"])
    out(f"Training plan for {setup.model} (dry run: nothing imported, nothing written)")
    out(f"  base model:   {base['id']}  {base['hub_id']}  revision {base.get('revision') or 'NOT PINNED'}  {base['parameters']:,} parameters  {base.get('license')}")
    out(f"  template:     chat_template_kwargs {base.get('chat_template_kwargs', {})}")
    out(f"  adapter:      lora rank {cfg['adapter']['rank']} alpha {cfg['adapter']['alpha']} dropout {cfg['adapter']['dropout']} on {', '.join(cfg['adapter']['target_modules'])}")
    out(f"  optimizer:    lr {cfg['optimizer']['learning_rate']} {cfg['optimizer']['schedule']}, warm-up {cfg['optimizer']['warmup_steps']}, weight decay {cfg['optimizer']['weight_decay']}")
    out(f"  data:         {plan.examples} training examples, {n_val} validation items, max sequence {cfg['max_seq_len']} tokens, loss on assistant turns only")
    out(f"  steps:        {plan.steps_per_epoch} per epoch x {plan.epochs} epochs = {plan.total_steps} at most; validation every {cfg['validation']['every_steps']}; "
        f"early stop after {cfg['early_stopping']['patience']} runs without +{cfg['early_stopping']['min_delta']}; keep {cfg['keep_checkpoints']}")
    out(f"  batch:        effective {plan.effective_batch} = micro-batch {plan.micro_batch} x accumulation {plan.grad_accum} (device {plan.device}, precision {plan.precision})")
    out(f"  dataset:      {ds['dataset_sha256'][:16]}...  seed {plan.seed}")
    out(f"  machine:      {hw.device} ({hw.name}), {hw.ram_gb} GB RAM, {hw.disk_free_gb} GB free: " + ("meets models.md 6.4" if ok else "would be refused: " + "; ".join(problems)))
    out(f"  plan id:      {plan.plan_id}   (a real run needs --approved-plan {plan.plan_id}, approved by the owner)")
    out("  a real run also needs: a pinned and verified base model, the local judge host running for the final selection (if the model uses one), and the owner's approval [COST]")
    return 0
