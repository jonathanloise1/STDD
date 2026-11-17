"""The investigator's side of training and export (models.md 6, operations.md 11): examples, deterministic validation scoring, run.json blocks.

Training examples are whole conversations; the loss is on every assistant turn (calls and the report). Validation is teacher-forced:
each assistant turn of a validation conversation is regenerated from the conversation before it (calls are scored on tool and
arguments, reports on validity and cause), because the closed loop needs the monitor's executor and is the job of ``eval``.
"""
from __future__ import annotations

import json
import math
import re
from pathlib import Path
from typing import Any, Mapping, Sequence

from training.common import train_core as T
from training.common.config import REPO_ROOT
from training.common.hashing import canonical_sha256, rng, sha256_file
from training.common.jsonio import load_json, read_jsonl
from training.investigator import protocol as P

INV = Path(__file__).resolve().parent


class InvestigatorAdapter:
    model = "investigator"

    def __init__(self, cfg: Mapping[str, Any], protocol: Mapping[str, Any], dataset_dir: Path, catalog: Mapping[str, Any], schema: Mapping[str, Any], seed: int,
                 tool_role: str | None = None):
        self.cfg, self.protocol, self.dir, self.catalog, self.schema, self.seed = cfg, protocol, dataset_dir, catalog, schema, seed
        self.tool_role = tool_role
        self._val: list[dict[str, Any]] | None = None

    def _role(self, m: Mapping[str, str]) -> str:
        return "user" if self.tool_role == "user" and m["role"] == self.protocol["result_role"] else m["role"]

    def train_examples(self) -> list[dict[str, Any]]:
        return [{"id": r["id"], "messages": [{"role": self._role(m), "content": m["content"]} for m in r["messages"]]} for r in read_jsonl(self.dir / "train.jsonl")]

    def validation_items(self) -> list[dict[str, Any]]:
        """Up to ``validation_items`` assistant turns of the validation conversations, chosen by seed; deliberate denied calls are skipped."""
        if self._val is not None:
            return self._val
        turns = []
        for r in read_jsonl(self.dir / "validation.jsonl"):
            msgs = [{"role": self._role(m), "content": m["content"]} for m in r["messages"]]
            evidence = {"E1"}
            stories = self._stories(msgs[1]["content"])
            entity_names = [e["name"] for e in json.loads(re.search(r"<tools>(.*)</tools>", msgs[1]["content"], re.S).group(1))["entities"]]
            for k in range(2, len(msgs)):
                if msgs[k]["role"] == "assistant":
                    t = P.parse_turn(msgs[k]["content"])
                    if t.kind == "call" and P.tool_call_errors(self.catalog, entity_names, t.payload["tool"], t.payload["arguments"]):
                        continue
                    turns.append({"id": f"{r['id']}#{k}", "messages": msgs[:k], "target": msgs[k]["content"], "target_kind": t.kind, "evidence": sorted(evidence),
                                  "stories": stories, "gold": r["gold"]})
                else:
                    m = re.match(r"^<result>(.*)</result>", msgs[k]["content"], re.S)
                    if m:
                        evidence.add(json.loads(m.group(1)).get("evidence_id", ""))
        limit = self.cfg.get("validation_items", 300)
        order = list(range(len(turns)))
        rng(self.seed, "investigator/validation").shuffle(order)
        self._val = [turns[i] for i in sorted(order[:limit])]
        return self._val

    @staticmethod
    def _stories(user: str) -> list[str] | None:
        m = re.search(r"<pack>(.*?)</pack>", user, re.S)
        pk = json.loads(m.group(1)) if m else None
        return [pk["signature"]["user_story"]] if pk and pk.get("signature") else None

    def validation_metrics(self, items: Sequence[dict[str, Any]], outputs: Sequence[str]) -> dict[str, float]:
        n = len(items)
        valid = call_n = call_ok = rep_n = rep_ok = cause_n = cause_ok = 0
        for it, out in zip(items, outputs):
            got = P.parse_turn(out)
            want = P.parse_turn(it["target"])
            valid += got.kind != "invalid"
            if want.kind == "call":
                call_n += 1
                call_ok += got.kind == "call" and got.payload["tool"] == want.payload["tool"] and canonical_sha256(got.payload["arguments"]) == canonical_sha256(want.payload["arguments"])
            else:
                rep_n += 1
                good = got.kind == "report" and not P.validate_report(self.schema, got.payload, set(it["evidence"]), None if it["stories"] is None else set(it["stories"]))
                rep_ok += good
                if want.payload["verdict"] == "explained":
                    cause_n += 1
                    cause_ok += good and got.payload["verdict"] == "explained" and got.payload["cause_category"] == want.payload["cause_category"]
        div = lambda a, b: a / b if b else 1.0   # noqa: E731
        return {"format_valid": div(valid, n), "call_correct": div(call_ok, call_n), "report_valid": div(rep_ok, rep_n), "cause_correct": div(cause_ok, cause_n)}

    def score(self, metrics: Mapping[str, float]) -> float:
        if metrics["format_valid"] < self.cfg["format_valid_min"]:
            return 0.0
        return sum(w * metrics[k] for k, w in self.cfg["score_weights"].items())

    def final_metrics(self, items: Sequence[dict[str, Any]], outputs: Sequence[str]) -> dict[str, Any]:
        return self.validation_metrics(items, outputs)

    def final_choice(self, records: Sequence[dict[str, Any]]) -> dict[str, Any] | None:
        """No judge: the kept checkpoint with the highest validation score (the earlier step on a tie)."""
        ok = [r for r in records if r["score"] > 0]
        return sorted(ok, key=lambda r: (-r["score"], r["step"]))[0] if ok else None


class InvestigatorSetup:
    model = "investigator"
    tier: str | None = None
    tool_role: str | None = None        # set from the chosen base model by train and export (candidates.json ``tool_role``)
    informative_exports = ("merged",)       # transformers cannot decode under the grammar of operations.md 11.3: its report does not gate (lock.GATING)

    def __init__(self, training_dir: Path, repo_root: Path | None = None):
        self.training_dir = training_dir
        self.repo_root = repo_root or (training_dir.parent if (training_dir.parent / "docs").exists() else REPO_ROOT)
        self.dir = training_dir / "investigator"
        self.config_dir = self.dir / "config"
        self.out = training_dir / "out" / "investigator"
        self._cands = load_json(self.config_dir / "candidates.json")
        self.max_parameters = self._cands.get("max_parameters")
        self.export_formats_default = load_json(self.config_dir / "export.json")["formats"]

    def seed(self) -> int:
        return load_json(self.dir / "config.json")["seed"]

    def dataset_dir(self) -> Path:
        return self.out / "dataset"

    def protocol(self) -> dict[str, Any]:
        return P.load_protocol(self.dir / "protocol.json")

    def dataset_block(self) -> dict[str, Any]:
        cfg = load_json(self.dir / "config.json")
        stats = load_json(self.dataset_dir() / "stats.json")
        return {"config_sha256": sha256_file(self.dir / "config.json"), "dataset_sha256": stats["dataset_sha256"],
                "domains": {"train": len(list((self.dir / "worlds").glob("*.json"))) - len(cfg["holdout_domains"]) - len(cfg["validation_domains"]),
                            "validation": len(cfg["validation_domains"]), "test": len(cfg["holdout_domains"])},
                "examples": stats["counts"]["per_split"]["train"], "teacher_models": []}

    def protocol_ref(self) -> dict[str, Any]:
        """The protocol block hashed as the bytes between its markers (models.md 8.1), plus the format file."""
        proto = self.protocol()
        return {"path": proto["system"]["source"], "markers": [proto["system"]["begin"], proto["system"]["end"]], "sha256": P.block_sha256(proto, self.repo_root),
                "format": {"path": "training/investigator/protocol.json", "sha256": sha256_file(self.dir / "protocol.json")}}

    def contracts(self) -> dict[str, int]:
        required = load_json(self.config_dir / "train.json")["required_contracts"]
        doc = load_json(self.repo_root / "docs" / "contracts.json")["contracts"]
        missing = [k for k in required if k not in doc]
        if missing:
            raise T.TrainError(f"docs/contracts.json lacks the investigator contracts: {', '.join(missing)}")
        return {k: doc[k]["version"] for k in required}

    def adapter(self, train_cfg: Mapping[str, Any], judge: Any = None) -> InvestigatorAdapter:
        proto = self.protocol()
        return InvestigatorAdapter(train_cfg, proto, self.dataset_dir(), load_json(self.repo_root / "docs" / "support" / "tools.json"),
                                   P.investigation_schema(self.repo_root, proto), self.seed(), self.tool_role)

    def template_cases(self) -> list[list[dict[str, str]]]:
        """Multi-turn conversations built from the golden cases: system block, user turn, a call, its result and a report."""
        golden = load_json(self.dir / "protocol.golden.json")
        proto = self.protocol()
        system = P.system_text(proto, self.repo_root)
        by = {c["id"]: c for c in golden["render"]}
        call, result, report = by["t01-call-logs"]["expected"], by["t03-result-ok"]["expected"], by["t05-report-explained"]["expected"]
        result_role = "user" if self.tool_role == "user" else proto["result_role"]
        cases = []
        for c in golden["render"]:
            if c["kind"] == "user":
                cases.append([{"role": "system", "content": system}, {"role": "user", "content": c["expected"]}])
        user = by["u01-rate-with-pack"]["expected"]
        cases.append([{"role": "system", "content": system}, {"role": "user", "content": user}, {"role": "assistant", "content": call},
                      {"role": result_role, "content": result}])
        cases.append([{"role": "system", "content": system}, {"role": "user", "content": user}, {"role": "assistant", "content": call},
                      {"role": result_role, "content": result}, {"role": "assistant", "content": report}])
        return cases

    def export_extras(self, fmt: str, entry: Mapping[str, Any], base: Mapping[str, Any], tier: str | None) -> dict[str, Any]:
        """``tier``, ``context_length`` and ``memory_mb`` of an export (models.md 8.1): read by the monitor's tier selection."""
        cfg = load_json(self.config_dir / "export.json")
        size = sum(f["bytes"] for f in entry["files"])
        return {"tier": tier or base.get("tier") or "small", "context_length": cfg["tiers"][tier or base.get("tier") or "small"]["context_length"],
                "memory_mb": math.ceil(size / 1_000_000 * cfg["memory_factor"]) + cfg["kv_overhead_mb"]}

    def preflight(self, real: bool) -> list[str]:
        problems = []
        try:
            self.contracts()
        except T.TrainError as exc:
            problems.append(str(exc))
        return problems
