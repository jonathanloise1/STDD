"""``python -m training.assistant.validate``: assemble the dataset from the teacher cache, run every validator of
models.md 5.7, write ``dataset/{split}.jsonl``, ``rejected.jsonl`` and ``stats.json`` (5.8). No network, no cost.

Dataset-level validators (V13 to V15) fail the command (exit 1); item-level ones reject the item.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any

from training.assistant import external, protocol as P, stats as S, textchecks, validators as V
from training.assistant.dataset import assemble
from training.assistant.generate import ASSISTANT, check_config, default_out, load_all
from training.assistant.jobs import Pipeline, load_templates
from training.assistant.walk import cache_walk
from training.common.config import REPO_ROOT
from training.common.hashing import sha256_bytes
from training.common.jsonio import dumps_pretty, read_jsonl, write_json, write_jsonl
from training.common.teacher import JobCache

SPLITS = ["train", "validation", "test"]


def dataset_sha256(dataset_dir: Path) -> str:
    """SHA-256 of the three split files (concatenated in the order train, validation, test)."""
    return sha256_bytes(b"".join((dataset_dir / f"{s}.jsonl").read_bytes() for s in SPLITS))


def build_dataset(pipe: Pipeline, blocklist: list[str]) -> tuple[dict[str, list[dict[str, Any]]], list[dict[str, Any]], dict[str, Any]]:
    """Attach targets, run the item validators and V12. Returns ``(accepted records by split, rejected, extra stats)``."""
    asm = assemble(pipe)
    rejected: list[dict[str, Any]] = []
    accepted: dict[str, list[dict[str, Any]]] = {s: [] for s in ("train", "validation", "test", "seen")}
    for kind in ("train", "validation", "test", "seen"):
        pending: list[dict[str, Any]] = []
        for ex in sorted(asm.examples.get(kind, []), key=lambda e: e.id):
            if ex.cls.startswith("pos_"):
                ex.target = pipe.answers.get(ex.id)
            rec = ex.record()
            if rec["target"] is None:
                rejected.append({"id": ex.id, "stage": "answers", "validator": "V06", "validators": ["V06"], "domain": ex.domain, "lang": ex.lang, "reasons": {"V06": ["no teacher answer in the cache"]}})
                continue
            failed = V.run_item_validators(rec, pipe.protocol, blocklist, pipe.verdicts.get(ex.id))
            if failed:
                stage = "verify" if set(failed) <= {"V08", "V11"} else "assemble"
                rejected.append({"id": ex.id, "stage": stage, "validator": sorted(failed)[0], "validators": sorted(failed), "domain": ex.domain, "lang": ex.lang, "reasons": failed})
                continue
            pending.append(rec)
        dup = V.v12(pending)
        for rec in pending:
            if rec["id"] in dup:
                rejected.append({"id": rec["id"], "stage": "assemble", "validator": "V12", "validators": ["V12"], "domain": rec["domain"], "lang": rec["lang"], "reasons": {"V12": [dup[rec["id"]]]}})
            else:
                accepted[kind].append(rec)
    for r in pipe.rejections:                       # question-, story- and page-level rejections found while building
        rejected.append({"id": r["key"], "stage": r["stage"], "validator": r["validator"], "validators": [r["validator"]], "domain": r["domain"], "lang": r["lang"], "reasons": {r["validator"]: [r["reason"]]}})
    extra = {"shortfall": asm.shortfall, "assembly_skipped": asm.skipped, "train_class_targets": asm.counts_target}
    return accepted, rejected, extra


def run(out_dir: Path, config_dir: Path, tools: str = "auto", docs_root: Path = REPO_ROOT / "docs", out=print) -> int:
    cfg, domains, retrieval = load_all(config_dir)
    check_config(cfg, domains)
    pipe = Pipeline(cfg, domains, load_templates(ASSISTANT / "teacher"), retrieval)
    cache = JobCache(out_dir / "teacher-cache")
    usage = cache_walk(pipe, cache)
    missing = {s: u.jobs - u.found for s, u in usage.items() if u.jobs - u.found}
    if missing:
        out("Warning: the teacher cache is incomplete, jobs without a response: " + ", ".join(f"{s} {n}" for s, n in missing.items()))
    blocklist = textchecks.load_blocklist()
    accepted, rejected, extra = build_dataset(pipe, blocklist)

    # V03: built-in page check ran at ingestion; the frontend manual:check runs on the written trees when available
    external_state = "not run"
    if tools != "off":
        for st in pipe.alive():
            tree = out_dir / "domains" / st.id
            if not tree.exists():
                continue
            findings = external.manual_check(tree)
            if findings is None:
                external_state = "unavailable"
                break
            external_state = "run"
            for f in findings:
                rejected.append({"id": f["path"], "stage": "pages", "validator": "V03", "validators": ["V03"], "domain": st.id, "lang": None, "reasons": {"V03": [f"{f['check']}: {f['message']}"]}})
        if tools == "require" and external_state != "run":
            out("manual:check could not be run (--tools require)")
            return 1
    out(f"V03 external manual:check: {external_state}")

    dataset_dir = out_dir / "dataset"
    for split in SPLITS:
        write_jsonl(dataset_dir / f"{split}.jsonl", accepted[split])
    write_jsonl(dataset_dir / "seen.jsonl", accepted["seen"])
    write_jsonl(dataset_dir / "rejected.jsonl", rejected)
    sha = dataset_sha256(dataset_dir)

    all_items = [r for s in SPLITS for r in accepted[s]]
    failures = {
        "V13": V.v13(accepted["train"], all_items + accepted["seen"], pipe.protocol),
        "V14": V.v14(accepted["train"], cfg["classes"]),
        "V15": V.v15(all_items + accepted["seen"], domains, docs_root),
    }
    stats = S.compute(accepted, rejected, usage, sha, {**extra, "split_sha256": {s: sha256_bytes((dataset_dir / f"{s}.jsonl").read_bytes()) for s in SPLITS},
                                                       "seen_items": len(accepted["seen"]), "v03_external": external_state,
                                                       "dataset_validators": {k: {"passed": not v, "failures": v[:50], "failure_count": len(v)} for k, v in failures.items()},
                                                       "incomplete_stages": missing})
    write_json(dataset_dir / "stats.json", stats)
    out(S.format_tables(stats))
    bad = {k: v for k, v in failures.items() if v}
    for k, v in bad.items():
        out(f"\n{k} FAILED ({len(v)}):")
        for line in v[:10]:
            out(f"  {line}")
    out("\n" + ("Dataset validators failed: see models.md section 10." if bad else "Dataset validators V13 to V15 passed."))
    return 1 if bad else 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.assistant.validate", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, default=None, help="output folder (default training/out/assistant)")
    ap.add_argument("--config-dir", type=Path, default=ASSISTANT / "config")
    ap.add_argument("--tools", choices=["auto", "off", "require"], default="auto", help="run the frontend manual:check on the domain trees (default: when available)")
    ap.add_argument("--docs-root", type=Path, default=REPO_ROOT / "docs", help="sample app docs for V15")
    args = ap.parse_args(argv)
    return run(args.out or default_out(), args.config_dir, args.tools, args.docs_root)


if __name__ == "__main__":
    raise SystemExit(main())
