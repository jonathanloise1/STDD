"""Gaps found by walking the guides for a first training: what is recorded, partial runs, the labelling flow, the launchers' flags, the template check."""
import json
import os
import shutil
import stat
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

import pytest

from conftest import ROOT
from support import FakeJudgeClient, make_assistant_repo, oracle_backend, write_labels
from test_eval_assistant import bench, env_for, make_run
from test_lock import LARGE_GGUF, NOW, SMALL_GGUF, add_inv_run, entry_of, make_inv_root, make_root
from training.assistant import eval as AE, labels as LB
from training.common import evalcore as EC, judge as J, lock as LOCK
from training.common.backends import BackendError, LlamaServerBackend, shared_server_flags
from training.common.jsonio import load_json, read_jsonl, write_json

HOST = ROOT / "src" / "support" / "model-host"


# ---------------------------------------------------------------- lock: what is recorded (gap 1) and partial runs (gap 5)

def test_the_investigator_is_recorded_on_its_gguf_report_and_a_merged_report_that_failed_does_not_block_it(tmp_path):
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "small", SMALL_GGUF)
    run = root / "training/out/investigator/runs/2026-11-20-a"
    merged = json.loads((run / "eval-merged.json").read_text())
    merged["passed"] = False                                                # no grammar under transformers: informative only
    (run / "eval-merged.json").write_text(json.dumps(merged))
    LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    e = entry_of(root)
    assert e["evaluation"]["report"] == "models/investigator/eval/1.0.0-small.json" and e["evaluation"]["passed"] is True
    assert json.loads((root / "models/investigator/eval/1.0.0-small.json").read_text())["export"] == "gguf"
    assert json.loads((root / "models/investigator/eval/1.0.0-small-merged.json").read_text())["passed"] is False


def test_the_investigator_needs_no_merged_report_at_all(tmp_path):
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "small", SMALL_GGUF)
    (root / "training/out/investigator/runs/2026-11-20-a/eval-merged.json").unlink()
    LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    assert not (root / "models/investigator/eval/1.0.0-small-merged.json").exists()


def test_the_investigator_export_that_is_deployed_must_still_pass(tmp_path):
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "small", SMALL_GGUF)
    run = root / "training/out/investigator/runs/2026-11-20-a"
    gguf = json.loads((run / "eval-gguf.json").read_text())
    gguf["passed"] = False
    (run / "eval-gguf.json").write_text(json.dumps(gguf))
    with pytest.raises(LOCK.LockError, match="eval-gguf.json did not pass"):
        LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    (run / "eval-gguf.json").unlink()
    with pytest.raises(LOCK.LockError, match="missing evaluation report eval-gguf.json"):
        LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    assert not (root / "models" / "model.lock").exists()


def test_the_assistant_still_needs_merged_and_every_export_to_pass(tmp_path):
    root = make_root(tmp_path)
    p = root / "training/out/assistant/runs/2026-11-20-a/eval-merged.json"
    rep = json.loads(p.read_text())
    rep["passed"] = False
    p.write_text(json.dumps(rep))
    with pytest.raises(LOCK.LockError, match="eval-merged.json did not pass"):
        LOCK.record("assistant", "2026-11-20-a", root, now=NOW)


@pytest.mark.parametrize("model", ["assistant", "investigator"])
def test_record_refuses_a_report_marked_partial(tmp_path, model):
    if model == "assistant":
        root, run_id, fmt = make_root(tmp_path), "2026-11-20-a", "gguf"
    else:
        root, run_id, fmt = make_inv_root(tmp_path), "2026-11-20-a", "gguf"
        add_inv_run(root, run_id, "small", SMALL_GGUF)
    p = root / f"training/out/{model}/runs/{run_id}/eval-{fmt}.json"
    rep = json.loads(p.read_text())
    rep["partial"] = {"suite": "sample-app", "limit": 1}
    p.write_text(json.dumps(rep))
    with pytest.raises(LOCK.LockError, match="partial run"):
        LOCK.record(model, run_id, root, now=NOW)
    assert not (root / "models" / "model.lock").exists()


def test_a_partial_assistant_run_writes_beside_the_full_report_and_is_marked(validated_assistant, tmp_path):
    out_src, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out_src, cdir)
    write_labels(root)
    env = env_for(root, oracle_backend(root))
    AE.main(["--freeze-sets"], env)
    run_dir = make_run(root)
    cfg = AE.load_eval_config(env)
    est = AE.judge_estimate(env, cfg, AE.build_judge(env, cfg, False))["id"]
    bench_file, pack_dir, _ = bench(root)
    common = ["--run", run_dir.name, "--approved-estimate", est, "--items", str(bench_file), "--pack-dir", str(pack_dir)]
    assert AE.main(["--export", "merged", *common], env) == 0
    full = (run_dir / "eval-merged.json").read_bytes()
    AE.main(["--export", "merged", "--suite", "platform", *common], env)
    assert (run_dir / "eval-merged.json").read_bytes() == full
    assert load_json(run_dir / "eval-platform-merged.json")["partial"] == {"suite": "platform"}
    assert "partial" not in load_json(run_dir / "eval-merged.json")


# ---------------------------------------------------------------- the labelling flow (gap 3)

@pytest.fixture()
def frozen(validated_assistant, tmp_path):
    out_src, cdir = validated_assistant
    root = make_assistant_repo(tmp_path, out_src, cdir)
    (root / "training" / "assistant" / "eval" / "human-labels.jsonl").unlink(missing_ok=True)
    msgs: list[str] = []
    env = env_for(root, oracle_backend(root), messages=msgs)
    assert AE.main(["--freeze-sets"], env) == 0
    return root, env, msgs


def answers_file(root: Path, n: int = 40) -> Path:
    rows = [{"set": "platform_test", "id": r["id"], "outcome": "answered", "answer": f"model answer to {r['id']}"}
            for r in read_jsonl(root / "training" / "out" / "assistant" / "dataset" / "test.jsonl") if r["class"].startswith("pos_")][:n]
    rows.append({"set": "seen_domains", "id": "x", "outcome": "answered", "answer": "ignored"})
    p = root / "answers.jsonl"
    p.write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    return p


def read_answers(items):
    it = iter(items)
    return lambda prompt: next(it)


def test_select_picks_in_scope_items_per_language_and_alternates_model_and_reference_answers(frozen):
    root, env, msgs = frozen
    assert LB.main(["select", "--per-language", "6", "--answers", str(answers_file(root))], env) == 0
    rows = read_jsonl(LB.todo_path(env))
    assert len(rows) == 12 and {r["lang"] for r in rows} == {"en", "it"} and all(r["class"].startswith("pos_") for r in rows)
    assert len({r["id"] for r in rows}) == 12 and all(r["faithful"] is None and r["useful"] is None for r in rows)
    sources = [r["answer_source"] for r in rows if r["lang"] == "en"]
    assert sources == ["reference", "model", "reference", "model", "reference", "model"] and "model" in [r["answer_source"] for r in rows]
    own = next(r for r in rows if r["answer_source"] == "model")
    assert own["answer"] == f"model answer to {own['id']}" and own["reference"] != own["answer"]
    # deterministic, and it never overwrites work by itself
    first = LB.todo_path(env).read_bytes()
    assert LB.main(["select", "--per-language", "6", "--answers", str(answers_file(root))], env) == 2
    assert LB.main(["select", "--per-language", "6", "--answers", str(answers_file(root)), "--force"], env) == 0 and LB.todo_path(env).read_bytes() == first


def test_select_without_model_answers_warns_that_the_calibration_proves_little(frozen):
    root, env, msgs = frozen
    assert LB.main(["select", "--per-language", "4"], env) == 0
    assert any("no model answers" in m for m in msgs) and {r["answer_source"] for r in read_jsonl(LB.todo_path(env))} == {"reference"}


def test_select_refuses_a_test_set_that_is_not_frozen(frozen):
    root, env, msgs = frozen
    p = root / "training" / "assistant" / "config" / "eval.json"
    cfg = load_json(p)
    cfg["test_set_sha256"] = None
    write_json(p, cfg)
    assert LB.main(["select", "--per-language", "4"], env) == 2 and not LB.todo_path(env).exists()


def test_the_terminal_flow_saves_each_item_and_resumes_where_it_stopped(frozen):
    root, env, msgs = frozen
    LB.main(["select", "--per-language", "3", "--answers", str(answers_file(root))], env)
    # two items labelled, then the person quits on the faithful question of the third
    assert LB.main(["label"], env, read_answers(["y", "n", "x", "n", "y", "q"])) == 0
    rows = read_jsonl(LB.todo_path(env))
    assert [(r["faithful"], r["useful"]) for r in rows[:3]] == [(True, False), (False, True), (None, None)]
    assert LB.counts(rows) == {"total": 6, "labelled": 2, "left": 4}
    assert any("ANSWER TO LABEL" in m and rows[0]["id"] in m for m in msgs)
    # a second session starts at the third item: the answer "x" was invalid and asked again, never recorded
    assert LB.main(["label", "--lang", "en"], env, read_answers(["y", "y"])) == 0
    rows = read_jsonl(LB.todo_path(env))
    assert (rows[2]["faithful"], rows[2]["useful"]) == (True, True) and rows[3]["faithful"] is None
    assert LB.main(["status"], env) == 0 and any(m == "en: 3 of 3 labelled" for m in msgs)


def test_finish_refuses_an_incomplete_file_and_writes_the_exact_format_when_complete(frozen):
    root, env, msgs = frozen
    LB.main(["select", "--per-language", "3", "--answers", str(answers_file(root))], env)
    assert LB.main(["finish"], env) == 2 and not (root / "training" / "assistant" / "eval" / "human-labels.jsonl").exists()
    answers = ["y", "y", "y", "n", "n", "y"] * 2                              # 6 items, both labels occur in both metrics
    assert LB.main(["label"], env, read_answers(answers)) == 0
    assert LB.main(["finish"], env) == 0
    out = root / "training" / "assistant" / "eval" / "human-labels.jsonl"
    rows = read_jsonl(out)
    assert len(rows) == 6 and all(list(r) == LB.LABEL_KEYS for r in rows)
    assert all(isinstance(r["faithful"], bool) and isinstance(r["useful"], bool) for r in rows)
    # exactly what `eval` reads: load_labels, the judge's agreement and its cache key
    cfg = AE.load_eval_config(env)
    labels = AE.load_labels(env, cfg)
    assert labels == rows
    judge = AE.build_judge(env, cfg, False)
    assert set(J.agreement(judge, labels)) == {"faithful", "useful"}


def test_a_file_completed_by_hand_is_accepted_and_a_changed_test_set_is_not(frozen, capsys):
    root, env, msgs = frozen
    LB.main(["select", "--per-language", "3"], env)
    rows = read_jsonl(LB.todo_path(env))
    for k, r in enumerate(rows):
        r["faithful"], r["useful"] = bool(k % 2), bool(k % 3)
    LB.todo_path(env).write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    assert LB.main(["finish"], env) == 0
    rows[0]["faithful"] = "yes"                                              # not a boolean
    LB.todo_path(env).write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    assert LB.main(["finish"], env) == 2
    rows[0]["faithful"] = True
    rows[1]["test_set_sha256"] = "0" * 64                                    # selected from another test set
    LB.todo_path(env).write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    assert LB.main(["finish"], env) == 2 and "another test set" in capsys.readouterr().err


def test_the_default_size_is_120_items_when_the_test_set_has_enough(frozen):
    assert LB.build_parser().parse_args(["select"]).per_language == 60


# ---------------------------------------------------------------- the launchers and the evaluation share their flags (gap 4)

def test_the_evaluation_server_command_carries_exactly_the_shared_flags():
    shared = shared_server_flags()
    assert "--jinja" in shared
    cmd = LlamaServerBackend("model.gguf", "llama-server", {}, 8192, 0, port=1234).command()
    assert [c for c in cmd if c in shared] == shared and cmd[cmd.index("--parallel") + 1] == "1" and cmd[cmd.index("--ctx-size") + 1] == "8192"


def test_an_empty_flag_file_is_refused(tmp_path):
    p = tmp_path / "flags.txt"
    p.write_text("# nothing\n\n", encoding="utf-8")
    with pytest.raises(BackendError):
        shared_server_flags(p)


def test_both_launchers_read_the_shared_flags_and_do_not_hard_code_them():
    for name in ("start.sh", "start.ps1"):
        text = (HOST / name).read_text(encoding="utf-8")
        assert "shared-flags.txt" in text and "--jinja" not in text.replace("(--jinja and the like)", ""), name
    assert "$SharedFlags" in (HOST / "start.ps1").read_text(encoding="utf-8") and "SHARED_FLAGS[@]" in (HOST / "start.sh").read_text(encoding="utf-8")


@pytest.mark.skipif(sys.platform == "win32" or not shutil.which("bash") or not shutil.which("curl"), reason="the launcher needs bash and curl")
def test_start_sh_passes_the_shared_flags_to_the_server(tmp_path):
    """A stand-in for llama-server records its arguments and answers /health: the real launcher runs, no model and no real host."""
    fake = tmp_path / "fake-llama-server"
    fake.write_text(f"""#!{sys.executable}
import http.server, os, sys
open(os.environ["ARGS_FILE"], "w").write("\\n".join(sys.argv[1:]))
port = int(sys.argv[sys.argv.index("--port") + 1])
class H(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200); self.end_headers(); self.wfile.write(b'{{"status":"ok"}}')
    def log_message(self, *a): pass
http.server.HTTPServer(("127.0.0.1", port), H).serve_forever()
""", encoding="utf-8")
    fake.chmod(fake.stat().st_mode | stat.S_IXUSR)
    (tmp_path / "m.gguf").write_bytes(b"x")
    port = LlamaServerBackend("m", port=None).port
    env = {**os.environ, "ARGS_FILE": str(tmp_path / "args.txt"), "MODEL_HOST_MODEL": str(tmp_path / "m.gguf"), "MODEL_HOST_ALIAS": "t-1.0.0", "MODEL_HOST_PORT": str(port),
           "MODEL_HOST_DEVICE": "cpu", "MODEL_HOST_CPU_BIN": str(fake), "MODEL_HOST_START_TIMEOUT": "20", "TMPDIR": str(tmp_path)}
    proc = subprocess.Popen(["bash", str(HOST / "start.sh")], env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    try:
        deadline = time.time() + 25
        while time.time() < deadline:
            try:
                urllib.request.urlopen(f"http://127.0.0.1:{port}/health", timeout=1).read()
                break
            except OSError:
                time.sleep(0.2)
        args = (tmp_path / "args.txt").read_text(encoding="utf-8").split("\n")
    finally:
        proc.terminate()
        proc.wait(timeout=20)
        subprocess.run(["pkill", "-f", str(fake)], check=False)
    assert all(flag in args for flag in shared_server_flags()), args
    assert args[args.index("--alias") + 1] == "t-1.0.0@cpu"


# ---------------------------------------------------------------- the template check script (gap 7)

def test_template_check_comment_says_where_the_package_must_be_installed():
    text = (ROOT / "training" / "assistant" / "js" / "template_check.mjs").read_text(encoding="utf-8")
    assert "globally" not in text and "--prefix training/assistant/js" in text and "not from a global install" in text


@pytest.mark.skipif(not shutil.which("node"), reason="node is not installed")
def test_template_check_names_the_fix_when_the_package_is_not_where_the_script_looks(tmp_path):
    script = tmp_path / "template_check.mjs"                                  # a copy in a folder with no node_modules above it
    shutil.copy(ROOT / "training" / "assistant" / "js" / "template_check.mjs", script)
    (tmp_path / "cases.json").write_text("{}", encoding="utf-8")
    res = subprocess.run(["node", str(script), str(tmp_path / "cases.json")], capture_output=True, text=True, env={**os.environ, "NODE_PATH": str(tmp_path)})
    assert res.returncode == 1 and "@huggingface/transformers" in res.stderr and "npm install --prefix training/assistant/js" in res.stderr


@pytest.mark.skipif(not shutil.which("node"), reason="node is not installed")
def test_a_failing_template_check_is_refused_with_its_message_not_a_traceback(tmp_path):
    from training.common import export_core as X
    script = tmp_path / "check.mjs"
    script.write_text('process.stderr.write("boom from node\\n"); process.exit(3);\n', encoding="utf-8")
    tools = X.RealExportTools({"llama_cpp": {}}, {}, node_script=script)
    with pytest.raises(X.ExportError, match="boom from node"):
        tools.render_js(tmp_path, {}, [[{"role": "user", "content": "x"}]])
