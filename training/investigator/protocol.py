"""Renderer, parser and validators of the investigator protocol (operations.md 11.3).

``protocol.json`` is the single definition of the turn formats and stop-rule numbers; the rules text itself is the block
between the markers in ``.github/agents/support.agent.md`` and is read from there, never copied. All JSON inside turns is
compact (``separators=(",", ":")``, characters kept), keys in the order given. Other runtimes (the monitor's C# host) must
reproduce ``protocol.golden.json`` byte for byte.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

from training.common import schema as S
from training.common.config import REPO_ROOT
from training.common.hashing import sha256_bytes
from training.common.jsonio import load_json

PROTOCOL_PATH = Path(__file__).with_name("protocol.json")
CAUSE_CATEGORIES = ["access", "data_missing", "data_conflict", "dependency_unavailable", "configuration", "capacity", "code_defect",
                    "external_input", "expected_behaviour", "unknown"]
ABSTAIN_REASONS = ["insufficient_evidence", "conflicting_evidence", "out_of_scope", "budget_exhausted"]
FAILED_STATUSES = ("error", "denied", "timeout")
_CALL = re.compile(r"^<call>(.*)</call>$", re.S)
_REPORT = re.compile(r"^<report>(.*)</report>$", re.S)


def load_protocol(path: str | Path = PROTOCOL_PATH) -> dict[str, Any]:
    return load_json(path)


def dumps(obj: Any) -> str:
    return json.dumps(obj, separators=(",", ":"), ensure_ascii=False)


def protocol_sha256(path: str | Path = PROTOCOL_PATH) -> str:
    return sha256_bytes(Path(path).read_bytes())


# ---------------------------------------------------------------- the rules block

def block_bytes(protocol: Mapping[str, Any], root: Path = REPO_ROOT) -> bytes:
    """The bytes between the markers (what the lock hashes, models.md 8.1). Raises ``ValueError`` unless each marker occurs once."""
    src = (root / protocol["system"]["source"]).read_bytes()
    begin, end = protocol["system"]["begin"].encode(), protocol["system"]["end"].encode()
    if src.count(begin) != 1 or src.count(end) != 1:
        raise ValueError(f"the protocol markers must occur exactly once in {protocol['system']['source']} (check O09)")
    a = src.index(begin) + len(begin)
    b = src.index(end)
    if b < a:
        raise ValueError("the end marker precedes the begin marker")
    return src[a:b]


def block_sha256(protocol: Mapping[str, Any], root: Path = REPO_ROOT) -> str:
    return sha256_bytes(block_bytes(protocol, root))


def system_text(protocol: Mapping[str, Any], root: Path = REPO_ROOT) -> str:
    """The system message: the block between the markers without the surrounding line breaks, LF line ends."""
    return block_bytes(protocol, root).decode("utf-8").replace("\r\n", "\n").strip("\n")


# ---------------------------------------------------------------- rendering

def render_user(protocol: Mapping[str, Any], incident: Any, pack: Any, tools: Sequence[Mapping[str, Any]], entities: Sequence[Mapping[str, Any]],
                constraints: Sequence[Mapping[str, str]], calls: int) -> str:
    """The first user turn. ``tools`` hold name, description and parameters; ``entities`` name and description."""
    t = protocol["turns"]
    tool_json = dumps({"tools": [{"name": x["name"], "description": x["description"], "parameters": x["parameters"]} for x in tools],
                       "entities": [{"name": e["name"], "description": e["description"]} for e in entities]})
    lines = "\n".join(t["constraint_line"].replace("{id}", c["id"]).replace("{text}", c["text"]) for c in constraints)
    return _fill(t["user"], {"incident": dumps(incident), "pack": dumps(pack), "tools": tool_json, "constraints": lines, "calls": str(calls)})


def render_call(protocol: Mapping[str, Any], tool: str, arguments: Mapping[str, Any]) -> str:
    return _fill(protocol["turns"]["call"], {"call": dumps({"tool": tool, "arguments": arguments})})


def render_report(protocol: Mapping[str, Any], report: Mapping[str, Any]) -> str:
    return _fill(protocol["turns"]["report"], {"report": dumps(report)})


def render_result(protocol: Mapping[str, Any], envelope: Mapping[str, Any], calls_left: int) -> str:
    return _fill(protocol["turns"]["result"], {"result": dumps(envelope), "calls": str(max(0, calls_left))})


def render_repair(protocol: Mapping[str, Any], errors: Sequence[str]) -> str:
    """The user turn of the one repair allowed after an invalid report (11.3)."""
    return _fill(protocol["turns"]["repair"], {"errors": "; ".join(errors)[:600]})


def _fill(template: str, values: Mapping[str, str]) -> str:
    """One left-to-right pass; substituted values are not scanned again."""
    return re.sub(r"\{(\w+)\}", lambda m: values[m.group(1)] if m.group(1) in values else m.group(0), template)


# ---------------------------------------------------------------- parsing

@dataclass
class Turn:
    kind: str                            # "call" | "report" | "invalid"
    payload: dict[str, Any] | None = None
    error: str | None = None


def parse_turn(text: str) -> Turn:
    """An assistant turn is exactly one ``<call>{json}</call>`` or ``<report>{json}</report>`` (surrounding whitespace ignored)."""
    t = text.replace("\r\n", "\n").strip()
    for rx, kind in ((_CALL, "call"), (_REPORT, "report")):
        m = rx.match(t)
        if not m:
            continue
        try:
            data = json.loads(m.group(1))
        except json.JSONDecodeError as exc:
            return Turn("invalid", None, f"{kind} is not JSON: {exc.msg}")
        if not isinstance(data, dict):
            return Turn("invalid", None, f"{kind} is not an object")
        if kind == "call" and (set(data) != {"tool", "arguments"} or not isinstance(data["tool"], str) or not isinstance(data["arguments"], dict)):
            return Turn("invalid", None, 'a call has exactly "tool" and "arguments"')
        return Turn(kind, data)
    return Turn("invalid", None, "neither a call nor a report")


# ---------------------------------------------------------------- validation

def investigation_schema(root: Path = REPO_ROOT, protocol: Mapping[str, Any] | None = None) -> dict[str, Any]:
    protocol = protocol or load_protocol()
    doc = load_json(root / protocol["report"]["schema"])
    schema = dict(doc["$defs"][protocol["report"]["def"].rsplit("/", 1)[1]])
    schema["$defs"] = doc["$defs"]
    return schema


def validate_report(schema: Mapping[str, Any], report: Any, evidence_ids: set[str], stories: set[str] | None) -> list[str]:
    """Validation after the report (11.3): schema, every ``evidence_ref`` exists, ``user_story`` is in the pack or ``null``, and the
    field rules of the table. ``stories`` is ``None`` when there is no pack."""
    errors = S.validate(dict(schema), report)
    if errors or not isinstance(report, dict):
        return errors
    for ref in report["evidence_refs"]:
        if ref not in evidence_ids:
            errors.append(f"evidence_refs: {ref} was not received")
    story = report["user_story"]
    if story is not None and (stories is None or story not in stories):
        errors.append(f"user_story {story} is not in the pack")
    if report["verdict"] == "explained":
        if report["abstain_reason"] is not None:
            errors.append("abstain_reason must be null when explained")
        if not report["evidence_refs"]:
            errors.append("an explained report cites at least one evidence id")
        if report["cause"] is None:
            errors.append("an explained report has a cause")
    else:
        if report["abstain_reason"] is None:
            errors.append("an abstain report has an abstain_reason")
        if report["cause"] is not None:
            errors.append("cause must be null when abstaining")
    return errors


def normalise_report(protocol: Mapping[str, Any], report: Mapping[str, Any]) -> dict[str, Any]:
    """``confidence`` below the minimum with ``explained`` is rewritten to ``abstain`` (``insufficient_evidence``)."""
    out = dict(report)
    if out["verdict"] == "explained" and out["confidence"] < protocol["report"]["min_confidence_explained"]:
        out["verdict"], out["abstain_reason"] = "abstain", "insufficient_evidence"
    return out


def tool_call_errors(catalog: Mapping[str, Any], entity_names: Sequence[str], tool: str, arguments: Mapping[str, Any]) -> list[str]:
    """Executor-side validation of a call against the catalog (7.3): known tool, schema, entity enum, one-of for signature_lookup."""
    spec = next((t for t in catalog["tools"] if t["name"] == tool), None)
    if spec is None:
        return [f"unknown tool {tool}"]
    schema = spec["parameters"]
    errors = S.validate(schema, dict(arguments))
    for name, sub in schema.get("properties", {}).items():
        if sub.get("x-enum-from") == "entities" and name in arguments and arguments[name] not in entity_names:
            errors.append(f"$.{name}: {arguments[name]!r} is not an entity of the tool list")
    if tool == "signature_lookup" and sum(k in arguments for k in ("signature_id", "log_key", "template_id")) != 1:
        errors.append("exactly one of signature_id, log_key, template_id")
    return errors
