"""Tool results for the investigator's data and evaluation (operations.md 11.6, 14.4).

A ``ToolRunner`` returns the result envelope (7.1) of one call over a world directory. Two implementations:

- ``DotnetToolRunner``: the monitor's real executor and masking, one call at a time through
  ``dotnet run --project src/support/MyApp.Monitor -- tool {name} --world {dir} --args {json} --incident {id}``. Results are cached by the
  world's content, the call and the incident, so a rerun never repeats a call. This is the only runner the real commands use.
- ``FakeToolRunner``: a Python double of the executor for tests and dry runs (validation, duplicates, evidence ids, limits and masking
  with the ``masking.py`` port). It is not used by a real ``generate`` run.

``generate`` never formats a result itself: it takes whatever the runner returns.
"""
from __future__ import annotations

import json
import subprocess
import uuid
from pathlib import Path
from typing import Any, Mapping, Protocol

from training.common.config import REPO_ROOT
from training.common.hashing import canonical_sha256
from training.common.jsonio import atomic_write_text, dumps_pretty, load_json
from training.investigator import protocol as P
from training.investigator.masking import Masker
from training.investigator.world import world_hash


class ToolRunnerError(RuntimeError):
    pass


class ToolRunner(Protocol):
    def call(self, world_dir: Path, tool: str, arguments: Mapping[str, Any], incident: str, call_no: int) -> dict[str, Any]: ...


class DotnetToolRunner:
    """The monitor's executor. ``command`` and ``runner`` are injectable for tests of the wiring only.

    The executor keeps state per incident (evidence numbering, duplicates), possibly across processes, so every call carries the incident id
    plus a per-instance salt: a second run never meets the first run's history. The cache key is therefore not the incident id but the world's
    content and the calls made so far in the incident (the history), which is all an envelope depends on."""

    def __init__(self, repo_root: Path = REPO_ROOT, cache_dir: Path | None = None, timeout: float = 180.0,
                 command: list[str] | None = None, runner: Any = None, salt: str | None = None):
        self.root, self.cache_dir, self.timeout = repo_root, cache_dir, timeout
        self.command = command or ["dotnet", "run", "--project", "src/support/MyApp.Monitor", "--"]
        self.runner = runner or subprocess.run
        self.salt = salt or uuid.uuid4().hex[:8]
        self._worlds: dict[str, str] = {}
        self._history: dict[str, list[str]] = {}

    def _key(self, world_dir: Path, tool: str, arguments: Mapping[str, Any], incident: str) -> str:
        wh = self._worlds.setdefault(str(world_dir), world_hash(world_dir))
        return canonical_sha256({"world": wh, "tool": tool, "args": arguments, "history": self._history.get(incident, [])})

    def call(self, world_dir: Path, tool: str, arguments: Mapping[str, Any], incident: str, call_no: int) -> dict[str, Any]:
        key = self._key(world_dir, tool, arguments, incident)
        self._history.setdefault(incident, []).append(canonical_sha256({"tool": tool, "args": arguments}))
        cached = self.cache_dir / key[:2] / f"{key}.json" if self.cache_dir else None
        if cached and cached.exists():
            return json.loads(cached.read_text(encoding="utf-8"))
        cmd = self.command + ["tool", tool, "--world", str(world_dir), "--args", json.dumps(dict(arguments), separators=(",", ":")), "--incident", f"{incident}~{self.salt}"]
        try:
            res = self.runner(cmd, cwd=self.root, capture_output=True, text=True, timeout=self.timeout)
        except (OSError, subprocess.SubprocessError) as exc:
            raise ToolRunnerError(f"the monitor's tool command could not run ({type(exc).__name__}: {exc}); a real run needs `dotnet` and a built monitor") from exc
        # exit 1 means the call did not succeed (denied, error, duplicate ...): the envelope on the last line is the result. Anything else is a failure.
        try:
            envelope = json.loads(res.stdout.strip().splitlines()[-1])
        except (IndexError, json.JSONDecodeError):
            envelope = None
        if res.returncode not in (0, 1):
            raise ToolRunnerError(f"the monitor's tool command failed (exit {res.returncode}): {(res.stderr or res.stdout).strip()[:300]}")
        if not isinstance(envelope, dict) or "status" not in envelope:
            raise ToolRunnerError(f"the monitor's tool command did not print an envelope (exit {res.returncode}): {res.stderr.strip()[:300]}")
        if cached:
            atomic_write_text(cached, dumps_pretty(envelope))
        return envelope


class FakeToolRunner:
    """Test double of the executor over a world directory. State per incident: evidence numbering and the calls made."""

    def __init__(self, repo_root: Path = REPO_ROOT):
        self.repo_root = repo_root
        self.catalog = load_json(repo_root / "docs" / "support" / "tools.json")
        self.history: dict[str, list[tuple[str, str, str]]] = {}
        self.counters: dict[str, int] = {}
        self.calls = 0

    # -- the executor
    def call(self, world_dir: Path, tool: str, arguments: Mapping[str, Any], incident: str, call_no: int) -> dict[str, Any]:
        self.calls += 1
        hist = self.history.setdefault(incident, [])
        nxt = self.counters.get(incident, 2)
        eid = f"E{nxt}"
        spec = next((t for t in self.catalog["tools"] if t["name"] == tool), None)
        env = {"tool": tool, "call": call_no, "status": "ok", "evidence_id": eid, "source": "world", "rows": 0, "truncated": False, "fields_dropped": 0, "data": None}
        pack_entities = world_dir / "pack" / "entities.json"      # a world without its own pack is the sample application's (the bench): the repository's entities
        entities = load_json(pack_entities if pack_entities.exists() else self.repo_root / "docs" / "support" / "entities.json")["entities"]
        errors = P.tool_call_errors(self.catalog, [e["name"] for e in entities], tool, arguments)
        canon = canonical_sha256(dict(arguments))
        if errors:
            hist.append((tool, canon, eid))
            self.counters[incident] = nxt + 1
            return {**env, "status": "denied", "error": errors[0][:200]}
        earlier = next((h for h in hist if h[0] == tool and h[1] == canon), None)
        if earlier:
            hist.append((tool, canon, earlier[2]))
            return {**env, "status": "duplicate", "evidence_id": earlier[2], "error": f"same call as {earlier[2]}"}
        hist.append((tool, canon, eid))
        self.counters[incident] = nxt + 1
        policy = load_json(world_dir / "pack" / "masking-policy.json" if (world_dir / "pack" / "masking-policy.json").exists() else self.repo_root / "docs" / "support" / "masking-policy.json")
        m = Masker(policy)
        data, rows, dropped = getattr(self, f"_{tool}")(world_dir, dict(arguments), m, policy, entities)
        limits = spec["limits"]
        text = json.dumps(data, ensure_ascii=False)
        truncated = len(text) > limits["max_output_chars"]
        status = "ok" if rows else "empty"
        if tool == "sql_entity_read":
            status = "ok"
        return {**env, "status": status, "rows": rows, "truncated": truncated, "fields_dropped": dropped, "data": data}

    # -- tools (7.4)
    def _logs_by_correlation(self, world: Path, a: dict[str, Any], m: Masker, policy: Mapping[str, Any], entities: list[dict[str, Any]]):
        cols = policy["tools"]["logs_by_correlation"]
        recs = [json.loads(l) for l in (world / "logs.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        recs = sorted((r for r in recs if r.get("TraceId") == a["trace_id"]), key=lambda r: r["Timestamp"])[: a.get("limit", 100)]
        out = []
        for r in recs:
            p = r.get("Properties", {})
            exc = r.get("Exception")
            row = {"timestamp": r["Timestamp"], "kind": "exception" if exc else "dependency" if "Dependency" in p and "ResultCode" in p else "trace", "level": r["Level"],
                   "category": r["Category"], "log_key": r.get("LogKey"), "template": r.get("MessageTemplate"), "message": r["Message"], "span_id": r["SpanId"],
                   "exception_type": (exc or {}).get("Type"), "top_frame": (exc or {}).get("Frames", [None])[0]}
            if "Dependency" in p and "ResultCode" in p:
                row.update(name=p["Dependency"], result_code=p["ResultCode"], duration_ms=p.get("DurationMs"))
            clean = {}
            for k, v in row.items():
                if v is None:
                    continue
                clean[k] = m.apply_class(cols.get(f"records[].{k}", "free_text"), v)
            out.append(clean)
        return {"records": out}, len(out), 0

    def _sql_entity_read(self, world: Path, a: dict[str, Any], m: Masker, policy: Mapping[str, Any], entities: list[dict[str, Any]]):
        ent = next(e for e in entities if e["name"] == a["entity"])
        f = world / "entities" / f"{a['entity']}.jsonl"
        rows = [json.loads(l) for l in f.read_text(encoding="utf-8").splitlines() if l.strip()] if f.exists() else []
        row = next((r for r in rows if str(r.get(ent["key"])) == a["id"]), None)
        if row is None:
            return {"entity": a["entity"], "id": a["id"], "found": False, "fields": {}}, 0, 0
        fields, dropped = m.mask_fields(policy["entities"][a["entity"]], row)
        return {"entity": a["entity"], "id": a["id"], "found": True, "fields": fields}, 1, dropped

    def _audit_trail_by_entity(self, world: Path, a: dict[str, Any], m: Masker, policy: Mapping[str, Any], entities: list[dict[str, Any]]):
        ent = next(e for e in entities if e["name"] == a["entity"])
        rows = [json.loads(l) for l in (world / "audit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        rows = sorted((r for r in rows if r["EntityType"] == ent["audit_entity_type"] and r["EntityId"] == a["id"]), key=lambda r: r["Timestamp"], reverse=True)[: a.get("limit", 20)]
        sec = policy["entities"][a["entity"]]
        cols = policy["tools"]["audit_trail_by_entity"]
        out, dropped = [], 0
        for r in rows:
            old, d1 = m.mask_fields(sec, r.get("OldValues", {}))
            new, d2 = m.mask_fields(sec, r.get("NewValues", {}))
            dropped += d1 + d2
            out.append({"timestamp": m.apply_class(cols["changes[].timestamp"], r["Timestamp"]), "action": m.apply_class("code", r["Action"]),
                        "user_id": m.apply_class("id", r["UserId"]), "changed_properties": [m.apply_class("code", p) for p in r.get("ChangedProperties", [])],
                        "old_values": old, "new_values": new, "correlation_id": m.apply_class("id", r.get("CorrelationId", ""))})
        return {"changes": out}, len(out), dropped

    def _health_status(self, world: Path, a: dict[str, Any], m: Masker, policy: Mapping[str, Any], entities: list[dict[str, Any]]):
        h = json.loads((world / "health.json").read_text(encoding="utf-8"))
        return {"status": m.apply_class("code", h["status"]),
                "checks": [{"name": m.apply_class("code", c["name"]), "status": m.apply_class("code", c["status"]), "duration_ms": m.apply_class("number", c["duration_ms"])}
                           for c in h["checks"]]}, len(h["checks"]), 0

    def _signature_lookup(self, world: Path, a: dict[str, Any], m: Masker, policy: Mapping[str, Any], entities: list[dict[str, Any]]):
        sig_file = world / "pack" / "signatures.json"       # a bench world has no pack of its own: the signatures are the monitor's, not simulated
        sigs = json.loads(sig_file.read_text(encoding="utf-8"))["signatures"] if sig_file.exists() else []
        key, val = next((k, v) for k, v in a.items() if k in ("signature_id", "log_key", "template_id"))
        hit = next((s for s in sigs if (key == "signature_id" and s["id"] == val) or (key == "log_key" and s["match"].get("log_key") == val)
                    or (key == "template_id" and s["match"].get("template_id") == val)), None)
        if hit is None:
            return {"found": False, "signature": None}, 0, 0
        sig = json.loads(json.dumps(hit))
        if sig.get("call_site"):
            sig["call_site"].pop("file", None)
            sig["call_site"].pop("line", None)
        return {"found": True, "signature": sig}, 1, 0
