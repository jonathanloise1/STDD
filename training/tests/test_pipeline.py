"""Generation and validation end to end with the offline teacher: estimate, dry run, approval, resume, budget, dataset."""
import copy
import json
import re
import socket
from pathlib import Path

import pytest

from conftest import ROOT, small_setup
from training.assistant import generate as G, validate as VAL, validators as V
from training.assistant.fake_teacher import FakeTeacher
from training.common import teacher as T
from training.common.jsonio import read_jsonl
from training.common.teacher import JobCache


@pytest.fixture(autouse=True)
def no_network(monkeypatch):
    def refuse(*a, **k):
        raise AssertionError("network access attempted")
    monkeypatch.setattr(socket.socket, "connect", refuse)


class Exploding:
    wants_job = True

    def complete(self, **kw):
        raise AssertionError("a cached job was sent again")


# ---- configuration against the document

def test_dataset_json_equals_the_document():
    doc = ROOT / "docs" / "guidelines" / "models.md"
    if not doc.exists():
        pytest.skip("models.md not available")
    block = re.search(r"### 5\.6 Teacher jobs.*?```json\n(.*?)\n```", doc.read_text(encoding="utf-8"), re.S).group(1)
    assert json.loads(block) == G.load_all()[0]


def test_domains_follow_5_1():
    cfg, domains, _ = G.load_all()
    assert len(domains) == 40 and len({d["id"] for d in domains}) == 40
    assert {s: sum(d["split"] == s for d in domains) for s in ("train", "validation", "test")} == {"train": 30, "validation": 4, "test": 6}
    assert all(set(d) == {"id", "sector", "app", "users", "split"} for d in domains)
    train = {d["sector"] for d in domains if d["split"] == "train"}
    assert not train & {d["sector"] for d in domains if d["split"] != "train"}
    assert len({d["sector"] for d in domains}) == 40
    assert abs(sum(cfg["classes"].values()) - 1) < 1e-9 and sum(cfg["classes"].values()) > 0
    assert cfg["classes"]["pos_distant"] + cfg["classes"]["pos_mid"] + cfg["classes"]["pos_near"] + cfg["classes"]["pos_multi"] == pytest.approx(0.65)
    G.check_config(cfg, domains)


def test_blocklist_names_do_not_appear_in_domains():
    from training.assistant import textchecks
    _, domains, _ = G.load_all()
    bl = textchecks.load_blocklist()
    assert all(textchecks.text_problem(f"{d['sector']} {d['app']} {d['users']}", bl) is None for d in domains)


def test_check_config_detects_mismatch():
    cfg, domains, _ = G.load_all()
    bad = copy.deepcopy(cfg)
    bad["splits"]["test"] = 5
    with pytest.raises(SystemExit):
        G.check_config(bad, domains)


# ---- estimate and dry run (default configuration, all 40 domains)

@pytest.fixture(scope="module")
def default_estimate():
    cfg, domains, retrieval = G.load_all()
    return G.build_estimate(cfg, domains, retrieval)


def test_estimate_covers_every_stage_and_has_a_stable_id(default_estimate):
    cfg, domains, retrieval = G.load_all()
    assert [s.stage for s in default_estimate["all_stages"]] == ["domain", "stories", "pages", "questions", "near_miss", "oos", "answers", "verify"]
    assert re.fullmatch(r"[0-9a-f]{12}", default_estimate["id"])
    assert default_estimate["id"] == G.build_estimate(cfg, domains, retrieval)["id"]
    by = {s.stage: s for s in default_estimate["all_stages"]}
    assert by["domain"].requests == 40 and by["oos"].requests == 80


def test_estimate_id_changes_with_the_configuration():
    cfg, doms, retrieval = small_setup()
    base = G.build_estimate(cfg, doms, retrieval)["id"]
    other = copy.deepcopy(cfg)
    other["teacher"]["tokens_per_second"] = 5.0
    assert G.build_estimate(other, doms, retrieval)["id"] != base
    other = copy.deepcopy(cfg)
    other["master_seed"] += 1
    assert G.build_estimate(other, doms, retrieval)["id"] != base


def test_estimate_id_does_not_depend_on_the_selected_splits():
    cfg, doms, retrieval = small_setup()
    assert G.build_estimate(cfg, doms, retrieval, ["test"])["id"] == G.build_estimate(cfg, doms, retrieval)["id"]
    sub = G.build_estimate(cfg, doms, retrieval, ["test"])
    assert sum(s.requests for s in sub["stages"]) < sum(s.requests for s in sub["all_stages"])


def test_cli_estimate_and_dry_run_print_and_write_nothing(tmp_path, monkeypatch, capsys, default_estimate):
    monkeypatch.chdir(tmp_path)
    before = set(ROOT.joinpath("training").rglob("*")) - set(ROOT.joinpath("training").rglob("__pycache__"))
    assert G.main(["--estimate"]) == 0
    out = capsys.readouterr().out
    assert f"estimate_id: {default_estimate['id']}" in out and "total" in out
    assert G.main(["--dry-run"]) == 0
    out = capsys.readouterr().out
    for stage in G.STAGES:
        assert f"=== stage {stage}:" in out
    assert out.count("--- d") >= 3 * len(G.STAGES) - 4
    assert "nothing sent, nothing written" in out
    assert list(tmp_path.iterdir()) == []
    after = set(ROOT.joinpath("training").rglob("*")) - set(ROOT.joinpath("training").rglob("__pycache__"))
    assert after == before and not (ROOT / "training" / "out").exists()


def test_every_template_renders_in_the_dry_run(default_estimate):
    pipe = default_estimate["pipe"]
    seen = set()
    for jobs in default_estimate["by_stage"].values():
        for j in jobs:
            system, user = pipe.render(j)
            assert user and "{{" not in system + user
            seen.add(j["template"])
    assert seen == {"domain.v1", "stories.v1", "pages.v1", "questions.v1", "multi.v1", "near_miss.v1", "oos.v1", "answers.v1", "verify_answer.v1", "verify_near_miss.v1"}


def test_answers_job_sends_the_rendered_protocol_prompt(default_estimate):
    from training.assistant import protocol as P
    pipe = default_estimate["pipe"]
    job = default_estimate["by_stage"]["answers"][0]
    system, user = pipe.render(job)
    assert system == job["vars"]["system"] and user == job["vars"]["user"]
    assert system.startswith("You answer questions from users of ") and "Manual sections:" in user


def test_a_real_run_needs_the_approval(tmp_path, capsys):
    cfg, doms, retrieval = small_setup()
    messages = []
    assert G.cmd_run(cfg, doms, retrieval, ["test"], None, tmp_path, client_factory=lambda p: Exploding(), out=messages.append) == 2
    assert G.cmd_run(cfg, doms, retrieval, ["test"], "000000000000", tmp_path, client_factory=lambda p: Exploding(), out=messages.append) == 2
    assert "does not match" in messages[-1] and list(tmp_path.iterdir()) == []


def test_a_real_run_needs_a_measured_speed(tmp_path):
    cfg, doms, retrieval = small_setup()
    cfg["teacher"]["tokens_per_second"] = 0
    est = G.build_estimate(cfg, doms, retrieval)["id"]
    with pytest.raises(SystemExit, match="tokens_per_second"):
        G.cmd_run(cfg, doms, retrieval, ["test"], est, tmp_path, client_factory=lambda p: Exploding())


def test_splits_option():
    assert G.parse_splits("test,validation") == ["test", "validation"] and G.parse_splits(None) == ["train", "validation", "test"]
    with pytest.raises(SystemExit):
        G.parse_splits("dev")


def test_main_requires_splits_for_a_real_run():
    with pytest.raises(SystemExit):
        G.main([])


# ---- a complete small run

def test_the_run_completes_and_writes_trees_and_run_json(generated):
    cfg, doms, retrieval, out, cdir, code, est, messages = generated
    assert code == 0 and any(m.startswith("stage verify") for m in messages)
    assert (out / "domains" / "d01" / "docs" / "manual" / "en" / "index.md").exists()
    runs = list((out / "runs").iterdir())
    assert len(runs) == 1
    run = json.loads((runs[0] / "run.json").read_text())
    assert run["commands"][0]["command"] == "generate" and set(run["commands"][0]["config_sha256"]) == {"dataset", "domains", "protocol", "retrieval"}
    cache = list((out / "teacher-cache").glob("*/*.json"))
    assert cache and all(p.parent.name == p.stem[:2] for p in cache)


def test_resume_after_a_complete_run_sends_nothing(generated):
    cfg, doms, retrieval, out, *_ = generated
    est = G.build_estimate(cfg, doms, retrieval)["id"]
    assert G.cmd_run(cfg, doms, retrieval, ["test", "validation", "train"], est, out, client_factory=lambda p: Exploding(), out=lambda m: None) == 0


def test_resume_sends_only_the_missing_jobs(generated, tmp_path):
    cfg, doms, retrieval, out, *_ = generated
    import shutil
    copy_dir = tmp_path / "copy"
    shutil.copytree(out / "teacher-cache", copy_dir / "teacher-cache")
    files = sorted((copy_dir / "teacher-cache").glob("*/*.json"))
    stage = lambda p: json.loads(p.read_text())["stage"]       # noqa: E731
    victims = [p for p in files if stage(p) == "questions"][:3] + [p for p in files if stage(p) == "pages"][:2]
    for p in victims:
        p.unlink()
    fakes = []
    def factory(pipe):
        f = FakeTeacher(pipe)
        fakes.append(f)
        return f
    est = G.build_estimate(cfg, doms, retrieval)["id"]
    assert G.cmd_run(cfg, doms, retrieval, ["test", "validation", "train"], est, copy_dir, client_factory=factory, out=lambda m: None) == 0
    assert len(fakes[0].calls) == len(victims)


def test_time_limit_stops_cleanly_and_the_same_command_resumes(tmp_path, monkeypatch):
    import functools
    from training.common.teacher import JobRunner
    ticks = iter(range(10 ** 9))
    monkeypatch.setattr(G, "JobRunner", functools.partial(JobRunner, clock=lambda: float(next(ticks))))     # every read of the clock is one second
    cfg, doms, retrieval = small_setup()
    cfg["teacher"]["max_hours"] = 40 / 3600
    est = G.build_estimate(cfg, doms, retrieval)["id"]
    messages = []
    fakes = []
    def factory(pipe):
        fakes.append(FakeTeacher(pipe))
        return fakes[-1]
    assert G.cmd_run(cfg, doms, retrieval, ["test", "validation", "train"], est, tmp_path, client_factory=factory, out=messages.append) == 1
    assert any("time limit" in m for m in messages)
    first = len(fakes[0].calls)
    assert 0 < first < 1500
    cfg["teacher"]["max_hours"] = None                        # a changed config needs a new approval
    est2 = G.build_estimate(cfg, doms, retrieval)["id"]
    assert est2 != est
    assert G.cmd_run(cfg, doms, retrieval, ["test", "validation", "train"], est2, tmp_path, client_factory=factory, out=lambda m: None) == 0
    assert len(fakes[1].calls) > 0


# ---- validation of the generated cache

@pytest.fixture(scope="module")
def validated(generated, tmp_path_factory):
    cfg, doms, retrieval, out, cdir, *_ = generated
    docs = tmp_path_factory.mktemp("docs")
    messages = []
    code = VAL.run(out, cdir, tools="off", docs_root=docs, out=messages.append)
    return out, cdir, code, messages, docs


def test_validate_writes_the_dataset_files(validated):
    out, cdir, code, messages, docs = validated
    d = out / "dataset"
    assert {p.name for p in d.iterdir()} >= {"train.jsonl", "validation.jsonl", "test.jsonl", "seen.jsonl", "rejected.jsonl", "stats.json"}
    stats = json.loads((d / "stats.json").read_text())
    assert stats["counts"]["per_split"]["test"] > 0 and stats["counts"]["per_split"]["train"] > 100
    for key in ("overlap_histogram_10_bins", "gold_position_histogram", "context_chars", "answer_chars", "rejections", "teacher", "dataset_sha256", "counts"):
        assert key in stats
    assert len(next(iter(stats["overlap_histogram_10_bins"].values()))["en"]) == 10
    assert set(stats["dataset_validators"]) == {"V13", "V14", "V15"}
    assert stats["dataset_validators"]["V15"]["passed"] and stats["dataset_validators"]["V13"]["passed"]
    assert "dataset_sha256" in "\n".join(messages)


def test_validate_twice_gives_the_same_dataset_hash(validated):
    out, cdir, code, messages, docs = validated
    first = json.loads((out / "dataset" / "stats.json").read_text())["dataset_sha256"]
    VAL.run(out, cdir, tools="off", docs_root=docs, out=lambda m: None)
    again = json.loads((out / "dataset" / "stats.json").read_text())["dataset_sha256"]
    assert first == again == VAL.dataset_sha256(out / "dataset")


def test_every_record_is_valid_and_splits_are_disjoint_by_domain(validated, protocol):
    out, cdir, *_ = validated
    recs = {s: read_jsonl(out / "dataset" / f"{s}.jsonl") for s in ("train", "validation", "test")}
    domains = {s: {r["domain"] for r in rs} for s, rs in recs.items()}
    assert not (domains["train"] & domains["test"] or domains["train"] & domains["validation"] or domains["test"] & domains["validation"])
    from training.assistant import protocol as P
    for rs in recs.values():
        for r in rs:
            assert V.v01(r) == []
            parsed = P.parse_answer(protocol, r["lang"], r["target"], len(r["context"]))
            assert parsed.outcome == ("refused" if r["class"].startswith("ref_") else "answered")
            if parsed.outcome == "answered":
                assert set(r["gold"]) <= set(parsed.cited) or (r["class"] != "pos_multi" and set(r["gold"]) & set(parsed.cited))
    ids = [r["id"] for rs in recs.values() for r in rs]
    assert len(ids) == len(set(ids))


def test_train_shares_and_languages(validated):
    out, *_ = validated
    train = read_jsonl(out / "dataset" / "train.jsonl")
    n = len(train)
    refusals = sum(r["class"].startswith("ref_") for r in train) / n
    assert 0.30 <= refusals <= 0.40
    en = sum(r["lang"] == "en" for r in train) / n
    assert 0.48 <= en <= 0.52
    from collections import Counter
    c = Counter(r["class"] for r in train)
    for cls, want in G.load_all()[0]["classes"].items():
        assert abs(c[cls] / n - want) <= 0.03


def test_gold_positions_and_context_budget(validated):
    out, *_ = validated
    for r in read_jsonl(out / "dataset" / "train.jsonl"):
        assert 2 <= len(r["context"]) <= 4
        if r["class"] == "ref_removed" or r["class"].startswith("ref_"):
            assert r["gold"] == []
        assert sum(len(c["text"]) for c in r["context"]) <= 4000


def test_evaluation_splits_follow_the_composition(validated):
    out, *_ = validated
    test = read_jsonl(out / "dataset" / "test.jsonl")
    from collections import Counter
    c = Counter((r["lang"], r["class"].startswith("pos_"), r["class"]) for r in test)
    assert not any(r["class"] in ("pos_multi", "ref_removed") for r in test)
    for lang in ("en", "it"):
        assert sum(n for (l, _p, cls), n in c.items() if l == lang and cls == "ref_near_miss") <= 12
        assert sum(n for (l, _p, cls), n in c.items() if l == lang and cls == "ref_oos") <= 12


def test_seen_check_questions_are_not_in_train(validated):
    out, *_ = validated
    seen = read_jsonl(out / "dataset" / "seen.jsonl")
    train = {(r["domain"], r["lang"], r["question"]) for r in read_jsonl(out / "dataset" / "train.jsonl")}
    assert seen and not any((r["domain"], r["lang"], r["question"]) in train for r in seen)
    assert all(r["split"] == "train" for r in seen)


def test_rejections_carry_validator_ids(validated):
    out, *_ = validated
    rej = read_jsonl(out / "dataset" / "rejected.jsonl")
    assert rej and all(re.fullmatch(r"V\d\d|format", r["validator"]) and r["validators"] and r["reasons"] for r in rej)


def test_a_tampered_cache_is_caught_by_the_item_validators(generated, tmp_path):
    """An answer that cites the wrong section, or invents content, is rejected with V07 or V08."""
    cfg, doms, retrieval, out, cdir, *_ = generated
    import shutil
    work = tmp_path / "w"
    shutil.copytree(out / "teacher-cache", work / "teacher-cache")
    shutil.copytree(cdir, work / "cfg")
    changed = 0
    for p in sorted((work / "teacher-cache").glob("*/*.json")):
        e = json.loads(p.read_text())
        if e["stage"] == "answers" and changed < 6:
            e["response"] = "Press the blue lever to launch rockets into orbit tonight.\nSource: [1]" if changed % 2 else e["response"].rsplit("\n", 1)[0] + "\nSource: [1], [2], [3], [4]"
            p.write_text(json.dumps(e))
            changed += 1
    VAL.run(work, work / "cfg", tools="off", docs_root=tmp_path, out=lambda m: None)
    rej = read_jsonl(work / "dataset" / "rejected.jsonl")
    assert {v for r in rej for v in r["validators"]} & {"V06", "V07", "V08"}
    assert sum(1 for r in rej if r["stage"] in ("assemble", "answers", "verify")) == 6        # every tampered answer is rejected


def test_the_validator_exit_code_fails_on_dataset_level_problems(generated, validated, tmp_path):
    """V15: the sample-app documentation must not appear in any item."""
    cfg, doms, retrieval, out, cdir, *_ = generated
    train = read_jsonl(out / "dataset" / "train.jsonl") if (out / "dataset" / "train.jsonl").exists() else None
    assert train is not None
    r = train[0]
    docs = tmp_path / "docs" / "areas" / "x"
    docs.mkdir(parents=True)
    (docs / "userstories.md").write_text(r["context"][0]["title"] + "\n" + r["context"][0]["text"], encoding="utf-8")
    code = VAL.run(out, cdir, tools="off", docs_root=tmp_path / "docs", out=lambda m: None)
    assert code == 1
    stats = json.loads((out / "dataset" / "stats.json").read_text())
    assert stats["dataset_validators"]["V15"]["passed"] is False
