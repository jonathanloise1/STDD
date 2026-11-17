"""Fakes and builders shared by the tests of train, export, eval and the offline rehearsals (no model, no network beyond the loopback stand-in)."""
from __future__ import annotations

import json
import shutil
from pathlib import Path
from typing import Any, Mapping, Sequence

from conftest import ROOT
from training.assistant import protocol as P
from training.common.backends import FakeBackend
from training.common.hardware import HardwareInfo
from training.common.jsonio import load_json, read_jsonl, write_json
from training.common.teacher import Completion

IGNORE = shutil.ignore_patterns("__pycache__", "*.pyc", "out")


def hardware(device: str = "cuda", ram: float = 64.0, vram: float | None = 24.0, disk: float = 500.0) -> HardwareInfo:
    return HardwareInfo(device, "Fake GPU", ram, vram, disk, 16, "3.12", "none")


# ---------------------------------------------------------------- the assistant's temporary repository

def make_assistant_repo(tmp: Path, validated_out: Path, cdir: Path, judge_model: str = "fake-judge", labels: int = 6) -> Path:
    """A repository root with the assistant's code-side files, a validated dataset and the config needed by every command."""
    root = tmp / "repo"
    (root / "docs").mkdir(parents=True)
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": {k: {"version": 1} for k in ("manual-page", "help-pack", "assistant-protocol", "assistant-retrieval")}}))
    a = root / "training" / "assistant"
    shutil.copytree(ROOT / "training" / "assistant" / "config", a / "config")
    for name in ("protocol.json", "protocol.golden.json", "retrieval.json"):
        shutil.copy(ROOT / "training" / "assistant" / name, a / name)
    shutil.copytree(ROOT / "training" / "assistant" / "eval", a / "eval")
    for name in ("dataset.json", "domains.json"):
        shutil.copy(cdir / name, a / "config" / name)
    ev = load_json(a / "config" / "eval.json")
    ev["judge"]["model"] = judge_model
    write_json(a / "config" / "eval.json", ev)
    out = root / "training" / "out" / "assistant"
    shutil.copytree(validated_out, out)
    return root


def write_labels(root: Path, n: int = 6) -> None:
    """Human labels for the judge calibration (4.4): items whose answer the fake judge approves."""
    protocol = P.load_protocol(root / "training" / "assistant" / "protocol.json")
    rows = []
    for rec in read_jsonl(root / "training" / "out" / "assistant" / "dataset" / "validation.jsonl"):
        if rec["class"].startswith("pos_"):
            rows.append({"id": rec["id"], "question": rec["question"], "context": rec["context"], "gold": rec["gold"], "reference": rec["target"], "answer": rec["target"],
                         "faithful": True, "useful": True})
        if len(rows) == n:
            break
    p = root / "training" / "assistant" / "eval" / "human-labels.jsonl"
    p.write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")


class FakeJudgeClient:
    """A ``TeacherClient``: approves everything (or what ``verdict`` says)."""

    wants_job = False

    def __init__(self, verdict: dict[str, Any] | None = None, outputs: Sequence[str] | None = None):
        self.verdict = verdict or {"faithful": True, "useful": True, "unsupported": []}
        self.outputs = list(outputs or [])
        self.calls = 0

    def complete(self, *, model: str, system: str, user: str, params: Mapping[str, Any], seed: int) -> Completion:
        self.calls += 1
        text = self.outputs.pop(0) if self.outputs else json.dumps(self.verdict)
        return Completion(text, 400, 30)


def oracle_backend(root: Path, wrong: set[str] | None = None, refuse_all: bool = False) -> FakeBackend:
    """Answers every dataset item with its reference target (refusals included); ``wrong`` ids get the refusal instead."""
    protocol = P.load_protocol(root / "training" / "assistant" / "protocol.json")
    table: dict[str, str] = {}
    for split in ("test", "seen", "validation", "train"):
        f = root / "training" / "out" / "assistant" / "dataset" / f"{split}.jsonl"
        if not f.exists():
            continue
        for rec in read_jsonl(f):
            system, user = P.render_messages(protocol, rec["lang"], rec["app_name"], rec["context"], rec["question"])
            table[user] = rec["target"] if rec["id"] not in (wrong or set()) else protocol["languages"][rec["lang"]]["refusal"]

    def responder(messages):
        user = messages[-1]["content"]
        lang = "it" if "Rispondi" in messages[0]["content"] or "Italian" in messages[0]["content"] else "en"
        if refuse_all or user not in table:
            return protocol["languages"][lang]["refusal"]
        return table[user]

    return FakeBackend(responder)


# ---------------------------------------------------------------- training

class FakeEngine:
    """A training engine with no model: the loss decays, validation outputs are the oracle's answers for ``good_until`` steps and wrong after."""

    def __init__(self, adapter, good_until: int = 10 ** 9, loss0: float = 1.0, decay: float = 0.9, crash_at: int | None = None):
        self.adapter, self.good_until, self.loss0, self.decay, self.crash_at = adapter, good_until, loss0, decay, crash_at
        self.step = 0
        self.saved: list[str] = []
        self.loaded: list[str] = []
        self.prepared_from: Path | None = None
        self.closed = False
        self.current = 0

    def prepare(self, resume_from):
        self.prepared_from = resume_from
        if resume_from is not None:
            self.step = int((resume_from / "step.txt").read_text())

    def train_step(self, indices):
        if self.crash_at is not None and self.step + 1 == self.crash_at:
            raise RuntimeError("simulated crash")
        self.step += 1
        self.current = self.step
        return self.loss0 * (self.decay ** self.step)

    def generate(self, items):
        good = self.current <= self.good_until
        return [(it.get("target") or it["record"]["target"]) if good else "garbage" for it in items]

    def save_adapter(self, path: Path):
        path.mkdir(parents=True, exist_ok=True)
        (path / "adapter_model.safetensors").write_bytes(f"adapter at step {self.step}".encode())
        (path / "step.txt").write_text(str(self.step))
        self.saved.append(path.name)

    def load_adapter(self, path: Path):
        self.loaded.append(path.name)
        self.current = int((path / "step.txt").read_text())

    def save_state(self, path: Path):
        self.save_adapter(path)

    def close(self):
        self.closed = True


class FakeExportTools:
    """Export tools that write small files; the three template renderers return the same strings unless ``js_differs``."""

    def __init__(self, js_differs: bool = False, formats=("onnx", "gguf")):
        self.js_differs, self.calls = js_differs, []

    def merge(self, base, adapter_dir, out: Path):
        self.calls.append("merge")
        out.mkdir(parents=True, exist_ok=True)
        (out / "config.json").write_text("{}")
        (out / "model.safetensors").write_bytes(b"merged")

    def to_onnx(self, merged: Path, out: Path, variant: str):
        self.calls.append(f"onnx-{variant}")
        (out / "onnx").mkdir(parents=True, exist_ok=True)
        (out / "onnx" / ("model_q4f16.onnx" if variant == "q4f16" else "model_quantized.onnx")).write_bytes(b"onnx weights")
        for n in ("config.json", "generation_config.json", "tokenizer.json", "tokenizer_config.json"):
            (out / n).write_text("{}")

    def to_gguf(self, merged: Path, outfile: Path, variant: str):
        self.calls.append(f"gguf-{variant}")
        outfile.parent.mkdir(parents=True, exist_ok=True)
        outfile.write_bytes(b"gguf weights" * 5)

    def _render(self, cases):
        return ["".join(f"<|{m['role']}|>{m['content']}" for m in c) + "<|assistant|>" for c in cases]

    def render_python(self, merged, kwargs, cases):
        return self._render(cases)

    def render_js(self, onnx_root, kwargs, cases):
        out = self._render(cases)
        return [out[0] + " "] + out[1:] if self.js_differs else out

    def render_llama(self, gguf, kwargs, cases):
        return self._render(cases)


# ---------------------------------------------------------------- the investigator's temporary repository

def make_inv_repo(tmp: Path) -> Path:
    """A repository root with docs/support, the support agent, contracts and the investigator's config, worlds and protocol files."""
    root = tmp / "invrepo"
    (root / "docs").mkdir(parents=True)
    shutil.copytree(ROOT / "docs" / "support", root / "docs" / "support", ignore=shutil.ignore_patterns("profiles", "templates", "examples", "bench"))
    (root / ".github" / "agents").mkdir(parents=True)
    shutil.copy(ROOT / ".github" / "agents" / "support.agent.md", root / ".github" / "agents" / "support.agent.md")
    contracts = {k: {"version": 1} for k in ("incident-report", "tool-catalog", "investigator-protocol")}
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": contracts}))
    shutil.copytree(ROOT / "training" / "investigator", root / "training" / "investigator", ignore=IGNORE)
    return root


def oracle_investigator(root: Path, scenario_files, seed: int = 20261106):
    """A backend that plays the oracle's plain path for each scenario: its calls, then the gold report (found by the incident text)."""
    from dataclasses import replace
    from training.common.hashing import canonical_sha256
    from training.investigator import protocol as IP, scenarios as SC, world as W
    from training.investigator.generate import load_worlds
    specs = load_worlds(root / "training" / "investigator")
    table = {}
    for f in scenario_files:
        scn = load_json(f)
        spec = specs[scn["domain"]]
        idx = int(scn["id"].rsplit("-", 1)[1])
        plan = replace(SC.plan_scenario(seed, spec, idx, scn["split"]), variant="plain")
        built = SC.build_scenario(plan, spec, seed, root)
        steps = built.steps
        evidence = {f"{s.tool}|{canonical_sha256(s.arguments)}": f"E{i + 2}" for i, s in enumerate(steps) if s.supporting and not scn["gold"]["verdict"] == "abstain"}
        report = SC.make_report(plan, built, evidence, W.random.Random(1))
        table[IP.dumps(scn["prompt"]["incident"])] = (steps, report)
    proto = IP.load_protocol(root / "training" / "investigator" / "protocol.json")

    def responder(messages, constraint=None):
        user = messages[1]["content"]
        key = next(k for k in table if k in user)
        steps, report = table[key]
        done = sum(1 for m in messages if m["role"] == "assistant")
        if done < len(steps):
            s = steps[done]
            return IP.render_call(proto, s.tool, s.arguments)
        return IP.render_report(proto, report)

    return FakeBackend(responder)
