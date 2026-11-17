"""Lock file read, write, record and verify (L01 to L08; L09 and the embeddings entry are in test_embed.py), the card and the fetch tool, with fake artifacts."""
import hashlib
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path

import pytest

from conftest import ROOT
from training.common import card as CARD, fetch as FETCH, lock as LOCK
from training.common.hashing import sha256_file

NOW = datetime(2026, 11, 20, 18, 0, 0, tzinfo=timezone.utc)
ONNX = b"fake onnx weights"
GGUF = b"fake gguf weights" * 3


def sha(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()


def make_root(tmp_path: Path, passed: bool = True, version: str = "1.0.0") -> Path:
    """A repository with the files a recorded model depends on and one finished run."""
    root = tmp_path / "repo"
    (root / "docs").mkdir(parents=True)
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": {k: {"version": 1} for k in ("manual-page", "help-pack", "assistant-protocol", "assistant-retrieval")}}))
    a = root / "training" / "assistant"
    (a / "config").mkdir(parents=True)
    shutil.copy(ROOT / "training/assistant/protocol.json", a / "protocol.json")
    shutil.copy(ROOT / "training/assistant/retrieval.json", a / "retrieval.json")
    (a / "config" / "eval.json").write_text('{"thresholds": 1}')
    shutil.copy(ROOT / "training/assistant/config/dataset.json", a / "config" / "dataset.json")
    run = root / "training" / "out" / "assistant" / "runs" / "2026-11-20-a"
    run.mkdir(parents=True)
    (run / "run.json").write_text(json.dumps({
        "model_version": version, "status": "trained",
        "base_model": {"hub_id": "org/base-0.6b", "revision": "abc123", "license": "apache-2.0", "parameters": 600000000, "chat_template_kwargs": {}},
        "adapter": {"method": "lora", "rank": 16, "alpha": 32, "checkpoint": "step-2750"},
        "dataset": {"config_sha256": "a" * 64, "dataset_sha256": "b" * 64, "domains": {"train": 30, "validation": 4, "test": 6}, "examples": 12000, "teacher_models": ["teacher-x"]},
        "training": {"run_id": "2026-11-20-a", "code_commit": "deadbeef", "seed": 20261106, "hardware": "GPU"},
        "artifact_url": "https://store.example/assistant/1.0.0"}))
    (run / "exports.json").write_text(json.dumps({"exports": [
        {"format": "onnx", "variant": "q4f16", "runtime": "browser", "files": [{"path": "onnx/model_q4f16.onnx", "sha256": sha(ONNX), "bytes": len(ONNX)}]},
        {"format": "gguf", "variant": "Q4_K_M", "runtime": "server", "files": [{"path": "assistant-1.0.0-Q4_K_M.gguf", "sha256": sha(GGUF), "bytes": len(GGUF)}]}]}))
    thr = sha256_file(a / "config" / "eval.json")
    for fmt in ("merged", "onnx", "gguf"):
        (run / f"eval-{fmt}.json").write_text(json.dumps({
            "schema_version": 1, "model": "assistant", "model_version": version, "export": fmt, "suite_version": 1, "thresholds_sha256": thr,
            "judge": {"model": "j", "prompt_version": 1, "human_agreement": {"faithful": 0.95, "useful": 0.92}},
            "results": {"platform_test": {"en": {"useful": 0.8, "n": 360}, "it": {"useful": 0.79, "n": 360}}, "latency": {"p50_ms": 900, "p95_ms": 1800}},
            "passed": passed if fmt == "gguf" else True}))
    return root


def recorded(tmp_path, **kw) -> Path:
    root = make_root(tmp_path, **kw)
    LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    CARD.refresh("assistant", root)
    return root


def ids(root, **kw):
    return [f.id for f in LOCK.verify(root, **kw)]


# ---- record

def test_record_writes_the_entry_in_the_order_of_the_document(tmp_path):
    root = make_root(tmp_path)
    entry = LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    data = json.loads((root / "models" / "model.lock").read_text())
    assert list(data) == ["schema_version", "models"]
    e = data["models"]["assistant"]
    assert list(e) == LOCK.ENTRY_KEYS and e["recorded_at"] == "2026-11-20T18:00:00Z"
    assert e["protocol"] == {"path": "training/assistant/protocol.json", "sha256": sha256_file(root / "training/assistant/protocol.json")}
    assert e["contracts"] == {"manual-page": 1, "help-pack": 1, "assistant-protocol": 1, "assistant-retrieval": 1}
    assert [x["url"] for x in e["exports"]] == ["https://store.example/assistant/1.0.0/onnx/", "https://store.example/assistant/1.0.0/gguf/"]
    assert e["evaluation"] == {"suite_version": 1, "report": "models/assistant/eval/1.0.0.json", "report_sha256": sha256_file(root / "models/assistant/eval/1.0.0.json"), "passed": True}
    assert (root / "models/assistant/eval/1.0.0-onnx.json").exists() and entry["card"] == "models/assistant/MODEL_CARD.md"


def test_record_refuses_a_run_that_did_not_pass(tmp_path):
    root = make_root(tmp_path, passed=False)
    with pytest.raises(LOCK.LockError, match="did not pass"):
        LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    assert not (root / "models" / "model.lock").exists()


@pytest.mark.parametrize("remove,fragment", [("eval-onnx.json", "missing evaluation report"), ("exports.json", "exports.json"), ("run.json", "run.json")])
def test_record_needs_every_input(tmp_path, remove, fragment):
    root = make_root(tmp_path)
    (root / "training/out/assistant/runs/2026-11-20-a" / remove).unlink()
    with pytest.raises(LOCK.LockError, match=fragment):
        LOCK.record("assistant", "2026-11-20-a", root, now=NOW)


def test_record_needs_the_artifact_url(tmp_path):
    root = make_root(tmp_path)
    p = root / "training/out/assistant/runs/2026-11-20-a/run.json"
    d = json.loads(p.read_text())
    del d["artifact_url"]
    p.write_text(json.dumps(d))
    with pytest.raises(LOCK.LockError, match="artifact store"):
        LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    assert LOCK.record("assistant", "2026-11-20-a", root, artifact_url="https://x/y", now=NOW)["exports"][0]["url"] == "https://x/y/onnx/"


def test_untrained_candidate_needs_null_adapter_and_dataset(tmp_path):
    root = make_root(tmp_path)
    p = root / "training/out/assistant/runs/2026-11-20-a/run.json"
    d = json.loads(p.read_text())
    d["status"] = "untrained-candidate"
    p.write_text(json.dumps(d))
    with pytest.raises(LOCK.LockError, match="null"):
        LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    d["adapter"] = d["dataset"] = None
    p.write_text(json.dumps(d))
    assert LOCK.record("assistant", "2026-11-20-a", root, now=NOW)["status"] == "untrained-candidate"


def test_record_replaces_the_entry_of_the_same_model(tmp_path):
    root = recorded(tmp_path)
    LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    assert list(json.loads((root / "models/model.lock").read_text())["models"]) == ["assistant"]


# ---- verify

def test_verify_passes_on_a_recorded_model_with_a_card(tmp_path):
    root = recorded(tmp_path)
    assert LOCK.verify(root) == []
    assert LOCK.main(["--verify", "--root", str(root)]) == 0


def test_l01_unparsable_or_unknown_schema(tmp_path):
    root = recorded(tmp_path)
    lock = root / "models/model.lock"
    lock.write_text("{nope")
    assert ids(root) == ["L01"]
    lock.write_text(json.dumps({"schema_version": 3, "models": {}}))
    assert ids(root) == ["L01"]
    lock.write_text(json.dumps({"schema_version": 1, "models": {}}))          # a version 1 lock is read as version 2 (models.md 8.2 L01)
    assert ids(root) == []
    lock.unlink()
    assert ids(root) == ["L01"]
    assert LOCK.main(["--verify", "--root", str(root)]) == 1


def mutate_lock(root, fn):
    p = root / "models/model.lock"
    d = json.loads(p.read_text())
    fn(d["models"]["assistant"])
    p.write_text(json.dumps(d))


def test_l02_contract_version_differs(tmp_path):
    root = recorded(tmp_path)
    (root / "docs/contracts.json").write_text(json.dumps({"contracts": {"manual-page": {"version": 2}, "help-pack": {"version": 1}, "assistant-protocol": {"version": 1}, "assistant-retrieval": {"version": 1}}}))
    assert ids(root) == ["L02"]


@pytest.mark.parametrize("name", ["protocol.json", "retrieval.json"])
def test_l03_protocol_or_retrieval_bytes_changed(tmp_path, name):
    root = recorded(tmp_path)
    p = root / "training/assistant" / name
    p.write_text(p.read_text() + " ")
    assert ids(root) == ["L03"]


def test_l04_evaluation(tmp_path):
    root = recorded(tmp_path)
    mutate_lock(root, lambda e: e["evaluation"].update(passed=False))
    assert "L04" in ids(root)
    root = recorded(tmp_path / "b")
    report = root / "models/assistant/eval/1.0.0.json"
    report.write_text(report.read_text() + " ")
    assert "L04" in ids(root)                                                  # report SHA-256 differs
    root = recorded(tmp_path / "c")
    (root / "training/assistant/config/eval.json").write_text('{"thresholds": 2}')
    assert any("thresholds_sha256" in str(f) for f in LOCK.verify(root))


def test_l05_exports(tmp_path):
    root = recorded(tmp_path)
    mutate_lock(root, lambda e: e["exports"][0].update(url=""))
    assert ids(root) == ["L05"]
    root = recorded(tmp_path / "b")
    mutate_lock(root, lambda e: e["exports"][1].update(files=[]))
    assert ids(root) == ["L05"]
    root = recorded(tmp_path / "c")
    mutate_lock(root, lambda e: e["exports"][0]["files"][0].update(sha256="xyz"))
    assert ids(root) == ["L05"]


def test_l06_card(tmp_path):
    root = recorded(tmp_path)
    card = root / "models/assistant/MODEL_CARD.md"
    card.write_text(card.read_text().replace("## Limitations", "## Notes"))
    assert ids(root) == ["L06"]
    card.unlink()
    assert ids(root) == ["L06"]


def test_l07_incompatible_pack(tmp_path):
    root = recorded(tmp_path)
    pack = root / "src/frontend/public/help/en/pack.json"
    pack.parent.mkdir(parents=True)
    pack.write_text(json.dumps({"assistant": {"compatible": True}}))
    assert ids(root) == []
    pack.write_text(json.dumps({"assistant": {"compatible": False}}))
    assert ids(root) == ["L07"]


def test_l08_weights(tmp_path, monkeypatch):
    monkeypatch.setenv("MYAPP_MODELS_DIR", str(tmp_path / "myapp-models"))
    root = recorded(tmp_path)
    w = tmp_path / "myapp-models/assistant/onnx/model_q4f16.onnx"
    w.parent.mkdir(parents=True)
    w.write_bytes(ONNX)
    assert ids(root) == []
    w.write_bytes(b"corrupt")
    assert ids(root) == ["L08"]


def test_verify_one_model_only_and_missing_entry(tmp_path):
    root = recorded(tmp_path)
    assert LOCK.verify(root, only="assistant") == []
    assert ids(root, only="investigator") == ["L01"]


# ---- card

def test_card_has_the_exact_headings_in_order_and_preserves_owner_text(tmp_path):
    root = recorded(tmp_path)
    card = root / "models/assistant/MODEL_CARD.md"
    text = card.read_text()
    import re
    assert re.findall(r"^## (.+)$", text, re.M) == LOCK.CARD_HEADINGS
    assert "org/base-0.6b" in text and "model_q4f16.onnx" in text and "| p95_ms | 1800 |" in text
    card.write_text(text.replace("To be written by the owner.", "Weak on long questions.").replace("Not run yet.", "Ran on 2026-11-21."))
    CARD.refresh("assistant", root)
    again = card.read_text()
    assert "Weak on long questions." in again and "Ran on 2026-11-21." in again
    assert again.count("- 1.0.0 (2026-11-20)") == 1                           # no duplicate history line
    LOCK.record("assistant", "2026-11-20-a", root, now=datetime(2026, 11, 22, tzinfo=timezone.utc))
    CARD.refresh("assistant", root)
    assert card.read_text().count("- 1.0.0 (") == 1


def test_card_needs_a_lock_entry(tmp_path):
    root = make_root(tmp_path)
    with pytest.raises(LOCK.LockError):
        CARD.refresh("assistant", root)
    assert CARD.main(["--model", "assistant", "--root", str(root)]) == 1


def test_card_for_untrained_candidate(tmp_path):
    root = make_root(tmp_path)
    p = root / "training/out/assistant/runs/2026-11-20-a/run.json"
    d = json.loads(p.read_text())
    d.update(status="untrained-candidate", adapter=None, dataset=None)
    p.write_text(json.dumps(d))
    LOCK.record("assistant", "2026-11-20-a", root, now=NOW)
    CARD.refresh("assistant", root)
    assert "untrained candidate" in (root / "models/assistant/MODEL_CARD.md").read_text()
    assert LOCK.verify(root) == []


# ---- fetch

def fake_store():
    blobs = {"https://store.example/assistant/1.0.0/onnx/model_q4f16.onnx": ONNX, "https://store.example/assistant/1.0.0/gguf/assistant-1.0.0-Q4_K_M.gguf": GGUF}
    calls = []

    def download(url, dest):
        calls.append(url)
        dest.write_bytes(blobs[url])
    return download, calls, blobs


def test_file_url_does_not_repeat_the_export_folder():
    assert FETCH.file_url("https://s/a/1.0.0/onnx/", "onnx/model_q4f16.onnx") == "https://s/a/1.0.0/onnx/model_q4f16.onnx"
    assert FETCH.file_url("https://s/a/1.0.0/gguf/", "assistant.gguf") == "https://s/a/1.0.0/gguf/assistant.gguf"
    assert FETCH.file_url("https://s/a/1.0.0/onnx", "onnx/tokenizer.json") == "https://s/a/1.0.0/onnx/tokenizer.json"


def test_fetch_downloads_checks_and_then_skips(tmp_path, monkeypatch):
    monkeypatch.setenv("MYAPP_MODELS_DIR", str(tmp_path / "myapp-models"))
    root = recorded(tmp_path)
    dl, calls, _ = fake_store()
    msgs = []
    assert FETCH.fetch("assistant", root, downloader=dl, out=msgs.append) == 0
    w = tmp_path / "myapp-models" / "assistant"          # outside the repository, the folder compose mounts; the GGUF has compose's name
    assert (w / "onnx/model_q4f16.onnx").read_bytes() == ONNX and (w / "assistant.gguf").read_bytes() == GGUF
    assert not (root / "models/assistant/weights").exists()
    assert LOCK.verify(root) == [] and len(calls) == 2
    assert FETCH.fetch("assistant", root, downloader=dl, out=msgs.append) == 0 and len(calls) == 2      # present files are not downloaded again
    assert not list(w.rglob("*.part"))


def test_fetch_rejects_a_download_with_a_wrong_hash(tmp_path, monkeypatch):
    monkeypatch.setenv("MYAPP_MODELS_DIR", str(tmp_path / "myapp-models"))
    root = recorded(tmp_path)
    def bad(url, dest):
        dest.write_bytes(b"x" * len(ONNX))
    msgs = []
    assert FETCH.fetch("assistant", root, runtime="browser", downloader=bad, out=msgs.append) == 1
    assert any("FAILED" in m for m in msgs)
    assert not (tmp_path / "myapp-models/assistant/onnx/model_q4f16.onnx").exists()
    assert not list((tmp_path / "myapp-models/assistant").rglob("*.part"))


def test_fetch_filters_and_web_root_layout(tmp_path, monkeypatch):
    monkeypatch.setenv("MYAPP_MODELS_DIR", str(tmp_path / "myapp-models"))
    root = recorded(tmp_path)
    dl, calls, _ = fake_store()
    to = tmp_path / "public" / "models" / "assistant"
    assert FETCH.fetch("assistant", root, to=to, runtime="browser", downloader=dl, out=lambda m: None) == 0
    assert (to / "1.0.0" / "onnx" / "model_q4f16.onnx").exists() and len(calls) == 1
    assert not (to / "1.0.0" / "assistant-1.0.0-Q4_K_M.gguf").exists()


def test_server_gguf_names_follow_compose():
    from training.common.weights import local_path
    fl = {"path": "x/model-Q4_K_M.gguf"}
    assert local_path("assistant", {"format": "gguf"}, fl) == "assistant.gguf"
    assert local_path("investigator", {"format": "gguf", "tier": "small"}, fl) == "investigator.gguf"
    assert local_path("investigator", {"format": "gguf", "tier": "large"}, fl) == "investigator-large.gguf"
    assert local_path("assistant", {"format": "onnx"}, {"path": "onnx/m.onnx"}) == "onnx/m.onnx"


def test_fetch_reports_a_failing_downloader(tmp_path, monkeypatch):
    monkeypatch.setenv("MYAPP_MODELS_DIR", str(tmp_path / "myapp-models"))
    root = recorded(tmp_path)
    def boom(url, dest):
        raise OSError("no network")
    msgs = []
    assert FETCH.fetch("assistant", root, downloader=boom, out=msgs.append) == 1
    assert sum("no network" in m for m in msgs) == 2
    with pytest.raises(LOCK.LockError):
        FETCH.fetch("investigator", root, downloader=boom)


# ---- the investigator: one export per tier, one run per tier (models.md 8.1, operations.md 11.2)

BEGIN, END = "<!-- protocol:begin -->", "<!-- protocol:end -->"
AGENT = f"# Support\n\n{BEGIN}\nYou investigate one incident.\n{END}\n"
SMALL_GGUF, LARGE_GGUF = b"small gguf" * 4, b"large gguf" * 9


def make_inv_root(tmp_path: Path) -> Path:
    root = tmp_path / "repo"
    (root / "docs").mkdir(parents=True)
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": {"investigator-protocol": {"version": 1}, "incident-report": {"version": 1}}}))
    (root / ".github" / "agents").mkdir(parents=True)
    (root / ".github/agents/support.agent.md").write_text(AGENT, encoding="utf-8")
    (root / "training/investigator/config").mkdir(parents=True)
    (root / "training/investigator/config/eval.json").write_text('{"thresholds": 1}')
    return root


def add_inv_run(root: Path, run_id: str, tier: str, blob: bytes, version: str = "1.0.0", passed: bool = True, protocol_sha: str | None = None) -> None:
    block = AGENT.encode().split(BEGIN.encode())[1].split(END.encode())[0]
    run = root / "training/out/investigator/runs" / run_id
    run.mkdir(parents=True)
    base = {"small": "org/base-1b", "large": "org/base-8b"}[tier]
    (run / "run.json").write_text(json.dumps({
        "model_version": version, "status": "trained", "tier": tier,
        "base_model": {"hub_id": base, "revision": "abc", "license": "apache-2.0", "parameters": 1, "chat_template_kwargs": {}},
        "adapter": {"method": "lora", "rank": 16, "alpha": 32, "checkpoint": f"step-{tier}"},
        "protocol": {"path": ".github/agents/support.agent.md", "markers": [BEGIN, END], "sha256": protocol_sha or sha(block)},
        "contracts": {"investigator-protocol": 1, "incident-report": 1},
        "dataset": {"config_sha256": "a" * 64, "dataset_sha256": "b" * 64, "domains": {"train": 17, "validation": 2, "test": 5}, "examples": 100, "teacher_models": ["t"]},
        "training": {"run_id": run_id, "code_commit": "deadbeef", "seed": 1, "hardware": "GPU"},
        "artifact_url": "https://store.example/investigator/1.0.0"}))
    (run / "exports.json").write_text(json.dumps({"exports": [
        {"format": "gguf", "variant": "Q4_K_M", "runtime": "server", "tier": tier, "context_length": 8192 if tier == "small" else 16384,
         "memory_mb": 900 if tier == "small" else 6000, "files": [{"path": f"investigator-{version}-{tier}-Q4_K_M.gguf", "sha256": sha(blob), "bytes": len(blob)}]}]}))
    thr = sha256_file(root / "training/investigator/config/eval.json")
    for fmt in ("merged", "gguf"):
        (run / f"eval-{fmt}.json").write_text(json.dumps({"schema_version": 1, "model": "investigator", "model_version": version, "export": fmt, "suite_version": 1,
                                                          "thresholds_sha256": thr, "judge": None, "tier": tier, "results": {}, "passed": passed}))


def two_tiers(tmp_path: Path) -> Path:
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "small", SMALL_GGUF)
    add_inv_run(root, "2026-11-21-a", "large", LARGE_GGUF)
    LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    LOCK.record("investigator", "2026-11-21-a", root, now=datetime(2026, 11, 21, 9, 0, 0, tzinfo=timezone.utc))
    (root / "models/investigator").mkdir(parents=True, exist_ok=True)
    CARD.refresh("investigator", root)
    return root


def entry_of(root: Path) -> dict:
    return json.loads((root / "models/model.lock").read_text())["models"]["investigator"]


def test_a_second_tier_is_added_to_the_entry_and_does_not_replace_the_first(tmp_path):
    root = two_tiers(tmp_path)
    e = entry_of(root)
    assert [(x["tier"], x["context_length"], x["memory_mb"]) for x in e["exports"]] == [("small", 8192, 900), ("large", 16384, 6000)]
    assert list(e["tiers"]) == ["small", "large"]
    assert e["tiers"]["small"]["base_model"]["hub_id"] == "org/base-1b" and e["tiers"]["large"]["base_model"]["hub_id"] == "org/base-8b"
    assert e["base_model"]["hub_id"] == "org/base-1b" and e["evaluation"] == e["tiers"]["small"]["evaluation"]      # the top level mirrors the smallest tier
    assert e["tiers"]["small"]["evaluation"]["report"] == "models/investigator/eval/1.0.0-small.json"
    assert e["tiers"]["large"]["evaluation"]["report"] == "models/investigator/eval/1.0.0-large.json"
    assert (root / "models/investigator/eval/1.0.0-small.json").exists() and (root / "models/investigator/eval/1.0.0-large-merged.json").exists()
    assert e["recorded_at"] == "2026-11-21T09:00:00Z" and list(e) == ["model_version", "status", "base_model", "adapter", "protocol", "contracts", "dataset", "training", "exports", "evaluation", "tiers", "card", "recorded_at"]
    assert LOCK.verify(root) == []


def test_recording_a_tier_again_replaces_only_that_tier(tmp_path):
    root = two_tiers(tmp_path)
    newer = b"large gguf retrained" * 3
    add_inv_run(root, "2026-11-22-a", "large", newer)
    LOCK.record("investigator", "2026-11-22-a", root, now=NOW)
    e = entry_of(root)
    assert [x["tier"] for x in e["exports"]] == ["small", "large"]
    assert e["exports"][0]["files"][0]["sha256"] == sha(SMALL_GGUF) and e["exports"][1]["files"][0]["sha256"] == sha(newer)
    assert e["tiers"]["large"]["training"]["run_id"] == "2026-11-22-a" and e["tiers"]["small"]["training"]["run_id"] == "2026-11-20-a"


def test_a_new_model_version_starts_a_new_entry(tmp_path):
    root = two_tiers(tmp_path)
    add_inv_run(root, "2026-12-01-a", "small", SMALL_GGUF, version="1.1.0")
    LOCK.record("investigator", "2026-12-01-a", root, now=NOW)
    e = entry_of(root)
    assert e["model_version"] == "1.1.0" and list(e["tiers"]) == ["small"] and [x["tier"] for x in e["exports"]] == ["small"]


def test_a_tier_trained_on_another_protocol_cannot_join_the_entry(tmp_path):
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "small", SMALL_GGUF)
    add_inv_run(root, "2026-11-21-a", "large", LARGE_GGUF, protocol_sha="f" * 64)
    LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    before = (root / "models/model.lock").read_text()
    with pytest.raises(LOCK.LockError, match="protocol"):
        LOCK.record("investigator", "2026-11-21-a", root, now=NOW)
    assert (root / "models/model.lock").read_text() == before and not (root / "models/investigator/eval/1.0.0-large.json").exists()


def test_a_tier_that_did_not_pass_is_refused_and_a_run_holds_one_tier(tmp_path):
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "large", LARGE_GGUF, passed=False)
    with pytest.raises(LOCK.LockError, match="did not pass"):
        LOCK.record("investigator", "2026-11-20-a", root, now=NOW)
    p = root / "training/out/investigator/runs/2026-11-20-a/exports.json"
    d = json.loads(p.read_text())
    d["exports"].append({**d["exports"][0], "tier": "small", "format": "gguf"})
    p.write_text(json.dumps(d))
    with pytest.raises(LOCK.LockError, match="one tier"):
        LOCK.record("investigator", "2026-11-20-a", root, now=NOW)


def test_verify_checks_each_tier_separately(tmp_path):
    root = two_tiers(tmp_path)
    assert ids(root) == []
    (root / "models/investigator/eval/1.0.0-large.json").write_text("{}")
    got = LOCK.verify(root)
    assert {x.id for x in got} == {"L04"} and all("tier large" in x.message for x in got if "tier" in x.message) and any("tier large" in x.message for x in got)


def test_verify_checks_the_tier_fields_of_the_exports(tmp_path):
    root = two_tiers(tmp_path)
    lock = root / "models/model.lock"
    data = json.loads(lock.read_text())
    e = data["models"]["investigator"]
    del e["exports"][1]["memory_mb"]
    e["exports"].append({**e["exports"][1]})
    e["exports"][0]["tier"] = "huge"
    lock.write_text(json.dumps(data))
    msgs = [x.message for x in LOCK.verify(root) if x.id == "L05"]
    assert any("memory_mb" in m for m in msgs) and any("valid tier" in m for m in msgs) and any("two gguf exports" in m for m in msgs)
    del e["tiers"]["large"]
    e["exports"] = [x for x in e["exports"] if x.get("tier") == "large"]
    lock.write_text(json.dumps(data))
    assert any("no section in tiers" in x.message for x in LOCK.verify(root))


def test_l03_checks_the_marked_protocol_block(tmp_path):
    root = two_tiers(tmp_path)
    assert ids(root) == []
    agent = root / ".github/agents/support.agent.md"
    agent.write_text(AGENT.replace("one incident", "two incidents"), encoding="utf-8")
    assert ids(root) == ["L03"]
    agent.write_text(AGENT + BEGIN, encoding="utf-8")
    assert "markers must occur exactly once" in LOCK.verify(root)[0].message


def test_the_lock_main_records_the_second_tier(tmp_path):
    root = make_inv_root(tmp_path)
    add_inv_run(root, "2026-11-20-a", "small", SMALL_GGUF)
    add_inv_run(root, "2026-11-21-a", "large", LARGE_GGUF)
    assert LOCK.main(["--record", "--model", "investigator", "--run", "2026-11-20-a", "--root", str(root)]) == 0
    assert LOCK.main(["--record", "--model", "investigator", "--run", "2026-11-21-a", "--root", str(root)]) == 0
    assert list(entry_of(root)["tiers"]) == ["small", "large"]
