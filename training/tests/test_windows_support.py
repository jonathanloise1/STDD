"""Windows and WSL support, the pre-made GGUF route and the follow-ups of the first PC session (models.md 9.4, 6.1, 6.4)."""
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

from conftest import ROOT
from support import FakeEngine, FakeExportTools, FakeJudgeClient, hardware
from test_common import info
from test_train_export import OK_HW, PINNED, adapter_for, pin_export, plan_id_of, repo, tiny_cfg, train_env  # noqa: F401  (repo is a fixture)
from training.assistant import export as AEXPORT, external as EXT, train as ATRAIN
from training.common import export_core as X, hardware as H, train_core as T, weights as WT
from training.common.hashing import sha256_file
from training.common.jsonio import load_json, write_json
from training.common.teacher import TeacherError
from training.investigator import eval as IE


# ---------------------------------------------------------------- hardware

def test_wsl_is_detected_from_the_environment_or_the_kernel(monkeypatch, tmp_path):
    monkeypatch.setattr(H.sys, "platform", "linux")
    for k in ("WSL_DISTRO_NAME", "WSL_INTEROP"):
        monkeypatch.delenv(k, raising=False)
    monkeypatch.setenv("WSL_DISTRO_NAME", "Ubuntu-24.04")
    assert H.is_wsl()
    monkeypatch.delenv("WSL_DISTRO_NAME")
    monkeypatch.setattr(H, "Path", lambda p: tmp_path / "version")
    (tmp_path / "version").write_text("Linux version 5.15.153.1-microsoft-standard-WSL2")
    assert H.is_wsl()
    (tmp_path / "version").write_text("Linux version 6.8.0-generic")
    assert not H.is_wsl()
    monkeypatch.setattr(H.sys, "platform", "darwin")
    assert not H.is_wsl()


def test_a_folder_under_mnt_is_a_windows_drive():
    assert H.windows_drive_root("/mnt/c/Users/me/stdd") == "/mnt/c"
    assert H.windows_drive_root("/mnt/wsl/x") is None and H.windows_drive_root("/home/me") is None


def test_nvidia_smi_falls_back_to_the_wsl_copy(monkeypatch):
    monkeypatch.setattr(H.shutil, "which", lambda n: None)
    monkeypatch.setattr(H.os.path, "exists", lambda p: p == H.WSL_NVIDIA_SMI)
    assert H.nvidia_smi_path() == "/usr/lib/wsl/lib/nvidia-smi"
    monkeypatch.setattr(H.shutil, "which", lambda n: "/usr/bin/nvidia-smi")
    assert H.nvidia_smi_path() == "/usr/bin/nvidia-smi"


def test_detect_reads_the_gpu_through_the_wsl_nvidia_smi_and_says_where_the_disk_is(monkeypatch, tmp_path):
    monkeypatch.setitem(sys.modules, "torch", None)                   # an import of torch fails: detection falls back to nvidia-smi
    monkeypatch.setattr(H, "is_wsl", lambda: True)
    monkeypatch.setattr(H, "nvidia_smi_path", lambda: "/usr/lib/wsl/lib/nvidia-smi")
    seen = []
    monkeypatch.setattr(H, "_run", lambda cmd: (seen.append(cmd[0]) or "NVIDIA GeForce RTX 5090, 32768"))
    got = H.detect(tmp_path)
    assert got.device == "cuda" and got.vram_gb == 32.0 and got.wsl and "/usr/lib/wsl/lib/nvidia-smi" in seen
    assert any("virtual disk" in n for n in got.notes)
    monkeypatch.setattr(H, "windows_drive_root", lambda p: "/mnt/c")
    assert any("Windows drive /mnt/c" in n for n in H.detect(tmp_path).notes)


def test_wmic_is_gone_and_the_windows_memory_comes_from_the_api():
    src = Path(H.__file__).read_text(encoding="utf-8")
    assert 'wmic", "computersystem"' not in src and "GlobalMemoryStatusEx" in src


def test_minimum_vram_ram_and_micro_batch_follow_the_model_size(monkeypatch):
    monkeypatch.setattr(H, "sys", SimpleNamespace(version_info=(3, 12, 1), platform="linux"))
    assert H.min_cuda_requirements(600_000_000) == (12, 32) and H.min_cuda_requirements(2_000_000_000)[0] == 16
    assert H.min_cuda_requirements(4_000_000_000) == (24, 48) and H.min_cuda_requirements(9_000_000_000)[0] == 40
    assert H.evaluate(info(vram_gb=16.0), 600_000_000)[0] is True
    ok, problems = H.evaluate(info(vram_gb=16.0, ram_gb=64.0), 4_000_000_000)
    assert not ok and any("24 GB" in p for p in problems)
    assert H.evaluate(info(vram_gb=24.0, ram_gb=64.0), 4_000_000_000)[0] is True
    gpu = info(vram_gb=24.0)
    assert H.micro_batch_plan(gpu, 32, 600_000_000)["micro_batch"] == 8
    assert H.micro_batch_plan(gpu, 32, 4_000_000_000)["micro_batch"] == 4
    assert H.micro_batch_plan(gpu, 32, 9_000_000_000)["micro_batch"] == 2
    rep = H.build_report(gpu, True, [], 4_000_000_000)
    assert rep["requirements"]["vram_gb"] == 24 and rep["batch_plan"]["micro_batch"] == 4


def test_the_micro_batch_override_keeps_the_effective_batch():
    gpu = info(vram_gb=24.0)
    assert H.micro_batch_plan(gpu, 32, override=2) == {"micro_batch": 2, "grad_accum": 16, "effective_batch": 32}
    assert H.micro_batch_plan(gpu, 32, override=5)["micro_batch"] == 4          # rounded down to a divisor
    assert H.micro_batch_plan(gpu, 32, override=99)["micro_batch"] == 32
    plan = T.build_plan({"batch": {"effective": 32}, "epochs": 1, "precision": {"cuda": "bf16"}}, 100, hardware("cuda", vram=24), 1, {}, 600_000_000, 1)
    assert (plan.micro_batch, plan.grad_accum) == (1, 32)


def test_train_accepts_micro_batch_and_prints_it(repo):
    lines: list[str] = []
    assert ATRAIN.main(["--dry-run", "--micro-batch", "2"], training_dir=repo / "training", hardware=hardware("cuda", vram=24), out=lines.append, evaluate=OK_HW) == 0
    assert "micro-batch 2 x accumulation 16" in "\n".join(lines)


# ---------------------------------------------------------------- the judge fails in the final selection

class FlakyJudgeClient(FakeJudgeClient):
    def __init__(self):
        super().__init__()
        self.down = False

    def complete(self, **kw):
        if self.down:
            raise TeacherError("connection refused (is the teacher host running at http://localhost:8093/v1?)")
        return super().complete(**kw)


def test_a_judge_failure_in_the_final_selection_keeps_the_run_and_resumes(repo):
    cfg = tiny_cfg(repo)
    client = FlakyJudgeClient()
    setup, adapter = adapter_for(repo, cfg, client)
    plan = T.build_plan(cfg, len(adapter.train_examples()), hardware(), 1, {})
    work = repo / "runs" / "2026-11-20-a" / "train"
    logs: list[str] = []
    orig = adapter.final_metrics

    def failing(items, outputs):
        client.down = True
        return orig(items, outputs)

    adapter.final_metrics = failing
    with pytest.raises(T.TrainError, match=r"judge failed during the final selection.*--run 2026-11-20-a"):
        T.run_training(FakeEngine(adapter), adapter, plan, cfg, work, logs.append)
    saved = load_json(work / "state.json")
    assert saved["stop_reason"] and not saved["finished"]                      # the loop is over and saved
    client.down = False
    adapter.final_metrics = orig
    second = FakeEngine(adapter)
    out = T.run_training(second, adapter, plan, cfg, work, logs.append)
    assert out.selected is not None and out.stop_reason == saved["stop_reason"] and "resuming" in "\n".join(logs)
    assert second.step == saved["step"]                                         # no step was trained again


def test_the_train_command_explains_a_judge_failure_and_resumes_with_run(repo, monkeypatch, capsys):
    tiny_cfg(repo)
    train_env(repo, monkeypatch)
    hw = hardware("cuda")
    pid = plan_id_of(repo, hw)
    client = FlakyJudgeClient()
    cfg = load_json(repo / "training/assistant/config/train.json")
    adapter_holder = {}

    def engine(base, c, plan, examples, kwargs, log):
        _, ad = adapter_for(repo, c, client)
        adapter_holder["a"] = ad
        return FakeEngine(ad)

    def judge_factory(real):
        j = adapter_for(repo, cfg, client)[1].judge
        orig = j.__class__
        return j

    orig_complete = client.complete
    calls = {"n": 0}

    def complete(**kw):
        calls["n"] += 1
        return orig_complete(**kw)

    run_id = "2026-11-20-a"
    args = ["--approved-plan", pid, "--run", run_id]
    kw = dict(training_dir=repo / "training", hardware=hw, engine_factory=engine, evaluate=OK_HW, judge_factory=judge_factory, out=lambda m: None)
    # first run: the judge goes down as soon as the loop is over
    from training.common import train_core as TC
    real_final = TC.TrainError
    ad0 = adapter_for(repo, cfg, client)[1]
    orig_run = TC.run_training

    def run_then_down(engine_, adapter, plan, c, work, log=print, resume=True):
        orig_final = adapter.final_metrics

        def final(items, outputs):
            client.down = True
            return orig_final(items, outputs)

        adapter.final_metrics = final
        return orig_run(engine_, adapter, plan, c, work, log, resume)

    monkeypatch.setattr(TC, "run_training", run_then_down)
    assert ATRAIN.main(args, **kw) == 2
    err = capsys.readouterr().err
    assert "judge failed during the final selection" in err and f"--run {run_id}" in err
    client.down = False
    monkeypatch.setattr(TC, "run_training", orig_run)
    assert ATRAIN.main(args, **kw) == 0                                         # the same command with --run goes straight to the final selection


# ---------------------------------------------------------------- export

def test_llama_quantize_is_found_in_release_folders_and_with_exe(tmp_path):
    assert X.find_quantize_bin(tmp_path, "llama-quantize") == tmp_path / "build" / "bin" / "llama-quantize"          # none exists: the default path
    rel = tmp_path / "build" / "bin" / "Release"
    rel.mkdir(parents=True)
    (rel / "llama-quantize.exe").write_bytes(b"x")
    assert X.find_quantize_bin(tmp_path, "llama-quantize") == rel / "llama-quantize.exe"
    (tmp_path / "build" / "bin" / "llama-quantize").write_bytes(b"x")
    assert X.find_quantize_bin(tmp_path, "llama-quantize") == tmp_path / "build" / "bin" / "llama-quantize"            # build/bin first
    assert X.find_quantize_bin(tmp_path, "llama-quantize.exe") == tmp_path / "build" / "bin" / "llama-quantize"


def test_converters_run_with_this_interpreter_or_the_one_the_environment_names():
    cfg = {"python_env": "LLAMA_CPP_PYTHON"}
    assert X.interpreter(cfg, {}) == sys.executable
    assert X.interpreter(cfg, {"LLAMA_CPP_PYTHON": "/venv/bin/python"}) == "/venv/bin/python"
    assert X.interpreter({}, {"LLAMA_CPP_PYTHON": "/venv/bin/python"}) == sys.executable
    assert X.with_interpreter(["python", "convert.py", "python"], "/p") == ["/p", "convert.py", "python"]
    assert X.with_interpreter(["/bin/quantize", "a"], "/p") == ["/bin/quantize", "a"]
    for f in ("assistant", "investigator"):
        c = load_json(ROOT / "training" / f / "config" / "export.json")
        assert c["transformers_js"]["python_env"] == "TRANSFORMERS_JS_PYTHON" and c["llama_cpp"]["python_env"] == "LLAMA_CPP_PYTHON"


def test_real_export_tools_start_the_converters_with_the_chosen_interpreter(tmp_path):
    cfg = load_json(ROOT / "training" / "assistant" / "config" / "export.json")
    llama, tjs = tmp_path / "llama.cpp", tmp_path / "tjs"
    (llama / "build" / "bin" / "Release").mkdir(parents=True)
    (llama / "build" / "bin" / "Release" / "llama-quantize.exe").write_bytes(b"x")
    ran = []
    tools = X.RealExportTools(cfg, {"LLAMA_CPP_DIR": str(llama), "TRANSFORMERS_JS_DIR": str(tjs), "TRANSFORMERS_JS_PYTHON": "/tjs/python"}, runner=lambda c, cwd: ran.append(c))
    tools.to_gguf(tmp_path / "merged", tmp_path / "out" / "m.gguf", "Q4_K_M")
    assert ran[0][0] == sys.executable and ran[1][0].endswith("llama-quantize.exe")
    tools.to_onnx(tmp_path / "merged", tmp_path / "onnx", "q4f16")
    assert ran[2][0] == "/tjs/python"


def test_merging_an_untrained_candidate_takes_the_snapshot_and_loads_no_model(tmp_path, monkeypatch):
    calls = []
    fake = SimpleNamespace(snapshot_download=lambda hub, revision=None, local_dir=None: calls.append((hub, revision, local_dir)))
    monkeypatch.setitem(sys.modules, "huggingface_hub", fake)
    monkeypatch.setitem(sys.modules, "torch", None)                          # importing torch would fail: the BF16 load is not attempted
    X.RealExportTools({}, {}).merge({"hub_id": "Qwen/x", "revision": "a" * 40}, None, tmp_path / "merged")
    assert calls == [("Qwen/x", "a" * 40, str(tmp_path / "merged"))]


# ---------------------------------------------------------------- weights outside the repository, pre-made GGUF

def test_the_weights_folder_is_outside_the_repository_by_default(tmp_path):
    assert WT.models_dir({}) == Path("~/myapp-models").expanduser()
    assert WT.models_dir({"MYAPP_MODELS_DIR": str(tmp_path)}) == tmp_path
    assert not str(WT.models_dir({})).startswith(str(ROOT))
    assert WT.weights_path({"file": "a.gguf", "path": "teacher/a.gguf"}, env={"MYAPP_MODELS_DIR": str(tmp_path)}) == tmp_path / "teacher" / "a.gguf"
    assert WT.weights_path({"file": "a.gguf"}, "/x/y.gguf") == Path("/x/y.gguf")


def test_a_weights_file_needs_a_recorded_matching_hash(tmp_path):
    f = tmp_path / "m.gguf"
    block = {"repo": "o/r", "file": "m.gguf", "sha256": None}
    with pytest.raises(WT.WeightsError, match="does not exist.*o/r"):
        WT.check_weights(block, f)
    f.write_bytes(b"weights")
    with pytest.raises(WT.WeightsError, match="not recorded.*training.common.weights"):
        WT.check_weights(block, f)
    with pytest.raises(WT.WeightsError, match="pins"):
        WT.check_weights({**block, "sha256": "0" * 64}, f)
    assert WT.check_weights({**block, "sha256": sha256_file(f)}, f) == sha256_file(f)


def test_the_weights_command_prints_the_hash_and_checks_a_configured_block(tmp_path, capsys):
    f = tmp_path / "w.gguf"
    f.write_bytes(b"abc")
    assert WT.main([str(f)]) == 0 and capsys.readouterr().out.strip() == sha256_file(f)
    assert WT.main(["--check", "teacher", "--file", str(f)]) == 2                      # the configured hash is not recorded yet
    assert "not recorded" in capsys.readouterr().err
    assert WT.main(["--check", "nope"]) == 2 and WT.main([]) == 2


def test_the_configured_sources_of_the_weights():
    teacher = load_json(ROOT / "training/assistant/config/dataset.json")["teacher"]["weights"]
    assert teacher["file"] == "Qwen3.8-27B-Q4_K_M.gguf" and teacher["repo"] and teacher["sha256"] is None
    assert teacher["self_made_from"] == {"hub_id": "Qwen/Qwen3.8-27B", "revision": "37561816e66b5b9153b1b64c10968c18c1e6f4a0"}
    assert not WT.weights_path(teacher, env={}).is_relative_to(ROOT)
    big = next(c for c in load_json(ROOT / "training/investigator/config/candidates.json")["candidates"] if c["id"] == "qwen3.6-35b-a3b")
    assert big["gguf"]["quantization"] == "Q4_K_M" and big["gguf"]["sha256"] is None and big["trainable"] is False
    fallback = next(c for c in load_json(ROOT / "training/assistant/config/candidates.json")["candidates"] if c["id"] == "qwen3-0.6b")
    assert fallback["revision"] == "c1899de289a04d12100db370d81485cdf75e47ca"


def test_eval_refuses_a_candidate_that_does_not_fit_in_bf16_and_names_the_gguf_route():
    big = {"id": "big", "parameters": 35_000_000_000}
    with pytest.raises(IE.EvalError, match=r"about 75 GB in BF16.*gguf.*training.common.weights"):
        IE.check_fits_bf16(big, info(vram_gb=32.0, ram_gb=64.0))
    IE.check_fits_bf16({"id": "small", "parameters": 4_000_000_000}, info(vram_gb=24.0))
    IE.check_fits_bf16(big, info(vram_gb=24.0, ram_gb=128.0), device="cpu")          # on the CPU the RAM counts


def candidate_env(tmp_path, block=None, hw=None, environ=None):
    tdir = tmp_path / "training"
    shutil.copytree(ROOT / "training" / "investigator" / "config", tdir / "investigator" / "config")
    p = tdir / "investigator" / "config" / "candidates.json"
    data = load_json(p)
    for c in data["candidates"]:
        if c["id"] == "qwen3.6-35b-a3b":
            c.pop("gguf", None)
            if block:
                c["gguf"] = block
    write_json(p, data)
    return IE.Env(training_dir=tdir, repo_root=tmp_path, hardware=hw or info(vram_gb=24.0, ram_gb=64.0), environ=environ or {}, log=lambda m: None), data


def test_the_investigator_candidates_command_refuses_bf16_that_does_not_fit(tmp_path, capsys):
    env, _ = candidate_env(tmp_path)
    assert IE.main(["--candidates", "--tier", "large", "--only", "qwen3.6-35b-a3b", "--allow-unverified", "--artifacts-dir", str(tmp_path / "a")], env) == 2
    assert "cannot be evaluated through `eval --candidates`" in capsys.readouterr().err


def test_a_premade_gguf_is_served_by_llama_cpp_after_its_hash_is_checked(tmp_path, monkeypatch):
    models = tmp_path / "models"
    (models / "investigator").mkdir(parents=True)
    f = models / "investigator" / "q.gguf"
    f.write_bytes(b"4-bit weights")
    block = {"repo": "o/r", "file": "q.gguf", "path": "investigator/q.gguf", "quantization": "Q4_K_M", "sha256": None}
    env, data = candidate_env(tmp_path, block, environ={"MYAPP_MODELS_DIR": str(models)})
    cand = next(c for c in data["candidates"] if c["id"] == "qwen3.6-35b-a3b")
    started = []

    class Fake:
        name = "fake"

        def __init__(self, gguf, server_bin, kwargs, ctx, layers):
            started.append((Path(gguf), ctx, layers))

    monkeypatch.setattr(IE, "LlamaServerBackend", Fake)
    with pytest.raises(WT.WeightsError, match="not recorded"):
        IE._candidate_backend(env, cand, "large", "auto", None, tmp_path)
    cand["gguf"]["sha256"] = sha256_file(f)
    backend, export = IE._candidate_backend(env, cand, "large", "auto", None, tmp_path)
    assert export == "gguf" and started == [(f, IE._tier_context(env, "large"), 999)] and type(backend.inner) is Fake
    cand["gguf"]["sha256"] = "0" * 64
    with pytest.raises(WT.WeightsError, match="pins"):
        IE._candidate_backend(env, cand, "large", "auto", None, tmp_path)


def test_exporting_a_candidate_with_a_premade_gguf_links_it_and_records_its_hash(repo, tmp_path):
    pin_export(repo)
    models = tmp_path / "models"
    (models / "x").mkdir(parents=True)
    gguf = models / "x" / "Q.gguf"
    gguf.write_bytes(b"premade 4-bit weights")
    c = repo / "training" / "assistant" / "config" / "candidates.json"
    data = load_json(c)
    for x in data["candidates"]:
        x["revision"], x["verified"] = "e" * 40, True
        if x["id"] == "qwen3-0.6b":
            x["gguf"] = {"repo": "o/r", "file": "Q.gguf", "path": "x/Q.gguf", "quantization": "Q4_K_M", "sha256": None}
    write_json(c, data)
    env = {**PINNED, "MYAPP_MODELS_DIR": str(models)}
    tools = FakeExportTools()
    assert AEXPORT.main(["--candidate", "qwen3-0.6b", "--formats", "gguf"], training_dir=repo / "training", env=env, tools_factory=lambda cfg: tools) == 2      # hash not recorded
    data["candidates"][-1]["gguf"]["sha256"] = sha256_file(gguf)
    write_json(c, data)
    lines: list[str] = []
    assert AEXPORT.main(["--candidate", "qwen3-0.6b", "--formats", "gguf"], training_dir=repo / "training", env=env, tools_factory=lambda cfg: tools, out=lines.append) == 0
    assert tools.calls == []                                                   # no merge and no conversion
    run_dir = sorted((repo / "training" / "out" / "assistant" / "runs").iterdir())[-1]   # the newest run: the directory order is not defined
    manifest = load_json(run_dir / "exports.json")
    assert manifest["exports"][0]["variant"] == "Q4_K_M" and manifest["exports"][0]["files"][0]["sha256"] == sha256_file(gguf)
    assert (run_dir / "gguf" / manifest["exports"][0]["files"][0]["path"]).exists() and not (run_dir / "merged").exists()
    # the ONNX export has no pre-made file
    assert AEXPORT.main(["--candidate", "qwen3-0.6b", "--formats", "onnx,gguf"], training_dir=repo / "training", env=env, tools_factory=lambda cfg: tools) == 2


# ---------------------------------------------------------------- npm, requirements, line endings, launcher

def test_npm_is_started_by_its_full_path(monkeypatch, tmp_path):
    monkeypatch.setattr(EXT.shutil, "which", lambda n: r"C:\Program Files\nodejs\npm.CMD" if n == "npm" else None)
    assert EXT.npm_path() == r"C:\Program Files\nodejs\npm.CMD"
    (tmp_path / "package.json").write_text(json.dumps({"scripts": {"manual:check": "x", "help:build": "x"}}))
    seen = []
    ok = SimpleNamespace(returncode=0, stdout="", stderr="")
    EXT.manual_check(tmp_path, tmp_path, runner=lambda cmd, **kw: (seen.append(cmd) or ok))
    EXT.help_build(tmp_path, tmp_path / "o", tmp_path, runner=lambda cmd, **kw: (seen.append(cmd) or ok))
    assert all(c[0] == r"C:\Program Files\nodejs\npm.CMD" for c in seen) and len(seen) == 2
    monkeypatch.setattr(EXT.shutil, "which", lambda n: r"C:\n\npm.cmd" if n == "npm.cmd" else None)
    assert EXT.npm_path().endswith("npm.cmd")


def test_requirements_note_the_cuda_128_index_for_rtx_50():
    text = (ROOT / "training" / "requirements.txt").read_text(encoding="utf-8")
    assert "--extra-index-url https://download.pytorch.org/whl/cu128" in text and "RTX 50" in text
    assert "\n--extra-index-url" not in text                               # a note only: the default index stays until the owner chooses


def test_gitattributes_sets_lf_everywhere_and_for_shell_scripts():
    lines = [l.strip() for l in (ROOT / ".gitattributes").read_text(encoding="utf-8").splitlines()]
    assert "* text=auto eol=lf" in lines and "*.sh text eol=lf" in lines


def test_the_powershell_launcher_quotes_paths_keeps_the_handle_and_skips_proxies():
    ps = (ROOT / "src" / "support" / "model-host" / "start.ps1").read_text(encoding="utf-8")
    assert "ConvertTo-QuotedArgument" in ps and "-ArgumentList $quoted" in ps
    assert "$null = $script:Server.Handle" in ps and "-NoProxy" in ps
    assert "Start-Sleep -Milliseconds 500" in ps and "finally" in ps and "BEST EFFORT" in ps
    assert "best effort" in (ROOT / "src" / "support" / "model-host" / "README.md").read_text(encoding="utf-8").lower()


def test_models_md_6_4_points_to_the_windows_walkthrough():
    text = (ROOT / "docs" / "guidelines" / "models.md").read_text(encoding="utf-8")
    six_four = text.split("### 6.4 Hardware")[1].split("### 6.5")[0]
    assert "9.4" in six_four
