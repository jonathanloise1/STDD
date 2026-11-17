"""Golden cases of the investigator protocol (the counterpart of ``training/assistant/golden.py``).

    python -m training.investigator.golden --write     regenerate protocol.golden.json
    python -m training.investigator.golden --check     fail when the file differs from what the code produces

Language-neutral cases for every implementation (this module, the monitor's C# host): rendering of the user turn, call, result, report
and repair turns, parsing of assistant turns, report validation and the GBNF grammar of a turn (the host passes it to llama.cpp). The rules text is not in the file (it lives in
``support.agent.md``); the file carries its SHA-256 so a change of the block shows up as a golden change.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any

from training.common.config import REPO_ROOT
from training.common.jsonio import dumps_pretty, load_json
from training.investigator import grammar as GR, protocol as P

GOLDEN_PATH = Path(__file__).with_name("protocol.golden.json")

TOOLS = [
    {"name": "logs_by_correlation", "description": "Returns the log entries of one request, identified by its trace id.",
     "parameters": {"type": "object", "additionalProperties": False, "required": ["trace_id"], "properties": {"trace_id": {"type": "string", "pattern": "^[0-9a-f]{32}$"}, "limit": {"type": "integer", "minimum": 1, "maximum": 200}}}},
    {"name": "health_status", "description": "Returns the current health of the application.", "parameters": {"type": "object", "additionalProperties": False, "properties": {}}},
]
ENTITIES = [{"name": "membership", "description": "The link between a person and a site."}, {"name": "site", "description": "A tenant."}]
CONSTRAINTS = [{"id": "C01", "text": "Every tool is read-only."}, {"id": "C03", "text": "An investigation may make at most 12 tool calls."}]
INCIDENT = {"trigger": "rate", "severity": "Warning", "environment": "prod", "signature": {"kind": "keyed", "id": "loans.lend.access_lost", "log_key": "loans.lend.access_lost", "template_id": None, "template": None},
            "count": 41, "rate_per_hour": 120, "first_seen": "2026-11-06T09:12:44.120Z", "last_seen": "2026-11-06T09:58:03.511Z",
            "evidence": [{"id": "E1", "source": "signal", "records": [{"LogKey": "loans.lend.access_lost", "Message": "contact [email] à \"quoted\" {braces}", "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736"}]}]}
PACK = {"signature": {"id": "loans.lend.access_lost", "user_story": "US-LOAN-01", "status": "active"}, "runbook": "### Meaning\n\nA member lost access."}
ENV_OK = {"tool": "logs_by_correlation", "call": 1, "status": "ok", "evidence_id": "E2", "source": "world", "rows": 1, "truncated": False, "fields_dropped": 0,
          "data": {"records": [{"timestamp": "2026-11-06T09:58:03.511Z", "message": "café – \"x\""}]}}
ENV_DENIED = {"tool": "logs_by_correlation", "call": 2, "status": "denied", "evidence_id": "E3", "source": "world", "rows": 0, "truncated": False, "fields_dropped": 0, "data": None, "error": "$.limit: above 200"}
REPORT_OK = {"verdict": "explained", "abstain_reason": None, "cause_category": "access", "cause": "The member was removed at 09:10.", "confidence": 0.82, "evidence_refs": ["E1", "E2"],
             "area": "loans", "user_story": "US-LOAN-01", "reproduction": None, "next_checks": ["Ask the admin."]}
REPORT_ABSTAIN = {"verdict": "abstain", "abstain_reason": "budget_exhausted", "cause_category": "unknown", "cause": None, "confidence": 0.2, "evidence_refs": [], "area": None, "user_story": None,
                  "reproduction": None, "next_checks": []}
TRACE = "4bf92f3577b34da6a3ce929d0e0e4736"

RENDER_USER = [
    ("u01-rate-with-pack", "Rate trigger with a pack, budget 6", INCIDENT, PACK, 6),
    ("u02-unknown-no-pack", "Unknown trigger, no pack (null), budget 10", {**INCIDENT, "trigger": "unknown", "signature": {"kind": "template", "id": None, "log_key": None, "template_id": "t-0123456789ab", "template": "Failed for {Id}"}}, None, 10),
    ("u03-budget-one", "Budget 1", INCIDENT, PACK, 1),
    ("u04-non-ascii-kept", "Non-ASCII characters are kept, not escaped", {**INCIDENT, "environment": "pröd"}, PACK, 6),
    ("u05-rule-check-failed", "A failed rule check is part of the incident (the trigger stays a value of the report schema)", {**INCIDENT, "trigger": "severity", "rule_checks": [{"tool": "health_status", "expect": "status == Healthy", "passed": False}]}, PACK, 6),
    ("u06-braces-in-values", "Braces in values stay literal and are not substituted again", {**INCIDENT, "count": 1, "note": "{budget} {tools} {calls}"}, PACK, 6),
]
RENDER_TURNS = [
    ("t01-call-logs", "A call with an optional argument", "call", {"tool": "logs_by_correlation", "arguments": {"trace_id": TRACE, "limit": 50}}),
    ("t02-call-no-arguments", "A call without arguments", "call", {"tool": "health_status", "arguments": {}}),
    ("t03-result-ok", "An ok result with 2 calls left", "result", (ENV_OK, 2)),
    ("t04-result-denied-last", "A denied result with no calls left", "result", (ENV_DENIED, 0)),
    ("t05-report-explained", "An explained report", "report", REPORT_OK),
    ("t06-report-abstain", "An abstain report", "report", REPORT_ABSTAIN),
    ("t07-repair", "The repair turn after an invalid report", "repair", ["evidence_refs: E9 was not received", "user_story US-XXX-01 is not in the pack"]),
]
PARSE = [
    ("p01-call", '<call>{"tool":"health_status","arguments":{}}</call>'),
    ("p02-call-spaced-json", '<call>{"tool": "logs_by_correlation", "arguments": {"trace_id": "%s"}}</call>' % TRACE),
    ("p03-report", "<report>" + P.dumps(REPORT_OK) + "</report>"),
    ("p04-surrounding-whitespace", "\n  <call>{\"tool\":\"health_status\",\"arguments\":{}}</call>\r\n"),
    ("p05-text-before", 'I will check the logs. <call>{"tool":"health_status","arguments":{}}</call>'),
    ("p06-two-calls", '<call>{"tool":"health_status","arguments":{}}</call><call>{"tool":"health_status","arguments":{}}</call>'),
    ("p07-bad-json", '<call>{"tool":"health_status",</call>'),
    ("p08-missing-arguments", '<call>{"tool":"health_status"}</call>'),
    ("p09-extra-key", '<call>{"tool":"health_status","arguments":{},"why":"x"}</call>'),
    ("p10-call-array", "<call>[1,2]</call>"),
    ("p11-unclosed", '<report>{"verdict":"abstain"}'),
    ("p12-empty", ""),
    ("p13-multiline-report", "<report>{\"verdict\":\n\"abstain\"}</report>"),
]
VALIDATE = [
    ("v01-valid", REPORT_OK, ["E1", "E2"], ["US-LOAN-01"]),
    ("v02-unknown-evidence", {**REPORT_OK, "evidence_refs": ["E1", "E9"]}, ["E1", "E2"], ["US-LOAN-01"]),
    ("v03-story-not-in-pack", REPORT_OK, ["E1", "E2"], ["US-OTHR-01"]),
    ("v04-story-without-pack", REPORT_OK, ["E1", "E2"], None),
    ("v05-explained-without-evidence", {**REPORT_OK, "evidence_refs": []}, ["E1"], ["US-LOAN-01"]),
    ("v06-abstain-valid", REPORT_ABSTAIN, ["E1"], None),
    ("v07-abstain-with-cause", {**REPORT_ABSTAIN, "cause": "x"}, ["E1"], None),
    ("v08-cause-too-long", {**REPORT_OK, "cause": "x" * 301}, ["E1", "E2"], ["US-LOAN-01"]),
    ("v09-bad-category", {**REPORT_OK, "cause_category": "network"}, ["E1", "E2"], ["US-LOAN-01"]),
    ("v10-extra-property", {**REPORT_OK, "extra": 1}, ["E1", "E2"], ["US-LOAN-01"]),
]
NORMALISE = [("n01-low-confidence-explained", {**REPORT_OK, "confidence": 0.4}), ("n02-confidence-at-limit", {**REPORT_OK, "confidence": 0.5}), ("n03-abstain-unchanged", REPORT_ABSTAIN)]


def build(root: Path = REPO_ROOT, protocol_path: Path = P.PROTOCOL_PATH) -> dict[str, Any]:
    proto = P.load_protocol(protocol_path)
    schema = P.investigation_schema(root, proto)
    render = []
    for cid, desc, incident, pack, calls in RENDER_USER:
        render.append({"id": cid, "description": desc, "kind": "user", "input": {"incident": incident, "pack": pack, "tools": TOOLS, "entities": ENTITIES, "constraints": CONSTRAINTS, "calls": calls},
                       "expected": P.render_user(proto, incident, pack, TOOLS, ENTITIES, CONSTRAINTS, calls)})
    for cid, desc, kind, data in RENDER_TURNS:
        exp = (P.render_call(proto, data["tool"], data["arguments"]) if kind == "call" else P.render_result(proto, data[0], data[1]) if kind == "result"
               else P.render_report(proto, data) if kind == "report" else P.render_repair(proto, data))
        render.append({"id": cid, "description": desc, "kind": kind, "input": data if kind != "result" else {"envelope": data[0], "calls_left": data[1]}, "expected": exp})
    parse = []
    for cid, text in PARSE:
        t = P.parse_turn(text)
        parse.append({"id": cid, "output": text, "expected": {"kind": t.kind, "payload": t.payload}})
    validate = []
    for cid, report, ev, stories in VALIDATE:
        errs = P.validate_report(schema, report, set(ev), None if stories is None else set(stories))
        validate.append({"id": cid, "report": report, "evidence_ids": ev, "stories": stories, "expected_valid": not errs})
    normalise = [{"id": cid, "report": r, "expected": P.normalise_report(proto, r)} for cid, r in NORMALISE]
    catalog = {"tools": TOOLS}
    entity_names = [e["name"] for e in ENTITIES]
    grammar = [{"id": cid, "description": desc, "only_report": only,
                "input": {"tools": TOOLS, "entities": entity_names, "report_schema_path": proto["report"]["schema"], "report_def": proto["report"]["def"]},
                "expected": GR.turn_grammar(catalog, schema, entity_names, only)}
               for cid, desc, only in (("g01-call-or-report", "A turn that may be a call or the report", False), ("g02-report-only", "A turn constrained to the report", True))]
    return {"schema_version": 1, "protocol_sha256": P.protocol_sha256(protocol_path), "system_block_sha256": P.block_sha256(proto, root),
            "format": {"json": "compact separators (',', ':'), non-ASCII characters kept", "lengths": "Unicode code points"},
            "render": render, "parse": parse, "validate": validate, "normalise": normalise, "grammar": grammar}


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.investigator.golden", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--write", action="store_true")
    g.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    text = dumps_pretty(build())
    if args.write:
        GOLDEN_PATH.write_text(text, encoding="utf-8", newline="\n")
        print(f"wrote {GOLDEN_PATH}")
        return 0
    if not GOLDEN_PATH.exists() or GOLDEN_PATH.read_text(encoding="utf-8") != text:
        print("protocol.golden.json differs from the code: run --write after a reviewed protocol change", file=sys.stderr)
        return 1
    print("golden cases match")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
