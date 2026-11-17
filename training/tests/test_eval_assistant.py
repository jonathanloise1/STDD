"""The assistant's evaluation: thresholds equal the documents, metrics, the judge, reports, export loss, candidates, the project bench."""
import json
import re
from pathlib import Path

import pytest

from conftest import ROOT
from support import FakeJudgeClient, make_assistant_repo, oracle_backend, write_labels
from training.assistant import eval as E, metrics as M
from training.common import evalcore as EC, judge as J
from training.common.jsonio import load_json, read_jsonl, write_json
from training.common.judge import Verdict

MODELS_MD = ROOT / "docs" / "guidelines" / "models.md"
MANUAL_MD = ROOT / "docs" / "guidelines" / "manual.md"


# ---------------------------------------------------------------- configuration equals the documents

def _table_rows(text: str, heading: str) -> dict[str, str]:
    block = re.search(re.escape(heading) + r".*?\n\n(\|.*?)\n\n", text, re.S).group(1)
    rows = {}
    for line in block.splitlines()[2:]:
        cells = [c.strip() for c in line.strip("|").split("|")]
        rows[cells[0]] = cells[1]
    return rows


def test_eval_json_thresholds_equal_models_md_4_5():
    if not MODELS_MD.exists():
        pytest.skip("models.md not available")
    rows = _table_rows(MODELS_MD.read_text(encoding="utf-8"), "### 4.5 Thresholds")
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "eval.json")
    names = {"Format valid": "format_valid", "Faithful": "faithful", "Useful": "useful", "Wrongful refusal": "wrongful_refusal", "Wrongful refusal, distant": "wrongful_refusal_distant",
             "Out-of-scope refusal": "oos_refusal", "Near-miss refusal": "near_miss_refusal", "Correct source": "correct_source"}
    for doc_name, key in names.items():
        op, value = rows[doc_name].split()
        assert cfg["model_thresholds"][key] == {"op": op, "value": float(value)}, doc_name
    assert len(cfg["model_thresholds"]) == len(names)
    gap = next(v for k, v in rows.items() if k.startswith("Transfer gap"))
    assert cfg["transfer_gap"] == {"op": gap.split()[0], "value": float(gap.split()[1])}
    loss = next(v for k, v in rows.items() if k.startswith("Export loss"))
    useful, faithful = (float(x) for x in re.findall(r"<= ([\d.]+)", loss))
    assert cfg["export_loss"] == {"useful_drop_max": useful, "faithful_drop_max": faithful}


def test_eval_json_latency_targets_equal_models_md():
    text = MODELS_MD.read_text(encoding="utf-8") if MODELS_MD.exists() else pytest.skip("models.md not available")
    para = re.search(r"Latency, per tier.*?\n\n", text, re.S).group(0)
    seconds = [int(x) for x in re.findall(r"<= (\d+) s", para)]
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "eval.json")["latency"]["targets"]
    assert [cfg["browser-webgpu"]["p95_ms"], cfg["server-gpu"]["p95_ms"], cfg["server-cpu"]["p95_ms"]] == [s * 1000 for s in seconds]


def test_eval_json_sample_app_gates_equal_manual_md_8_3():
    if not MANUAL_MD.exists():
        pytest.skip("manual.md not available")
    text = MANUAL_MD.read_text(encoding="utf-8")
    block = re.search(r"Answer metrics apply when.*?\n\n(\|.*?)\n\n", text, re.S).group(1)
    gates = {}
    for line in block.splitlines()[2:]:
        name, gate = [c.strip() for c in line.strip("|").split("|")]
        gates[name] = gate.split()
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "eval.json")["sample_app"]["gates"]
    expect = {"Faithful (of answered)": "faithful", "Useful (of in-scope)": "useful", "Wrongful refusal (of in-scope with a hit in the context)": "wrongful_refusal",
              "Out-of-scope refusal (no-match or model refusal)": "oos_refusal", "Correct source (of answered)": "correct_source"}
    for doc, key in expect.items():
        assert cfg[key] == {"op": gates[doc][0], "value": float(gates[doc][1])}, doc


def test_judge_and_suite_settings_follow_the_document():
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "eval.json")
    assert cfg["judge"]["min_human_agreement"] == 0.90 and cfg["judge"]["audit_items"] == 30
    assert cfg["latency"]["items"] == 100 and cfg["latency"]["max_new_tokens"] == 120 and cfg["latency"]["blocks_release"] is False
    assert cfg["suite_version"] == 1 and cfg["test_set_sha256"] is None      # frozen by --freeze-sets, not shipped


def test_config_files_are_complete_and_unverified_candidates_are_refused_for_real_runs():
    from training.common import train_core as T
    cands = T.load_candidates(ROOT / "training" / "assistant" / "config" / "candidates.json")
    assert [c["id"] for c in cands] == ["qwen3.5-0.8b", "qwen3.5-2b", "qwen3-0.6b"]          # Qwen only (owner decision of 2026-11-16)
    assert all(c["verified"] is False and c["checked"]["date"] and c["chat_template_kwargs"] == {"enable_thinking": False} for c in cands)
    assert all(c["parameters"] <= T.MAX_PARAMETERS for c in cands)
    assert all(len(c["revision"]) == 40 for c in cands if c["kind"] != "fallback") and cands[2]["revision"] == "c1899de289a04d12100db370d81485cdf75e47ca"
    with pytest.raises(T.TrainError, match="verified"):
        T.select_base_model(cands, None, real_run=True)
    with pytest.raises(T.TrainError, match="verified"):                                  # the fallback is pinned now, but not verified by the owner
        T.select_base_model(cands, "qwen3-0.6b", real_run=True)
    assert T.select_base_model(cands, None)["id"] == "qwen3.5-0.8b"        # smallest first (6.1 rule 2); the fallback is never chosen by size
    tj = load_json(ROOT / "training" / "assistant" / "config" / "train.json")
    assert (tj["adapter"]["rank"], tj["adapter"]["alpha"], tj["adapter"]["dropout"]) == (16, 32, 0.05)
    assert (tj["optimizer"]["learning_rate"], tj["optimizer"]["warmup_steps"], tj["optimizer"]["weight_decay"]) == (1e-4, 50, 0.0)
    assert (tj["batch"]["effective"], tj["max_seq_len"], tj["epochs"], tj["keep_checkpoints"]) == (32, 2048, 2, 3)
    assert (tj["validation"]["every_steps"], tj["early_stopping"], tj["memorisation"]) == (250, {"min_delta": 0.005, "patience": 4}, {"loss_below": 0.05, "window": 50})
    assert tj["score_weights"] == {"wrongful_refusal": 0.35, "oos_refusal": 0.20, "near_miss_refusal": 0.20, "correct_source": 0.25}


# ---------------------------------------------------------------- metrics

def res(i, cls, outcome, cited=(), gold=(1,), overlap=0.5, lang="en"):
    return {"id": f"x{i}", "lang": lang, "class": cls, "outcome": outcome, "cited": list(cited), "gold": list(gold), "overlap": overlap, "source": "model", "latency_ms": 100.0, "ttft_ms": 20.0}


def test_metrics_follow_the_definitions_of_4_3():
    rs = [res(1, "pos_distant", "answered", [1], overlap=0.05), res(2, "pos_distant", "refused", overlap=0.1), res(3, "pos_mid", "answered", [2], overlap=0.2),
          res(4, "pos_near", "invalid", overlap=0.5), res(5, "ref_oos", "refused", gold=()), res(6, "ref_oos", "answered", [1], gold=()),
          res(7, "ref_near_miss", "refused", gold=()), res(8, "ref_near_miss", "refused", gold=())]
    v = {r["id"]: Verdict(True, r["id"] == "x1", []) for r in rs if r["outcome"] == "answered"}
    m = M.compute_metrics(rs, v)
    assert m["format_valid"] == 7 / 8
    assert m["oos_refusal"] == 0.5 and m["near_miss_refusal"] == 1.0
    assert m["wrongful_refusal"] == 0.5                         # refused (2) and invalid (4) of 4 in scope
    assert m["wrongful_refusal_distant"] == 0.5                 # items 1 and 2 are distant, item 2 refused
    assert m["correct_source"] == 0.5                           # answered in scope: x1 cites gold 1, x3 cites 2
    assert m["faithful"] == 1.0 and m["useful"] == 0.25         # only x1 is useful, of 4 in scope
    assert m["n"] == 8 and m["n_in_scope"] == 4 and m["answered"] == 3


def test_metrics_without_judge_do_not_measure_faithful_and_useful():
    m = M.compute_metrics([res(1, "pos_mid", "answered", [1])], None)
    assert m["faithful"] is None and m["useful"] is None
    assert EC.check_rules({"faithful": None}, {"faithful": {"op": ">=", "value": 0.95}}, "x")[0]["value"] is None      # never passes


def test_empty_populations_are_none_not_zero():
    m = M.compute_metrics([res(1, "pos_mid", "answered", [1])], {"x1": Verdict(True, True, [])})
    assert m["oos_refusal"] is None and m["near_miss_refusal"] is None


def test_percentiles_and_latency_summary():
    rs = [{"source": "model", "latency_ms": float(i), "ttft_ms": float(i) / 10} for i in range(1, 101)]
    s = M.latency_summary(rs)
    assert (s["p50_ms"], s["p95_ms"], s["n"]) == (50.0, 95.0, 100)


# ---------------------------------------------------------------- the judge

def _item(i=1):
    return {"id": f"j{i}", "question": "q", "context": [{"title": "t", "text": "x"}], "gold": [1], "reference": "r"}


def test_judge_retries_once_then_counts_as_not_faithful(tmp_path):
    tpl = E.Template.load(ROOT / "training" / "assistant" / "eval", "judge.v1")
    client = FakeJudgeClient(outputs=["not json", "still not json"])
    judge = J.Judge(client, "m", tpl, 1, tmp_path)
    v = judge.judge(_item(), "answer")
    assert (v.faithful, v.useful, v.valid) == (False, False, False) and client.calls == 2
    again = judge.judge(_item(), "answer")                                  # cached: no third call
    assert client.calls == 2 and again.cached


def test_judge_schema_check_and_cache_key(tmp_path):
    assert J.parse_verdict('{"faithful": true, "useful": false, "unsupported": ["x"]}').useful is False
    assert J.parse_verdict('{"faithful": "yes", "useful": true}') is None
    assert J.parse_verdict('{"faithful": true, "useful": true, "extra": 1}') is None
    tpl = E.Template.load(ROOT / "training" / "assistant" / "eval", "judge.v1")
    client = FakeJudgeClient()
    judge = J.Judge(client, "m", tpl, 1, tmp_path)
    judge.judge(_item(), "answer one")
    judge.judge(_item(), "answer two")                                      # a different answer is judged again
    assert client.calls == 2
    j2 = J.Judge(client, "m", tpl, 2, tmp_path)                             # a different prompt version too
    j2.judge(_item(), "answer one")
    assert client.calls == 3


def test_judge_estimate_gives_tokens_and_a_duration_from_the_configured_speed(tmp_path):
    tpl = E.Template.load(ROOT / "training" / "assistant" / "eval", "judge.v1")
    pairs = [(_item(i), f"answer {i}") for i in (1, 2, 3)]
    slow = J.estimate(pairs, J.Judge(FakeJudgeClient(), "m", tpl, 1, tmp_path / "a", {"tokens_per_second": 10, "prompt_tokens_per_second": 100}))
    fast = J.estimate(pairs, J.Judge(FakeJudgeClient(), "m", tpl, 1, tmp_path / "b", {"tokens_per_second": 20, "prompt_tokens_per_second": 200}))
    unknown = J.estimate(pairs, J.Judge(FakeJudgeClient(), "m", tpl, 1, tmp_path / "c"))
    assert slow["calls"] == 3 and slow["output_tokens"] == 180 and slow["input_tokens"] > 0 and "cost_usd" not in slow
    assert slow["seconds"] == pytest.approx(2 * fast["seconds"], rel=0.01) and unknown["seconds"] == 0.0
    assert slow["id"] != fast["id"]                                         # the rates are part of what the owner approves


def test_a_judged_item_is_never_judged_again_after_an_interrupted_run(tmp_path):
    tpl = E.Template.load(ROOT / "training" / "assistant" / "eval", "judge.v1")
    client = FakeJudgeClient()
    judge = J.Judge(client, "m", tpl, 1, tmp_path)
    judge.judge(_item(1), "a")
    again = J.Judge(client, "m", tpl, 1, tmp_path)                          # a new process resumes from the cache
    assert again.judge(_item(1), "a").cached and client.calls == 1
    assert J.estimate([(_item(1), "a"), (_item(2), "b")], again)["calls"] == 1


# ---------------------------------------------------------------- full evaluations

@pytest.fixture()
def repo(validated_assistant, tmp_path):
    out, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out, cdir)
    write_labels(root)
    return root


def make_run(root: Path, run_id="2026-11-20-a", version="1.0.0") -> Path:
    run_dir = root / "training" / "out" / "assistant" / "runs" / run_id
    write_json(run_dir / "run.json", {"model_version": version, "base_model": {"hub_id": "o/b", "revision": "a" * 40, "license": "x", "parameters": 6, "chat_template_kwargs": {}}})
    return run_dir


def env_for(root: Path, backend, client=None, messages=None):
    return E.Env(training_dir=root / "training", repo_root=root, backend_factory=lambda **kw: backend, client_factory=lambda jc: client or FakeJudgeClient(),
                 log=(messages if messages is not None else []).append)


def bench(root: Path, n_in=22):
    """A project bench and packs from one test domain, so the rendered prompts equal the dataset's."""
    recs = read_jsonl(root / "training" / "out" / "assistant" / "dataset" / "test.jsonl")
    packs, rows = {}, []
    for lang in ("en", "it"):
        mine = [r for r in recs if r["lang"] == lang]
        sections = {}
        for r in mine:
            for s in r["context"]:
                sections[s["id"]] = {**s, "story": s["id"]}
        packs[lang] = {"project": {"name": mine[0]["app_name"]}, "sections": list(sections.values())}
        ins = [r for r in mine if r["class"].startswith("pos_")][:n_in]
        oos = [r for r in mine if r["class"] == "ref_oos"][:6]
        for r in ins:
            rows.append({"id": r["id"], "lang": lang, "question": r["question"], "kind": "in_scope", "story": r["context"][r["gold"][0] - 1]["id"], "context": [s["id"] for s in r["context"]], "in_scope_decision": True})
        for r in oos:
            rows.append({"id": r["id"], "lang": lang, "question": r["question"], "kind": "out_of_scope", "story": None, "context": [s["id"] for s in r["context"]], "in_scope_decision": True})
    pack_dir = root / "packs"
    for lang, p in packs.items():
        write_json(pack_dir / lang / "pack.json", p)
    bench_file = root / "bench.jsonl"
    bench_file.write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    return bench_file, pack_dir, rows


def freeze(env):
    assert E.main(["--freeze-sets"], env) == 0


def test_a_full_evaluation_passes_and_writes_the_report_of_4_6(repo):
    backend = oracle_backend(repo)
    msgs: list[str] = []
    env = env_for(repo, backend, messages=msgs)
    freeze(env)
    run_dir = make_run(repo)
    bench_file, pack_dir, rows = bench(repo)
    est_id = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))["id"]
    code = E.main(["--export", "merged", "--run", run_dir.name, "--approved-estimate", est_id, "--items", str(bench_file), "--pack-dir", str(pack_dir), "--latency-tier", "server-gpu"], env)
    assert code == 0, "\n".join(msgs)
    rep = load_json(run_dir / "eval-merged.json")
    assert rep["passed"] is True and rep["failures"] == []
    assert list(rep)[:9] == ["schema_version", "model", "model_version", "export", "suite_version", "thresholds_sha256", "test_set_sha256", "judge", "hardware"]
    assert rep["thresholds_sha256"] == EC.thresholds_sha256("assistant", repo / "training")
    assert rep["judge"]["human_agreement"] == {"faithful": 1.0, "useful": 1.0}
    pt = rep["results"]["platform_test"]
    assert set(pt) == {"en", "it"} and pt["en"]["format_valid"] == 1.0 and pt["en"]["useful"] == 1.0 and pt["en"]["oos_refusal"] == 1.0
    assert set(rep["results"]) >= {"platform_test", "platform_end_to_end", "sample_app", "seen_domains", "latency"}
    assert rep["results"]["latency"]["tier"] == "server-gpu" and rep["results"]["latency"]["meets_target"] is True
    assert rep["results"]["sample_app"]["en"]["gated"] is True
    from training.common.hashing import sha256_file
    assert rep["items_sha256"] == sha256_file(run_dir / "items-merged.json".replace(".json", ".jsonl"))
    assert load_json(run_dir / "run.json")["commands"][-1]["command"] == "eval --export merged"


def test_a_real_run_needs_the_approved_estimate_and_no_model_runs_before(repo):
    backend = oracle_backend(repo)
    env = env_for(repo, backend)
    freeze(env)
    run_dir = make_run(repo)
    assert E.main(["--export", "merged", "--run", run_dir.name], env) == 2
    assert E.main(["--export", "merged", "--run", run_dir.name, "--approved-estimate", "000000000000"], env) == 2
    assert backend.calls == [] and not (run_dir / "eval-merged.json").exists()


def test_an_unfrozen_or_changed_test_set_is_refused(repo):
    env = env_for(repo, oracle_backend(repo))
    run_dir = make_run(repo)
    est = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))["id"]
    assert E.main(["--export", "merged", "--run", run_dir.name, "--approved-estimate", est, "--no-end-to-end"], env) == 2
    freeze(env)
    test = repo / "training" / "out" / "assistant" / "dataset" / "test.jsonl"
    test.write_text(test.read_text(encoding="utf-8").replace("\n", "\n", 1) + "\n", encoding="utf-8")
    with pytest.raises(E.EvalError, match="differs"):
        E.check_frozen(env, E.load_eval_config(env))


def test_wrongful_refusals_fail_the_thresholds_and_are_listed(repo):
    ids = {r["id"] for r in read_jsonl(repo / "training" / "out" / "assistant" / "dataset" / "test.jsonl") if r["class"].startswith("pos_")}
    backend = oracle_backend(repo, wrong=ids)
    msgs: list[str] = []
    env = env_for(repo, backend, messages=msgs)
    freeze(env)
    run_dir = make_run(repo)
    est = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))["id"]
    bench_file, pack_dir, _ = bench(repo)
    code = E.main(["--export", "merged", "--run", run_dir.name, "--approved-estimate", est, "--items", str(bench_file), "--pack-dir", str(pack_dir), "--no-end-to-end"], env)
    assert code == 1
    rep = load_json(run_dir / "eval-merged.json")
    assert rep["passed"] is False
    metrics = {(f["where"], f["metric"]) for f in rep["failures"]}
    assert ("platform_test/en", "wrongful_refusal") in metrics and ("platform_test/it", "useful") in metrics
    assert any("FAILED" in m for m in msgs)


def test_no_judge_means_the_report_cannot_pass(repo):
    env = env_for(repo, oracle_backend(repo))
    freeze(env)
    run_dir = make_run(repo)
    code = E.main(["--export", "merged", "--run", run_dir.name, "--no-judge", "--no-end-to-end"], env)
    assert code == 1
    rep = load_json(run_dir / "eval-merged.json")
    assert rep["judge"]["model"] is None and rep["results"]["platform_test"]["en"]["faithful"] is None
    assert any(f["metric"] == "faithful" for f in rep["failures"])


def test_a_judge_that_disagrees_with_the_human_labels_fails_calibration(repo):
    client = FakeJudgeClient(verdict={"faithful": False, "useful": False, "unsupported": ["x"]})
    env = env_for(repo, oracle_backend(repo), client=client)
    freeze(env)
    run_dir = make_run(repo)
    est = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))["id"]
    E.main(["--export", "merged", "--run", run_dir.name, "--approved-estimate", est, "--no-end-to-end"], env)
    rep = load_json(run_dir / "eval-merged.json")
    assert {"human_agreement.faithful", "human_agreement.useful"} <= {f["metric"] for f in rep["failures"]}


def test_missing_human_labels_are_a_failure_not_a_pass(repo):
    (repo / "training" / "assistant" / "eval" / "human-labels.jsonl").unlink()
    env = env_for(repo, oracle_backend(repo))
    freeze(env)
    run_dir = make_run(repo)
    est = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))["id"]
    E.main(["--export", "merged", "--run", run_dir.name, "--approved-estimate", est, "--no-end-to-end"], env)
    assert any(f["where"] == "judge" and f["metric"] == "human_agreement" for f in load_json(run_dir / "eval-merged.json")["failures"])


def test_export_loss_is_measured_against_the_merged_report(repo):
    good = oracle_backend(repo)
    env = env_for(repo, good)
    freeze(env)
    run_dir = make_run(repo)
    est = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))["id"]
    common = ["--run", run_dir.name, "--approved-estimate", est, "--no-end-to-end"]
    # an export before the merged report exists: the loss cannot be measured
    E.main(["--export", "gguf", *common], env)
    assert any(f["metric"] == "merged report" for f in load_json(run_dir / "eval-gguf.json")["failures"])
    E.main(["--export", "merged", *common], env)
    E.main(["--export", "gguf", *common], env)
    gguf = load_json(run_dir / "eval-gguf.json")
    assert not [f for f in gguf["failures"] if f["where"].startswith("export_loss")]
    # a lossy export: 20 % of the in-scope answers become refusals
    pos = sorted(r["id"] for r in read_jsonl(repo / "training" / "out" / "assistant" / "dataset" / "test.jsonl") if r["class"].startswith("pos_"))
    lossy = env_for(repo, oracle_backend(repo, wrong=set(pos[::5])))
    E.main(["--export", "onnx", *common], lossy)
    onnx = load_json(run_dir / "eval-onnx.json")
    fails = {f["metric"] for f in onnx["failures"] if f["where"].startswith("export_loss")}
    assert "useful_drop" in fails and onnx["passed"] is False


def test_candidates_are_measured_and_the_smallest_passing_one_wins(repo):
    cfg = load_json(repo / "training" / "assistant" / "config" / "candidates.json")
    seen = []

    def factory(**kw):
        seen.append(kw["candidate"]["id"])
        return oracle_backend(repo, refuse_all=kw["candidate"]["id"] == "qwen3-0.6b")      # the smallest (the fallback) never answers: fails

    env = E.Env(training_dir=repo / "training", repo_root=repo, backend_factory=factory, client_factory=lambda jc: FakeJudgeClient(), log=lambda m: None)
    freeze(env)
    est = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False), runs=len(cfg["candidates"]), untrained=True)["id"]
    assert E.main(["--candidates", "--allow-unverified", "--approved-estimate", est, "--no-end-to-end"], env) == 0
    assert seen == ["qwen3-0.6b", "qwen3.5-0.8b", "qwen3.5-2b"]
    summary = load_json(repo / "training" / "out" / "assistant" / "candidates" / "summary.json")
    assert summary["winner"] == "qwen3.5-0.8b" and summary["candidates"]["qwen3-0.6b"]["passed"] is False
    assert (repo / "models" / "assistant" / "eval" / "candidates" / "qwen3.5-0.8b.json").exists()


def test_candidates_need_the_estimate_approval_and_pinned_models(repo):
    env = env_for(repo, oracle_backend(repo))
    assert E.main(["--candidates", "--allow-unverified"], env) == 2
    env.backend_factory = None                                              # a real run: the candidates are unpinned and unverified
    assert E.main(["--candidates", "--no-judge"], env) == 2


def test_estimate_is_stable_and_runs_no_model(repo, capsys):
    backend = oracle_backend(repo)
    env = env_for(repo, backend)
    a = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))
    b = E.judge_estimate(env, E.load_eval_config(env), E.build_judge(env, E.load_eval_config(env), False))
    assert a == b and a["calls"] > 0 and backend.calls == []
    assert E.main(["--estimate"], env) == 0


def test_real_judge_run_needs_a_model_but_no_key(repo, monkeypatch):
    env = E.Env(training_dir=repo / "training", repo_root=repo)
    monkeypatch.delenv("JUDGE_API_KEY", raising=False)
    cfg = E.load_eval_config(env)
    cfg["judge"]["model"] = "served-name"
    assert E.build_judge(env, cfg, True).model == "served-name"             # no key variable needed for a local host
    assert cfg["judge"]["base_url"] == "http://localhost:8093/v1"
    cfg = E.load_eval_config(env)
    cfg["judge"]["model"] = None
    with pytest.raises(E.EvalError, match="judge.model"):
        E.build_judge(env, cfg, True)


def test_the_project_suite_alone_writes_its_own_report(repo):
    env = env_for(repo, oracle_backend(repo))
    run_dir = make_run(repo)
    bench_file, pack_dir, rows = bench(repo)
    cfg = E.load_eval_config(env)
    judge = E.build_judge(env, cfg, False)
    est = E._project_estimate(env, cfg, judge, type("A", (), {"items": bench_file})())["id"]
    assert E.main(["--suite", "project", "--items", str(bench_file), "--pack-dir", str(pack_dir), "--export", "gguf", "--run", run_dir.name, "--approved-estimate", est], env) == 0
    rep = load_json(run_dir / "eval-project-gguf.json")
    assert rep["passed"] and "platform_test" not in rep["results"] and rep["results"]["sample_app"]["it"]["n_in_scope"] == 22


def test_the_project_suite_defaults_to_the_gguf_export_as_in_models_md_2_2(repo):
    env = env_for(repo, oracle_backend(repo))
    run_dir = make_run(repo)
    bench_file, pack_dir, rows = bench(repo)
    cfg = E.load_eval_config(env)
    est = E._project_estimate(env, cfg, E.build_judge(env, cfg, False), type("A", (), {"items": bench_file})())["id"]
    assert E.main(["--suite", "project", "--items", str(bench_file), "--pack-dir", str(pack_dir), "--approved-estimate", est], env) == 0
    assert (run_dir / "eval-project-gguf.json").exists()


def test_a_language_with_few_bench_items_is_reported_but_not_gated(repo):
    env = env_for(repo, oracle_backend(repo))
    run_dir = make_run(repo)
    bench_file, pack_dir, rows = bench(repo, n_in=5)
    cfg = E.load_eval_config(env)
    est = E._project_estimate(env, cfg, E.build_judge(env, cfg, False), type("A", (), {"items": bench_file})())["id"]
    E.main(["--suite", "project", "--items", str(bench_file), "--pack-dir", str(pack_dir), "--export", "gguf", "--run", run_dir.name, "--approved-estimate", est], env)
    assert load_json(run_dir / "eval-project-gguf.json")["results"]["sample_app"]["en"]["gated"] is False


def test_a_question_the_out_of_scope_decision_rejected_is_a_refusal_without_a_model_call(repo):
    env = env_for(repo, oracle_backend(repo))
    bench_file, pack_dir, rows = bench(repo, n_in=2)
    rows = [dict(r, in_scope_decision=False) if r["kind"] == "out_of_scope" else r for r in rows]
    items = E.bench_items(rows, E.load_packs(pack_dir))
    backend = oracle_backend(repo)
    from training.assistant import protocol as P
    out = M.run_items(backend, P.load_protocol(repo / "training" / "assistant" / "protocol.json"), items)
    oos = [r for r in out if r["class"] == "ref_oos"]
    assert oos and all(r["outcome"] == "refused" and r["source"] == "no-match" for r in oos)
    assert len(backend.calls) == len(out) - len(oos)


def test_runtime_items_lose_the_gold_when_retrieval_missed_it():
    items = [{"id": "a", "class": "pos_mid", "context": [{"id": "s1"}], "gold": [1]}, {"id": "b", "class": "ref_oos", "context": [], "gold": []}]
    rc = {"a": {"context": [{"id": "s9", "title": "t", "text": "x"}], "gold_ids": ["s1"]}, "b": {"context": [], "gold_ids": []}}
    out = E.runtime_items(items, rc)
    assert out[0]["class"] == "nohit" and out[0]["gold"] == [] and out[1]["class"] == "ref_oos"
