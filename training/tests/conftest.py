"""Shared fixtures. The repository root is put on ``sys.path`` so that ``pytest training/tests`` works from anywhere."""
from __future__ import annotations

import copy
import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from training.assistant import generate as G  # noqa: E402
from training.assistant.fake_teacher import FakeTeacher  # noqa: E402


def small_setup():
    """A small configuration: 3 train, 1 validation, 1 test domain, few stories, a small target."""
    cfg, domains, retrieval = G.load_all()
    doms = [d for d in domains if d["id"] in ("d01", "d02", "d03", "d31", "d35")]
    cfg = copy.deepcopy(cfg)
    cfg["splits"] = {"train": 3, "validation": 1, "test": 1}
    cfg["per_domain"]["areas"] = [2, 3]
    cfg["per_domain"]["stories"] = [5, 7]
    cfg["target_examples"]["train"] = 400
    cfg["teacher"].update(model="fake-teacher", verifier_model="fake-verifier", tokens_per_second=50.0,
                          prompt_tokens_per_second=500.0)
    return cfg, doms, retrieval


@pytest.fixture(scope="session")
def golden():
    return json.loads((ROOT / "training" / "assistant" / "protocol.golden.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="session")
def protocol():
    from training.assistant import protocol as P
    return P.load_protocol()


@pytest.fixture(scope="session")
def generated(tmp_path_factory):
    """A complete small run with the offline teacher: returns (cfg, domains, retrieval, out_dir, config_dir, exit code)."""
    cfg, doms, retrieval = small_setup()
    out = tmp_path_factory.mktemp("assistant-out")
    cdir = out / "cfg"
    cdir.mkdir()
    (cdir / "dataset.json").write_text(json.dumps(cfg), encoding="utf-8")
    (cdir / "domains.json").write_text(json.dumps(doms), encoding="utf-8")
    est = G.build_estimate(cfg, doms, retrieval)
    messages: list[str] = []
    code = G.cmd_run(cfg, doms, retrieval, ["test", "validation", "train"], est["id"], out,
                     client_factory=lambda pipe: FakeTeacher(pipe), out=messages.append)
    return cfg, doms, retrieval, out, cdir, code, est["id"], messages


@pytest.fixture(scope="session")
def validated_assistant(generated, tmp_path_factory):
    """A validated dataset in a private copy of the generated cache: ``(out dir, config dir)``. Shared by the train, export and eval tests."""
    import shutil
    from training.assistant import validate as VAL
    cfg, doms, retrieval, out, cdir, *_ = generated
    private = tmp_path_factory.mktemp("assistant-validated")
    shutil.copytree(out, private / "out", ignore=shutil.ignore_patterns("cfg"))
    docs = tmp_path_factory.mktemp("docs-empty")
    VAL.run(private / "out", cdir, tools="off", docs_root=docs, out=lambda m: None)    # the small data may miss V14 shares; the files are written
    return private / "out", cdir
