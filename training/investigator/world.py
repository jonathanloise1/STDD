"""The seeded world simulator of operations.md 11.6.

A world spec (``worlds/{domain}.json``) plus a fault and a seed render a world directory:

    logs.jsonl                  format 3.4, synthetic personal data present
    entities/{entity}.jsonl     one row per record
    audit.jsonl                 changes with old and new values
    health.json                 status and checks
    pack/                       signatures.json, entities.json, masking-policy.json, tools.json, constraints.json, runbooks/

Everything is a pure function of the spec, the fault and the seed (deterministic). Nothing here formats a tool result: results
come from the monitor's executor over this directory (``tools.py``).
"""
from __future__ import annotations

import json
import random
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Mapping

from training.common.config import REPO_ROOT
from training.common.hashing import sha256_text
from training.common.jsonio import dumps_line, load_json
from training.investigator import phrasings as PH

BASE_DATE = datetime(2026, 9, 1, 8, 0, 0, tzinfo=timezone.utc)
HOSTS = ["app01", "app02", "app03"]


def iso(t: datetime) -> str:
    return t.strftime("%Y-%m-%dT%H:%M:%S.") + f"{t.microsecond // 1000:03d}Z"


def guid(r: random.Random) -> str:
    h = "".join(r.choice("0123456789abcdef") for _ in range(32))
    return f"{h[:8]}-{h[8:12]}-4{h[13:16]}-a{h[17:20]}-{h[20:]}"


def hex_id(r: random.Random, n: int) -> str:
    return "".join(r.choice("0123456789abcdef") for _ in range(n))


def person(r: random.Random) -> dict[str, str]:
    first, last = r.choice(PH.FIRST), r.choice(PH.LAST)
    email = f"{first.lower()}.{last.lower()}@{r.choice(PH.DOMAINS)}"
    d = [str(r.randint(0, 9)) for _ in range(6)]
    phone = r.choice(PH.PHONE_FORMATS).format(**dict(zip("abcdef", d)))
    tax = "".join(r.choice(PH.TAX_CODE_LETTERS) for _ in range(6)) + f"{r.randint(50, 99)}A{r.randint(10, 28)}H{r.randint(100, 999)}Z"
    return {"first": first, "last": last, "email": email, "phone": phone, "tax": tax}


def note(r: random.Random) -> str:
    return r.choice(PH.NOTE_TEMPLATES).format(**person(r))


@dataclass
class Step:
    tool: str
    arguments: dict[str, Any]
    supporting: bool = False
    extra: bool = False          # a call the plain path does not need (irrelevant or duplicate)
    bad: bool = False            # a call with wrong arguments that the executor denies


@dataclass
class World:
    """Everything one scenario needs, before it is written to disk."""
    spec: Mapping[str, Any]
    logs: list[dict[str, Any]] = field(default_factory=list)
    entities: dict[str, list[dict[str, Any]]] = field(default_factory=dict)
    audit: list[dict[str, Any]] = field(default_factory=list)
    health: dict[str, Any] = field(default_factory=dict)
    signatures: list[dict[str, Any]] = field(default_factory=list)
    runbook_text: dict[str, str] = field(default_factory=dict)

    def entity_names(self) -> list[str]:
        return [e["name"] for e in self.spec["entities"]]


def log_record(spec: Mapping[str, Any], r: random.Random, t: datetime, level: str, category: str, template: str, props: Mapping[str, str],
               log_key: str | None, trace: str, version: str, exception: Mapping[str, Any] | None = None, event_id: int = 1) -> dict[str, Any]:
    msg = template
    for k, v in props.items():
        msg = msg.replace("{" + k + "}", str(v))
    rec: dict[str, Any] = {"Timestamp": iso(t), "Level": level, "Category": category, "EventId": event_id}
    if log_key:
        rec["LogKey"] = log_key
    rec.update(MessageTemplate=template, Message=msg, TraceId=trace, SpanId=hex_id(r, 16), Properties=dict(props))
    if exception:
        rec["Exception"] = dict(exception)
    rec.update(Service=spec["service"], Version=version, Host=r.choice(HOSTS), Environment="Production")
    return rec


def background(spec: Mapping[str, Any], r: random.Random, t0: datetime, site: str, n: int) -> list[dict[str, Any]]:
    """Healthy traffic of other requests: unkeyed information records with personal data in properties."""
    out = []
    for i in range(n):
        t = t0 - timedelta(minutes=r.randint(1, 50), seconds=r.randint(0, 59))
        area = r.choice(spec["areas"])
        p = person(r)
        out.append(log_record(spec, r, t, "Information", f"{spec['namespace']}.{area['id'].capitalize()}Service", "Request completed for site {SiteId} by {Contact}",
                              {"SiteId": site, "Contact": p["email"]}, None, hex_id(r, 32), spec["version_after"], event_id=r.randint(100, 120)))
    return out


def base_rows(spec: Mapping[str, Any], r: random.Random, site: str, accounts: list[str], members: list[str], t0: datetime) -> dict[str, list[dict[str, Any]]]:
    rec_name = spec["record"]
    rows: dict[str, list[dict[str, Any]]] = {"site": [], "account": [], "membership": [], rec_name: []}
    rows["site"].append({"Id": site, "Name": spec["app"] + " site " + str(r.randint(1, 99)), "Status": "Active", "IsDeleted": False, "CreatedAt": iso(t0 - timedelta(days=90))})
    for a in accounts:
        p = person(r)
        rows["account"].append({"Id": a, "Email": p["email"], "Phone": p["phone"], "DisplayName": f"{p['first']} {p['last']}", "Status": "Active", "CreatedAt": iso(t0 - timedelta(days=60)),
                                "TaxCode": p["tax"]})
    for a, m in zip(accounts, members):
        rows["membership"].append({"Id": m, "SiteId": site, "AccountId": a, "Role": r.choice(["Admin", "Editor", "Viewer"]), "Status": "Active", "ChangedAt": iso(t0 - timedelta(days=30))})
    for _ in range(3):
        rows[rec_name].append({"Id": guid(r), "SiteId": site, "OwnerAccountId": r.choice(accounts), "Status": r.choice(["Open", "Done", "Hold"]), "Reference": "REF" + str(r.randint(1000, 9999)),
                               "Quantity": r.randint(1, 20), "DueDate": iso(t0 + timedelta(days=r.randint(1, 20))), "Notes": note(r), "CreatedAt": iso(t0 - timedelta(days=10)),
                               "UpdatedAt": iso(t0 - timedelta(days=2))})
    return rows


def pack_files(spec: Mapping[str, Any], world: World, repo_root: Path) -> dict[str, Any]:
    """The pack built from the spec: policy and catalog from ``docs/support`` with the world's entity sections, signatures, runbooks."""
    policy = load_json(repo_root / "docs" / "support" / "masking-policy.json")
    policy["entities"] = {e["name"]: e["masking"] for e in spec["entities"]}
    entities = {"schema_version": 1, "entities": [{k: e[k] for k in ("name", "description", "table", "key", "key_type", "audit_entity_type")} | {"source_kind": "sqlserver"}
                                                  for e in spec["entities"]]}
    return {"masking-policy.json": policy, "entities.json": entities, "tools.json": load_json(repo_root / "docs" / "support" / "tools.json"),
            "constraints.json": load_json(repo_root / "docs" / "support" / "constraints.json"),
            "signatures.json": {"schema_version": 1, "signatures": world.signatures}}


def write_world(world: World, out_dir: Path, repo_root: Path = REPO_ROOT) -> None:
    """Write the world directory (deterministic bytes)."""
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "logs.jsonl").write_text("".join(dumps_line(x) + "\n" for x in sorted(world.logs, key=lambda x: x["Timestamp"])), encoding="utf-8")
    (out_dir / "entities").mkdir(exist_ok=True)
    for name, rows in sorted(world.entities.items()):
        (out_dir / "entities" / f"{name}.jsonl").write_text("".join(dumps_line(x) + "\n" for x in rows), encoding="utf-8")
    (out_dir / "audit.jsonl").write_text("".join(dumps_line(x) + "\n" for x in sorted(world.audit, key=lambda x: x["Timestamp"])), encoding="utf-8")
    (out_dir / "health.json").write_text(json.dumps(world.health, indent=2) + "\n", encoding="utf-8")
    pack = out_dir / "pack"
    (pack / "runbooks").mkdir(parents=True, exist_ok=True)
    for name, content in pack_files(world.spec, world, repo_root).items():
        (pack / name).write_text(json.dumps(content, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for anchor, text in sorted(world.runbook_text.items()):
        (pack / "runbooks" / f"{anchor}.md").write_text(text, encoding="utf-8")


def world_hash(path: Path) -> str:
    """SHA-256 over the relative names and bytes of every file of a world directory (the tool cache key)."""
    parts = []
    for p in sorted(path.rglob("*")):
        if p.is_file():
            parts.append(p.relative_to(path).as_posix() + ":" + sha256_text(p.read_text(encoding="utf-8")))
    return sha256_text("\n".join(parts))


def load_spec(path: Path) -> dict[str, Any]:
    return load_json(path)
