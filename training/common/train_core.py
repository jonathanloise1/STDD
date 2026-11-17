"""Adapter training, shared by both models (models.md 6.1 to 6.4).

The decisions are plain Python and tested with a fake engine; the heavy work is one class, ``TorchEngine``, whose imports
(torch, transformers, peft) happen inside its methods:

- ``select_base_model``: the selection rule of 6.1 over ``candidates.json``;
- ``build_plan``: steps, batch plan for the device, plan id (the approval of a ``[COST]`` run);
- ``tokenize_conversation``: loss on assistant turns only, every other token masked;
- ``EarlyStopper``, ``MemorisationMonitor``, ``CheckpointBook``, ``validation_score``: 6.3;
- ``run_training``: the loop with validation, checkpoint comparison, early stopping and resume;
- ``select_final``: the final selection among the kept checkpoints.

A ``TrainAdapter`` (one per model id) supplies the examples, how validation is scored and how the final choice is made.
"""
from __future__ import annotations

import json
import math
import shutil
import time
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping, Protocol, Sequence

from .hashing import canonical_sha256, derive_seed, rng
from .hardware import HardwareInfo, micro_batch_plan
from .jsonio import load_json, write_json
from .teacher import TeacherError

MAX_PARAMETERS = 2_500_000_000        # 6.1 requirement for the assistant; the investigator's file sets its own ceiling. Applies to trainable candidates only
MIN_CONTEXT = 4096
HEX40 = set("0123456789abcdef")


class TrainError(RuntimeError):
    pass


# ---------------------------------------------------------------- base model selection (6.1)

def load_candidates(path: Path) -> list[dict[str, Any]]:
    return load_json(path)["candidates"]


def is_trainable(c: Mapping[str, Any]) -> bool:
    """``trainable`` is true unless the entry says ``false`` (an untrained-only candidate, models.md 6.1)."""
    return c.get("trainable", True) is not False


def check_candidate(c: Mapping[str, Any], max_parameters: int | None = MAX_PARAMETERS, real_run: bool = False,
                    allow_unverified: bool = False, purpose: str = "eval") -> list[str]:
    """Problems of a candidate against the requirement table of 6.1 (config checks only).

    ``purpose`` is ``train`` (an untrained-only candidate is refused), or ``eval`` / ``export`` (accepted). The parameter
    ceiling applies to trainable candidates only: an untrained-only candidate is never trained, so its size is limited by the
    machine that serves it, not by the training hardware."""
    problems = []
    if purpose == "train" and not is_trainable(c):
        problems.append(f"{c['id']}: marked trainable false in candidates.json (untrained only: it is evaluated and exported as it is, never trained)")
    if is_trainable(c) and max_parameters is not None and c["parameters"] > max_parameters:
        problems.append(f"{c['id']}: {c['parameters']:,} parameters is above {max_parameters:,}")
    if c.get("context_length", 0) < MIN_CONTEXT:
        problems.append(f"{c['id']}: context {c.get('context_length')} is below {MIN_CONTEXT}")
    if real_run:
        rev = c.get("revision")
        if not rev or len(rev) != 40 or set(rev) - HEX40:
            problems.append(f"{c['id']}: revision must be a pinned 40-character commit hash (is {rev!r})")
        if not c.get("verified") and not allow_unverified:
            problems.append(f"{c['id']}: not marked verified in candidates.json (confirm hub id, licence and revision, then set verified)")
        if not c.get("license"):
            problems.append(f"{c['id']}: no licence recorded")
    return problems


def select_base_model(candidates: Sequence[Mapping[str, Any]], requested: str | None = None, *, max_parameters: int | None = MAX_PARAMETERS,
                      real_run: bool = False, allow_unverified: bool = False, tier: str | None = None,
                      purpose: str = "eval") -> Mapping[str, Any]:
    """``requested`` or, when ``None``, the smallest candidate (6.1 rule 2; with ``tier`` only that tier's candidates; with
    ``purpose="train"`` only trainable ones). Raises ``TrainError`` listing the problems of the chosen one."""
    pool = [c for c in candidates if tier is None or c.get("tier") == tier]
    if purpose == "train" and not requested:
        pool = [c for c in pool if is_trainable(c)]
    if not pool:
        raise TrainError(f"no candidate{' for tier ' + tier if tier else ''} in candidates.json")
    if requested:
        found = [c for c in pool if c["id"] == requested]
        if not found:
            raise TrainError(f"candidate {requested} is not in candidates.json (have: {', '.join(c['id'] for c in pool)})")
        chosen = found[0]
    else:
        chosen = sorted(pool, key=lambda c: (c.get("kind") == "fallback", c["parameters"], c["id"]))[0]
    problems = check_candidate(chosen, max_parameters, real_run, allow_unverified, purpose)
    if problems:
        raise TrainError("; ".join(problems))
    return chosen


# ---------------------------------------------------------------- plan

@dataclass
class Plan:
    examples: int
    effective_batch: int
    steps_per_epoch: int
    epochs: int
    total_steps: int
    micro_batch: int
    grad_accum: int
    device: str
    precision: str
    seed: int
    plan_id: str

    def as_dict(self) -> dict[str, Any]:
        return self.__dict__.copy()


def build_plan(cfg: Mapping[str, Any], n_examples: int, hw: HardwareInfo | None, seed: int, extra_identity: Mapping[str, Any],
               model_parameters: int = 600_000_000, micro_batch: int | None = None) -> Plan:
    eff = cfg["batch"]["effective"]
    spe = math.ceil(n_examples / eff) if n_examples else 0
    total = spe * cfg["epochs"]
    if hw is not None:
        mb = micro_batch_plan(hw, eff, model_parameters, micro_batch)
        device = hw.device
        precision = cfg["precision"].get(device, "fp32")
    else:
        mb, device, precision = {"micro_batch": 1, "grad_accum": eff}, "unknown", "unknown"
    pid = canonical_sha256({"train": cfg, "examples": n_examples, "seed": seed, **dict(extra_identity)})[:12]
    return Plan(n_examples, eff, spe, cfg["epochs"], total, mb["micro_batch"], mb["grad_accum"], device, precision, seed, pid)


def batch_indices(n: int, step: int, effective: int, seed: int) -> list[int]:
    """Example indices of optimizer step ``step`` (0-based): one seeded permutation per epoch, so a resumed run sees the
    batches it would have seen. The last batch of an epoch wraps to the start of the same permutation."""
    spe = math.ceil(n / effective)
    epoch, pos = divmod(step, spe)
    order = list(range(n))
    rng(seed, f"order/{epoch}").shuffle(order)
    start = pos * effective
    return [order[(start + i) % n] for i in range(effective)]


# ---------------------------------------------------------------- loss mask

def tokenize_conversation(tokenizer: Any, messages: Sequence[Mapping[str, str]], template_kwargs: Mapping[str, Any], max_len: int
                          ) -> tuple[list[int], list[int]] | None:
    """Token ids and labels of a conversation: labels equal the ids on assistant turns (including the template's end of
    turn) and ``-100`` everywhere else. The rendered text must grow by prefix as turns are added; otherwise the chat
    template is not usable for masking and ``TrainError`` says so. Returns ``None`` when longer than ``max_len``."""
    full = tokenizer.apply_chat_template(list(messages), tokenize=False, add_generation_prompt=False, **template_kwargs)
    spans: list[tuple[int, int]] = []
    for i, m in enumerate(messages):
        if m["role"] != "assistant":
            continue
        start_text = tokenizer.apply_chat_template(list(messages[:i]), tokenize=False, add_generation_prompt=True, **template_kwargs)
        end_text = tokenizer.apply_chat_template(list(messages[: i + 1]), tokenize=False, add_generation_prompt=False, **template_kwargs)
        if not (full.startswith(end_text) and end_text.startswith(start_text)):
            raise TrainError("the chat template is not prefix-stable across turns: loss on assistant turns only cannot be built (fix chat_template_kwargs)")
        spans.append((len(start_text), len(end_text)))
    enc = tokenizer(full, add_special_tokens=False, return_offsets_mapping=True)
    ids = list(enc["input_ids"])
    if len(ids) > max_len:
        return None
    labels = [tid if any(a <= s < b for a, b in spans) else -100 for tid, (s, _e) in zip(ids, enc["offset_mapping"])]
    return ids, labels


# ---------------------------------------------------------------- 6.3 rules

def validation_score(metrics: Mapping[str, float], weights: Mapping[str, float], format_valid_min: float) -> float:
    """``S = 0.35 x (1 - wrongful_refusal) + 0.20 x oos_refusal + 0.20 x near_miss_refusal + 0.25 x correct_source``, counted
    only if format valid is at least ``format_valid_min``; otherwise 0."""
    if metrics.get("format_valid", 0.0) < format_valid_min:
        return 0.0
    return (weights["wrongful_refusal"] * (1 - metrics["wrongful_refusal"]) + weights["oos_refusal"] * metrics["oos_refusal"]
            + weights["near_miss_refusal"] * metrics["near_miss_refusal"] + weights["correct_source"] * metrics["correct_source"])


class EarlyStopper:
    """Stop after ``patience`` consecutive validation runs without an improvement of at least ``min_delta``."""

    def __init__(self, min_delta: float, patience: int, best: float | None = None, bad: int = 0):
        self.min_delta, self.patience, self.best, self.bad = min_delta, patience, best, bad

    def update(self, score: float) -> bool:
        if self.best is None or score >= self.best + self.min_delta:
            self.best, self.bad = score, 0
        else:
            self.bad += 1
        return self.bad >= self.patience

    def state(self) -> dict[str, Any]:
        return {"best": self.best, "bad": self.bad}


class MemorisationMonitor:
    """Moving average of the training loss over ``window`` steps below ``threshold`` (only once the window is full)."""

    def __init__(self, threshold: float, window: int, values: Sequence[float] = ()):
        self.threshold, self.window = threshold, window
        self.values: deque[float] = deque(values, maxlen=window)

    def update(self, loss: float) -> bool:
        self.values.append(loss)
        return len(self.values) == self.window and sum(self.values) / self.window < self.threshold

    @property
    def average(self) -> float | None:
        return sum(self.values) / len(self.values) if self.values else None


class CheckpointBook:
    """The ``keep`` best checkpoints by validation score; an earlier step wins a tie."""

    def __init__(self, keep: int, entries: Sequence[dict[str, Any]] = ()):
        self.keep = keep
        self.entries: list[dict[str, Any]] = [dict(e) for e in entries]

    def add(self, entry: dict[str, Any]) -> list[dict[str, Any]]:
        """Add a checkpoint; return the entries that no longer belong to the kept set (their folders can be deleted)."""
        self.entries.append(entry)
        self.entries.sort(key=lambda e: (-e["score"], e["step"]))
        dropped, self.entries = self.entries[self.keep:], self.entries[: self.keep]
        return dropped


# ---------------------------------------------------------------- adapter and engine protocols

class TrainAdapter(Protocol):
    model: str

    def train_examples(self) -> list[dict[str, Any]]: ...                       # {"id", "messages": [...]}
    def validation_items(self) -> list[dict[str, Any]]: ...                     # {"id", "messages": prompt messages, ...}
    def validation_metrics(self, items: Sequence[dict[str, Any]], outputs: Sequence[str]) -> dict[str, float]: ...
    def score(self, metrics: Mapping[str, float]) -> float: ...
    def final_metrics(self, items: Sequence[dict[str, Any]], outputs: Sequence[str]) -> dict[str, Any]: ...
    def final_choice(self, records: Sequence[dict[str, Any]]) -> dict[str, Any] | None: ...


class Engine(Protocol):
    def prepare(self, resume_from: Path | None) -> None: ...
    def train_step(self, indices: Sequence[int]) -> float: ...
    def generate(self, items: Sequence[dict[str, Any]]) -> list[str]: ...
    def save_adapter(self, path: Path) -> None: ...
    def load_adapter(self, path: Path) -> None: ...
    def save_state(self, path: Path) -> None: ...
    def close(self) -> None: ...


@dataclass
class TrainOutcome:
    stop_reason: str
    steps: int
    kept: list[dict[str, Any]]
    history: list[dict[str, Any]]
    selected: dict[str, Any] | None
    final_records: list[dict[str, Any]] = field(default_factory=list)
    memorisation: bool = False


def _state_path(state_dir: Path) -> Path:
    return state_dir / "state.json"


def run_training(engine: Engine, adapter: TrainAdapter, plan: Plan, cfg: Mapping[str, Any], work_dir: Path,
                 log: Callable[[str], None] = print, resume: bool = True) -> TrainOutcome:
    """Train, validate every ``validation.every_steps``, keep the best checkpoints, stop early, select the final one.
    ``work_dir`` holds ``state.json``, ``checkpoints/last`` (resume) and ``checkpoints/step-{n}`` (kept)."""
    ckpt = work_dir / "checkpoints"
    sp = _state_path(work_dir)
    state: dict[str, Any] = {"step": 0, "history": [], "stopper": {}, "book": [], "losses": [], "stop_reason": None, "finished": False,
                             "final_records": [], "selected": None}
    if resume and sp.exists():
        state = load_json(sp)
        log(f"resuming at step {state['step']}" + (" (already finished)" if state["finished"] else ""))
    if state["finished"]:
        return _outcome(state)
    stopper = EarlyStopper(cfg["early_stopping"]["min_delta"], cfg["early_stopping"]["patience"], **state["stopper"])
    mem = MemorisationMonitor(cfg["memorisation"]["loss_below"], cfg["memorisation"]["window"], state["losses"])
    book = CheckpointBook(cfg["keep_checkpoints"], state["book"])
    every, save_every, log_every = cfg["validation"]["every_steps"], cfg["checkpoint_every_steps"], cfg["log_every_steps"]
    val_items = adapter.validation_items()
    engine.prepare(ckpt / "last" if state["step"] > 0 and (ckpt / "last").exists() else None)
    n, t0 = plan.examples, time.monotonic()

    def persist() -> None:
        state.update(stopper=stopper.state(), book=book.entries, losses=list(mem.values))
        engine.save_state(ckpt / "last")
        write_json(sp, state)

    try:
        while state["step"] < plan.total_steps and state["stop_reason"] is None:
            loss = engine.train_step(batch_indices(n, state["step"], plan.effective_batch, plan.seed))
            state["step"] += 1
            step = state["step"]
            memorised = mem.update(loss)
            if step % log_every == 0:
                log(f"step {step}/{plan.total_steps} loss {loss:.4f} ({(time.monotonic() - t0) / 60:.1f} min)")
            if memorised:
                state["stop_reason"] = "memorisation"
            due = step % every == 0 or memorised or step == plan.total_steps
            if due:
                outputs = engine.generate(val_items)
                metrics = adapter.validation_metrics(val_items, outputs)
                score = adapter.score(metrics)
                state["history"].append({"step": step, "score": round(score, 6), "loss": round(loss, 6), "metrics": metrics})
                log(f"validation at step {step}: S = {score:.4f}")
                path = ckpt / f"step-{step}"
                engine.save_adapter(path)
                for gone in book.add({"step": step, "score": score, "path": str(path)}):
                    shutil.rmtree(gone["path"], ignore_errors=True)
                if state["stop_reason"] is None and stopper.update(score):
                    state["stop_reason"] = "early_stopping"
            if due or step % save_every == 0:
                persist()
        if state["stop_reason"] is None:
            state["stop_reason"] = "max_steps"
        records = []
        state["stop_reason"] = state["stop_reason"] or "max_steps"
        persist()                                            # the loop is over: a failure below resumes straight into the final selection
        try:
            for e in book.entries:                           # final selection among the kept checkpoints (6.3)
                engine.load_adapter(Path(e["path"]))
                rec = adapter.final_metrics(val_items, engine.generate(val_items))
                records.append({"step": e["step"], "score": e["score"], **rec})
                log(f"final check of step {e['step']}: " + ", ".join(f"{k} {v}" for k, v in rec.items() if isinstance(v, (int, float))))
        except TeacherError as exc:
            raise TrainError(f"the judge failed during the final selection: {exc}. Training is finished and saved ({state['step']} steps, stop reason "
                             f"{state['stop_reason']}): start the judge host and run the same command again with --run {work_dir.parent.name} "
                             "(it goes straight to the final selection; models.md 9.4)") from exc
        chosen = adapter.final_choice(records)
        state.update(final_records=records, selected=chosen, finished=True, stopper=stopper.state(), book=book.entries, losses=list(mem.values))
        write_json(sp, state)
    finally:
        engine.close()
    return _outcome(state)


def _outcome(state: Mapping[str, Any]) -> TrainOutcome:
    return TrainOutcome(state["stop_reason"], state["step"], list(state["book"]), list(state["history"]), state["selected"],
                        list(state["final_records"]), state["stop_reason"] == "memorisation")


# ---------------------------------------------------------------- the real engine (torch, transformers, peft)

def pick_dtype(torch: Any, device: str, setting: str) -> tuple[Any, str]:
    """bf16 on CUDA and on Apple MPS where supported, otherwise fp32 (6.2)."""
    if device == "cuda":
        if setting.startswith("bf16") and torch.cuda.is_bf16_supported():
            return torch.bfloat16, "bf16"
        return torch.float32, "fp32"
    if device == "mps" and setting.startswith("bf16"):
        try:
            torch.zeros(2, dtype=torch.bfloat16, device="mps") @ torch.zeros(2, dtype=torch.bfloat16, device="mps")
            return torch.bfloat16, "bf16"
        except Exception:  # noqa: BLE001 - any failure means unsupported
            return torch.float32, "fp32"
    return torch.float32, "fp32"


class TorchEngine:
    """LoRA training of one base model on one device. Not constructed by tests or dry runs."""

    def __init__(self, base: Mapping[str, Any], cfg: Mapping[str, Any], plan: Plan, examples: Sequence[dict[str, Any]],
                 template_kwargs: Mapping[str, Any], log: Callable[[str], None] = print):
        self.base, self.cfg, self.plan, self.examples = base, cfg, plan, examples
        self.kwargs, self.log = dict(template_kwargs), log
        self.micro = plan.micro_batch
        self._cache: dict[int, tuple[list[int], list[int]]] = {}

    # -- setup
    def prepare(self, resume_from: Path | None) -> None:
        import torch  # type: ignore
        from peft import LoraConfig, get_peft_model  # type: ignore
        from transformers import AutoTokenizer, get_cosine_schedule_with_warmup  # type: ignore
        from .loading import load_text_model
        self.torch = torch
        dev = self.plan.device
        self.dtype, name = pick_dtype(torch, dev, self.cfg["precision"].get(dev, "fp32"))
        self.log(f"device {dev}, precision {name}, micro-batch {self.micro} x accumulation {self.plan.grad_accum}")
        torch.manual_seed(self.plan.seed)
        self.tok = AutoTokenizer.from_pretrained(self.base["hub_id"], revision=self.base["revision"])
        if self.tok.pad_token_id is None:
            self.tok.pad_token = self.tok.eos_token
        model = load_text_model(self.base["hub_id"], self.base["revision"], self.dtype, self.cfg.get("loading"))
        model.to(dev)
        model.config.use_cache = False
        if hasattr(model, "gradient_checkpointing_enable"):
            model.gradient_checkpointing_enable()
            model.enable_input_require_grads()
        a = self.cfg["adapter"]
        lora = dict(r=a["rank"], lora_alpha=a["alpha"], lora_dropout=a["dropout"], target_modules=a["target_modules"], task_type="CAUSAL_LM")
        if a.get("exclude_modules"):
            lora["exclude_modules"] = a["exclude_modules"]       # keeps the adapter out of the vision tower (peft 0.14 or later)
        self.model = get_peft_model(model, LoraConfig(**lora))
        o = self.cfg["optimizer"]
        params = [p for p in self.model.parameters() if p.requires_grad]
        self.opt = torch.optim.AdamW(params, lr=o["learning_rate"], weight_decay=o["weight_decay"])
        self.sched = get_cosine_schedule_with_warmup(self.opt, o["warmup_steps"], max(1, self.plan.total_steps))
        self.params = params
        if resume_from is not None:
            self.load_state(resume_from)
        self.model.train()

    def _example(self, i: int) -> tuple[list[int], list[int]]:
        if i not in self._cache:
            got = tokenize_conversation(self.tok, self.examples[i]["messages"], self.kwargs, self.cfg["max_seq_len"])
            if got is None:
                raise TrainError(f"example {self.examples[i]['id']} is longer than max_seq_len ({self.cfg['max_seq_len']}); a target is never cut (run validate: V13)")
            self._cache[i] = got
        return self._cache[i]

    def _collate(self, rows: Sequence[tuple[list[int], list[int]]]):
        torch, width = self.torch, max(len(r[0]) for r in rows)
        pad = self.tok.pad_token_id
        ids = torch.tensor([r[0] + [pad] * (width - len(r[0])) for r in rows])
        lab = torch.tensor([r[1] + [-100] * (width - len(r[1])) for r in rows])
        att = torch.tensor([[1] * len(r[0]) + [0] * (width - len(r[0])) for r in rows])
        dev = self.plan.device
        return ids.to(dev), att.to(dev), lab.to(dev)

    # -- training
    def train_step(self, indices: Sequence[int]) -> float:
        torch = self.torch
        rows = [self._example(i) for i in indices]
        total_tokens = sum(sum(1 for t in r[1][1:] if t != -100) for r in rows) or 1
        while True:
            try:
                self.opt.zero_grad(set_to_none=True)
                loss_sum = 0.0
                for s in range(0, len(rows), self.micro):
                    ids, att, lab = self._collate(rows[s: s + self.micro])
                    logits = self.model(input_ids=ids, attention_mask=att).logits
                    loss = torch.nn.functional.cross_entropy(logits[:, :-1].reshape(-1, logits.size(-1)).float(), lab[:, 1:].reshape(-1),
                                                             ignore_index=-100, reduction="sum") / total_tokens
                    loss.backward()
                    loss_sum += float(loss.detach())
                break
            except RuntimeError as exc:                      # out of memory: halve the micro-batch and retry the step
                if "out of memory" not in str(exc).lower() or self.micro == 1:
                    raise
                self.micro = max(1, self.micro // 2)
                self.log(f"out of memory: micro-batch now {self.micro}")
                if self.plan.device == "cuda":
                    torch.cuda.empty_cache()
        torch.nn.utils.clip_grad_norm_(self.params, self.cfg["optimizer"]["max_grad_norm"])
        self.opt.step()
        self.sched.step()
        return loss_sum

    # -- validation generation (greedy, left padding)
    def generate(self, items: Sequence[dict[str, Any]]) -> list[str]:
        torch = self.torch
        self.model.eval()
        self.model.config.use_cache = True
        self.tok.padding_side = "left"
        outs: list[str] = []
        bs = self.cfg["validation"]["batch_size"]
        with torch.no_grad():
            for s in range(0, len(items), bs):
                texts = [self.tok.apply_chat_template(it["messages"], tokenize=False, add_generation_prompt=True, **self.kwargs) for it in items[s: s + bs]]
                enc = self.tok(texts, return_tensors="pt", padding=True, add_special_tokens=False).to(self.plan.device)
                gen = self.model.generate(**enc, max_new_tokens=self.cfg["validation"]["max_new_tokens"], do_sample=False, temperature=None, top_p=None,
                                          top_k=None, pad_token_id=self.tok.pad_token_id)
                outs += [self.tok.decode(g[enc["input_ids"].shape[1]:], skip_special_tokens=True) for g in gen]
        self.model.config.use_cache = False
        self.model.train()
        return outs

    # -- checkpoints
    def save_adapter(self, path: Path) -> None:
        path.mkdir(parents=True, exist_ok=True)
        self.model.save_pretrained(str(path))

    def load_adapter(self, path: Path) -> None:
        from peft import set_peft_model_state_dict  # type: ignore
        from safetensors.torch import load_file  # type: ignore
        set_peft_model_state_dict(self.model, load_file(str(path / "adapter_model.safetensors")))

    def save_state(self, path: Path) -> None:
        self.save_adapter(path)
        self.torch.save({"optimizer": self.opt.state_dict(), "scheduler": self.sched.state_dict(), "rng": self.torch.get_rng_state()},
                        str(path / "training_state.pt"))

    def load_state(self, path: Path) -> None:
        self.load_adapter(path)
        st = self.torch.load(str(path / "training_state.pt"), map_location="cpu")
        self.opt.load_state_dict(st["optimizer"])
        self.sched.load_state_dict(st["scheduler"])
        self.torch.set_rng_state(st["rng"])

    def close(self) -> None:
        for name in ("model", "opt", "sched"):
            if hasattr(self, name):
                delattr(self, name)
        try:
            if self.plan.device == "cuda":
                self.torch.cuda.empty_cache()
        except Exception:  # noqa: BLE001
            pass
