"""The investigation loop used by the evaluation: a Python port of the host's rules (operations.md 11.3).

One investigation = system and user messages, then assistant turns (each exactly one call or one report) and tool results, until a
report. Stop rules, enforced here as the host enforces them:

| Rule | Host action |
|------|-------------|
| Calls left reach 0 | Next turn constrained to the report |
| Same tool and arguments twice | Result ``duplicate``; on the second duplicate the next turn is constrained to the report |
| Two consecutive results ``error``, ``denied`` or ``timeout`` | Next turn constrained to the report |
| Wall time reached | Stop; investigation ``failed``; rule verdict kept |
| Context above 75 % of the model's length | Oldest results replaced by ``{"evidence_id": ..., "summarised": true, "rows": n}`` |

After the report: schema, evidence references, user story in the pack, confidence below 0.5 rewritten to abstain; one repair turn with the
validation error, a second failure marks the investigation ``failed``. Tool results come from a ``ToolRunner`` (the monitor's executor).
"""
from __future__ import annotations

import json
import re
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from training.common.backends import AnswerBackend
from training.common.hashing import canonical_sha256
from training.investigator import grammar as GR, protocol as P
from training.investigator.tools import ToolRunner


@dataclass
class Investigation:
    status: str                                   # "ok" | "failed"
    report: dict[str, Any] | None = None
    calls: list[dict[str, Any]] = field(default_factory=list)       # {"tool", "arguments", "status", "evidence_id"}
    report_valid_first_time: bool = False
    repaired: bool = False
    budget_exhausted: bool = False
    duration_s: float = 0.0
    error: str | None = None
    turns: list[dict[str, str]] = field(default_factory=list)


def summarise_old_results(messages: list[dict[str, str]], result_role: str, context_tokens: int, fraction: float,
                          count_tokens: Callable[[str], int] = lambda t: round(len(t) / 3.5)) -> list[dict[str, str]]:
    """Replace the oldest results by their summary until the conversation is within ``fraction`` of ``context_tokens``."""
    msgs = [dict(m) for m in messages]
    total = sum(count_tokens(m["content"]) for m in msgs)
    for m in msgs:
        if total <= fraction * context_tokens:
            break
        if m["role"] != result_role or "summarised" in m["content"]:
            continue
        match = re.match(r'^<result>(.*)</result>\n(<budget calls="\d+" />)$', m["content"], re.S)
        if not match:
            continue
        env = json.loads(match.group(1))
        new = f'<result>{P.dumps({"evidence_id": env.get("evidence_id"), "summarised": True, "rows": env.get("rows", 0)})}</result>\n{match.group(2)}'
        total += count_tokens(new) - count_tokens(m["content"])
        m["content"] = new
    return msgs


def investigate(backend: AnswerBackend, scenario: Mapping[str, Any], *, world_dir: Path, runner: ToolRunner, protocol: Mapping[str, Any], system: str,
                catalog: Mapping[str, Any], constraints: Sequence[Mapping[str, str]], schema: Mapping[str, Any], budget: int, timeout_s: float = 600.0,
                context_tokens: int | None = None, clock: Callable[[], float] = time.monotonic, use_grammar: bool = True,
                incident_id: str | None = None) -> Investigation:
    """Run one investigation of a scenario file (see ``generate.scenario_record``) against its world."""
    prompt = scenario["prompt"]
    entities = prompt["entities"]
    entity_names = [e["name"] for e in entities]
    stories = {prompt["pack"]["signature"]["user_story"]} if prompt["pack"] else None
    messages = [{"role": "system", "content": system},
                {"role": "user", "content": P.render_user(protocol, prompt["incident"], prompt["pack"], catalog["tools"], entities, constraints, budget)}]
    stop = protocol["stop_rules"]
    t0 = clock()
    inv = Investigation("ok")
    calls_left, call_no = budget, 0
    evidence: set[str] = {"E1"}
    seen: dict[str, str] = {}
    dups, fails, force_report, repairs = 0, 0, False, 0
    grammars: dict[bool, str] = {}
    max_new = protocol["decoding"]["max_new_tokens"]
    while True:
        if clock() - t0 > timeout_s:
            inv.status, inv.error = "failed", "wall time reached"
            break
        only_report = force_report or calls_left <= 0
        constraint: dict[str, Any] | None = None
        if use_grammar:
            if only_report not in grammars:
                grammars[only_report] = GR.turn_grammar(catalog, schema, entity_names, only_report)
            constraint = {"grammar": grammars[only_report], "kind": "report" if only_report else "call|report"}
        view = summarise_old_results(messages, protocol["result_role"], context_tokens, stop["context_summary_fraction"]) if context_tokens else messages
        res = backend.generate(view, max_new_tokens=max_new, constraint=constraint)
        turn = P.parse_turn(res.text)
        messages.append({"role": "assistant", "content": res.text})
        if turn.kind == "call" and not only_report:
            tool, args = turn.payload["tool"], turn.payload["arguments"]
            call_no += 1
            key = canonical_sha256({"tool": tool, "arguments": args})
            errors = P.tool_call_errors(catalog, entity_names, tool, args)
            env = runner.call(world_dir, tool, args, incident_id or scenario["incident_id"], call_no)
            if errors and env.get("status") != "denied":
                env = {**env, "status": "denied", "error": errors[0]}
            calls_left -= 1
            if env["status"] in ("ok", "empty"):
                evidence.add(env["evidence_id"])
            elif env["status"] == "duplicate":
                dups += 1
            fails = fails + 1 if env["status"] in P.FAILED_STATUSES else 0
            seen[key] = env.get("evidence_id", "")
            inv.calls.append({"tool": tool, "arguments": args, "status": env["status"], "evidence_id": env.get("evidence_id")})
            messages.append({"role": protocol["result_role"], "content": P.render_result(protocol, env, calls_left)})
            if calls_left <= 0:
                inv.budget_exhausted = True
            force_report = calls_left <= 0 or fails >= stop["consecutive_failures"] or dups >= stop["duplicate_limit"]
            continue
        # a report (or something that is not a valid turn, which counts as an invalid report)
        errors = [turn.error or "not a report"] if turn.kind != "report" else P.validate_report(schema, turn.payload, evidence, stories)
        if turn.kind == "call":
            errors = ["the next turn must be the report"]
        if not errors:
            inv.report = P.normalise_report(protocol, turn.payload)
            inv.report_valid_first_time = repairs == 0
            inv.repaired = repairs > 0
            break
        if repairs >= protocol["report"]["repair_turns"]:
            inv.status, inv.error, inv.repaired = "failed", "invalid report after the repair turn: " + "; ".join(errors)[:200], True
            break
        repairs += 1
        force_report = True
        messages.append({"role": "user", "content": P.render_repair(protocol, errors)})
    inv.duration_s = clock() - t0
    inv.turns = messages[2:]
    return inv
