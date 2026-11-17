"""Scenarios of the synthetic incident generator (operations.md 11.6): a world, one injected fault, a trigger and a gold answer.

``plan_scenario`` decides everything from the seed (class mix, fault, trigger, trajectory variant, budget). ``build_scenario`` renders
the world in memory and returns the incident, the pack, the oracle's calls and the gold. ``make_report`` writes the final report from
the gold with one of at least 10 phrasings of the cause. Deterministic: the same seed gives the same bytes.

Mix: about 60 percent answerable, 20 percent abstain (the cause lies in a source no tool covers, the evidence conflicts, or the budget
is too small), 10 percent budget pressure (answerable with exactly enough calls) and 10 percent expected behaviour.
"""
from __future__ import annotations

import random
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from typing import Any, Mapping, Sequence

from training.common.hashing import rng, sha256_text
from training.investigator import phrasings as PH
from training.investigator.masking import Masker
from training.investigator.world import BASE_DATE, Step, World, background, base_rows, guid, hex_id, iso, log_record

FAULT_TO_CATEGORY = {"access": "access", "data_missing": "data_missing", "data_conflict": "data_conflict", "dependency": "dependency_unavailable",
                     "configuration": "configuration", "capacity": "capacity", "code_defect": "code_defect", "external_input": "external_input",
                     "expected": "expected_behaviour"}
ANSWERABLE_FAULTS = ["access", "data_missing", "data_conflict", "dependency", "configuration", "capacity", "code_defect", "external_input"]
CLASSES = [("answerable", 0.60), ("abstain", 0.20), ("budget_pressure", 0.10), ("expected_behaviour", 0.10)]
ABSTAIN_KINDS = [("out_of_scope", 0.4), ("insufficient", 0.2), ("conflicting", 0.2), ("budget", 0.2)]
# The values of the report's `trigger` (incident-report.schema.json). "severity" stands for an incident whose trigger is not residual and whose runbook
# check failed: a failed rule check is a residual condition (operations.md 11.1), not a trigger value, and shows in `rule_checks`.
TRIGGERS = [("unknown", 0.35), ("rate", 0.25), ("noise_out_of_context", 0.15), ("severity", 0.25)]
VARIANTS = ["plain", "extra_call", "recover", "duplicate", "reordered"]
ABSTAIN_REASON = {"out_of_scope": "out_of_scope", "insufficient": "insufficient_evidence", "conflicting": "conflicting_evidence", "budget": "budget_exhausted"}


def _weighted(r: random.Random, table: Sequence[tuple[str, float]]) -> str:
    return r.choices([k for k, _ in table], weights=[w for _, w in table], k=1)[0]


@dataclass
class Plan:
    id: str
    domain: str
    split: str
    seed_key: str
    klass: str                          # answerable | abstain | budget_pressure | expected_behaviour
    fault: str
    abstain_kind: str | None
    trigger: str
    variant: str
    budget_choice: int                  # 6 or 10, the tier-like budget used when no override applies
    misleading_runbook: bool


def plan_scenario(master_seed: int, spec: Mapping[str, Any], index: int, split: str) -> Plan:
    sid = f"{spec['id']}-{split}-{index:06d}"
    r = rng(master_seed, f"{sid}/plan")
    klass = _weighted(r, CLASSES)
    abstain = None
    if klass == "expected_behaviour":
        fault = "expected"
    elif klass == "abstain":
        abstain = _weighted(r, ABSTAIN_KINDS)
        fault = {"out_of_scope": "component", "insufficient": "component", "conflicting": "access", "budget": "data_missing"}[abstain]
    elif klass == "budget_pressure":
        fault = r.choice(["access", "data_missing", "data_conflict", "dependency", "capacity"])
    else:
        fault = r.choice(ANSWERABLE_FAULTS)
    trigger = "rate" if fault == "expected" else "unknown" if abstain == "insufficient" else _weighted(r, TRIGGERS)
    variant = r.choice(VARIANTS) if klass in ("answerable", "expected_behaviour") else "plain"
    return Plan(sid, spec["id"], split, sid, klass, fault, abstain, trigger, variant, r.choice([6, 10]), klass == "answerable" and trigger in ("rate", "severity") and r.random() < 0.08)


@dataclass
class Built:
    plan: Plan
    world: World
    incident: dict[str, Any]
    pack: Any
    steps: list[Step]
    gold: dict[str, Any]
    budget_override: int | None
    cause_vars: dict[str, str]
    trace_id: str
    incident_id: str


def _frames(spec: Mapping[str, Any], log: Mapping[str, Any], r: random.Random) -> list[str]:
    svc = log["category"].rsplit(".", 1)[1]
    return [f"{spec['namespace']}.{svc}.{r.choice(['Handle', 'Process', 'Save'])}Async", f"{spec['namespace']}.{svc}.Validate", "System.Threading.Tasks.Task.Run"]


def build_scenario(plan: Plan, spec: Mapping[str, Any], master_seed: int, repo_root=None) -> Built:
    """Render the world of a plan in memory."""
    from training.common.config import REPO_ROOT
    repo_root = repo_root or REPO_ROOT
    r = rng(master_seed, f"{plan.id}/world")
    t0 = BASE_DATE + timedelta(days=r.randint(0, 60), minutes=r.randint(0, 300))
    site, admin, acct, other = guid(r), guid(r), guid(r), guid(r)
    members = [guid(r), guid(r), guid(r)]
    rec = spec["record"]
    logdef = next(l for l in spec["logs"] if l["fault"] == plan.fault)
    area = next(a for a in spec["areas"] if a["id"] == logdef["area"])
    ver_after, ver_before = spec["version_after"], spec["version_before"]
    trace, trace2 = hex_id(r, 32), hex_id(r, 32)
    rows = base_rows(spec, r, site, [acct, other, admin], members, t0)
    rid = guid(r)
    audit: list[dict[str, Any]] = []
    health = {"status": "Healthy", "checks": [{"name": "database", "status": "Healthy", "duration_ms": r.randint(5, 25)}, {"name": spec["dependencies"][0]["name"], "status": "Healthy", "duration_ms": r.randint(20, 90)}]}
    dep = spec["dependencies"][0]["name"]
    setting = spec["settings"][0]
    source = r.choice(["csv-import", "partner-feed", "mobile-upload", "web-form"])
    component = r.choice(spec["components"])
    category = category_of = FAULT_TO_CATEGORY.get(plan.fault, "unknown")
    steps: list[Step] = []
    cause_vars = {"when": (t0 - timedelta(minutes=7)).strftime("%H:%M"), "dependency": dep, "setting": setting, "source": source, "component": component, "version": ver_after,
                  "record": rec.replace("_", " "), "area": area["title"].lower(), "action": area.get("verb", "save")}
    props: dict[str, str] = {}
    exception = None
    extra_recs: list[dict[str, Any]] = []
    level = logdef["level"]

    def audit_row(t: datetime, etype: str, eid: str, action: str, user: str, changed: list[str], old: Mapping[str, Any], new: Mapping[str, Any]) -> dict[str, Any]:
        return {"Timestamp": iso(t), "EntityType": etype, "EntityId": eid, "Action": action, "UserId": user, "ChangedProperties": changed, "OldValues": dict(old),
                "NewValues": dict(new), "CorrelationId": hex_id(r, 32)}

    fault = plan.fault
    if fault == "access":
        m = members[0]
        props = {"UserId": acct, "SiteId": site, "MembershipId": m}
        rows["membership"][0].update(AccountId=acct, Status="Active" if plan.abstain_kind == "conflicting" else "Removed")
        if plan.abstain_kind == "conflicting":
            audit.append(audit_row(t0 - timedelta(days=3), "SiteMember", m, "Create", admin, ["Status"], {}, {"Status": "Active"}))
            health["status"], health["checks"][1]["status"], health["checks"][1]["duration_ms"] = "Unhealthy", "Unhealthy", 30000
            extra_recs.append(log_record(spec, r, t0 - timedelta(seconds=2), "Error", logdef["category"], "Call to {Dependency} failed", {"Dependency": dep, "ResultCode": "503", "DurationMs": "30000"},
                                         None, trace, ver_after))
            steps = [Step("logs_by_correlation", {"trace_id": trace}), Step("audit_trail_by_entity", {"entity": "membership", "id": m}), Step("health_status", {})]
        else:
            audit.append(audit_row(t0 - timedelta(minutes=7), "SiteMember", m, "Update", admin, ["Status"], {"Status": "Active"}, {"Status": "Removed"}))
            steps = [Step("logs_by_correlation", {"trace_id": trace}), Step("audit_trail_by_entity", {"entity": "membership", "id": m}, True)]
    elif fault == "data_missing":
        props = {"RecordId": rid, "SiteId": site}
        audit.append(audit_row(t0 - timedelta(days=2), rec_audit(spec), rid, "Create", acct, ["Status"], {}, {"Status": "Open"}))
        audit.append(audit_row(t0 - timedelta(minutes=9), rec_audit(spec), rid, "Delete", other, ["Status"], {"Status": "Open"}, {}))
        steps = [Step("sql_entity_read", {"entity": rec, "id": rid}, True), Step("audit_trail_by_entity", {"entity": rec, "id": rid}, True)]
    elif fault == "data_conflict":
        rid1 = guid(r)
        ref = "REF" + str(r.randint(10000, 99999))
        props = {"RecordId": rid, "ExternalRef": ref, "ConflictWith": rid1}
        for ident, who, dt in ((rid1, acct, 3), (rid, other, 1)):
            rows[rec].append({"Id": ident, "SiteId": site, "OwnerAccountId": who, "Status": "Open", "Reference": ref, "Quantity": 1, "DueDate": iso(t0 + timedelta(days=3)), "Notes": "", "CreatedAt": iso(t0 - timedelta(seconds=dt)), "UpdatedAt": iso(t0)})
            audit.append(audit_row(t0 - timedelta(seconds=dt), rec_audit(spec), ident, "Create", who, ["Reference"], {}, {"Reference": ref}))
        extra_recs.append(log_record(spec, r, t0 - timedelta(seconds=3), "Information", logdef["category"], "{Record} {RecordId} created", {"Record": rec, "RecordId": rid1}, None, trace, ver_after))
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True), Step("audit_trail_by_entity", {"entity": rec, "id": rid1}, True)]
    elif fault == "dependency":
        props = {"Dependency": dep, "RecordId": rid, "SiteId": site}
        health["status"] = "Degraded"
        health["checks"][1].update(status="Unhealthy", duration_ms=30000)
        extra_recs.append(log_record(spec, r, t0 - timedelta(seconds=1), "Error", logdef["category"], "Dependency call {Dependency} finished", {"Dependency": dep, "ResultCode": "503", "DurationMs": "30000"}, None, trace, ver_after))
        exception = {"Type": "System.TimeoutException", "Message": f"The call to {dep} timed out", "Frames": _frames(spec, logdef, r)}
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True), Step("health_status", {}, True)]
    elif fault == "configuration":
        props = {"Setting": setting, "SiteId": site}
        exception = {"Type": "System.InvalidOperationException", "Message": f"Setting {setting} is missing or empty", "Frames": _frames(spec, logdef, r)}
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True)]
    elif fault == "capacity":
        props = {"DurationMs": str(r.randint(29000, 45000)), "SiteId": site}
        health["status"] = "Degraded"
        health["checks"][0].update(status="Degraded", duration_ms=r.randint(4500, 9000))
        for k in range(3):
            extra_recs.append(log_record(spec, r, t0 - timedelta(seconds=k + 1), "Error", logdef["category"], "Request timed out after {DurationMs} ms for site {SiteId}", {"DurationMs": str(r.randint(29000, 45000)), "SiteId": site}, logdef["key"], trace2, ver_after))
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True), Step("health_status", {}, True)]
    elif fault == "code_defect":
        props = {"RecordId": rid, "SiteId": site}
        exception = {"Type": "System.NullReferenceException", "Message": "Object reference not set to an instance of an object", "Frames": _frames(spec, logdef, r)}
        extra_recs.append(log_record(spec, r, t0 - timedelta(days=5), "Information", logdef["category"], "{Record} {RecordId} handled", {"Record": rec, "RecordId": guid(r)}, None, hex_id(r, 32), ver_before))
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True)]
    elif fault == "external_input":
        props = {"Source": source, "SiteId": site}
        exception = {"Type": "System.FormatException", "Message": "Input string was not in a correct format", "Frames": _frames(spec, logdef, r)}
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True)]
    elif fault == "expected":
        props = {"SiteId": site, "Source": source}
        steps = [Step("logs_by_correlation", {"trace_id": trace}, True), Step("health_status", {}, True)]
    else:   # component: the cause lies in a source no tool covers
        if plan.abstain_kind == "out_of_scope":
            props = {"Component": component, "RecordId": rid}
            exception = {"Type": "System.InvalidOperationException", "Message": "Operation failed", "Frames": _frames(spec, logdef, r)}
        else:
            props = {"RecordId": rid}
        steps = [Step("logs_by_correlation", {"trace_id": trace}), Step("health_status", {}) if plan.abstain_kind == "out_of_scope" else Step("signature_lookup", {"log_key": logdef["key"]})]

    template = logdef["template"]
    key_rec = log_record(spec, r, t0, level, logdef["category"], template, props, logdef["key"], trace, ver_after, exception)
    key_rec2 = log_record(spec, r, t0 - timedelta(minutes=2), level, logdef["category"], template, props, logdef["key"], trace2, ver_after)
    logs = [key_rec, key_rec2] + extra_recs + background(spec, r, t0, site, 6)
    world = World(spec, logs, rows, audit, health)

    # triggers, signature, runbook
    rb = next(x for x in spec["runbooks"] if x["log_key"] == logdef["key"])
    keyed = plan.trigger != "unknown"
    claimed = category
    if plan.misleading_runbook:
        claimed = r.choice([c for c in PH.CAUSES if c != category])
    sig = signature_record(spec, logdef, rb, claimed, plan, ver_after) if keyed else None
    world.signatures = [signature_record(spec, logdef, rb, claimed, plan, ver_after)] if keyed else []
    world.runbook_text = {rb["anchor"]: runbook_markdown(rb, logdef)}
    masker = Masker(__import__("json").loads((repo_root / "docs" / "support" / "masking-policy.json").read_text(encoding="utf-8")))
    count = r.randint(5, 80)
    rate = r.randint(60, 400) if plan.trigger == "rate" or fault == "expected" else r.randint(1, 20)
    incident: dict[str, Any] = {
        "trigger": plan.trigger, "severity": level, "environment": "prod",
        "signature": ({"kind": "keyed", "id": logdef["key"], "log_key": logdef["key"], "template_id": None, "template": None} if keyed else
                      {"kind": "template", "id": None, "log_key": None, "template_id": "t-" + sha256_text(logdef["key"] + template)[:12], "template": template}),
        "count": count, "rate_per_hour": rate, "first_seen": iso(t0 - timedelta(minutes=40)), "last_seen": iso(t0),
        "evidence": [{"id": "E1", "source": "signal", "records": [masker.mask_signal(key_rec), masker.mask_signal(key_rec2)]}]}
    if plan.trigger == "severity":                       # severity escalation whose runbook check failed (11.1)
        tool, args = (steps[1] if len(steps) > 1 else steps[0]).tool, (steps[1] if len(steps) > 1 else steps[0]).arguments
        incident["rule_checks"] = [{"tool": tool, "expect": "data absent" if fault in ("data_missing",) else "status == Healthy", "passed": False}]
    pack = {"signature": _pack_signature(sig), "runbook": world.runbook_text[rb["anchor"]]} if keyed else None
    budget_override = None
    if plan.klass == "budget_pressure":
        budget_override = len([s for s in steps])
    if plan.abstain_kind == "budget":
        steps, budget_override = steps[:1], 1
    steps = apply_variant(plan, steps, r, logdef, trace, spec, budget_override)
    reason = ABSTAIN_REASON.get(plan.abstain_kind or "")
    supporting = [] if reason else [{"tool": s.tool, "arguments": s.arguments} for s in steps if s.supporting]
    gold = {"verdict": "abstain" if reason else "explained", "abstain_reason": reason, "cause_category": "unknown" if reason else category,
            "area": logdef["area"] if keyed else None, "user_story": logdef["story"] if keyed else None, "supporting_evidence": supporting,
            "min_calls": len(base_steps(plan, steps))}
    return Built(plan, world, incident, pack, steps, gold, budget_override, cause_vars, trace, "INC-" + plan.id)


def rec_audit(spec: Mapping[str, Any]) -> str:
    return next(e["audit_entity_type"] for e in spec["entities"] if e["name"] == spec["record"])


def base_steps(plan: Plan, steps: Sequence[Step]) -> list[Step]:
    """The calls the plain oracle path needs: variants add calls that are not required (a bad first try, a duplicate, an irrelevant call)."""
    seen: set[str] = set()
    out = []
    for s in steps:
        k = s.tool + repr(sorted(s.arguments.items()))
        if s.extra or k in seen or s.bad:
            continue
        seen.add(k)
        out.append(s)
    return out


def apply_variant(plan: Plan, steps: list[Step], r: random.Random, logdef: Mapping[str, Any], trace: str, spec: Mapping[str, Any], budget_override: int | None) -> list[Step]:
    """One variant per scenario: an irrelevant call, a recovery after a denied call, a duplicate attempt or a different order."""
    v = plan.variant
    if v == "plain" or budget_override is not None:
        return steps
    out = list(steps)
    if v == "extra_call":
        used = {s.tool for s in steps}
        filler = Step("health_status", {}, extra=True) if "health_status" not in used else Step("signature_lookup", {"log_key": logdef["key"]}, extra=True)
        out.insert(len(out) - 1, filler)
    elif v == "recover":
        i = next((k for k, s in enumerate(steps) if s.tool != "health_status"), None)
        if i is not None:
            s = steps[i]
            bad = dict(s.arguments)
            if s.tool == "logs_by_correlation":
                bad["limit"] = 500          # above the tool's maximum of 200: the executor denies it
            elif "entity" in bad:
                bad["entity"] = bad["entity"].capitalize()
            elif s.tool == "signature_lookup":
                bad = {"log_key": logdef["key"], "template_id": "t-000000000000"}
            wrong = Step(s.tool, bad, False, bad=True)
            out.insert(i, wrong)
    elif v == "duplicate" and steps:
        i = r.randrange(len(steps))
        dup = Step(steps[i].tool, dict(steps[i].arguments), False, extra=True)
        out.insert(i + 1, dup)
    elif v == "reordered" and len(steps) >= 2 and not any(s.tool == "signature_lookup" for s in steps):
        out[0], out[1] = out[1], out[0]
    return out


def signature_record(spec: Mapping[str, Any], logdef: Mapping[str, Any], rb: Mapping[str, Any], category: str, plan: Plan, version: str) -> dict[str, Any]:
    noise = plan.trigger == "noise_out_of_context" or plan.fault == "expected"
    return {"id": logdef["key"], "kind": "keyed", "match": {"log_key": logdef["key"]}, "area": logdef["area"], "user_story": logdef["story"], "test_case": logdef["test_case"],
            "severity": logdef["severity"], "message": logdef["template"], "call_site": {"file": "src/Services.cs", "line": 10, "method": "Handle", "template": logdef["template"], "parameters": []},
            "runbook": {"anchor": rb["anchor"], "path": f"docs/areas/{logdef['area']}/operations.md", "text": runbook_markdown(rb, logdef)},
            "rate": {"expected_per_hour": rb["expected_rate_per_hour"]}, "fingerprint_by": rb["fingerprint_by"], "cause_category": category,
            "status": "noise" if noise else "active", "fixed_in": None,
            "noise": {"rationale": "Known and accepted at busy times.", "expires": "2027-12-31"} if noise else None, "rule_checks": [], "history": []}


def _pack_signature(sig: Mapping[str, Any] | None) -> dict[str, Any] | None:
    if sig is None:
        return None
    out = {k: v for k, v in sig.items() if k != "call_site"}
    return out


def runbook_markdown(rb: Mapping[str, Any], logdef: Mapping[str, Any]) -> str:
    return (f"### Meaning\n\n{rb['meaning']}\n\n### Check\n\nRead the logs of the request and the record named in the entry.\n\n### Rule checks\n\nNone.\n\n"
            f"### Resolve\n\nFollow the cause found in the evidence; hand over to development when it is a defect.\n\n### History\n\nNone.\n")


# ---------------------------------------------------------------- report

def make_report(plan: Plan, built: Built, evidence: Mapping[str, str], r: random.Random) -> dict[str, Any]:
    """The final report from the gold. ``evidence`` maps ``"{tool}|{canonical args}"`` of supporting calls to their evidence ids."""
    g = built.gold
    refs = sorted({evidence[k] for k in evidence}, key=lambda e: int(e[1:]))
    if g["verdict"] == "abstain":
        report = {"verdict": "abstain", "abstain_reason": g["abstain_reason"], "cause_category": "unknown", "cause": None, "confidence": round(r.uniform(0.1, 0.4), 2),
                  "evidence_refs": [], "area": g["area"], "user_story": g["user_story"], "reproduction": None,
                  "next_checks": [t.format(**built.cause_vars) for t in PH.NEXT_CHECKS["unknown"]][:2]}
        return report
    cat = g["cause_category"]
    cause = r.choice(PH.CAUSES[cat]).format(**built.cause_vars)
    repro = PH.REPRO.get(cat)
    reproduction = None if repro is None else {"preconditions": [s.format(**built.cause_vars) for s in repro["preconditions"]], "steps": [s.format(**built.cause_vars) for s in repro["steps"]],
                                               "expected": repro["expected"].format(**built.cause_vars), "observed": repro["observed"].format(**built.cause_vars)}
    refs = (["E1"] if cat in ("code_defect", "external_input", "configuration") else []) + refs
    return {"verdict": "explained", "abstain_reason": None, "cause_category": cat, "cause": cause[:300], "confidence": round(r.uniform(0.62, 0.95), 2),
            "evidence_refs": refs[:10] or ["E1"], "area": g["area"], "user_story": g["user_story"], "reproduction": reproduction,
            "next_checks": [t.format(**built.cause_vars) for t in PH.NEXT_CHECKS[cat]][:5]}
