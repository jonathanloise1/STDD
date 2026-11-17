"""The ``export`` command, shared by ``training.assistant.export`` and ``training.investigator.export`` (models.md 6.5, 6.6).

    export --dry-run [--run ID | --candidate ID]     validate, print every step and external command; imports nothing, writes nothing
    export [--run ID | --candidate ID]               merge, ONNX / GGUF, manifest and template check
    export ... --evaluate --approved-estimate ID     then run `eval --export merged|onnx|gguf` (the hook of 6.6) [COST]

A trained model is exported from the checkpoint its run selected; an untrained candidate (models.md 4.1) from the base model
(`--candidate ID`, run.json gets status ``untrained-candidate``, adapter and dataset null).
"""
from __future__ import annotations

import argparse
import os
import platform
import sys
from pathlib import Path
from typing import Any, Callable, Mapping

from . import export_core as X, train_core as T
from .config import next_run_id
from .evalcore import latest_run
from .jsonio import load_json
from .runs import RunRecorder, code_commit
from .train_cli import set_run_keys
from .hashing import sha256_file
from . import weights as W


def build_parser(prog: str, description: str) -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog=prog, description=description, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="validate and print the plan; imports no heavy dependency, writes nothing")
    ap.add_argument("--run", help="run id (default: the latest run)")
    ap.add_argument("--candidate", help="export the untrained base model of this candidate instead of a trained checkpoint (models.md 4.1)")
    ap.add_argument("--tier", choices=["small", "large"], help="investigator: the tier of the candidate")
    ap.add_argument("--model-version", default="1.0.0", help="version for an untrained candidate (default 1.0.0)")
    ap.add_argument("--formats", help="comma separated subset of the configured formats (onnx,gguf)")
    ap.add_argument("--variant", action="append", default=[], metavar="FORMAT=VARIANT", help="use a variant, e.g. gguf=Q8_0 or onnx=q8 (the fallback of 6.5)")
    ap.add_argument("--gguf", metavar="FILE", help="with --candidate: the path of the candidate's pre-made GGUF, when it is not in the weights folder (MYAPP_MODELS_DIR); its hash is checked")
    ap.add_argument("--from-bf16", action="store_true", help="with --candidate: ignore the candidate's pre-made GGUF and convert its BF16 weights (needs about 80 GB of RAM and 165 GB of disk for the 35B-A3B)")
    ap.add_argument("--evaluate", action="store_true", help="run `eval --export` for merged and every export afterwards (needs --approved-estimate) [COST]")
    ap.add_argument("--approved-estimate", metavar="ID", help="passed to the evaluation hook")
    ap.add_argument("--allow-unverified", action="store_true", help="accept candidates that are not pinned and verified (rehearsals only)")
    return ap


def main(setup: Any, argv: list[str] | None = None, *, prog: str, tools_factory: Callable[[Mapping[str, Any]], X.ExportTools] | None = None,
         env: Mapping[str, str] | None = None, eval_main: Callable[[list[str]], int] | None = None, out: Callable[[str], None] = print) -> int:
    args = build_parser(prog, __doc__ or "").parse_args(argv)
    env = env if env is not None else os.environ
    real = not args.dry_run
    try:
        cfg = load_json(setup.config_dir / "export.json")
        formats = [f for f in (args.formats.split(",") if args.formats else cfg["formats"]) if f]
        bad = [f for f in formats if f not in cfg["formats"]]
        if bad:
            raise X.ExportError(f"formats {bad} are not configured in export.json ({cfg['formats']})")
        variants = dict(cfg["variants"])
        for v in args.variant:
            fmt, _, name = v.partition("=")
            if fmt not in variants or not name:
                raise X.ExportError(f"--variant {v}: expected FORMAT=VARIANT for one of {sorted(variants)}")
            variants[fmt] = name
        candidate_mode = bool(args.candidate)
        premade_path: Path | None = None
        premade_sha: str | None = None
        tier = args.tier or getattr(setup, "tier", None)
        if candidate_mode:
            cands = T.load_candidates(setup.config_dir / "candidates.json")
            base = dict(T.select_base_model(cands, args.candidate, max_parameters=setup.max_parameters, real_run=real, allow_unverified=args.allow_unverified,
                                            tier=args.tier or getattr(setup, "tier", None)))
            run_id = args.run or (next_run_id(setup.model, setup.training_dir, runs=setup.out / "runs") if real else "{new run id}")
            version, adapter_dir = args.model_version, None
            block = None if args.from_bf16 else base.get("gguf")
            if block and "gguf" in formats:
                if "onnx" in formats:
                    if args.formats:
                        raise X.ExportError(f"{base['id']} has a pre-made GGUF and no ONNX export: use --formats gguf")
                    formats = [f for f in formats if f != "onnx"]
                variants["gguf"] = block["quantization"]
                premade_path = W.weights_path(block, args.gguf, env)
                if real:
                    premade_sha = W.check_weights(block, premade_path, base["id"])
        else:
            run_id = args.run or latest_run(setup.model, model_out=setup.out)
            if run_id is None:
                raise X.ExportError("no run found: train first, or export a candidate with --candidate")
            run = load_json(setup.out / "runs" / run_id / "run.json")
            if run.get("status") != "trained":
                raise X.ExportError(f"run {run_id} is not a successfully trained run (status {run.get('status')!r}): see its selection in run.json")
            base, version = run["base_model"], run["model_version"]
            adapter_dir = setup.out / "runs" / run_id / "train" / "checkpoints" / run["adapter"]["checkpoint"]
            if real and not adapter_dir.exists():
                raise X.ExportError(f"the selected checkpoint {adapter_dir} does not exist")
        run_dir = setup.out / "runs" / run_id
        kwargs = base.get("chat_template_kwargs", {})
        if hasattr(setup, "tool_role"):
            setup.tool_role = base.get("tool_role")
        cases = setup.template_cases()
        if not cases:
            raise X.ExportError("no golden cases to run the template check on")
        steps = X.plan_commands(cfg, formats, variants, run_dir, setup.model, version, tier=tier)
        if args.dry_run:
            out(f"Export plan for {setup.model} {version} from run {run_id} (dry run: nothing imported, nothing written)")
            out(f"  source:   " + (f"base model {base['hub_id']} revision {base.get('revision') or 'NOT PINNED'} (untrained candidate)" if candidate_mode
                                   else f"checkpoint {run['adapter']['checkpoint']} on {base['hub_id']}"))
            out(f"  formats:  {', '.join(f'{f} {variants[f]}' for f in formats)}; chat_template_kwargs {kwargs}")
            if premade_path is not None:
                out(f"  pre-made GGUF: {premade_path} (hash {'recorded' if block.get('sha256') else 'NOT RECORDED: a real export would be refused'}); no merge, no conversion: the file is linked into the run")
            else:
                for name, cmd in steps:
                    out(f"  {name:14s} {' '.join(cmd)}")
            out(f"  template check: {len(cases)} golden cases through the Python tokenizer" + (", transformers.js (node)" if "onnx" in formats else "") + (", llama.cpp /apply-template" if "gguf" in formats else "") + "; all must be equal byte for byte")
            problems = X.check_pins(cfg, formats, env) if premade_path is None else []
            for p in problems:
                out(f"  a real export would be refused: {p}")
            out("  after export: eval --export merged, then " + ", then ".join(f"eval --export {f}" for f in formats) + " (each [COST], owner approves the estimate)")
            return 0
        problems = X.check_pins(cfg, formats, env) if premade_path is None else []
        if problems:
            raise X.ExportError("; ".join(problems))
    except (X.ExportError, T.TrainError, W.WeightsError, FileNotFoundError) as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2

    tools = tools_factory(cfg) if tools_factory else X.RealExportTools(cfg, env, log=out)
    if premade_path is not None and hasattr(tools, "hub_tokenizer"):
        tools.hub_tokenizer = (base["hub_id"], base.get("revision"))
    tier = args.tier or getattr(setup, "tier", None) or (run.get("tier") if not candidate_mode else None)
    req = X.ExportRequest(setup.model, run_dir, version, base, adapter_dir, kwargs, formats, variants, cases, cfg,
                          lambda fmt, entry: setup.export_extras(fmt, entry, base, tier) if hasattr(setup, "export_extras") else {}, tier,
                          premade_path, premade_sha)
    try:
        with RunRecorder(setup.model, "export", {"export": sha256_file(setup.config_dir / "export.json")}, run_id, root=setup.training_dir, model_out=setup.out) as rec:
            manifest = X.run_export(req, tools, out)
            rec.extra.update(template_check=manifest["template_check"]["passed"], formats=formats, variants=variants)
            if candidate_mode:
                set_run_keys(rec.path, {
                    "model_version": version, "status": "untrained-candidate",
                    "base_model": {"hub_id": base["hub_id"], "revision": base["revision"], "license": base["license"], "parameters": base["parameters"], "chat_template_kwargs": kwargs,
                                   **({"trainable": False} if not T.is_trainable(base) else {}),
                                   **({"tool_role": base["tool_role"]} if base.get("tool_role") else {})},
                    "adapter": None, "dataset": None,
                    "training": {"run_id": run_id, "code_commit": code_commit(), "seed": setup.seed(), "hardware": platform.processor() or platform.machine()},
                    **({"protocol": setup.protocol_ref()} if setup.protocol_ref() is not None else {}),
                    **({"contracts": setup.contracts()} if setup.contracts() is not None else {})})
    except X.ExportError as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2
    if not manifest["template_check"]["passed"]:
        out("TEMPLATE CHECK FAILED: the rendered prompts differ between the Python tokenizer, transformers.js and llama.cpp; fix chat_template_kwargs "
            "in candidates.json and export again (models.md 9.1 step 9). First mismatch: " + str(manifest["template_check"]["mismatches"][0]))
        return 1
    out(f"exports written to {run_dir / 'exports.json'}; template check passed on {manifest['template_check']['cases']} cases")
    if args.evaluate:
        if eval_main is None:
            raise SystemExit("--evaluate is wired by the model's own export command")
        informative = getattr(setup, "informative_exports", ())
        order = [*formats, "merged"] if informative else ["merged", *formats]            # the gating exports first when one report does not gate
        for fmt in [f for f in order if f != "merged"] if premade_path is not None else order:    # a pre-made GGUF has no merged weights to evaluate
            code = eval_main(["--export", fmt, "--run", run_id, "--approved-estimate", args.approved_estimate or ""] + (["--tier", tier] if tier else []))
            if code != 0 and fmt in informative:
                out(f"evaluation of {fmt} did not pass (exit {code}); it is informative and does not block the record")
            elif code != 0:
                out(f"evaluation of {fmt} did not pass (exit {code}); stopping")
                return code
        return 0
    else:
        out("next: " + "; ".join(f"python -m training.{setup.model}.eval --export {f} --run {run_id}" for f in ["merged", *formats]))
        out("browser check (models.md 6.6): on a machine with a real GPU ask the 20 fixed bench items through the help panel and write the card's Browser check table")
    return 0
