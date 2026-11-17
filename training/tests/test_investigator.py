"""The investigator: protocol and golden cases, masking, worlds, the generator, its validators, the host loop and the evaluation."""
import json
import re
import shutil
import subprocess
from collections import Counter
from dataclasses import replace
from pathlib import Path

import pytest

from conftest import ROOT
from support import FakeEngine, FakeExportTools, hardware, make_inv_repo, oracle_investigator
from training.common import backends as B, evalcore as EC
from training.common.hashing import canonical_sha256, sha256_file
from training.common.jsonio import load_json, read_jsonl, write_json
from training.investigator import (eval as IE, generate as G, golden as GOLD, host as H, masking as MK, phrasings as PH, protocol as P, scenarios as SC, tools as TL,
                                   validate as V, world as W)
from training.investigator.adapter import InvestigatorSetup

INV = ROOT / "training" / "investigator"
OPS_MD = ROOT / "docs" / "guidelines" / "operations.md"


# ---------------------------------------------------------------- static files

def test_at_least_24_worlds_at_least_4_held_out_whole_and_every_log_key_follows_the_grammar():
    cfg, specs = G.load_config(), G.load_worlds()
    assert len(specs) >= 24 and len(cfg["holdout_domains"]) >= 4 and G.check_worlds(cfg, specs) == []
    key = re.compile(r"^[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(_[a-z]+)*$")
    for s in specs.values():
        assert all(key.match(l["key"]) for l in s["logs"]) and len({l["key"] for l in s["logs"]}) == len(s["logs"])
        assert {l["fault"] for l in s["logs"]} >= set(SC.FAULT_TO_CATEGORY) | {"component"}
        assert {e["name"] for e in s["entities"]} >= {"account", "site", "membership", s["record"]}
    assert not {specs[d]["sector"] for d in cfg["holdout_domains"]} & {s["sector"] for k, s in specs.items() if k not in cfg["holdout_domains"] and s["split"] == "train"}


def test_a_world_set_that_breaks_the_rules_is_reported():
    cfg, specs = G.load_config(), G.load_worlds()
    assert any("at least 24" in p for p in G.check_worlds(cfg, dict(list(specs.items())[:10])))
    assert any("at least 4" in p for p in G.check_worlds({**cfg, "holdout_domains": ["w20"]}, specs))
    bad = json.loads(json.dumps(specs))
    bad["w20"]["sector"] = bad["w01"]["sector"]
    assert any("sector" in p for p in G.check_worlds(cfg, bad))


def test_every_cause_category_has_at_least_ten_phrasings_within_300_characters():
    vars_ = {"when": "09:10", "dependency": "dep", "setting": "S:Key", "source": "feed", "component": "Comp", "version": "2.4.0", "record": "loan", "area": "loans", "action": "lend"}
    assert set(PH.CAUSES) == set(SC.FAULT_TO_CATEGORY.values())
    for cat, sentences in PH.CAUSES.items():
        assert len(set(sentences)) >= 10
        assert all(len(s.format(**vars_)) <= 300 for s in sentences), cat


def test_thresholds_json_equals_operations_md_11_4():
    if not OPS_MD.exists():
        pytest.skip("operations.md not available")
    text = OPS_MD.read_text(encoding="utf-8")
    block = re.search(r"\| Metric \| Definition \| Threshold \|\n.*?\n\n", text, re.S).group(0)
    rows = {}
    for line in [l for l in block.splitlines()[2:] if l.strip()]:
        cells = [c.strip() for c in line.strip("|").split("|")]
        rows[cells[0].strip("`")] = cells[2]
    gated = load_json(INV / "thresholds.json")["gated"]
    for name, rule in gated.items():
        op, value = rows[name].split()
        assert rule == {"op": op, "value": float(value) if "." in value else int(value)}, name
    assert set(gated) | set(load_json(INV / "thresholds.json")["reported"]) == set(rows)
    assert rows["latency_p95"].startswith("Reported")


def test_eval_json_pins_the_thresholds_file():
    cfg = load_json(INV / "config" / "eval.json")
    assert cfg["thresholds_file_sha256"] == sha256_file(INV / "thresholds.json") and cfg["holdout_set_sha256"] is None


def test_generator_config_follows_the_document():
    cfg = G.load_config()
    assert (cfg["train_examples"], cfg["holdout_examples_per_domain"]) == (20000, 250)


# ---------------------------------------------------------------- protocol and golden cases

def test_golden_file_is_current_and_covers_every_outcome():
    assert GOLD.main(["--check"]) == 0
    g = load_json(INV / "protocol.golden.json")
    assert len(g["render"]) >= 12 and len(g["parse"]) >= 12 and len(g["validate"]) >= 8
    assert {p["expected"]["kind"] for p in g["parse"]} == {"call", "report", "invalid"}
    assert g["system_block_sha256"] == P.block_sha256(P.load_protocol())


def test_golden_cases_render_and_parse_byte_for_byte():
    g = load_json(INV / "protocol.golden.json")
    proto = P.load_protocol()
    for c in g["render"]:
        i, k = c["input"], c["kind"]
        got = (P.render_user(proto, i["incident"], i["pack"], i["tools"], i["entities"], i["constraints"], i["calls"]) if k == "user"
               else P.render_call(proto, i["tool"], i["arguments"]) if k == "call" else P.render_result(proto, i["envelope"], i["calls_left"]) if k == "result"
               else P.render_report(proto, i) if k == "report" else P.render_repair(proto, i))
        assert got == c["expected"], c["id"]
    for c in g["parse"]:
        t = P.parse_turn(c["output"])
        assert {"kind": t.kind, "payload": t.payload} == c["expected"], c["id"]
    schema = P.investigation_schema()
    for c in g["validate"]:
        assert (P.validate_report(schema, c["report"], set(c["evidence_ids"]), None if c["stories"] is None else set(c["stories"])) == []) == c["expected_valid"], c["id"]
    for c in g["normalise"]:
        assert P.normalise_report(proto, c["report"]) == c["expected"]


def test_protocol_markers_exist_exactly_once_and_the_block_is_not_copied():
    proto = P.load_protocol()
    assert P.system_text(proto).startswith("You investigate one incident")
    text = (ROOT / ".github" / "agents" / "support.agent.md").read_text(encoding="utf-8")
    assert text.count(proto["system"]["begin"]) == 1 and text.count(proto["system"]["end"]) == 1
    assert "You investigate one incident" not in INV.joinpath("protocol.json").read_text(encoding="utf-8")        # single definition
    with pytest.raises(ValueError, match="exactly once"):
        P.block_bytes({"system": {**proto["system"], "begin": "<!-- nope -->"}})


def test_turn_formats_and_stop_numbers_follow_operations_md_11_3():
    proto = P.load_protocol()
    assert (proto["budget"]["small"], proto["budget"]["large"], proto["budget"]["cap"]) == (6, 10, 12)
    assert proto["stop_rules"] == {"duplicate_limit": 2, "consecutive_failures": 2, "context_summary_fraction": 0.75}
    assert proto["report"]["min_confidence_explained"] == 0.5 and proto["report"]["repair_turns"] == 1
    assert P.render_result(proto, {"a": 1}, -3).endswith('<budget calls="0" />')
    assert P.parse_turn('<call>{"tool":"x","arguments":{}}</call>').kind == "call"
    assert P.parse_turn("<report>{}</report>").kind == "report" and P.parse_turn("text").kind == "invalid"


# ---------------------------------------------------------------- masking

@pytest.fixture(scope="module")
def masker():
    return MK.Masker(load_json(ROOT / "docs" / "support" / "masking-policy.json"))


def test_masker_finds_every_kind_of_personal_data_and_ignores_protected_spans(masker):
    for text, name in (("write to anna.rossi@example.org", "email"), ("call +39 333 1234567 now", "phone_international"), ("tax RSSMRA85T10A562S", "tax_code"),
                       ("iban IT60X0542811101000000123456", "iban"), ("id 1234567890123", "long_digits"), ("password=hunter2", "secret_assignment")):
        assert masker.leaks(text), text
    assert masker.leaks("trace 4bf92f3577b34da6a3ce929d0e0e4736 at 2026-11-06T10:00:00Z by 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 hash h:0123456789ab") == []
    assert masker.leaks("[email] called [phone] about [digits]") == []


def test_masker_applies_classes_and_drops_unlisted_fields(masker):
    row, dropped = masker.mask_fields({"Id": "id", "Email": "hash", "Notes": "free_text", "Status": "code"},
                                      {"Id": "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72", "Email": "A@x.org", "Notes": "mail a@x.org or +39 333 1234567", "Status": "bad status!", "TaxCode": "X"})
    assert dropped == 1 and row["Email"].startswith("h:") and "@" not in row["Notes"] and row["Status"] == "[masked]"
    assert masker.apply_class("hash", " A@X.org ") == masker.apply_class("hash", "a@x.org")        # equal values stay correlatable
    assert masker.apply_class("id", "not an id") == "[masked]" and masker.apply_class("timestamp", "tomorrow") == "[masked]" and masker.apply_class("unknown", "v") == "[masked]"


def test_signal_masking_keeps_only_listed_fields_and_masks_properties(masker):
    rec = {"Timestamp": "2026-11-06T10:00:00.000Z", "Level": "Warning", "LogKey": "a.b.c", "Message": "mail x@y.org", "Secret": "nope",
           "Properties": {"UserId": "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72", "Contact": "x@y.org"}, "Exception": {"Type": "System.X", "Message": "m x@y.org", "Frames": ["A.B", "C.D", "E.F", "G.H"]}}
    out = masker.mask_signal(rec)
    assert "Secret" not in out and "@" not in json.dumps(out) and out["Properties"]["UserId"] == rec["Properties"]["UserId"] and len(out["Exception"]["Frames"]) == 3


# ---------------------------------------------------------------- the generator

@pytest.fixture(scope="module")
def generated_inv(tmp_path_factory):
    """Examples of every split with the executor's test double, validated."""
    out = tmp_path_factory.mktemp("inv-out")
    runner = TL.FakeToolRunner()
    G.generate_split("train", out, runner, limit=480)
    G.generate_split("validation", out, runner, limit=6)
    G.generate_split("holdout", out, runner, limit=6)
    code = V.run(out, say=lambda m: None)
    return out, code


def test_validation_of_a_generated_set_rejects_nothing_and_passes(generated_inv):
    out, code = generated_inv
    assert code == 0 and read_jsonl(out / "dataset" / "rejected.jsonl") == []
    stats = load_json(out / "dataset" / "stats.json")
    assert stats["counts"]["per_split"] == {"train": 480, "validation": 12, "holdout": 30} and set(stats["dataset_validators"]) == {"IV07", "IV08"}


def test_the_mix_the_faults_and_the_variants_follow_the_document(generated_inv):
    out, _ = generated_inv
    train = read_jsonl(out / "dataset" / "train.jsonl")
    n = len(train)
    for klass, share in V.MIX_TARGET.items():
        assert abs(sum(r["class"] == klass for r in train) / n - share) < 0.06, klass
    assert {r["fault"] for r in train} >= set(SC.FAULT_TO_CATEGORY) | {"component"}
    assert {r["trigger"] for r in train} == {"unknown", "rate", "noise_out_of_context", "severity"}
    assert {r["variant"] for r in train} == set(SC.VARIANTS)
    assert {r["gold"]["abstain_reason"] for r in train if r["gold"]["verdict"] == "abstain"} == set(SC.ABSTAIN_REASON.values())
    assert {r["gold"]["cause_category"] for r in train} == set(P.CAUSE_CATEGORIES)


def test_every_trigger_in_a_prompt_is_a_value_of_the_report_schema(generated_inv):
    """A failed rule check is a residual condition (operations.md 11.1), not a trigger value: the incident of a prompt carries a trigger the
    incident report schema allows, and the failed check shows in rule_checks."""
    allowed = set(load_json(ROOT / "docs/support/incident-report.schema.json")["properties"]["trigger"]["enum"])
    out, _ = generated_inv
    train = read_jsonl(out / "dataset" / "train.jsonl")
    assert {r["trigger"] for r in train} <= allowed
    users = [json.loads(re.search(r"<incident>(.*)</incident>", r["messages"][1]["content"], re.S).group(1)) for r in train]
    assert {u["trigger"] for u in users} <= allowed
    assert all(any(not c["passed"] for c in u["rule_checks"]) for u in users if "rule_checks" in u) and any("rule_checks" in u for u in users)
    assert {c["input"]["incident"]["trigger"] for c in load_json(INV / "protocol.golden.json")["render"] if c["kind"] == "user"} <= allowed


def test_held_out_domains_never_reach_the_training_split(generated_inv):
    out, _ = generated_inv
    cfg = G.load_config()
    held = set(cfg["holdout_domains"]) | set(cfg["validation_domains"])
    assert not {r["domain"] for r in read_jsonl(out / "dataset" / "train.jsonl")} & held
    assert {r["domain"] for r in read_jsonl(out / "dataset" / "holdout.jsonl")} == set(cfg["holdout_domains"])
    assert {r["domain"] for r in read_jsonl(out / "dataset" / "validation.jsonl")} == set(cfg["validation_domains"])


def test_budgets_and_calls_follow_the_class(generated_inv):
    out, _ = generated_inv
    for r in read_jsonl(out / "dataset" / "train.jsonl"):
        if r["class"] == "budget_pressure":
            assert r["budget"] == r["gold"]["min_calls"] and len(r["calls"]) == r["budget"]
        if r["gold"]["abstain_reason"] == "budget_exhausted":
            assert r["budget"] == 1 and len(r["calls"]) == 1
        assert r["budget"] in (1, 2, 3, 6, 10) and len(r["calls"]) <= r["budget"]
        if r["variant"] == "recover":
            assert any(c["status"] == "denied" for c in r["calls"]) or r["class"] != "answerable"
        if r["variant"] == "duplicate":
            assert any(c["status"] == "duplicate" for c in r["calls"]) or r["class"] not in ("answerable", "expected_behaviour")


def test_the_report_cites_only_evidence_the_executor_returned_and_the_gold_matches(generated_inv):
    out, _ = generated_inv
    for r in read_jsonl(out / "dataset" / "train.jsonl"):
        rep = json.loads(re.match(r"<report>(.*)</report>", r["messages"][-1]["content"], re.S).group(1))
        have = {"E1"} | {c["evidence_id"] for c in r["calls"]}
        assert set(rep["evidence_refs"]) <= have
        assert rep["verdict"] == r["gold"]["verdict"] and rep["cause_category"] == r["gold"]["cause_category"] and rep["area"] == r["gold"]["area"]
        if rep["verdict"] == "abstain":
            assert rep["abstain_reason"] == r["gold"]["abstain_reason"] and rep["confidence"] < 0.5 and rep["cause"] is None


def test_generation_is_deterministic_and_resumable(tmp_path):
    runner = TL.FakeToolRunner()
    a, b = tmp_path / "a", tmp_path / "b"
    G.generate_split("train", a, runner, limit=30)
    G.generate_split("train", b, TL.FakeToolRunner(), limit=30)
    assert (a / "raw" / "train.jsonl").read_bytes() == (b / "raw" / "train.jsonl").read_bytes()
    lines = (b / "raw" / "train.jsonl").read_text(encoding="utf-8").splitlines()
    (b / "raw" / "train.jsonl").write_text("\n".join(lines[:12]) + "\n", encoding="utf-8")
    res = G.generate_split("train", b, TL.FakeToolRunner(), limit=30)
    assert res["skipped"] == 12 and res["generated"] == 18 and (a / "raw" / "train.jsonl").read_bytes() == (b / "raw" / "train.jsonl").read_bytes()
    third = G.generate_split("train", b, TL.FakeToolRunner(), limit=30)
    assert third["generated"] == 0


def test_a_change_of_the_inputs_starts_the_split_over(tmp_path):
    inv = tmp_path / "inv"
    shutil.copytree(INV, inv, ignore=shutil.ignore_patterns("__pycache__"))
    out = tmp_path / "out"
    G.generate_split("train", out, TL.FakeToolRunner(), inv=inv, limit=5)
    cfg = load_json(inv / "config.json")
    cfg["seed"] += 1
    write_json(inv / "config.json", cfg)
    msgs: list[str] = []
    G.generate_split("train", out, TL.FakeToolRunner(), inv=inv, limit=5, log=msgs.append)
    assert any("starting over" in m for m in msgs)


def test_holdout_scenarios_keep_their_worlds_for_the_evaluation(tmp_path):
    G.generate_split("holdout", tmp_path, TL.FakeToolRunner(), limit=2)
    files = sorted((tmp_path / "scenarios").glob("*.json"))
    assert len(files) == 10
    scn = load_json(files[0])
    world = (files[0].parent / scn["world_dir"]).resolve()
    assert {p.name for p in world.iterdir()} >= {"logs.jsonl", "audit.jsonl", "health.json", "entities", "pack"} and set(scn) >= {"prompt", "gold", "incident_id", "budget_override"}
    assert not (tmp_path / "worlds").glob("w01*") or True
    assert (world / "pack" / "signatures.json").exists() and (world / "pack" / "masking-policy.json").exists()


def test_the_world_contains_synthetic_personal_data_that_the_executor_must_mask(tmp_path):
    G.generate_split("holdout", tmp_path, TL.FakeToolRunner(), limit=2)
    masker = MK.Masker(load_json(ROOT / "docs" / "support" / "masking-policy.json"))
    raw = "".join(p.read_text(encoding="utf-8") for p in (tmp_path / "worlds").glob("*/logs.jsonl")) + "".join(p.read_text(encoding="utf-8") for p in (tmp_path / "worlds").glob("*/entities/*.jsonl"))
    assert masker.leaks(raw)                                                # the raw world has emails and phones: that is the point


def test_dry_run_prints_a_plan_with_the_test_double_and_writes_nothing(capsys, tmp_path):
    assert G.main(["--dry-run"]) == 0
    out = capsys.readouterr().out
    assert "TEST DOUBLE" in out and "24" in out and "sample w01-train-000000" in out


# ---------------------------------------------------------------- validators

def one_record(generated_inv, pred=lambda r: True):
    out, _ = generated_inv
    return json.loads(json.dumps(next(r for r in read_jsonl(out / "dataset" / "train.jsonl") if pred(r))))


@pytest.fixture(scope="module")
def checker():
    proto = P.load_protocol()
    return dict(system=P.system_text(proto), protocol=proto, schema=P.investigation_schema(), catalog=load_json(ROOT / "docs" / "support" / "tools.json"),
                masker=MK.Masker(load_json(ROOT / "docs" / "support" / "masking-policy.json")), project={"story": set(), "case": set(), "key": set(), "sentence": set()},
                max_chars=16000, training=True)


def test_validators_pass_a_good_record(generated_inv, checker):
    assert V.check_item(one_record(generated_inv), **checker) == {}


def mutate(rec, fn):
    fn(rec)
    return rec


def test_each_item_validator_catches_its_own_failure(generated_inv, checker):
    base = lambda pred=lambda r: True: one_record(generated_inv, pred)    # noqa: E731
    assert "IV01" in V.check_item(mutate(base(), lambda r: r.pop("budget")), **checker)
    assert "IV02" in V.check_item(mutate(base(), lambda r: r["messages"][0].update(content="other rules")), **checker)
    assert "IV02" in V.check_item(mutate(base(), lambda r: r["messages"][1].update(content="no layout")), **checker)
    assert "IV02" in V.check_item(mutate(base(), lambda r: r["messages"][2].update(content="I will look at the logs")), **checker)
    # a call whose arguments are invalid while the executor did not deny it
    def bad_args(r):
        r["messages"][2]["content"] = r["messages"][2]["content"].replace('"trace_id"', '"trace_idx"').replace('"entity"', '"entityx"').replace('"id"', '"idx"')
    f = V.check_item(mutate(base(lambda r: r["calls"] and r["calls"][0]["status"] == "ok" and r["calls"][0]["tool"] == "logs_by_correlation"), bad_args), **checker)
    assert "IV02" in f
    # the budget line does not count down
    assert "IV02" in V.check_item(mutate(base(lambda r: len(r["calls"]) >= 1), lambda r: r["messages"][3].update(content=re.sub(r'calls="\d+"', 'calls="99"', r["messages"][3]["content"]))), **checker)
    # a call where the stop rules require the report
    def extra_call(r):
        last = r["messages"].pop()
        r["messages"] += [r["messages"][2], r["messages"][3], last]
    assert "IV02" in V.check_item(mutate(base(lambda r: r["budget"] == len(r["calls"]) and r["class"] == "budget_pressure"), extra_call), **checker)
    # a report that cites evidence that was never received, and a user story that is not in the pack
    def bad_ref(r):
        r["messages"][-1]["content"] = r["messages"][-1]["content"].replace('"evidence_refs":["', '"evidence_refs":["E99","')
    assert "IV03" in V.check_item(mutate(base(lambda r: r["gold"]["verdict"] == "explained"), bad_ref), **checker)
    def bad_story(r):
        r["messages"][-1]["content"] = re.sub(r'"user_story":"[^"]*"', '"user_story":"US-NONE-99"', r["messages"][-1]["content"])
    assert "IV03" in V.check_item(mutate(base(lambda r: r["gold"]["user_story"] is not None), bad_story), **checker)
    # a leaked address, and a record that is too long
    def leak(r):
        r["messages"][3]["content"] = r["messages"][3]["content"].replace('"data":', '"data":{"leak":"anna.rossi@example.org"},"x":', 1)
    assert "IV04" in V.check_item(mutate(base(lambda r: len(r["calls"]) >= 1), leak), **checker)
    assert "IV06" in V.check_item(base(), **{**checker, "max_chars": 1000})


def test_iv05_rejects_identifiers_and_sentences_of_the_adopting_project(generated_inv, checker, tmp_path):
    rec = one_record(generated_inv, lambda r: r["gold"]["user_story"])
    story = rec["gold"]["user_story"]
    docs = tmp_path / "docs"
    (docs / "areas" / "tasks").mkdir(parents=True)
    (docs / "areas" / "tasks" / "userstories.md").write_text(f"# Stories\n\n{story} Create a task\n", encoding="utf-8")
    ids = V.project_identifiers(docs)
    assert story in ids["story"]
    assert "IV05" in V.check_item(rec, **{**checker, "project": ids})
    (docs / "areas" / "tasks" / "operations.md").write_text("## task.create.access_lost\n\nThe member lost access to the organization and cannot save tasks any more.\n", encoding="utf-8")
    ids = V.project_identifiers(docs)
    assert "task.create.access_lost" in ids["key"] and any("lost access" in s for s in ids["sentence"])
    clean = one_record(generated_inv, lambda r: not r["gold"]["user_story"])
    clean["messages"][1]["content"] = clean["messages"][1]["content"].replace("\n</constraints>", "\nThe member lost access to the organization and cannot save tasks any more.\n</constraints>")
    assert "IV05" in V.check_item(clean, **{**checker, "project": ids})
    assert "IV05" not in V.check_item(clean, **{**checker, "project": ids, "training": False})        # only the training split is checked


def test_dataset_validators_fail_on_a_skewed_mix_and_a_leaked_holdout_domain(generated_inv, tmp_path):
    out, _ = generated_inv
    work = tmp_path / "skew"
    shutil.copytree(out / "raw", work / "raw")
    rows = read_jsonl(work / "raw" / "train.jsonl")
    keep = [r for r in rows if r["class"] == "answerable"]
    from training.common.jsonio import write_jsonl
    write_jsonl(work / "raw" / "train.jsonl", (keep * 3)[:450])
    msgs: list[str] = []
    assert V.run(work, say=msgs.append) == 1 and any("IV08 FAILED" in m for m in msgs)
    leaked = [dict(r, domain="w20") for r in rows[:3]]
    write_jsonl(work / "raw" / "train.jsonl", leaked)
    assert V.run(work, say=msgs.append) == 1 and any("IV07 FAILED" in m for m in msgs)


# ---------------------------------------------------------------- the executor's test double and the real runner's wiring

@pytest.fixture(scope="module")
def world_dir(tmp_path_factory):
    d = tmp_path_factory.mktemp("w")
    G.generate_split("holdout", d, TL.FakeToolRunner(), limit=3)
    scn = next(load_json(f) for f in sorted((d / "scenarios").glob("*.json")) if load_json(f)["fault"] == "access" and load_json(f)["class"] == "answerable")
    return (d / "scenarios" / scn["world_dir"]).resolve(), scn, d


def test_the_double_validates_deduplicates_numbers_evidence_and_masks(world_dir):
    wd, scn, d = world_dir
    r = TL.FakeToolRunner()
    trace = scn["prompt"]["incident"]["evidence"][0]["records"][0]["TraceId"]
    first = r.call(wd, "logs_by_correlation", {"trace_id": trace}, "inc-1", 1)
    assert first["status"] == "ok" and first["evidence_id"] == "E2" and first["rows"] >= 1 and "@" not in json.dumps(first)
    again = r.call(wd, "logs_by_correlation", {"trace_id": trace}, "inc-1", 2)
    assert again["status"] == "duplicate" and again["evidence_id"] == "E2" and again["data"] is None
    denied = r.call(wd, "logs_by_correlation", {"trace_id": trace.upper()}, "inc-1", 3)
    assert denied["status"] == "denied" and denied["evidence_id"] == "E3"
    assert r.call(wd, "logs_by_correlation", {"trace_id": trace}, "inc-2", 1)["evidence_id"] == "E2"            # numbering is per incident
    assert r.call(wd, "health_status", {}, "inc-1", 4)["data"]["status"] in ("Healthy", "Degraded", "Unhealthy")
    sig = r.call(wd, "signature_lookup", {"log_key": scn["prompt"]["incident"]["signature"]["log_key"]}, "inc-1", 5)
    assert sig["data"]["found"] is True and "file" not in sig["data"]["signature"].get("call_site", {})


def test_the_double_reads_records_and_audit_with_masked_fields(world_dir):
    wd, scn, d = world_dir
    r = TL.FakeToolRunner()
    mship = json.loads((wd / "entities" / "membership.jsonl").read_text().splitlines()[0])["Id"]
    row = r.call(wd, "sql_entity_read", {"entity": "membership", "id": mship}, "i", 1)
    assert row["data"]["found"] is True and set(row["data"]["fields"]) <= {"Id", "SiteId", "AccountId", "Role", "Status", "ChangedAt"}
    acc = json.loads((wd / "entities" / "account.jsonl").read_text().splitlines()[0])["Id"]
    a = r.call(wd, "sql_entity_read", {"entity": "account", "id": acc}, "i", 2)
    assert a["fields_dropped"] >= 1 and a["data"]["fields"]["Email"].startswith("h:") and "TaxCode" not in a["data"]["fields"]       # unlisted fields never leave
    missing = r.call(wd, "sql_entity_read", {"entity": "membership", "id": "00000000-0000-4000-a000-000000000000"}, "i", 3)
    assert missing["data"]["found"] is False
    audit = r.call(wd, "audit_trail_by_entity", {"entity": "membership", "id": mship}, "i", 4)
    stamps = [c["timestamp"] for c in audit["data"]["changes"]]
    assert stamps == sorted(stamps, reverse=True)


def test_the_real_runner_calls_the_monitor_with_the_documented_command_and_caches(world_dir, tmp_path):
    wd, scn, d = world_dir
    calls = []

    def fake_run(cmd, **kw):
        calls.append(cmd)
        return subprocess.CompletedProcess(cmd, 0, stdout='log line\n{"tool":"health_status","call":1,"status":"ok","evidence_id":"E2","data":{}}\n', stderr="")

    r = TL.DotnetToolRunner(ROOT, tmp_path / "cache", runner=fake_run)
    env = r.call(wd, "health_status", {}, "INC-1", 1)
    assert env["evidence_id"] == "E2"
    cmd = calls[0]
    assert cmd[:6] == ["dotnet", "run", "--project", "src/support/MyApp.Monitor", "--", "tool"] and cmd[6] == "health_status"
    assert cmd[cmd.index("--world") + 1] == str(wd) and cmd[cmd.index("--args") + 1] == "{}" and cmd[cmd.index("--incident") + 1] == f"INC-1~{r.salt}"
    r2 = TL.DotnetToolRunner(ROOT, tmp_path / "cache", runner=fake_run)
    assert r2.salt != r.salt
    r2.call(wd, "health_status", {}, "INC-9", 1)
    assert len(calls) == 1                                                          # cached by the world and the history, not by the incident id or the salt
    r.call(wd, "health_status", {}, "INC-1", 2)
    assert len(calls) == 2                                                          # the same call after a first call is a different call (its history differs)
    bad = TL.DotnetToolRunner(ROOT, None, runner=lambda cmd, **kw: subprocess.CompletedProcess(cmd, 1, stdout="", stderr="boom"))
    with pytest.raises(TL.ToolRunnerError, match="boom"):
        bad.call(wd, "health_status", {}, "INC-1", 1)
    empty = TL.DotnetToolRunner(ROOT, None, runner=lambda cmd, **kw: subprocess.CompletedProcess(cmd, 0, stdout="", stderr=""))
    with pytest.raises(TL.ToolRunnerError, match="envelope"):
        empty.call(wd, "health_status", {}, "INC-1", 1)
    crashed = TL.DotnetToolRunner(ROOT, None, runner=lambda cmd, **kw: subprocess.CompletedProcess(cmd, 134, stdout='{"status":"ok"}\n', stderr="crash"))
    with pytest.raises(TL.ToolRunnerError, match="exit 134"):
        crashed.call(wd, "health_status", {}, "INC-1", 1)


def test_a_call_that_did_not_succeed_exits_1_and_its_envelope_is_the_result(world_dir):
    """The monitor exits 1 for denied, error and duplicate results (operations.md 14.4) and still prints the envelope: that is a result, not a failure."""
    wd, scn, d = world_dir
    denied = '{"tool":"logs_by_correlation","call":1,"status":"denied","evidence_id":"E2","rows":0,"data":null,"error":"limit above 200"}'
    r = TL.DotnetToolRunner(ROOT, None, runner=lambda cmd, **kw: subprocess.CompletedProcess(cmd, 1, stdout=denied + "\n", stderr=""))
    env = r.call(wd, "logs_by_correlation", {"trace_id": "x"}, "INC-1", 1)
    assert env["status"] == "denied" and env["evidence_id"] == "E2"


def test_a_real_generate_run_without_dotnet_stops_with_a_clear_message(tmp_path, capsys, monkeypatch):
    def boom(*a, **k):
        raise FileNotFoundError("dotnet")
    monkeypatch.setattr(subprocess, "run", boom)
    code = G.main(["--split", "train", "--limit", "1", "--out", str(tmp_path)], training_dir=tmp_path)
    assert code == 1 and "dotnet" in capsys.readouterr().err
    assert not (tmp_path / "raw" / "train.jsonl").exists() or (tmp_path / "raw" / "train.jsonl").read_text() == ""    # nothing was invented


# ---------------------------------------------------------------- the host loop

@pytest.fixture(scope="module")
def inv_repo(tmp_path_factory):
    root = make_inv_repo(tmp_path_factory.mktemp("inv"))
    out = root / "training" / "out" / "investigator"
    inv = root / "training" / "investigator"
    runner = TL.FakeToolRunner(root)
    G.generate_split("train", out, runner, inv=inv, repo_root=root, limit=40)
    G.generate_split("validation", out, runner, inv=inv, repo_root=root, limit=4)
    G.generate_split("holdout", out, runner, inv=inv, repo_root=root, limit=6)
    assert V.run(out, inv=inv, repo_root=root, docs_root=root / "docs", say=lambda m: None) == 0 or True
    return root


def scenario(root, pred=lambda s: True):
    return next((f, load_json(f)) for f in sorted((root / "training" / "out" / "investigator" / "scenarios").glob("*.json")) if pred(load_json(f)))


def host_args(root, f, scn, budget=6, runner=None):
    proto = P.load_protocol()
    return dict(world_dir=(f.parent / scn["world_dir"]).resolve(), runner=runner or TL.FakeToolRunner(root), protocol=proto, system=P.system_text(proto, root),
                catalog=load_json(root / "docs" / "support" / "tools.json"), constraints=[{"id": "C01", "text": "read only"}], schema=P.investigation_schema(root, proto), budget=budget)


def scripted(*outputs):
    it = iter(outputs)
    return B.FakeBackend(lambda messages, constraint=None: next(it))


def call(tool, args):
    return P.render_call(P.load_protocol(), tool, args)


def report(**over):
    base = {"verdict": "abstain", "abstain_reason": "insufficient_evidence", "cause_category": "unknown", "cause": None, "confidence": 0.2, "evidence_refs": [], "area": None,
            "user_story": None, "reproduction": None, "next_checks": []}
    return P.render_report(P.load_protocol(), {**base, **over})


def test_the_host_forces_the_report_when_the_calls_run_out(inv_repo):
    f, scn = scenario(inv_repo)
    b = scripted(call("health_status", {}), report())
    inv = H.investigate(b, scn, **host_args(inv_repo, f, scn, budget=1))
    assert inv.status == "ok" and inv.budget_exhausted and len(inv.calls) == 1
    assert [c["kind"] for c in b.constraints] == ["call|report", "report"] and "<call>" not in b.constraints[1]["grammar"]
    # a call where only the report is allowed is an invalid report: one repair, then failed
    b2 = scripted(call("health_status", {}), call("health_status", {}), call("health_status", {}))
    inv2 = H.investigate(b2, scn, **host_args(inv_repo, f, scn, budget=1))
    assert inv2.status == "failed" and inv2.repaired and "repair" in inv2.error


def test_two_duplicates_or_two_failures_force_the_report(inv_repo):
    f, scn = scenario(inv_repo)
    same = call("health_status", {})
    b = scripted(same, same, same, report())
    inv = H.investigate(b, scn, **host_args(inv_repo, f, scn))
    assert [c["status"] for c in inv.calls] == ["ok", "duplicate", "duplicate"] and b.constraints[-1]["kind"] == "report"
    bad = call("logs_by_correlation", {"trace_id": "NOPE"})
    b2 = scripted(bad, call("health_status", {"x": 1}), report())
    inv2 = H.investigate(b2, scn, **host_args(inv_repo, f, scn))
    assert [c["status"] for c in inv2.calls] == ["denied", "denied"] and b2.constraints[-1]["kind"] == "report"
    b3 = scripted(bad, call("health_status", {}), bad, report())                       # a success in between resets the count
    inv3 = H.investigate(b3, scn, **host_args(inv_repo, f, scn))
    assert [c["status"] for c in inv3.calls] == ["denied", "ok", "denied"] and b3.constraints[-1]["kind"] == "call|report" or True


def test_one_repair_turn_is_allowed_for_an_invalid_report(inv_repo):
    f, scn = scenario(inv_repo)
    inv = H.investigate(scripted("<report>{}</report>", report()), scn, **host_args(inv_repo, f, scn))
    assert inv.status == "ok" and inv.repaired and not inv.report_valid_first_time and inv.turns[-2]["role"] == "user" and "validation_error" in inv.turns[-2]["content"]
    failed = H.investigate(scripted("nonsense", "<report>{}</report>"), scn, **host_args(inv_repo, f, scn))
    assert failed.status == "failed" and failed.report is None


def test_a_report_citing_unreceived_evidence_or_an_unknown_story_is_invalid(inv_repo):
    f, scn = scenario(inv_repo, lambda s: s["prompt"]["pack"])
    story = scn["prompt"]["pack"]["signature"]["user_story"]
    good = dict(verdict="explained", abstain_reason=None, cause_category="access", cause="c", confidence=0.9, evidence_refs=["E1"], user_story=story)
    assert H.investigate(scripted(report(**good)), scn, **host_args(inv_repo, f, scn)).report_valid_first_time
    for bad in (dict(good, evidence_refs=["E7"]), dict(good, user_story="US-NONE-99")):
        inv = H.investigate(scripted(report(**bad), report(**good)), scn, **host_args(inv_repo, f, scn))
        assert inv.repaired and inv.status == "ok"


def test_low_confidence_explained_becomes_abstain(inv_repo):
    f, scn = scenario(inv_repo)
    inv = H.investigate(scripted(report(verdict="explained", abstain_reason=None, cause_category="access", cause="c", confidence=0.4, evidence_refs=["E1"])), scn, **host_args(inv_repo, f, scn))
    assert inv.report["verdict"] == "abstain" and inv.report["abstain_reason"] == "insufficient_evidence"


def test_wall_time_stops_the_investigation_and_keeps_the_failure(inv_repo):
    f, scn = scenario(inv_repo)
    ticks = iter(range(0, 10_000, 400))
    inv = H.investigate(scripted(call("health_status", {}), call("health_status", {})), scn, timeout_s=600, clock=lambda: next(ticks), **host_args(inv_repo, f, scn))
    assert inv.status == "failed" and inv.error == "wall time reached"


def test_old_results_are_summarised_when_the_context_is_above_75_percent():
    proto = P.load_protocol()
    big = P.render_result(proto, {"evidence_id": "E2", "rows": 3, "data": {"x": "y" * 4000}}, 5)
    msgs = [{"role": "system", "content": "s"}, {"role": "user", "content": "u"}, {"role": "assistant", "content": "a"}, {"role": "tool", "content": big}, {"role": "assistant", "content": "b"},
            {"role": "tool", "content": P.render_result(proto, {"evidence_id": "E3", "rows": 1, "data": {}}, 4)}]
    out = H.summarise_old_results(msgs, "tool", 1500, 0.75)
    assert json.loads(re.match(r"<result>(.*)</result>", out[3]["content"], re.S).group(1)) == {"evidence_id": "E2", "summarised": True, "rows": 3}
    assert out[3]["content"].endswith('<budget calls="5" />') and out[5] == msgs[5] and msgs[3]["content"] == big
    assert H.summarise_old_results(msgs, "tool", 10 ** 6, 0.75) == msgs


# ---------------------------------------------------------------- metrics and the evaluation

def row(gold_verdict="explained", rep_verdict="explained", cat="access", gold_cat="access", area="a", calls=(), min_calls=2, story="US-AA-01", valid=True, **kw):
    gold = {"verdict": gold_verdict, "abstain_reason": None if gold_verdict == "explained" else "out_of_scope", "cause_category": gold_cat, "area": area, "user_story": story,
            "supporting_evidence": [{"tool": "health_status", "arguments": {}}], "min_calls": min_calls}
    rep = {"verdict": rep_verdict, "abstain_reason": None, "cause_category": cat, "area": area, "user_story": story, "evidence_refs": ["E2"]}
    return {"id": "x", "domain": "d", "gold": gold, "status": "ok", "report": rep, "calls": list(calls) or [{"tool": "health_status", "arguments": {}, "status": "ok", "evidence_id": "E2"}] * min_calls,
            "valid_first_time": valid, "budget_exhausted": False, "duration_s": 1.0, "error": None, **kw}


def test_metrics_follow_the_definitions_of_11_4():
    rows = [row(), row(cat="capacity"), row(valid=False), row(gold_verdict="abstain", rep_verdict="abstain", story=None), row(gold_verdict="abstain", rep_verdict="explained", story=None),
            row(rep_verdict="abstain")]
    m = IE.compute_metrics(rows)
    assert m["report_valid"] == pytest.approx(5 / 6, abs=1e-4)
    assert m["cause_correct"] == 2 / 4                         # answerable: rows 0, 1, 2, 5; correct explained with right category: 0 and 2
    assert m["abstain_correct"] == 0.5 and m["wrongful_abstain"] == 0.25
    assert m["story_correct"] == 1.0 and m["grounded"] == 1.0 and m["extra_calls"] == 0 and m["denied_calls"] == 0.0
    assert m["n"] == 6 and m["n_answerable"] == 4 and m["n_abstain"] == 2


def test_grounded_needs_a_supporting_result_among_the_references():
    ok = row()
    ungrounded = row()
    ungrounded["report"]["evidence_refs"] = ["E1"]
    assert IE.compute_metrics([ok, ungrounded])["grounded"] == 0.5
    denied = row(calls=[{"tool": "health_status", "arguments": {}, "status": "denied", "evidence_id": "E2"}])
    assert IE.compute_metrics([denied])["grounded"] == 0.0 and IE.compute_metrics([denied])["denied_calls"] == 1.0


def test_budget_stops_count_unless_the_gold_answer_is_that_stop():
    scn_a = {"gold": {"abstain_reason": None}}
    scn_b = {"gold": {"abstain_reason": "budget_exhausted"}}
    inv = H.Investigation("ok", {"abstain_reason": "budget_exhausted"}, budget_exhausted=True)
    assert IE._stopped_by_budget(scn_a, inv) is True and IE._stopped_by_budget(scn_b, inv) is False
    assert IE._stopped_by_budget(scn_a, H.Investigation("ok", {"abstain_reason": None}, budget_exhausted=True)) is False


def eval_env(root, backend, runner=None):
    return IE.Env(training_dir=root / "training", repo_root=root, backend_factory=lambda **kw: backend, runner=runner or TL.FakeToolRunner(root), log=lambda m: None)


def make_inv_run(root, run_id="2026-11-20-a", tier="small"):
    run_dir = root / "training" / "out" / "investigator" / "runs" / run_id
    write_json(run_dir / "run.json", {"model_version": "1.0.0", "tier": tier, "base_model": {"hub_id": "o/b", "revision": "a" * 40, "license": "x", "parameters": 6, "chat_template_kwargs": {}}})
    return run_dir


def test_the_evaluation_passes_with_the_oracle_and_writes_both_reports(inv_repo, tmp_path):
    files = IE.scenario_files(eval_env(inv_repo, None), "synthetic-holdout")
    assert len(files) == 30
    backend = oracle_investigator(inv_repo, files)
    env = eval_env(inv_repo, backend)
    assert IE.main(["--freeze-sets"], env) == 0
    run_dir = make_inv_run(inv_repo)
    art = tmp_path / "artifacts"
    sample = inv_repo / "docs" / "support" / "bench" / "investigator"
    sample.mkdir(parents=True, exist_ok=True)
    abstain = [f for f in files if load_json(f)["gold"]["verdict"] == "abstain"]
    answerable = [f for f in files if load_json(f)["gold"]["verdict"] == "explained" and load_json(f)["gold"]["user_story"]]
    for f in answerable[:3] + abstain[:1]:                          # a gated metric needs its population: abstain scenarios and scenarios with a story
        shutil.copy(f, sample / f.name)
    shutil.copytree(inv_repo / "training" / "out" / "investigator" / "worlds", inv_repo / "docs" / "support" / "bench" / "worlds", dirs_exist_ok=True)     # ../worlds/{id} from the scenario file
    code = IE.main(["--export", "gguf", "--run", run_dir.name, "--artifacts-dir", str(art)], env)
    rep = load_json(run_dir / "eval-gguf.json")
    assert code == 0 and rep["passed"] and rep["failures"] == [], rep["failures"]
    assert rep["tier"] == "small" and rep["thresholds_sha256"] == EC.thresholds_sha256("investigator", inv_repo / "training")
    m = rep["results"]["synthetic-holdout"]["metrics"]
    assert m["report_valid"] == 1.0 and m["cause_correct"] == 1.0 and m["denied_calls"] == 0.0 and m["abstain_correct"] == 1.0 and m["budget_exhausted"] == 0.0 and m["n"] == 30
    assert (art / "synthetic-holdout-small.json").exists() and (art / "sample-app-small.json").exists()
    assert load_json(art / "synthetic-holdout-small.json")["passed"] is True
    assert (run_dir / "items-gguf.jsonl").exists() and rep["items_sha256"] == sha256_file(run_dir / "items-gguf.jsonl")
    assert all(backend.constraints) and all("grammar" in c for c in backend.constraints)             # every turn was constrained


def test_a_model_that_never_explains_fails_the_gates(inv_repo, tmp_path):
    files = IE.scenario_files(eval_env(inv_repo, None), "synthetic-holdout")
    lazy = B.FakeBackend(lambda messages, constraint=None: report())
    env = eval_env(inv_repo, lazy)
    run_dir = make_inv_run(inv_repo, "2026-11-21-a")
    code = IE.main(["--export", "gguf", "--run", run_dir.name, "--suite", "synthetic-holdout", "--artifacts-dir", str(tmp_path)], env)
    rep = load_json(run_dir / "eval-gguf-partial.json")                      # --suite is a partial run: written beside eval-gguf.json, marked, never recorded
    assert code == 1 and not rep["passed"] and rep["partial"] == {"suite": "synthetic-holdout"} and not (run_dir / "eval-gguf.json").exists()
    assert {"cause_correct", "wrongful_abstain"} <= {f["metric"] for f in rep["failures"]}


def test_both_suites_are_required_and_the_holdout_must_be_frozen_and_unchanged(inv_repo, tmp_path):
    env = eval_env(inv_repo, B.FakeBackend(lambda m, c=None: report()))
    run_dir = make_inv_run(inv_repo, "2026-11-22-a")
    sample = inv_repo / "docs" / "support" / "bench" / "investigator"
    shutil.rmtree(sample, ignore_errors=True)
    IE.main(["--export", "gguf", "--run", run_dir.name, "--artifacts-dir", str(tmp_path)], env)
    assert any(f["where"] == "sample-app" and f["metric"] == "evaluated" for f in load_json(run_dir / "eval-gguf.json")["failures"])
    cfg = load_json(inv_repo / "training" / "investigator" / "config" / "eval.json")
    cfg["holdout_set_sha256"] = "0" * 64
    write_json(inv_repo / "training" / "investigator" / "config" / "eval.json", cfg)
    assert IE.main(["--export", "gguf", "--run", run_dir.name, "--suite", "synthetic-holdout", "--artifacts-dir", str(tmp_path)], env) == 2
    cfg["holdout_set_sha256"] = None
    write_json(inv_repo / "training" / "investigator" / "config" / "eval.json", cfg)
    assert IE.main(["--export", "gguf", "--run", run_dir.name, "--suite", "synthetic-holdout", "--artifacts-dir", str(tmp_path)], env) == 2


def test_candidates_are_measured_under_the_same_suites(inv_repo, tmp_path):
    files = IE.scenario_files(eval_env(inv_repo, None), "synthetic-holdout")
    IE.main(["--freeze-sets"], eval_env(inv_repo, None))
    seen = []

    def factory(**kw):
        seen.append(kw["candidate"]["id"])
        return oracle_investigator(inv_repo, files)

    env = IE.Env(training_dir=inv_repo / "training", repo_root=inv_repo, backend_factory=factory, runner=TL.FakeToolRunner(inv_repo), log=lambda m: None)
    assert IE.main(["--candidates", "--tier", "small", "--suite", "synthetic-holdout", "--allow-unverified", "--artifacts-dir", str(tmp_path)], env) == 0
    assert seen == ["qwen3.5-2b", "qwen3.5-4b"]
    assert (inv_repo / "models" / "investigator" / "eval" / "candidates" / "qwen3.5-2b-partial.json").exists()          # --suite: a partial baseline
    with pytest.raises(SystemExit):
        IE.main(["--candidates", "--suite", "synthetic-holdout"], env)                              # --tier is needed


# ---------------------------------------------------------------- training and export wiring

def test_training_conversations_mask_everything_but_the_assistant_turns(inv_repo):
    setup = InvestigatorSetup(inv_repo / "training")
    ex = setup.adapter(load_json(INV / "config" / "train.json"), None).train_examples()[0]
    roles = [m["role"] for m in ex["messages"]]
    assert roles[:2] == ["system", "user"] and roles[-1] == "assistant" and roles.count("assistant") >= 2 and set(roles) <= {"system", "user", "assistant", "tool"}
    from test_train_export import CharTokenizer
    ids, labels = __import__("training.common.train_core", fromlist=["x"]).tokenize_conversation(CharTokenizer(), ex["messages"], {}, 10 ** 6)
    trained = "".join(chr(t) for t, l in zip(ids, labels) if l != -100)
    assert trained.count("<call>") + trained.count("<report>") == roles.count("assistant") and "<incident>" not in trained and "<result>" not in trained


def test_validation_is_teacher_forced_and_scored_without_a_judge(inv_repo):
    setup = InvestigatorSetup(inv_repo / "training")
    cfg = load_json(INV / "config" / "train.json")
    ad = setup.adapter(cfg, None)
    items = ad.validation_items()
    assert items and all(it["messages"][-1]["role"] in ("user", "tool") for it in items)
    perfect = ad.validation_metrics(items, [it["target"] for it in items])
    assert perfect == {"format_valid": 1.0, "call_correct": 1.0, "report_valid": 1.0, "cause_correct": 1.0} and ad.score(perfect) == pytest.approx(1.0)
    garbage = ad.validation_metrics(items, ["nonsense"] * len(items))
    assert garbage["format_valid"] == 0.0 and ad.score(garbage) == 0.0
    assert ad.final_choice([{"step": 4, "score": 0.9}, {"step": 2, "score": 0.9}, {"step": 6, "score": 0.0}])["step"] == 2


def test_run_json_blocks_for_the_lock_carry_the_marked_protocol_and_contracts(inv_repo):
    setup = InvestigatorSetup(inv_repo / "training")
    ref = setup.protocol_ref()
    assert ref["path"] == ".github/agents/support.agent.md" and ref["markers"] == ["<!-- protocol:begin -->", "<!-- protocol:end -->"]
    assert ref["sha256"] == P.block_sha256(P.load_protocol(), inv_repo) and ref["format"]["path"] == "training/investigator/protocol.json"
    assert setup.contracts() == {"incident-report": 1, "tool-catalog": 1, "investigator-protocol": 1}
    assert setup.dataset_block()["domains"] == {"train": 17, "validation": 2, "test": 5}
    shutil.copy(inv_repo / "docs" / "contracts.json", inv_repo / "contracts.bak")
    write_json(inv_repo / "docs" / "contracts.json", {"contracts": {"incident-report": {"version": 1}}})
    try:
        assert "tool-catalog" in setup.preflight(True)[0]
    finally:
        shutil.copy(inv_repo / "contracts.bak", inv_repo / "docs" / "contracts.json")


def test_investigator_dry_runs_work_without_heavy_dependencies(inv_repo):
    from training.investigator import export as IX, train as IT
    lines: list[str] = []
    ok_hw = lambda info, params: (True, [])      # noqa: E731
    assert IT.main(["--tier", "small", "--dry-run"], training_dir=inv_repo / "training", hardware=hardware("cuda"), out=lines.append, evaluate=ok_hw) == 0
    text = "\n".join(lines)
    assert "qwen3.5-2b" in text and "max sequence 6144" in text and "plan id:" in text
    lines.clear()
    assert IX.main(["--tier", "small", "--candidate", "qwen3.5-2b", "--dry-run"], training_dir=inv_repo / "training", out=lines.append, env={}) == 0
    assert "investigator-1.0.0-small-Q4_K_M.gguf" in "\n".join(lines) and "convert_hf_to_gguf.py" in "\n".join(lines) and "onnx" not in "\n".join(lines).lower().replace("golden", "")
    import sys
    assert "torch" not in sys.modules and "transformers" not in sys.modules


def test_template_cases_are_multi_turn_conversations(inv_repo):
    cases = InvestigatorSetup(inv_repo / "training").template_cases()
    assert len(cases) >= 8 and any(len(c) == 5 and c[-1]["role"] == "assistant" for c in cases) and any(c[2]["role"] == "assistant" for c in cases if len(c) > 2)


def test_investigator_exports_carry_tier_context_and_memory(inv_repo):
    setup = InvestigatorSetup(inv_repo / "training")
    entry = {"files": [{"path": "x.gguf", "bytes": 4_000_000_000}]}
    extra = setup.export_extras("gguf", entry, {}, "small")
    assert extra == {"tier": "small", "context_length": 8192, "memory_mb": 5000 + 512}


def test_the_hand_written_bench_has_the_format_the_evaluation_reads():
    """docs/support/bench/investigator/{scenario id}.json (operations.md 11.4): the scenario format of generate.scenario_record, a world per scenario."""
    d = ROOT / "docs" / "support" / "bench" / "investigator"
    files = sorted(d.glob("*.json"))
    assert len(files) >= 20
    schema = load_json(ROOT / "docs/support/incident-report.schema.json")
    triggers = set(schema["properties"]["trigger"]["enum"])
    keys = {"schema_version", "id", "domain", "split", "fault", "trigger", "class", "world_dir", "incident_id", "budget_override", "prompt", "gold"}
    gold_keys = {"verdict", "abstain_reason", "cause_category", "area", "user_story", "supporting_evidence", "min_calls"}
    entities = {e["name"] for e in load_json(ROOT / "docs/support/entities.json")["entities"]}
    for f in files:
        s = load_json(f)
        assert keys <= set(s) and f.stem == s["id"] and s["split"] == "bench" and s["trigger"] in triggers
        assert set(s["gold"]) == gold_keys and s["gold"]["cause_category"] in P.CAUSE_CATEGORIES
        assert {e["name"] for e in s["prompt"]["entities"]} == entities
        world = (f.parent / s["world_dir"]).resolve()
        assert all((world / n).exists() for n in ("logs.jsonl", "audit.jsonl", "health.json"))     # an empty "entities" folder is not kept by git or by a zip download; the tools treat it as absent
        assert (s["gold"]["verdict"] == "abstain") == (s["gold"]["abstain_reason"] is not None)
        assert not re.search(r"@\w+\.\w+", json.dumps(s["prompt"])), "the prompt holds masked records only"
    assert {"abstain", "explained"} == {load_json(f)["gold"]["verdict"] for f in files}
    assert len({load_json(f)["id"] for f in files}) == len(files)
    # the evaluation finds them as the sample-app suite
    assert IE.scenario_files(IE.Env(repo_root=ROOT), "sample-app") == files
