"""The complete offline rehearsals: every command of a model, one after the other, in a temporary repository, with fakes.

Assistant: fake teacher -> generate -> validate -> freeze -> train (fake engine) -> export (fake tools) -> eval merged, onnx, gguf (fake backend
and judge) -> lock --record -> card -> lock --verify. Investigator: generate (executor double) -> validate -> freeze -> train -> export -> eval
-> lock --record -> card -> lock --verify. This proves that the commands fit together (their files, hashes and keys); it proves nothing about
a real model, a real exporter, a real judge or the monitor's real executor.
"""
import json
import shutil
from pathlib import Path

import pytest

from conftest import ROOT, small_setup
from support import FakeEngine, FakeExportTools, FakeJudgeClient, hardware, make_assistant_repo, make_inv_repo, oracle_backend, oracle_investigator, write_labels
from test_eval_assistant import bench
from test_train_export import OK_HW, pin_export, tiny_cfg, train_env
from training.assistant import eval as AE, export as AX, generate as G, train as AT, validate as VAL
from training.assistant.fake_teacher import FakeTeacher
from training.common import card as CARD, lock as LOCK
from training.common.backends import FakeBackend
from training.common.hashing import sha256_file
from training.common.jsonio import load_json, read_jsonl, write_json
from training.investigator import eval as IE, export as IX, generate as IG, tools as TL, train as IT, validate as IV

PINNED = {"TRANSFORMERS_JS_DIR": ".", "LLAMA_CPP_DIR": "."}


def test_assistant_rehearsal_from_the_fake_teacher_to_a_verified_lock(tmp_path, monkeypatch):
    # 1. fake teacher -> generate (the offline teacher stands in for the local teacher host: no key, no network)
    cfg, doms, retrieval = small_setup()
    gen_out = tmp_path / "gen"
    cdir = gen_out / "cfg"
    cdir.mkdir(parents=True)
    write_json(cdir / "dataset.json", cfg)
    write_json(cdir / "domains.json", doms)
    est = G.build_estimate(cfg, doms, retrieval)
    assert G.cmd_run(cfg, doms, retrieval, ["test", "validation", "train"], est["id"], gen_out, client_factory=lambda pipe: FakeTeacher(pipe), out=lambda m: None) == 0
    root = make_assistant_repo(tmp_path, gen_out, cdir)
    train_dir = root / "training"
    out = train_dir / "out" / "assistant"

    # 2. validate (dataset-level shares need the full data; the files are what the next steps read)
    VAL.run(out, root / "training" / "assistant" / "config", tools="off", docs_root=root / "docs", out=lambda m: None)
    assert (out / "dataset" / "train.jsonl").exists() and (out / "dataset" / "seen.jsonl").exists()
    write_labels(root)

    # 3. freeze the evaluation sets, 4. dry run and train with a fake engine
    env = AE.Env(training_dir=train_dir, repo_root=root, backend_factory=lambda **kw: oracle_backend(root), client_factory=lambda jc: FakeJudgeClient(), log=lambda m: None)
    assert AE.main(["--freeze-sets"], env) == 0
    frozen = load_json(root / "training" / "assistant" / "config" / "eval.json")
    assert frozen["test_set_sha256"] == sha256_file(out / "dataset" / "test.jsonl")
    tiny_cfg(root)
    train_env(root, monkeypatch)                                  # pins and verifies the candidates, sets a fake judge key
    lines: list[str] = []
    hw = hardware("cuda")
    assert AT.main(["--dry-run"], training_dir=train_dir, hardware=hw, out=lines.append, evaluate=OK_HW) == 0
    plan_id = next(l for l in lines if "plan id:" in l).split()[2]

    def engine_factory(base, tcfg, plan, examples, kwargs, log):
        from test_train_export import adapter_for
        return FakeEngine(adapter_for(root, tcfg)[1])

    def judge_factory(real):
        return AE.build_judge(env, AE.load_eval_config(env), False)

    assert AT.main(["--approved-plan", plan_id], training_dir=train_dir, hardware=hw, engine_factory=engine_factory, judge_factory=judge_factory, evaluate=OK_HW, out=lambda m: None) == 0
    run_id = max(p.name for p in (out / "runs").iterdir())
    run = load_json(out / "runs" / run_id / "run.json")
    assert run["status"] == "trained" and all(k in run for k in LOCK.RUN_KEYS)

    # 5. export (fake tools), with the template check
    pin_export(root)
    assert AX.main(["--run", run_id], training_dir=train_dir, env=PINNED, tools_factory=lambda c: FakeExportTools(), out=lambda m: None) == 0
    manifest = load_json(out / "runs" / run_id / "exports.json")
    assert manifest["template_check"]["passed"] and {e["format"] for e in manifest["exports"]} == {"onnx", "gguf"}

    # 6. evaluate merged, onnx and gguf (fake backend, fake judge, the project bench) with the approved estimate
    bench_file, pack_dir, rows = bench(root)
    cfg_eval = AE.load_eval_config(env)
    est_id = AE.judge_estimate(env, cfg_eval, AE.build_judge(env, cfg_eval, False))["id"]
    for fmt in ("merged", "onnx", "gguf"):
        code = AE.main(["--export", fmt, "--run", run_id, "--approved-estimate", est_id, "--items", str(bench_file), "--pack-dir", str(pack_dir), "--latency-tier", "server-gpu"], env)
        assert code == 0, (fmt, load_json(out / "runs" / run_id / f"eval-{fmt}.json")["failures"])

    # 7. record, card, verify
    url = "https://artifacts.example.invalid/assistant/1.0.0"
    assert LOCK.main(["--record", "--model", "assistant", "--run", run_id, "--artifact-url", url, "--root", str(root)]) == 0
    assert CARD.main(["--model", "assistant", "--root", str(root)]) == 0
    assert LOCK.main(["--verify", "--root", str(root)]) == 0
    lock = load_json(root / "models" / "model.lock")["models"]["assistant"]
    assert lock["status"] == "trained" and lock["model_version"] == "1.0.0" and lock["adapter"]["checkpoint"] == run["adapter"]["checkpoint"]
    assert [e["format"] for e in lock["exports"]] == ["onnx", "gguf"] and lock["exports"][1]["url"] == url + "/gguf/"
    assert lock["evaluation"]["passed"] is True and lock["protocol"]["sha256"] == sha256_file(root / "training" / "assistant" / "protocol.json")
    for name in ("1.0.0.json", "1.0.0-onnx.json", "1.0.0-gguf.json"):
        assert (root / "models" / "assistant" / "eval" / name).exists()
    # verification is not vacuous: a changed threshold file or protocol breaks it (L04, L03)
    ev = root / "training" / "assistant" / "config" / "eval.json"
    original = ev.read_text(encoding="utf-8")
    ev.write_text(original.replace('"value": 0.99', '"value": 0.5'), encoding="utf-8")
    assert LOCK.main(["--verify", "--root", str(root)]) == 1
    ev.write_text(original, encoding="utf-8")
    assert LOCK.main(["--verify", "--root", str(root)]) == 0


def test_assistant_rehearsal_of_a_candidate_that_passes_without_any_training(tmp_path, validated_assistant, monkeypatch, capsys):
    """models.md 4.1 and 9.1 step 6: a candidate passes before the training set exists, and is exported, evaluated and recorded with no training run."""
    out_src, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out_src, cdir)
    train_dir = root / "training"
    out = train_dir / "out" / "assistant"
    for name in ("train.jsonl", "seen.jsonl"):                       # the state after step 5: no training set, no seen-domain set
        (out / "dataset" / name).unlink(missing_ok=True)
    write_labels(root)
    env = AE.Env(training_dir=train_dir, repo_root=root, backend_factory=lambda **kw: oracle_backend(root), client_factory=lambda jc: FakeJudgeClient(), log=lambda m: None)
    assert AE.main(["--freeze-sets"], env) == 0
    train_env(root, monkeypatch)
    cid = "qwen3-0.6b"
    cfg = AE.load_eval_config(env)
    est = AE.judge_estimate(env, cfg, AE.build_judge(env, cfg, False), untrained=True)["id"]

    # step 6: the untrained candidate is measured and passes: the transfer gap is not measured, it does not gate
    assert AE.main(["--candidates", "--only", cid, "--approved-estimate", est], env) == 0
    report = load_json(root / "models" / "assistant" / "eval" / "candidates" / f"{cid}.json")
    assert report["passed"] is True and report["failures"] == [] and report["untrained"] is True
    assert report["results"]["transfer_gap"] is None and "seen_domains" not in report["results"]
    assert load_json(out / "candidates" / "summary.json")["winner"] == cid

    # step 9: export it from the base model, no `train`
    pin_export(root)
    assert AX.main(["--candidate", cid], training_dir=train_dir, env=PINNED, tools_factory=lambda c: FakeExportTools(), out=lambda m: None) == 0
    run_id = max(p.name for p in (out / "runs").iterdir())
    run = load_json(out / "runs" / run_id / "run.json")
    assert run["status"] == "untrained-candidate" and run["adapter"] is None and run["dataset"] is None

    # step 10: every output passes on the run of the candidate; the estimate of that run has no seen-domain items either
    lines: list[str] = []
    env.log = lines.append
    assert AE.main(["--estimate", "--run", run_id], env) == 0
    assert f'"id": "{est}"' in "\n".join(lines)
    env.log = lambda m: None
    bench_file, pack_dir, rows = bench(root)
    for fmt in ("merged", "onnx", "gguf"):
        code = AE.main(["--export", fmt, "--run", run_id, "--approved-estimate", est, "--items", str(bench_file), "--pack-dir", str(pack_dir)], env)
        assert code == 0, (fmt, load_json(out / "runs" / run_id / f"eval-{fmt}.json")["failures"])
        rep = load_json(out / "runs" / run_id / f"eval-{fmt}.json")
        assert rep["untrained"] is True and rep["results"]["transfer_gap"] is None

    # step 11: record, card, verify
    assert LOCK.main(["--record", "--model", "assistant", "--run", run_id, "--artifact-url", "https://artifacts.example.invalid/assistant/1.0.0", "--root", str(root)]) == 0
    assert CARD.main(["--model", "assistant", "--root", str(root)]) == 0
    assert LOCK.main(["--verify", "--root", str(root)]) == 0
    entry = load_json(root / "models" / "model.lock")["models"]["assistant"]
    assert entry["status"] == "untrained-candidate" and entry["adapter"] is None and entry["dataset"] is None and entry["evaluation"]["passed"] is True
    assert entry["base_model"]["hub_id"] == "Qwen/Qwen3-0.6B"
    card = (root / "models" / "assistant" / "MODEL_CARD.md").read_text(encoding="utf-8")
    assert "untrained candidate" in card


def test_an_untrained_candidate_is_still_judged_on_every_threshold_but_the_transfer_gap(tmp_path, validated_assistant, monkeypatch):
    out_src, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out_src, cdir)
    write_labels(root)
    (root / "training" / "out" / "assistant" / "dataset" / "seen.jsonl").unlink(missing_ok=True)
    env = AE.Env(training_dir=root / "training", repo_root=root, backend_factory=lambda **kw: oracle_backend(root, refuse_all=True), client_factory=lambda jc: FakeJudgeClient(), log=lambda m: None)
    AE.main(["--freeze-sets"], env)
    train_env(root, monkeypatch)
    cfg = AE.load_eval_config(env)
    est = AE.judge_estimate(env, cfg, AE.build_judge(env, cfg, False), untrained=True)["id"]
    assert AE.main(["--candidates", "--only", "qwen3-0.6b", "--approved-estimate", est], env) == 0
    report = load_json(root / "models" / "assistant" / "eval" / "candidates" / "qwen3-0.6b.json")
    assert report["passed"] is False
    assert {f["metric"] for f in report["failures"]} >= {"wrongful_refusal"} and not any(f["where"].startswith("transfer_gap") for f in report["failures"])


def test_a_failed_evaluation_blocks_the_record(tmp_path, validated_assistant):
    out_src, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out_src, cdir)
    write_labels(root)
    env = AE.Env(training_dir=root / "training", repo_root=root, backend_factory=lambda **kw: oracle_backend(root, refuse_all=True), client_factory=lambda jc: FakeJudgeClient(), log=lambda m: None)
    AE.main(["--freeze-sets"], env)
    run_dir = root / "training" / "out" / "assistant" / "runs" / "2026-11-20-a"
    write_json(run_dir / "run.json", {"model_version": "1.0.0", "status": "untrained-candidate", "base_model": {"hub_id": "o/b", "revision": "a" * 40, "license": "x", "parameters": 6, "chat_template_kwargs": {}},
                                     "adapter": None, "dataset": None, "training": {"run_id": "2026-11-20-a", "code_commit": None, "seed": 1, "hardware": "x"}})
    write_json(run_dir / "exports.json", {"exports": [{"format": "gguf", "variant": "Q4_K_M", "runtime": "server", "files": [{"path": "a.gguf", "sha256": "a" * 64, "bytes": 1}]}]})
    est = AE.judge_estimate(env, AE.load_eval_config(env), AE.build_judge(env, AE.load_eval_config(env), False))["id"]
    for fmt in ("merged", "gguf"):
        assert AE.main(["--export", fmt, "--run", run_dir.name, "--approved-estimate", est, "--no-end-to-end", "--no-judge"], env) == 1
    assert LOCK.main(["--record", "--model", "assistant", "--run", run_dir.name, "--artifact-url", "https://x.invalid/a", "--root", str(root)]) == 1
    assert not (root / "models" / "model.lock").exists()


def test_investigator_rehearsal_from_the_generator_to_a_verified_lock(tmp_path, monkeypatch):
    root = make_inv_repo(tmp_path)
    tdir = root / "training"
    inv = tdir / "investigator"
    out = tdir / "out" / "investigator"

    # 1. generate (the executor's test double stands in for the monitor) and validate
    runner = TL.FakeToolRunner(root)
    for split, n in (("train", 40), ("validation", 4), ("holdout", 6)):
        IG.generate_split(split, out, runner, inv=inv, repo_root=root, limit=n)
    assert IV.run(out, inv=inv, repo_root=root, docs_root=root / "docs", say=lambda m: None) in (0, 1)
    assert read_jsonl(out / "dataset" / "rejected.jsonl") == [] and len(read_jsonl(out / "dataset" / "train.jsonl")) == 40

    # 2. freeze the held-out scenarios, 3. train with a fake engine on a pinned candidate
    env = IE.Env(training_dir=tdir, repo_root=root, runner=TL.FakeToolRunner(root), log=lambda m: None)
    assert IE.main(["--freeze-sets"], env) == 0
    cands = load_json(inv / "config" / "candidates.json")
    for c in cands["candidates"]:
        c["revision"], c["verified"] = "d" * 40, True
    write_json(inv / "config" / "candidates.json", cands)
    lines: list[str] = []
    hw = hardware("cuda")
    assert IT.main(["--tier", "small", "--dry-run"], training_dir=tdir, hardware=hw, out=lines.append, evaluate=OK_HW) == 0
    plan_id = next(l for l in lines if "plan id:" in l).split()[2]
    from training.investigator.adapter import InvestigatorSetup
    adapter = InvestigatorSetup(tdir).adapter(load_json(inv / "config" / "train.json"), None)
    assert IT.main(["--tier", "small", "--approved-plan", plan_id], training_dir=tdir, hardware=hw, evaluate=OK_HW, out=lambda m: None,
                   engine_factory=lambda base, cfg, plan, examples, kwargs, log: FakeEngine(InvestigatorSetup(tdir).adapter(cfg, None))) == 0
    run_id = max(p.name for p in (out / "runs").iterdir())
    run = load_json(out / "runs" / run_id / "run.json")
    assert run["status"] == "trained" and run["tier"] == "small" and run["protocol"]["markers"] and run["contracts"] == {"incident-report": 1, "tool-catalog": 1, "investigator-protocol": 1}

    # 4. export the GGUF of the tier (fake tools)
    p = inv / "config" / "export.json"
    ex = load_json(p)
    ex["llama_cpp"]["tag"] = "b9999"
    write_json(p, ex)
    assert IX.main(["--run", run_id], training_dir=tdir, env={"LLAMA_CPP_DIR": "."}, tools_factory=lambda c: FakeExportTools(), out=lambda m: None) == 0
    manifest = load_json(out / "runs" / run_id / "exports.json")
    assert [e["format"] for e in manifest["exports"]] == ["gguf"] and manifest["exports"][0]["tier"] == "small" and manifest["exports"][0]["context_length"] == 8192
    assert manifest["exports"][0]["files"][0]["path"] == f"investigator-1.0.0-small-Q4_K_M.gguf" and manifest["template_check"]["passed"]

    # 5. evaluate merged and gguf on both suites (the sample-app suite needs hand-written scenarios: a few held-out ones stand in)
    files = IE.scenario_files(env, "synthetic-holdout")
    sample = root / "docs" / "support" / "bench" / "investigator"
    sample.mkdir(parents=True)
    pick = [f for f in files if load_json(f)["gold"]["verdict"] == "explained" and load_json(f)["gold"]["user_story"]][:3] + [f for f in files if load_json(f)["gold"]["verdict"] == "abstain"][:1]
    for f in pick:
        shutil.copy(f, sample / f.name)
    shutil.copytree(out / "worlds", root / "docs" / "support" / "bench" / "worlds")
    backend = oracle_investigator(root, files)
    # the merged weights run under transformers, with no grammar: a model that cannot hold the format there must not block the record of the
    # gguf export, which is the runtime that is deployed (models.md 6.6, operations.md 11.4)
    no_grammar = FakeBackend(lambda messages, constraint=None: "this is not a call and not a report")
    env.backend_factory = lambda **kw: no_grammar if kw["export"] == "merged" else backend
    art = tmp_path / "artifacts"
    assert IE.main(["--export", "merged", "--run", run_id, "--artifacts-dir", str(art)], env) == 1
    assert IE.main(["--export", "gguf", "--run", run_id, "--artifacts-dir", str(art)], env) == 0
    assert (art / "synthetic-holdout-small.json").exists()
    # a partial run never replaces the full report and cannot be recorded
    full = (out / "runs" / run_id / "eval-gguf.json").read_bytes()
    assert IE.main(["--export", "gguf", "--run", run_id, "--artifacts-dir", str(art), "--suite", "sample-app", "--limit", "1"], env) in (0, 1)
    assert (out / "runs" / run_id / "eval-gguf.json").read_bytes() == full
    assert load_json(out / "runs" / run_id / "eval-gguf-partial.json")["partial"] == {"suite": "sample-app", "limit": 1}
    assert (art / "sample-app-small-partial.json").exists()

    # 6. record, card, verify
    url = "https://artifacts.example.invalid/investigator/1.0.0"
    assert LOCK.main(["--record", "--model", "investigator", "--run", run_id, "--artifact-url", url, "--root", str(root)]) == 0
    assert CARD.main(["--model", "investigator", "--root", str(root)]) == 0
    assert LOCK.main(["--verify", "--root", str(root)]) == 0
    entry = load_json(root / "models" / "model.lock")["models"]["investigator"]
    assert entry["protocol"]["markers"] == ["<!-- protocol:begin -->", "<!-- protocol:end -->"] and entry["exports"][0]["tier"] == "small" and entry["evaluation"]["passed"]
    recorded_report = root / "models" / "investigator" / "eval" / "1.0.0-small.json"           # the gguf report is the recorded one; merged is copied beside it
    assert load_json(recorded_report)["export"] == "gguf" and load_json(root / "models" / "investigator" / "eval" / "1.0.0-small-merged.json")["passed"] is False
    assert entry["contracts"]["investigator-protocol"] == 1 and "retrieval" not in entry
    card = (root / "models" / "investigator" / "MODEL_CARD.md").read_text(encoding="utf-8")
    assert "operations investigator" in card and "small" in card and "synthetic-holdout" in card
    # changing the thresholds breaks the verification (L04)
    ev = inv / "config" / "eval.json"
    ev.write_text(ev.read_text(encoding="utf-8").replace('"timeout_seconds": 600', '"timeout_seconds": 601'), encoding="utf-8")
    assert LOCK.main(["--verify", "--root", str(root)]) == 1


def test_investigator_rehearsal_of_a_candidate_that_passes_without_any_training(tmp_path):
    """operations.md 11.5 and models.md 9.2 step 5: a passing candidate goes to export, evaluation and record with no training run."""
    root = make_inv_repo(tmp_path)
    tdir = root / "training"
    inv = tdir / "investigator"
    out = tdir / "out" / "investigator"
    runner = TL.FakeToolRunner(root)
    IG.generate_split("holdout", out, runner, inv=inv, repo_root=root, limit=6)
    env = IE.Env(training_dir=tdir, repo_root=root, runner=TL.FakeToolRunner(root), log=lambda m: None)
    assert IE.main(["--freeze-sets"], env) == 0
    cands = load_json(inv / "config" / "candidates.json")
    for c in cands["candidates"]:
        c["revision"], c["verified"] = "d" * 40, True
    write_json(inv / "config" / "candidates.json", cands)
    files = IE.scenario_files(env, "synthetic-holdout")
    sample = root / "docs" / "support" / "bench" / "investigator"
    sample.mkdir(parents=True)
    for f in [f for f in files if load_json(f)["gold"]["verdict"] == "explained" and load_json(f)["gold"]["user_story"]][:3] + [f for f in files if load_json(f)["gold"]["verdict"] == "abstain"][:1]:
        shutil.copy(f, sample / f.name)
    shutil.copytree(out / "worlds", root / "docs" / "support" / "bench" / "worlds")
    env.backend_factory = lambda **kw: oracle_investigator(root, files)

    assert IE.main(["--candidates", "--tier", "small", "--only", "qwen3.5-2b"], env) == 0
    assert load_json(root / "models" / "investigator" / "eval" / "candidates" / "qwen3.5-2b.json")["passed"] is True

    ex = load_json(inv / "config" / "export.json")
    ex["llama_cpp"]["tag"] = "b9999"
    write_json(inv / "config" / "export.json", ex)
    assert IX.main(["--candidate", "qwen3.5-2b", "--tier", "small"], training_dir=tdir, env={"LLAMA_CPP_DIR": "."}, tools_factory=lambda c: FakeExportTools(), out=lambda m: None) == 0
    run_id = max(p.name for p in (out / "runs").iterdir())
    assert load_json(out / "runs" / run_id / "run.json")["status"] == "untrained-candidate"
    assert IE.main(["--export", "gguf", "--run", run_id, "--tier", "small", "--artifacts-dir", str(tmp_path / "artifacts")], env) == 0
    assert LOCK.main(["--record", "--model", "investigator", "--run", run_id, "--artifact-url", "https://artifacts.example.invalid/investigator/1.0.0", "--root", str(root)]) == 0
    assert CARD.main(["--model", "investigator", "--root", str(root)]) == 0
    assert LOCK.main(["--verify", "--root", str(root)]) == 0
    entry = load_json(root / "models" / "model.lock")["models"]["investigator"]
    assert entry["status"] == "untrained-candidate" and entry["adapter"] is None and entry["dataset"] is None and entry["tiers"]["small"]["status"] == "untrained-candidate"
