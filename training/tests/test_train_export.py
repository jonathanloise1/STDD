"""Training (6.1 to 6.4) and export (6.5, 6.6): the decisions, the commands and the dry runs, with fake engines and fake export tools."""
import json
import shutil
import sys
from pathlib import Path

import pytest

from conftest import ROOT
from support import FakeEngine, FakeExportTools, FakeJudgeClient, hardware, make_assistant_repo, write_labels
from training.assistant import export as AEXPORT, train as ATRAIN
from training.assistant.adapter import AssistantAdapter, AssistantSetup
from training.common import export_core as X, export_cli, hardware as H, train_cli, train_core as T
from training.common.jsonio import load_json, read_jsonl, write_json


# ---------------------------------------------------------------- 6.3 rules

def test_validation_score_counts_only_with_a_valid_format():
    w = {"wrongful_refusal": 0.35, "oos_refusal": 0.20, "near_miss_refusal": 0.20, "correct_source": 0.25}
    m = {"format_valid": 1.0, "wrongful_refusal": 0.0, "oos_refusal": 1.0, "near_miss_refusal": 1.0, "correct_source": 1.0}
    assert T.validation_score(m, w, 0.98) == pytest.approx(1.0)
    assert T.validation_score({**m, "wrongful_refusal": 0.1}, w, 0.98) == pytest.approx(0.965)
    assert T.validation_score({**m, "format_valid": 0.97}, w, 0.98) == 0.0


def test_early_stopper_needs_an_improvement_of_the_minimum_delta():
    s = T.EarlyStopper(0.005, 4)
    assert [s.update(x) for x in (0.80, 0.81, 0.812, 0.811, 0.813, 0.812)] == [False, False, False, False, False, True]
    s = T.EarlyStopper(0.005, 4)
    assert not any(s.update(x) for x in (0.8, 0.81, 0.82, 0.83, 0.84))      # steady improvement never stops
    s2 = T.EarlyStopper(0.005, 2, **s.state())
    assert s2.best == 0.84 and s2.update(0.84) is False and s2.update(0.84) is True


def test_memorisation_monitor_needs_a_full_window_below_the_limit():
    m = T.MemorisationMonitor(0.05, 5)
    assert [m.update(0.01) for _ in range(5)] == [False, False, False, False, True]
    m = T.MemorisationMonitor(0.05, 5)
    assert [m.update(x) for x in (0.9, 0.01, 0.01, 0.01, 0.01, 0.01)] == [False] * 5 + [True]     # the 0.9 must leave the window first


def test_checkpoint_book_keeps_the_best_three_and_the_earlier_on_a_tie():
    b = T.CheckpointBook(3)
    dropped = [b.add({"step": s, "score": sc, "path": f"p{s}"}) for s, sc in ((250, 0.7), (500, 0.8), (750, 0.8), (1000, 0.9), (1250, 0.75))]
    assert [e["step"] for e in b.entries] == [1000, 500, 750]
    assert [d[0]["step"] for d in dropped if d] == [250, 1250]


def test_batches_are_deterministic_and_cover_every_example_per_epoch():
    n, eff = 70, 32
    spe = 3
    seen = [i for s in range(spe) for i in T.batch_indices(n, s, eff, 7)]
    assert sorted(set(seen)) == list(range(n)) and T.batch_indices(n, 1, eff, 7) == T.batch_indices(n, 1, eff, 7)
    assert T.batch_indices(n, 0, eff, 7) != T.batch_indices(n, spe, eff, 7)         # a new epoch is a new order


def test_plan_follows_the_hardware_and_the_effective_batch():
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "train.json")
    plan = T.build_plan(cfg, 20000, hardware("cuda", vram=24), 1, {})
    assert (plan.effective_batch, plan.steps_per_epoch, plan.total_steps) == (32, 625, 1250)
    assert plan.micro_batch * plan.grad_accum == 32 and plan.precision == "bf16"
    mps = T.build_plan(cfg, 100, hardware("mps", ram=16, vram=None), 1, {})
    assert mps.micro_batch == 2 and mps.grad_accum == 16 and mps.precision == "bf16-if-supported-else-fp32"
    assert T.build_plan(cfg, 100, hardware("cuda"), 1, {"a": 1}).plan_id != T.build_plan(cfg, 100, hardware("cuda"), 1, {"a": 2}).plan_id


def test_base_model_selection_rules():
    cands = [{"id": "big", "hub_id": "o/b", "revision": "a" * 40, "parameters": 1_000_000_000, "license": "x", "context_length": 8192, "verified": True},
             {"id": "small", "hub_id": "o/s", "revision": "b" * 40, "parameters": 360_000_000, "license": "x", "context_length": 8192, "verified": True},
             {"id": "huge", "hub_id": "o/h", "revision": "c" * 40, "parameters": 3_000_000_000, "license": "x", "context_length": 8192, "verified": True},
             {"id": "short", "hub_id": "o/c", "revision": "d" * 40, "parameters": 500_000_000, "license": "x", "context_length": 2048, "verified": True}]
    assert T.select_base_model(cands, None, real_run=True)["id"] == "small"
    assert T.select_base_model(cands, "big")["id"] == "big"
    for bad, text in (("huge", "2,500,000,000"), ("short", "context")):
        with pytest.raises(T.TrainError, match=text):
            T.select_base_model(cands, bad)
    with pytest.raises(T.TrainError, match="not in candidates"):
        T.select_base_model(cands, "nope")


# ---------------------------------------------------------------- loss on assistant turns only

class CharTokenizer:
    """One token per character with offsets; the template wraps each message as ``<role>content</>``."""

    def apply_chat_template(self, messages, tokenize=False, add_generation_prompt=False, **kw):
        text = "".join(f"<{m['role']}>{m['content']}</>" for m in messages)
        return text + ("<assistant>" if add_generation_prompt else "")

    def __call__(self, text, add_special_tokens=False, return_offsets_mapping=False):
        return {"input_ids": [ord(c) for c in text], "offset_mapping": [(i, i + 1) for i in range(len(text))]}


def test_only_assistant_tokens_carry_a_loss():
    msgs = [{"role": "system", "content": "SYS"}, {"role": "user", "content": "ASK"}, {"role": "assistant", "content": "ANS"}]
    ids, labels = T.tokenize_conversation(CharTokenizer(), msgs, {}, 100)
    trained = "".join(chr(t) for t, l in zip(ids, labels) if l != -100)
    assert trained == "ANS</>"                                   # the answer and the end of the turn; the prompt is masked
    assert all(l == -100 for l in labels[: len("<system>SYS</><user>ASK</><assistant>")])


def test_every_assistant_turn_of_a_conversation_is_trained():
    msgs = [{"role": "system", "content": "S"}, {"role": "user", "content": "U"}, {"role": "assistant", "content": "A1"}, {"role": "tool", "content": "T"}, {"role": "assistant", "content": "A2"}]
    ids, labels = T.tokenize_conversation(CharTokenizer(), msgs, {}, 100)
    assert "".join(chr(t) for t, l in zip(ids, labels) if l != -100) == "A1</>A2</>"


def test_an_overlong_example_is_dropped_and_an_unstable_template_is_refused():
    msgs = [{"role": "user", "content": "x" * 50}, {"role": "assistant", "content": "y"}]
    assert T.tokenize_conversation(CharTokenizer(), msgs, {}, 10) is None

    class Unstable(CharTokenizer):
        def apply_chat_template(self, messages, tokenize=False, add_generation_prompt=False, **kw):
            return super().apply_chat_template(messages, tokenize, add_generation_prompt, **kw).replace("<assistant>", "<a>", 1) if len(messages) % 2 == 0 else super().apply_chat_template(messages, tokenize, add_generation_prompt)
    with pytest.raises(T.TrainError, match="prefix-stable"):
        T.tokenize_conversation(Unstable(), msgs, {}, 100)


# ---------------------------------------------------------------- the loop

@pytest.fixture()
def repo(validated_assistant, tmp_path):
    out, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out, cdir)
    write_labels(root)
    return root


def tiny_cfg(root: Path, **over):
    p = root / "training" / "assistant" / "config" / "train.json"
    cfg = load_json(p)
    cfg.update({"batch": {"effective": 32}, "epochs": 2, "validation": {**cfg["validation"], "every_steps": 4}, "checkpoint_every_steps": 2, "log_every_steps": 1000,
                "early_stopping": {"min_delta": 0.005, "patience": 3}, "memorisation": {"loss_below": 0.0, "window": 50}})
    cfg.update(over)
    write_json(p, cfg)
    return cfg


def adapter_for(root: Path, cfg, client=None):
    setup = AssistantSetup(root / "training")
    from training.assistant import eval as E
    env = E.Env(training_dir=root / "training", repo_root=root, client_factory=lambda jc: client or FakeJudgeClient())
    return setup, setup.adapter(cfg, E.build_judge(env, E.load_eval_config(env), False))


def run_loop(root, cfg, engine_kwargs=None, resume=True, engine=None, client=None, work=None):
    setup, adapter = adapter_for(root, cfg, client)
    examples = adapter.train_examples()
    plan = T.build_plan(cfg, len(examples), hardware(), 1, {})
    eng = engine or FakeEngine(adapter, **(engine_kwargs or {}))
    work = work or root / "work"
    msgs: list[str] = []
    out = T.run_training(eng, adapter, plan, cfg, work, msgs.append, resume=resume)
    return out, eng, plan, msgs


def test_the_loop_validates_keeps_three_and_selects_with_the_judge(repo):
    cfg = tiny_cfg(repo)
    out, eng, plan, msgs = run_loop(repo, cfg, {"good_until": 10 ** 9})
    assert out.stop_reason in ("max_steps", "early_stopping") and out.steps >= 4
    assert len(out.kept) <= 3 and all((repo / "work" / "checkpoints" / f"step-{e['step']}").exists() or True for e in out.kept)
    assert out.selected is not None and out.selected["faithful"] >= 0.95
    assert eng.closed and set(eng.loaded) == {f"step-{e['step']}" for e in out.kept}      # every kept checkpoint was evaluated with the judge
    assert {h["step"] for h in out.history} >= {4}
    on_disk = {p.name for p in (repo / "work" / "checkpoints").iterdir()}
    assert on_disk == {f"step-{e['step']}" for e in out.kept} | {"last"}                   # the others were deleted


def test_training_stops_early_when_the_score_stops_improving_and_picks_the_peak(repo):
    cfg = tiny_cfg(repo)
    out, eng, plan, msgs = run_loop(repo, cfg, {"good_until": 8})
    assert out.stop_reason == "early_stopping" and out.steps < plan.total_steps
    best = max(out.history, key=lambda h: h["score"])
    assert best["step"] <= 8 and out.selected["step"] <= 8                            # never a checkpoint past the peak
    assert any(e["step"] == best["step"] for e in out.kept)


def test_memorisation_stops_training_and_is_marked(repo):
    cfg = tiny_cfg(repo, memorisation={"loss_below": 0.5, "window": 3})
    out, eng, plan, msgs = run_loop(repo, cfg, {"loss0": 1.0, "decay": 0.5})
    assert out.stop_reason == "memorisation" and out.memorisation and out.steps < plan.total_steps
    assert out.selected is not None                                                    # the kept checkpoints are still compared


def test_a_run_that_crashes_resumes_from_its_saved_state(repo):
    cfg = tiny_cfg(repo)
    setup, adapter = adapter_for(repo, cfg)
    plan = T.build_plan(cfg, len(adapter.train_examples()), hardware(), 1, {})
    work = repo / "work"
    with pytest.raises(RuntimeError, match="simulated"):
        T.run_training(FakeEngine(adapter, crash_at=7), adapter, plan, cfg, work, lambda m: None)
    saved = load_json(work / "state.json")
    assert saved["step"] == 6 and not saved["finished"]
    second = FakeEngine(adapter)
    out = T.run_training(second, adapter, plan, cfg, work, lambda m: None)
    assert second.prepared_from == work / "checkpoints" / "last" and second.step >= 6
    fresh_out, *_ = run_loop(repo, cfg, work=repo / "fresh")
    assert [h["step"] for h in out.history] == [h["step"] for h in fresh_out.history]    # same validations as an uninterrupted run
    again = T.run_training(FakeEngine(adapter), adapter, plan, cfg, work, lambda m: None)    # finished: nothing runs again
    assert again.selected == out.selected


def test_no_kept_checkpoint_with_enough_faithfulness_selects_nothing(repo):
    cfg = tiny_cfg(repo)
    client = FakeJudgeClient(verdict={"faithful": False, "useful": True, "unsupported": ["x"]})
    out, *_ = run_loop(repo, cfg, client=client)
    assert out.selected is None


# ---------------------------------------------------------------- the command

OK_HW = lambda info, params: (True, [])      # noqa: E731  the machine check is tested in test_common; here the device is faked


def cli_args(*extra):
    return ["--allow-unverified", *extra]


def train_env(repo, monkeypatch):
    cands = repo / "training" / "assistant" / "config" / "candidates.json"
    c = load_json(cands)
    for x in c["candidates"]:
        x["revision"] = "f" * 40
        x["verified"] = True
    write_json(cands, c)


def test_dry_run_prints_the_plan_and_imports_no_heavy_dependency(repo):
    tiny_cfg(repo)
    for heavy in ("torch", "transformers", "peft", "onnxruntime"):
        assert heavy not in sys.modules
    lines: list[str] = []
    before = sorted(str(p) for p in (repo / "training").rglob("*"))
    code = ATRAIN.main(["--dry-run"], training_dir=repo / "training", hardware=hardware("mps", ram=32, vram=None), out=lines.append, evaluate=OK_HW)
    text = "\n".join(lines)
    assert code == 0 and "plan id:" in text and "2fc06364715b967f1860aea9cf38778875588b17" in text and "loss on assistant turns only" in text
    assert "micro-batch 4 x accumulation 8" in text and "lora rank 16" in text and "qwen3.5-0.8b" in text
    assert sorted(str(p) for p in (repo / "training").rglob("*")) == before                  # writes nothing
    for heavy in ("torch", "transformers", "peft"):
        assert heavy not in sys.modules


def test_dry_run_refuses_a_missing_dataset_and_a_bad_config(repo, capsys):
    shutil.rmtree(repo / "training" / "out" / "assistant" / "dataset")
    assert ATRAIN.main(["--dry-run"], training_dir=repo / "training", hardware=hardware()) == 2
    cfg = load_json(repo / "training" / "assistant" / "config" / "train.json")
    del cfg["epochs"]
    write_json(repo / "training" / "assistant" / "config" / "train.json", cfg)
    assert ATRAIN.main(["--dry-run"], training_dir=repo / "training", hardware=hardware()) == 2


def plan_id_of(repo, hw) -> str:
    lines: list[str] = []
    ATRAIN.main(["--dry-run"], training_dir=repo / "training", hardware=hw, out=lines.append, evaluate=OK_HW)
    return next(l for l in lines if "plan id:" in l).split()[2]


def test_a_real_run_needs_cuda_or_mps_a_pinned_model_and_the_approved_plan(repo, monkeypatch):
    tiny_cfg(repo)
    hw = hardware("cuda")
    pid = plan_id_of(repo, hw)
    # CPU is refused
    cpu = hardware("cpu", vram=None)
    assert ATRAIN.main(["--approved-plan", pid], training_dir=repo / "training", hardware=cpu, engine_factory=lambda *a: None, evaluate=OK_HW) == 2
    # an unpinned base model is refused
    assert ATRAIN.main(["--approved-plan", pid], training_dir=repo / "training", hardware=hw, engine_factory=lambda *a: None, evaluate=OK_HW) == 2
    train_env(repo, monkeypatch)
    # the plan must be approved, and the approval is for this plan only
    assert ATRAIN.main([], training_dir=repo / "training", hardware=hw, engine_factory=lambda *a: None, evaluate=OK_HW) == 2
    assert ATRAIN.main(["--approved-plan", "000000000000"], training_dir=repo / "training", hardware=hw, engine_factory=lambda *a: None, evaluate=OK_HW) == 2


def test_a_real_run_with_a_fake_engine_writes_the_run_keys_the_lock_reads(repo, monkeypatch):
    from training.common import lock as LOCK
    tiny_cfg(repo)
    train_env(repo, monkeypatch)
    hw = hardware("cuda")
    pid = plan_id_of(repo, hw)
    holder = {}

    def factory(base, cfg, plan, examples, kwargs, log):
        setup, adapter = adapter_for(repo, cfg)
        holder["engine"] = FakeEngine(adapter)
        holder["base"] = base
        return holder["engine"]

    out: list[str] = []
    code = ATRAIN.main(["--approved-plan", pid, "--model-version", "1.0.0"], training_dir=repo / "training", hardware=hw, engine_factory=factory, out=out.append, evaluate=OK_HW,
                       judge_factory=lambda real: adapter_for(repo, load_json(repo / "training/assistant/config/train.json"))[1].judge)
    assert code == 0, out
    run_id = sorted(p.name for p in (repo / "training" / "out" / "assistant" / "runs").iterdir())[-1]   # the newest run: the directory order is not defined
    run = load_json(repo / "training" / "out" / "assistant" / "runs" / run_id / "run.json")
    assert all(k in run for k in LOCK.RUN_KEYS)
    assert run["status"] == "trained" and run["model_version"] == "1.0.0" and run["adapter"]["checkpoint"].startswith("step-")
    assert run["adapter"]["rank"] == 16 and run["base_model"]["hub_id"] == holder["base"]["hub_id"] and run["training"]["seed"] == 20261106
    assert run["dataset"]["domains"] == {"train": 3, "validation": 1, "test": 1} and run["dataset"]["examples"] > 0
    assert run["commands"][-1]["command"] == "train" and "selection" in run and "stop_reason" in run


def test_the_judge_model_is_checked_before_hours_of_training(repo, monkeypatch):
    tiny_cfg(repo)
    train_env(repo, monkeypatch)
    ev = repo / "training" / "assistant" / "config" / "eval.json"
    cfg = load_json(ev)
    cfg["judge"]["model"] = None
    write_json(ev, cfg)
    hw = hardware("cuda")
    pid = plan_id_of(repo, hw)
    assert ATRAIN.main(["--approved-plan", pid], training_dir=repo / "training", hardware=hw, engine_factory=lambda *a: pytest.fail("trained"), evaluate=OK_HW) == 2


# ---------------------------------------------------------------- export

def trained_run(repo, run_id="2026-11-20-a", version="1.0.0") -> Path:
    run_dir = repo / "training" / "out" / "assistant" / "runs" / run_id
    write_json(run_dir / "run.json", {"model_version": version, "status": "trained",
                                      "base_model": {"hub_id": "o/b", "revision": "a" * 40, "license": "x", "parameters": 6, "chat_template_kwargs": {"enable_thinking": False}},
                                      "adapter": {"method": "lora", "rank": 16, "alpha": 32, "checkpoint": "step-8"}})
    ck = run_dir / "train" / "checkpoints" / "step-8"
    ck.mkdir(parents=True)
    (ck / "adapter_model.safetensors").write_bytes(b"adapter")
    return run_dir


PINNED = {"TRANSFORMERS_JS_DIR": ".", "LLAMA_CPP_DIR": "."}


def pin_export(repo):
    p = repo / "training" / "assistant" / "config" / "export.json"
    c = load_json(p)
    c["transformers_js"]["tag"] = "v3.0.0"
    c["llama_cpp"]["tag"] = "b9999"
    write_json(p, c)


def test_export_dry_run_prints_every_step_and_command_and_imports_nothing(repo, capsys):
    trained_run(repo)
    lines: list[str] = []
    code = AEXPORT.main(["--dry-run"], training_dir=repo / "training", out=lines.append, env={})
    text = "\n".join(lines)
    assert code == 0 and "step-8" in text and "convert.py" in text and "convert_hf_to_gguf.py" in text and "Q4_K_M" in text and "q4f16" in text
    assert "a real export would be refused: export.json transformers_js.tag is not pinned" in text
    assert "9 golden cases" not in text and "golden cases" in text
    assert not (repo / "training" / "out" / "assistant" / "runs" / "2026-11-20-a" / "exports.json").exists()
    for heavy in ("torch", "transformers", "peft", "onnxruntime"):
        assert heavy not in sys.modules


def test_export_dry_run_for_a_candidate_needs_no_run(repo):
    lines: list[str] = []
    assert AEXPORT.main(["--dry-run", "--candidate", "qwen3-0.6b"], training_dir=repo / "training", out=lines.append, env={}) == 0
    assert "untrained candidate" in "\n".join(lines) and "enable_thinking" in "\n".join(lines)


def test_a_real_export_is_refused_without_pinned_exporters(repo):
    trained_run(repo)
    assert AEXPORT.main([], training_dir=repo / "training", env={}, tools_factory=lambda cfg: FakeExportTools()) == 2
    pin_export(repo)
    assert AEXPORT.main([], training_dir=repo / "training", env={}, tools_factory=lambda cfg: FakeExportTools()) == 2       # checkouts missing
    assert AEXPORT.main([], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: FakeExportTools()) == 0


def test_export_writes_the_manifest_the_lock_reads_and_checks_the_template(repo):
    run_dir = trained_run(repo)
    pin_export(repo)
    tools = FakeExportTools()
    out: list[str] = []
    assert AEXPORT.main([], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: tools, out=out.append) == 0
    m = load_json(run_dir / "exports.json")
    fmts = {e["format"]: e for e in m["exports"]}
    assert set(fmts) == {"onnx", "gguf"} and fmts["onnx"]["variant"] == "q4f16" and fmts["gguf"]["variant"] == "Q4_K_M"
    assert fmts["onnx"]["runtime"] == "browser" and fmts["gguf"]["runtime"] == "server"
    assert {f["path"] for f in fmts["onnx"]["files"]} >= {"onnx/model_q4f16.onnx", "config.json", "tokenizer.json", "tokenizer_config.json", "generation_config.json"}
    assert fmts["gguf"]["files"][0]["path"] == "assistant-1.0.0-Q4_K_M.gguf"
    assert all(len(f["sha256"]) == 64 and f["bytes"] > 0 for e in m["exports"] for f in e["files"])
    assert m["template_check"]["passed"] and m["template_check"]["cases"] >= 12 and m["template_check"]["renderers"] == ["llama.cpp", "python", "transformers.js"]
    assert tools.calls == ["merge", "onnx-q4f16", "gguf-Q4_K_M"]
    assert any("next:" in l for l in out)


def test_export_is_resumable_and_rebuilds_only_what_changed(repo):
    run_dir = trained_run(repo)
    pin_export(repo)
    t1 = FakeExportTools()
    AEXPORT.main([], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: t1)
    t2 = FakeExportTools()
    AEXPORT.main([], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: t2)
    assert t2.calls == []                                                       # everything up to date
    t3 = FakeExportTools()
    AEXPORT.main(["--variant", "gguf=Q8_0"], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: t3)
    assert t3.calls == ["gguf-Q8_0"]                                            # only the changed variant, the fallback of 6.5
    assert load_json(run_dir / "exports.json")["exports"][1]["variant"] == "Q8_0"


def test_a_template_mismatch_fails_the_export_with_the_first_difference(repo):
    run_dir = trained_run(repo)
    pin_export(repo)
    out: list[str] = []
    assert AEXPORT.main([], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: FakeExportTools(js_differs=True), out=out.append) == 1
    m = load_json(run_dir / "exports.json")
    assert m["template_check"]["passed"] is False and m["template_check"]["mismatches"][0]["renderer"] == "transformers.js"
    assert any("TEMPLATE CHECK FAILED" in l for l in out)


def test_exporting_an_untrained_candidate_records_its_status(repo):
    pin_export(repo)
    c = repo / "training" / "assistant" / "config" / "candidates.json"
    data = load_json(c)
    for x in data["candidates"]:
        x["revision"], x["verified"] = "e" * 40, True
    write_json(c, data)
    assert AEXPORT.main(["--candidate", "qwen3-0.6b"], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: FakeExportTools()) == 0
    run_id = sorted(p.name for p in (repo / "training" / "out" / "assistant" / "runs").iterdir())[-1]   # the newest run: the directory order is not defined
    run = load_json(repo / "training" / "out" / "assistant" / "runs" / run_id / "run.json")
    assert run["status"] == "untrained-candidate" and run["adapter"] is None and run["dataset"] is None and run["base_model"]["hub_id"] == "Qwen/Qwen3-0.6B"


def test_a_run_that_did_not_train_is_not_exported(repo, capsys):
    run_dir = trained_run(repo)
    run = load_json(run_dir / "run.json")
    run["status"] = "failed"
    write_json(run_dir / "run.json", run)
    assert AEXPORT.main(["--dry-run"], training_dir=repo / "training", env={}) == 2


def test_the_evaluation_hook_runs_eval_for_merged_and_every_export(repo):
    trained_run(repo)
    pin_export(repo)
    calls: list[list[str]] = []
    code = AEXPORT.main(["--evaluate", "--approved-estimate", "abc123abc123"], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: FakeExportTools(),
                        eval_main=lambda a: calls.append(a) or 0)
    assert code == 0 and [c[1] for c in calls] == ["merged", "onnx", "gguf"] and all(c[-1] == "abc123abc123" for c in calls)
    stop = AEXPORT.main(["--evaluate", "--approved-estimate", "x"], training_dir=repo / "training", env=PINNED, tools_factory=lambda cfg: FakeExportTools(), eval_main=lambda a: 1)
    assert stop == 1


def test_export_json_pins_exist_but_are_not_filled_in():
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "export.json")
    assert cfg["transformers_js"]["tag"] is None and cfg["llama_cpp"]["tag"] is None and cfg["variants"] == {"onnx": "q4f16", "gguf": "Q4_K_M"}
    assert cfg["fallback_variants"] == {"onnx": "q8", "gguf": "Q8_0"} and cfg["formats"] == ["onnx", "gguf"]
