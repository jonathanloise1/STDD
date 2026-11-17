"""Qwen3.5 support (models.md 6.1, 6.2, 6.5): untrained-only candidates, text-only loading, the teacher's thinking switch, the judge rule and the protocol."""
import re
from pathlib import Path

import pytest

from conftest import ROOT
from support import FakeExportTools, make_inv_repo
from test_common import Stand
from training.common import backends as B, judge as J, loading as LD, teacher as TE, train_cli, train_core as T
from training.common.jsonio import load_json, write_json
from training.investigator import export as IX, protocol as P, train as IT

CFG = ROOT / "training"


def cand(**kw):
    return {"id": "m", "tier": "large", "hub_id": "o/m", "revision": "a" * 40, "parameters": 35_000_000_000, "license": "Apache-2.0", "context_length": 8192, "verified": True, **kw}


# ---------------------------------------------------------------- untrained-only candidates

def test_an_untrained_only_candidate_is_refused_by_train_accepted_by_eval_and_export_and_exempt_from_the_ceiling():
    moe = cand(trainable=False)
    assert T.check_candidate(moe, 14_000_000_000, purpose="eval") == [] and T.check_candidate(moe, 14_000_000_000, purpose="export") == []
    assert "trainable false" in T.check_candidate(moe, 14_000_000_000, purpose="train")[0]
    assert "above" in T.check_candidate(cand(), 14_000_000_000)[0]                               # a trainable one keeps the ceiling
    with pytest.raises(T.TrainError, match="trainable false"):
        T.select_base_model([moe], "m", max_parameters=14_000_000_000, purpose="train")
    assert T.select_base_model([moe], "m", max_parameters=14_000_000_000)["id"] == "m"
    small = cand(id="s", parameters=9_000_000_000)
    assert T.select_base_model([moe, small], None, max_parameters=14_000_000_000, purpose="train")["id"] == "s"      # default training choice skips it


def test_the_shipped_investigator_list_marks_the_moe_untrained_only_and_the_cli_refuses_to_train_it(tmp_path):
    cands = load_json(CFG / "investigator" / "config" / "candidates.json")["candidates"]
    by = {c["id"]: c for c in cands}
    assert by["qwen3.6-35b-a3b"]["trainable"] is False and by["qwen3.6-35b-a3b"]["tier"] == "large"
    assert all(c["tool_role"] == "user" and c["chat_template_kwargs"] == {"enable_thinking": False} and c["verified"] is False and c["checked"]["date"] for c in cands)
    assert T.check_candidate(by["qwen3.6-35b-a3b"], None, purpose="eval") == []
    root = make_inv_repo(tmp_path)
    errs: list[str] = []
    code = IT.main(["--tier", "large", "--candidate", "qwen3.6-35b-a3b", "--dry-run", "--allow-unverified"], training_dir=root / "training", out=errs.append)
    assert code == 2


def test_the_lock_records_an_untrained_only_candidate(tmp_path):
    root = make_inv_repo(tmp_path)
    tdir = root / "training"
    ex = load_json(tdir / "investigator" / "config" / "export.json")
    ex["llama_cpp"]["tag"] = "b9999"
    write_json(tdir / "investigator" / "config" / "export.json", ex)
    code = IX.main(["--candidate", "qwen3.6-35b-a3b", "--tier", "large", "--allow-unverified", "--from-bf16"], training_dir=tdir, env={"LLAMA_CPP_DIR": "."},
                   tools_factory=lambda c: FakeExportTools(), out=lambda m: None)
    assert code == 0
    out = tdir / "out" / "investigator" / "runs"
    run = load_json(max(out.iterdir()) / "run.json")
    assert run["status"] == "untrained-candidate" and run["base_model"]["trainable"] is False and run["base_model"]["tool_role"] == "user"


# ---------------------------------------------------------------- loading and configuration

class P_:
    def __init__(self):
        self.requires_grad = True


class FakeModel:
    def __init__(self, names):
        self.p = {n: P_() for n in names}

    def named_parameters(self):
        return self.p.items()


def test_the_vision_tower_is_frozen_and_the_text_model_is_not():
    m = FakeModel(["model.visual.blocks.0.attn.qkv.weight", "model.language_model.layers.0.self_attn.q_proj.weight", "model.language_model.layers.0.linear_attn.in_proj_qkv.weight",
                   "model.multi_modal_projector.linear.weight", "lm_head.weight"])
    assert LD.freeze_vision(m) == 2
    assert [n for n, p in m.p.items() if not p.requires_grad] == ["model.visual.blocks.0.attn.qkv.weight", "model.multi_modal_projector.linear.weight"]


def test_the_conditional_generation_class_is_found_in_the_architectures():
    class C:
        architectures = ["Qwen3_5ForConditionalGeneration"]
    assert LD.architecture_class_name(C()) == "Qwen3_5ForConditionalGeneration"
    assert LD.architecture_class_name(type("D", (), {"architectures": ["Qwen3ForCausalLM"]})()) is None


def test_the_training_configs_carry_the_deltanet_targets_the_loading_block_and_refuse_flash_attention_with_packing():
    for name in ("assistant", "investigator"):
        cfg = load_json(CFG / name / "config" / "train.json")
        mods = cfg["adapter"]["target_modules"]
        assert {"q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj", "in_proj_qkv", "in_proj_z", "in_proj_b", "in_proj_a", "out_proj"} <= set(mods)
        assert cfg["loading"]["text_only"] and cfg["loading"]["freeze_vision_tower"] and cfg["loading"]["attn_implementation"] is None and cfg["loading"]["packing"] is False
        assert cfg["precision"]["cuda"] == "bf16" and "field reports" in cfg["loading"]["note"]
        train_cli.validate_config(cfg)
        bad = {**cfg, "loading": {**cfg["loading"], "attn_implementation": "flash_attention_2", "packing": True}}
        with pytest.raises(T.TrainError, match="flash_attention_2"):
            train_cli.validate_config(bad)


def test_the_export_configs_mark_text_only_and_the_first_test_on_the_pc():
    for name in ("assistant", "investigator"):
        ex = load_json(CFG / name / "config" / "export.json")
        assert ex["text_only"] is True and "FIRST THING TO TEST" in ex["note"] and ex["first_test_on_pc"]
    assert "q4f16" == load_json(CFG / "assistant" / "config" / "export.json")["variants"]["onnx"]


# ---------------------------------------------------------------- teacher and judge

def test_the_teacher_turns_thinking_off_and_strips_think_blocks():
    assert TE.strip_think("plain answer") == "plain answer"
    assert TE.strip_think("<think>a\nb</think>\n\nanswer") == "answer"
    assert TE.strip_think("thought</think>answer") == "answer"
    assert TE.strip_think("answer<think>cut off") == "answer"
    s = Stand()
    try:
        TE.HttpTeacherClient(s.url).complete(model="qwen3.8-27b", system="s", user="u", params={"max_tokens": 4}, seed=1)
        assert s.requests[-1]["body"]["chat_template_kwargs"] == {"enable_thinking": False}
        TE.HttpTeacherClient(s.url, chat_template_kwargs={}).complete(model="m", system="s", user="u", params={"max_tokens": 4}, seed=1)
        assert "chat_template_kwargs" not in s.requests[-1]["body"]
    finally:
        s.close()


def test_the_judge_must_differ_from_the_model_it_judges():
    assert J.same_model("qwen3.5-2b", ["Qwen/Qwen3.5-2B"]) == "Qwen/Qwen3.5-2B"
    assert J.same_model("Qwen3.5-2B-Q4_K_M.gguf", ["qwen3.5-2b"]) == "qwen3.5-2b"
    assert J.same_model("qwen3.8-27b", ["Qwen/Qwen3.5-0.8B", "qwen3.5-0.8b", None]) is None
    assert J.same_model(None, ["x"]) is None
    from training.assistant import eval as AE
    with pytest.raises(AE.EvalError, match="must differ"):
        AE.check_judge_differs(type("J_", (), {"model": "qwen3.5-0.8b"})(), "qwen3.5-0.8b", "Qwen/Qwen3.5-0.8B")
    AE.check_judge_differs(type("J_", (), {"model": "qwen3.8-27b"})(), "qwen3.5-0.8b")


# ---------------------------------------------------------------- the protocol is the only tool format

NATIVE = ("<tool_call>", "</tool_call>", "<function=", "<parameter=", "<tool_response>", "</tool_response>")


def test_no_renderer_passes_tools_to_the_chat_template():
    for path in (ROOT / "training").rglob("*.py"):
        if "tests" in path.parts:
            continue
        text = path.read_text(encoding="utf-8")
        for m in re.finditer(r"apply_chat_template\([^)]*\)", text):
            assert "tools" not in m.group(0), f"{path} passes tools to the chat template"


def test_the_protocol_renderings_contain_nothing_of_the_native_tool_format():
    proto = P.load_protocol()
    texts = [P.system_text(proto), P.render_call(proto, "t", {"a": 1}), P.render_result(proto, {"status": "ok", "evidence_id": "E2", "rows": []}, 3),
             P.render_repair(proto, ["e"])]
    texts += [c["expected"] for c in load_json(CFG / "investigator" / "protocol.golden.json")["render"]]
    for t in texts:
        assert not any(n in t for n in NATIVE)


def test_a_tool_role_user_candidate_sends_results_as_user_turns():
    sent = []

    class Inner:
        name = "inner"

        def generate(self, messages, **kw):
            sent.append(messages)
            return B.GenResult("x")

        def apply_template(self, messages):
            return "|".join(m["role"] for m in messages)

        def info(self):
            return {}

        def close(self):
            pass

    msgs = [{"role": "system", "content": "s"}, {"role": "user", "content": "u"}, {"role": "assistant", "content": "a"}, {"role": "tool", "content": "r"}]
    mapped = B.RoleMappedBackend(Inner())
    assert mapped.apply_template(msgs) == "system|user|assistant|user"
    mapped.generate(msgs)
    assert [m["role"] for m in sent[0]] == ["system", "user", "assistant", "user"] and msgs[3]["role"] == "tool"
