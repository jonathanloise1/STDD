"""The assistant's side of training and export: examples, validation scoring, final selection, run.json blocks.

``AssistantSetup`` is what ``common/train_cli`` and ``common/export_cli`` need to train and export the model id
``assistant``. Prompts are rendered through ``protocol.py`` only (the single definition, models.md 3.1).
"""
from __future__ import annotations

from pathlib import Path
from typing import Any, Mapping, Sequence

from training.assistant import metrics as M, protocol as P
from training.common import train_core as T
from training.common.hashing import sha256_file
from training.common.jsonio import load_json, read_jsonl
from training.common.judge import Judge


class AssistantAdapter:
    model = "assistant"

    def __init__(self, cfg: Mapping[str, Any], protocol: Mapping[str, Any], dataset_dir: Path, judge: Judge | None = None):
        self.cfg, self.protocol, self.dir, self.judge = cfg, protocol, dataset_dir, judge
        self._val: list[dict[str, Any]] | None = None

    def _messages(self, rec: Mapping[str, Any]) -> list[dict[str, str]]:
        system, user = P.render_messages(self.protocol, rec["lang"], rec["app_name"], rec["context"], rec["question"])
        return [{"role": "system", "content": system}, {"role": "user", "content": user}]

    def train_examples(self) -> list[dict[str, Any]]:
        return [{"id": r["id"], "messages": self._messages(r) + [{"role": "assistant", "content": r["target"]}]} for r in read_jsonl(self.dir / "train.jsonl")]

    def validation_items(self) -> list[dict[str, Any]]:
        if self._val is None:
            self._val = [{"id": r["id"], "messages": self._messages(r), "record": r} for r in read_jsonl(self.dir / "validation.jsonl")]
        return self._val

    def _results(self, items: Sequence[Mapping[str, Any]], outputs: Sequence[str]) -> list[dict[str, Any]]:
        res = []
        for it, out in zip(items, outputs):
            rec = it["record"]
            parsed = P.parse_answer(self.protocol, rec["lang"], out, len(rec["context"]))
            res.append({"id": rec["id"], "lang": rec["lang"], "class": rec["class"], "gold": rec["gold"], "overlap": rec["overlap"],
                        "outcome": parsed.outcome, "cited": parsed.cited, "answer": out, "source": "model", "latency_ms": 0.0, "ttft_ms": None})
        return res

    def validation_metrics(self, items: Sequence[dict[str, Any]], outputs: Sequence[str]) -> dict[str, float]:
        """Automatic metrics only (6.3), over both languages together."""
        m = M.compute_metrics(self._results(items, outputs), None)
        return {k: (m[k] if m[k] is not None else 0.0) for k in ("format_valid", "wrongful_refusal", "oos_refusal", "near_miss_refusal", "correct_source")}

    def score(self, metrics: Mapping[str, float]) -> float:
        return T.validation_score(metrics, self.cfg["score_weights"], self.cfg["format_valid_min"])

    def final_metrics(self, items: Sequence[dict[str, Any]], outputs: Sequence[str]) -> dict[str, Any]:
        if self.judge is None:
            raise T.TrainError("the final selection needs the judge (6.3); none is configured")
        res = self._results(items, outputs)
        by_id = {i["id"]: i["record"] for i in items}
        verdicts = {it["id"]: self.judge.judge(it, ans) for it, ans in M.judge_items(res, by_id, {})}
        m = M.compute_metrics(res, verdicts)
        return {"faithful": m["faithful"], "useful": m["useful"], "answered": m["answered"]}

    def final_choice(self, records: Sequence[dict[str, Any]]) -> dict[str, Any] | None:
        """The highest useful among the checkpoints with faithful >= the minimum (6.3); a tie goes to the higher S, then the earlier step."""
        ok = [r for r in records if r.get("faithful") is not None and r["faithful"] >= self.cfg["selection"]["faithful_min"]]
        if not ok:
            return None
        return sorted(ok, key=lambda r: (-(r["useful"] or 0.0), -r["score"], r["step"]))[0]


class AssistantSetup:
    """What the shared train and export commands need to know about the assistant."""

    model = "assistant"
    max_parameters = T.MAX_PARAMETERS
    tier: str | None = None
    export_formats_default = ["onnx", "gguf"]

    def __init__(self, training_dir: Path):
        self.training_dir = training_dir
        self.dir = training_dir / "assistant"
        self.config_dir = self.dir / "config"
        self.out = training_dir / "out" / "assistant"

    def seed(self) -> int:
        return load_json(self.config_dir / "dataset.json")["master_seed"]

    def protocol(self) -> dict[str, Any]:
        return P.load_protocol(self.dir / "protocol.json")

    def dataset_dir(self) -> Path:
        return self.out / "dataset"

    def dataset_block(self) -> dict[str, Any]:
        """The ``dataset`` block of run.json (models.md 8.1)."""
        cfg = load_json(self.config_dir / "dataset.json")
        stats = load_json(self.dataset_dir() / "stats.json")
        n_train = stats["counts"]["per_split"]["train"]
        return {"config_sha256": sha256_file(self.config_dir / "dataset.json"), "dataset_sha256": stats["dataset_sha256"],
                "domains": cfg["splits"], "examples": n_train, "teacher_models": [cfg["teacher"]["model"], cfg["teacher"]["verifier_model"]]}

    def protocol_ref(self) -> dict[str, Any] | None:
        return None                 # lock default: training/assistant/protocol.json

    def contracts(self) -> dict[str, int] | None:
        return None                 # lock default: docs/contracts.json

    def adapter(self, train_cfg: Mapping[str, Any], judge: Judge | None) -> AssistantAdapter:
        return AssistantAdapter(train_cfg, self.protocol(), self.dataset_dir(), judge)

    def template_cases(self) -> list[list[dict[str, str]]]:
        """Messages of every golden render case (3.7): the template check renders them with three implementations."""
        golden = load_json(self.dir / "protocol.golden.json")
        return [[{"role": "system", "content": c["expected"]["system"]}, {"role": "user", "content": c["expected"]["user"]}] for c in golden["render"]]

    def preflight(self, real: bool) -> list[str]:
        """Problems that would only show after hours of training: the judge of the final selection must be usable."""
        ev = load_json(self.config_dir / "eval.json")["judge"]
        problems = []
        if not ev.get("model"):
            problems.append("eval.json judge.model is not set (the final selection of 6.3 uses the judge)")
        return problems
