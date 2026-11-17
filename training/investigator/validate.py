"""``python -m training.investigator.validate``: validators of the synthetic incident data (operations.md 11.6).

Reads ``training/out/investigator/raw/{train,validation,holdout}.jsonl``, writes ``dataset/{train,validation,holdout}.jsonl``, ``rejected.jsonl`` and ``stats.json``.

| Id   | Level   | Rejects or fails when |
|------|---------|-----------------------|
| IV01 | Item    | The record does not match the example format |
| IV02 | Item    | A turn does not follow 11.3: system text, user layout, one call or one report per assistant turn, call arguments valid for the executor (except a deliberate denied call), budget countdown, stop rules, a report last |
| IV03 | Item    | The final report fails the validation of 11.3 (schema, evidence references that exist, user story in the pack or null) |
| IV04 | Item    | A masking pattern matches any turn (an unmasked value leaked) |
| IV05 | Item    | A log key, test case id, story id or runbook sentence of the adopting project's ``docs/`` appears in a training item |
| IV06 | Item    | The item is longer than ``max_chars`` |
| IV07 | Dataset | A held-out or validation domain appears in the training split, a domain is in two splits, or a sector crosses the split |
| IV08 | Dataset | The mix is off: answerable about 60 %, abstain about 20 %, budget pressure about 10 %, expected behaviour about 10 % (tolerance 0.05, checked from 400 items) |
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any, Callable, Mapping

from training.common.config import REPO_ROOT, TRAINING_DIR
from training.common.hashing import sha256_bytes
from training.common.jsonio import load_json, read_jsonl, write_json, write_jsonl
from training.investigator import protocol as P
from training.investigator.generate import INV, check_worlds, default_out, load_config, load_worlds
from training.investigator.masking import Masker

RESULT_RE = re.compile(r'^<result>(.*)</result>\n<budget calls="(\d+)" />$', re.S)
STORY_RE = re.compile(r"\bUS-[A-Z]{2,8}-\d{2}\b")
CASE_RE = re.compile(r"\b[A-Z]{2,8}-[A-Z]{2,12}-\d{3}\b")
KEY_RE = re.compile(r"\b[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(?:_[a-z]+)*\b")
MIX_TARGET = {"answerable": 0.60, "abstain": 0.20, "budget_pressure": 0.10, "expected_behaviour": 0.10}
RECORD_KEYS = ["schema_version", "id", "split", "domain", "class", "fault", "trigger", "variant", "budget", "messages", "gold", "calls", "evidence_ids"]


def project_identifiers(docs_root: Path) -> dict[str, set[str]]:
    """Log keys, test case ids, story ids and runbook sentences of the adopting project's ``docs/areas`` and the unkeyed runbooks."""
    ids: dict[str, set[str]] = {"story": set(), "case": set(), "key": set(), "sentence": set()}
    files = list((docs_root / "areas").rglob("*.md")) if (docs_root / "areas").exists() else []
    for f in files:
        text = f.read_text(encoding="utf-8")
        ids["story"] |= set(STORY_RE.findall(text))
        ids["case"] |= set(CASE_RE.findall(text))
        if f.name == "operations.md":
            ids["key"] |= set(re.findall(r"^## ([a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(?:_[a-z]+)*)\s*$", text, re.M))
    runbooks = [*(f for f in files if f.name == "operations.md"), docs_root / "support" / "unkeyed-runbooks.md"]
    for f in runbooks:
        if f.exists():
            for line in f.read_text(encoding="utf-8").splitlines():
                line = line.strip(" -*")
                if len(line) >= 40 and not line.startswith(("|", "#", "`")):
                    ids["sentence"].add(line)
    return ids


def check_item(rec: Mapping[str, Any], *, system: str, protocol: Mapping[str, Any], schema: Mapping[str, Any], catalog: Mapping[str, Any],
               masker: Masker, project: Mapping[str, set[str]] | None, max_chars: int, training: bool) -> dict[str, list[str]]:
    """Failed validators of one item with their reasons (empty when it passes)."""
    failed: dict[str, list[str]] = {}
    add = lambda v, m: failed.setdefault(v, []).append(m)   # noqa: E731
    if list(rec.keys()) != RECORD_KEYS or rec["schema_version"] != 1:
        add("IV01", "record keys differ from the example format")
        return failed
    msgs = rec["messages"]
    if len(msgs) < 3 or msgs[0] != {"role": "system", "content": system}:
        add("IV02", "the first message is not the protocol block")
        return failed
    if msgs[1]["role"] != "user" or not re.match(r"^<incident>.*</incident>\n<pack>.*</pack>\n<tools>.*</tools>\n<constraints>\n.*\n</constraints>\n<budget calls=\"\d+\" />$", msgs[1]["content"], re.S):
        add("IV02", "the user turn does not have the layout of 11.3")
        return failed
    left = int(re.search(r'<budget calls="(\d+)" />$', msgs[1]["content"]).group(1))
    if left > protocol["budget"]["cap"]:
        add("IV02", f"budget {left} is above the cap {protocol['budget']['cap']}")
    pack = re.search(r"<pack>(.*)</pack>", msgs[1]["content"], re.S).group(1)
    stories: set[str] | None = None
    try:
        pk = json.loads(pack)
        stories = {pk["signature"]["user_story"]} if pk else None
    except (json.JSONDecodeError, KeyError, TypeError):
        add("IV02", "the pack is not JSON")
    entity_names = [e["name"] for e in json.loads(re.search(r"<tools>(.*)</tools>", msgs[1]["content"], re.S).group(1))["entities"]]
    evidence: set[str] = {"E1"}
    expect_report = False
    fails_in_row = 0
    dup_seen: Counter[str] = Counter()
    seen_calls: set[str] = set()
    final_report: dict[str, Any] | None = None
    i = 2
    while i < len(msgs):
        m = msgs[i]
        want_role = "assistant" if (i - 2) % 2 == 0 else protocol["result_role"]
        if m["role"] != want_role:
            add("IV02", f"turn {i}: role {m['role']} where {want_role} is expected")
            break
        if m["role"] == "assistant":
            t = P.parse_turn(m["content"])
            if t.kind == "invalid":
                add("IV02", f"turn {i}: {t.error}")
                break
            if t.kind == "report":
                if i != len(msgs) - 1:
                    add("IV02", f"turn {i}: a report before the last turn")
                final_report = t.payload
                break
            if expect_report:
                add("IV02", f"turn {i}: a call where the stop rules require the report")
                break
            if left <= 0:
                add("IV02", f"turn {i}: a call with no calls left")
                break
            key = m["content"]
            seen_calls.add(key)
            tool, args = t.payload["tool"], t.payload["arguments"]
            errs = P.tool_call_errors(catalog, entity_names, tool, args)
            pending_denied = bool(errs)
            i += 1
            if i >= len(msgs):
                add("IV02", "a call without a result")
                break
            rm = RESULT_RE.match(msgs[i]["content"]) if msgs[i]["role"] == protocol["result_role"] else None
            if not rm:
                add("IV02", f"turn {i}: not a result with the budget line")
                break
            try:
                env = json.loads(rm.group(1))
            except json.JSONDecodeError:
                add("IV02", f"turn {i}: the result is not JSON")
                break
            left -= 1
            if int(rm.group(2)) != left:
                add("IV02", f"turn {i}: the budget line says {rm.group(2)}, expected {left}")
            if pending_denied and env.get("status") != "denied":
                add("IV02", f"turn {i}: arguments invalid ({errs[0]}) but the executor did not deny the call")
            evidence.add(env.get("evidence_id", ""))
            if env.get("status") == "duplicate":
                dup_seen[key] += 1
            fails_in_row = fails_in_row + 1 if env.get("status") in P.FAILED_STATUSES else 0
            expect_report = left == 0 or fails_in_row >= protocol["stop_rules"]["consecutive_failures"] or any(c >= protocol["stop_rules"]["duplicate_limit"] for c in dup_seen.values())
        i += 1
    if final_report is None:
        add("IV02", "the conversation does not end with a report")
    else:
        errs = P.validate_report(schema, final_report, evidence, stories)
        for e in errs:
            add("IV03", e)
    for n, m in enumerate(msgs[1:], 1):
        leaks = masker.leaks(m["content"])
        if leaks:
            add("IV04", f"turn {n}: {', '.join(leaks)} matches")
    if sum(len(m["content"]) for m in msgs) > max_chars:
        add("IV06", f"longer than {max_chars} characters")
    if training and project:
        blob = "\n".join(m["content"] for m in msgs)
        for kind, vocab in (("story", project["story"]), ("case", project["case"]), ("key", project["key"])):
            hit = next((v for v in vocab if v in blob), None)
            if hit:
                add("IV05", f"{kind} {hit} of the project docs appears")
        hit = next((s for s in project["sentence"] if s in blob), None)
        if hit:
            add("IV05", f"a runbook sentence of the project docs appears: {hit[:60]}")
    return failed


def run(out: Path, *, inv: Path = INV, repo_root: Path = REPO_ROOT, docs_root: Path | None = None, say: Callable[[str], None] = print) -> int:
    cfg, specs = load_config(inv), load_worlds(inv)
    protocol = P.load_protocol(inv / "protocol.json")
    system = P.system_text(protocol, repo_root)
    schema = P.investigation_schema(repo_root, protocol)
    catalog = load_json(repo_root / "docs" / "support" / "tools.json")
    masker = Masker(load_json(repo_root / "docs" / "support" / "masking-policy.json"))
    project = project_identifiers(docs_root or repo_root / "docs")
    SPLITS = ("train", "validation", "holdout")
    accepted: dict[str, list[dict[str, Any]]] = {s: [] for s in SPLITS}
    rejected: list[dict[str, Any]] = []
    for split in SPLITS:
        raw = out / "raw" / f"{split}.jsonl"
        if not raw.exists():
            continue
        for rec in read_jsonl(raw):
            failed = check_item(rec, system=system, protocol=protocol, schema=schema, catalog=catalog, masker=masker, project=project, max_chars=cfg["max_chars"], training=split == "train")
            if failed:
                rejected.append({"id": rec.get("id"), "split": split, "validator": sorted(failed)[0], "validators": sorted(failed), "reasons": failed})
            else:
                accepted[split].append(rec)
    ds = out / "dataset"
    for split in SPLITS:
        write_jsonl(ds / f"{split}.jsonl", accepted[split])
    write_jsonl(ds / "rejected.jsonl", rejected)
    failures: dict[str, list[str]] = {"IV07": [], "IV08": []}
    problems = check_worlds(cfg, specs)
    failures["IV07"] += problems
    hold = set(cfg["holdout_domains"]) | set(cfg.get("validation_domains", []))
    failures["IV07"] += [f"held-out domain {r['domain']} in the training split ({r['id']})" for r in accepted["train"] if r["domain"] in hold][:10]
    n = len(accepted["train"])
    if n >= 400:
        for k, share in MIX_TARGET.items():
            got = sum(r["class"] == k for r in accepted["train"]) / n
            if abs(got - share) > 0.05:
                failures["IV08"].append(f"{k} is {got:.3f}, expected {share} +- 0.05")
    sha = sha256_bytes(b"".join((ds / f"{s}.jsonl").read_bytes() for s in SPLITS))
    stats = compute_stats(accepted, rejected, sha, failures)
    write_json(ds / "stats.json", stats)
    say(format_stats(stats))
    bad = {k: v for k, v in failures.items() if v}
    for k, v in bad.items():
        say(f"{k} FAILED ({len(v)}): " + "; ".join(v[:5]))
    say("Dataset validators failed." if bad else "Dataset validators IV07 and IV08 passed.")
    return 1 if bad else 0


def compute_stats(accepted: Mapping[str, list[dict[str, Any]]], rejected: list[dict[str, Any]], sha: str, failures: Mapping[str, list[str]]) -> dict[str, Any]:
    allr = [r for s in ("train", "validation", "holdout") for r in accepted[s]]
    chars = sorted(sum(len(m["content"]) for m in r["messages"]) for r in allr)
    pct = lambda p: chars[max(0, -(-p * len(chars) // 100) - 1)] if chars else 0   # noqa: E731
    by = lambda f: dict(sorted(Counter(f(r) for r in allr).items()))   # noqa: E731
    return {"counts": {"per_split": {s: len(accepted[s]) for s in ("train", "validation", "holdout")}, "per_class": by(lambda r: r["class"]), "per_fault": by(lambda r: r["fault"]),
                       "per_trigger": by(lambda r: r["trigger"]), "per_variant": by(lambda r: r["variant"]), "per_domain": by(lambda r: r["domain"]),
                       "per_verdict": by(lambda r: r["gold"]["verdict"]), "per_calls": by(lambda r: len(r["calls"]))},
            "characters": {"p50": pct(50), "p95": pct(95), "max": chars[-1] if chars else 0},
            "rejections": {"per_validator": dict(sorted(Counter(r["validator"] for r in rejected).items())), "total": len(rejected)},
            "dataset_validators": {k: {"passed": not v, "failures": v[:20]} for k, v in failures.items()}, "dataset_sha256": sha}


def format_stats(stats: Mapping[str, Any]) -> str:
    c = stats["counts"]
    lines = ["Examples per split: " + ", ".join(f"{k} {v}" for k, v in c["per_split"].items()),
             "Per class: " + ", ".join(f"{k} {v}" for k, v in c["per_class"].items()),
             "Per fault: " + ", ".join(f"{k} {v}" for k, v in c["per_fault"].items()),
             "Per trigger: " + ", ".join(f"{k} {v}" for k, v in c["per_trigger"].items()),
             "Characters: " + ", ".join(f"{k} {v}" for k, v in stats["characters"].items()),
             "Rejections: " + (", ".join(f"{k} {v}" for k, v in stats["rejections"]["per_validator"].items()) or "none"),
             f"dataset_sha256: {stats['dataset_sha256']}"]
    return "\n".join(lines)


def main(argv: list[str] | None = None, *, training_dir: Path = TRAINING_DIR) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.investigator.validate", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, help="output folder (default training/out/investigator)")
    ap.add_argument("--docs-root", type=Path, default=REPO_ROOT / "docs", help="the adopting project's docs for IV05")
    args = ap.parse_args(argv)
    return run(args.out or default_out(training_dir), docs_root=args.docs_root)


if __name__ == "__main__":
    raise SystemExit(main())
