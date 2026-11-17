"""The help embedding table: builder, reader, Python port, lock and fetch (models.md 7.8, manual.md 6.7). No network, no model: every
encoding goes through ``FakeEncoder``.

Test cases: US-HELP-04 (the assistant's retrieval); the table has no story of its own, it serves the help (manual.md 6.7).
"""
from __future__ import annotations

import base64
import copy
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

from conftest import ROOT
from embed_fixture import (CLUSTERS, CONFIG_OVERRIDES, FAKE_DIM, GLOSSARY_MD, GOLDEN_PATH, LOCALES, MODEL_VERSION, REMOVED, build_golden, build_table,
                           materialise, pack_of, serialise, vocab_json)
from training.assistant import embed as E, embtable, lexical as L
from training.common import fetch as FETCH, lock as LOCK
from training.common.hashing import sha256_bytes, sha256_file

EMB_GOLDEN = ROOT / "src" / "frontend" / "scripts" / "help" / "fixtures" / "embeddings.golden.json"
LEXICAL_GOLDEN = ROOT / "src" / "frontend" / "scripts" / "help" / "fixtures" / "lexical.golden.json"
TOLERANCE = 1e-9
SHA64 = "a" * 64


def fixture_root(tmp_path: Path, langs=("en", "it")) -> tuple[Path, Path, Path]:
    root = tmp_path / "repo"
    root.mkdir(parents=True)
    for lang in langs:
        packs, vocab = materialise(root, lang)
    return root, packs, vocab


def build_run(root: Path, packs: Path, vocab: Path, run_id="2026-11-20-a", **kw) -> int:
    return E.run_build(root, ["en", "it"], packs, run_id, MODEL_VERSION, "cpu", False, False,
                       encoder_factory=lambda cfg, dev: E.FakeEncoder(FAKE_DIM, CLUSTERS["en"] + CLUSTERS["it"]), vocab_path=vocab, out=lambda m: None, **kw)


# ---------------------------------------------------------------- tokenizer words

def test_tokenizer_words_follow_the_space_marker_byte_decoding_and_token_ids():
    vocab = {"Ġtask": 5, "ĠTask": 6, "task": 7, "ĠperchÃ©": 3, "Ġдом": 4, "Ġx": 8, "Ġa1b": 9,
             "Ġthe": 1, "ĠÃ¨": 10, "ĠcafÃ©": 2}
    words = E.tokenizer_words(vocab)
    assert words == ["the", "café", "perché", "task", "task"]   # token id order, lower-cased; both spellings count as accepted tokens; è has one character


def test_tokenizer_words_refuse_non_latin_non_letter_and_malformed_tokens():
    vocab = {"Ġæ°´": 1, "Ġab-c": 2, "Ġàb": 3, "Ġok": 4, "ĠÃ": 5}
    assert E.tokenizer_words(vocab) == ["ok"]


# ---------------------------------------------------------------- sources

def test_sources_read_the_pack_glossary_and_locale_strings_with_placeholders_and_tags_removed(tmp_path):
    root, packs, _ = fixture_root(tmp_path)
    cfg = E.load_config(root)
    src = E.read_sources(root, "en", packs, cfg)
    joined = "\n".join(src.texts)
    assert "Create a task" in joined and "Can I fix a typo in a task?" in joined          # titles and indexed questions
    assert "to-do item" in joined and "deadline" in joined                              # glossary aliases
    assert "welcome" in joined and "Sign out" in joined
    assert "{{" not in joined and "<b>" not in joined
    assert [p for p, _ in src.files] == ["docs/manual/glossary.md", "src/frontend/public/locales/en/common.json"]
    assert src.analyzer == E.analyzer_block("en")


def test_a_word_only_in_bench_files_is_absent_and_no_forbidden_file_is_opened(tmp_path, monkeypatch):
    root, packs, vocab = fixture_root(tmp_path)
    (root / "docs" / "manual" / "bench.md").write_text("# Help bench\n\n## Out of scope\n\n### en\n\n1. Where is the quokka sanctuary?\n\n## Extra\n\n| Question | Language | Story |\n|---|---|---|\n| \"Do narwhals count?\" | en | US-TASK-01 |\n", encoding="utf-8")
    area = root / "docs" / "areas" / "tasks"
    area.mkdir(parents=True)
    (area / "userstories.md").write_text("Questions users ask: How do I feed the okapi?\n", encoding="utf-8")
    opened: list[str] = []
    real_read_bytes, real_read_text = Path.read_bytes, Path.read_text
    monkeypatch.setattr(Path, "read_bytes", lambda self: (opened.append(str(self)), real_read_bytes(self))[1])
    monkeypatch.setattr(Path, "read_text", lambda self, *a, **k: (opened.append(str(self)), real_read_text(self, *a, **k))[1])
    res = build_table(root, "en")
    table = embtable.read_table(res.data, "en")
    for word in ("quokka", "narwhal", "okapi", "sanctuari"):
        assert word not in table.vocab
    assert not [p for p in opened if "/docs/areas/" in p or p.endswith("bench.md")]


def test_the_wordlist_is_a_source_only_with_a_licence(tmp_path):
    root, packs, _ = fixture_root(tmp_path, ("en",))
    cfg = E.load_config(root)
    cfg["sources"]["wordlists"]["en"] = {"path": "training/assistant/config/words.en.txt", "license": ""}
    with pytest.raises(E.BuildError, match="licence"):
        E.check_config(cfg)
    cfg["sources"]["wordlists"]["en"]["license"] = "CC0-1.0"
    E.check_config(cfg)
    (root / "training/assistant/config/words.en.txt").write_text("apple\nbanana\ntable\n", encoding="utf-8")
    (root / E.CONFIG_REL).write_text(json.dumps(cfg), encoding="utf-8")
    src = E.read_sources(root, "en", packs, E.load_config(root))
    assert src.wordlist == ["apple", "banana", "table"]
    assert ("training/assistant/config/words.en.txt", sha256_bytes(b"apple\nbanana\ntable\n")) in src.files


# ---------------------------------------------------------------- stems

def make_sources(texts, wordlist=()):
    return E.Sources("en", {}, list(texts), list(wordlist), [], E.analyzer_block("en"))


def test_stems_group_priority_representative_word_and_ranks():
    src = make_sources(["tasks tasks tasked taskful", "invoice"], wordlist=["invoice", "banana", "cat"])
    plan = E.plan_stems(src, ["the", "cat", "banana", "task", "tasks"], 100)
    by = {s.stem: s for s in plan.stems}
    assert [s.stem for s in plan.stems] == sorted(by)
    assert by["task"].group == "project" and by["task"].word == "tasks"                      # most frequent surface word
    assert by["invoic"].group == "project"                                                   # also in the word list: the project group wins
    assert by["banana"].group == "wordlist" and by["cat"].group == "wordlist"
    assert plan.ranked_total == 5
    assert by["banana"].rank == 3 and by["cat"].rank == 2


def test_representative_word_ties_go_to_the_lower_rank_then_the_code_point():
    src = make_sources(["running runs"])
    plan = E.plan_stems(src, ["runs", "running"], 10)
    assert {s.stem: s.word for s in plan.stems} == {"run": "runs"}                           # one occurrence each: rank 1 beats rank 2
    plan = E.plan_stems(make_sources(["running runs"]), [], 10)
    assert plan.stems[0].word == "running"                                                   # no rank: the code point order


def test_max_stems_never_drops_a_project_stem_and_orders_the_rest_by_rank():
    src = make_sources(["alpha beta gamma"])
    plan = E.plan_stems(src, ["delta", "epsilon", "zeta"], 2)
    assert sorted(s.stem for s in plan.stems) == ["alpha", "beta", "gamma"]
    plan = E.plan_stems(src, ["delta", "epsilon", "zeta"], 4)
    assert sorted(s.stem for s in plan.stems) == ["alpha", "beta", "delta", "gamma"]


def test_a_stem_outside_the_basic_multilingual_plane_is_refused():
    with pytest.raises(E.BuildError, match="Basic Multilingual Plane"):
        E.plan_stems(make_sources(["\U0001d4d0\U0001d4d1"]), [], 10)


# ---------------------------------------------------------------- table

def test_the_table_equals_the_golden_file_byte_for_byte_and_two_builds_agree():
    golden = json.loads(GOLDEN_PATH.read_text(encoding="utf-8"))
    assert serialise(build_golden()) == GOLDEN_PATH.read_text(encoding="utf-8"), "regenerate with `python training/tests/embed_fixture.py --write`"
    for lang in ("en", "it"):
        g = golden["languages"][lang]
        raw = base64.b64decode(g["table_base64"])
        assert sha256_bytes(raw) == g["table_sha256"]
        table = embtable.read_table(raw, lang)
        assert table.vocab == g["decoded"]["vocab"]
        assert np.allclose(table.decoded(), np.array(g["decoded"]["vectors"]), rtol=0, atol=0)


def test_two_builds_of_the_same_inputs_give_identical_bytes(tmp_path):
    a = build_table(tmp_path / "a", "en")
    b = build_table(tmp_path / "b", "en")
    assert a.data == b.data and a.sha256 == b.sha256


def test_layout_meta_weights_and_quantisation_of_a_built_table(tmp_path):
    res = build_table(tmp_path, "en")
    data = res.data
    assert data[:8] == b"HELPEMB\x00"
    dims = int.from_bytes(data[12:14], "little")
    count = int.from_bytes(data[16:20], "little")
    meta_n = int.from_bytes(data[20:24], "little")
    vocab_n = int.from_bytes(data[24:28], "little")
    assert (int.from_bytes(data[8:10], "little"), data[10:12], dims, count) == (1, b"en", CONFIG_OVERRIDES["table"]["dims"], res.stems)
    assert len(data) == 32 + 4 * dims + meta_n + vocab_n + count + count * dims
    table = embtable.read_table(data, "en")
    assert list(table.meta) == embtable.META_KEYS
    assert list(table.meta["model"]) == ["id", "version", "hub_id", "revision"]
    assert table.meta["sources"] == {"project": res.report["stems"]["project"], "wordlist": 0, "tokenizer": res.report["stems"]["tokenizer"]}
    assert table.meta["model"] == {"id": "embeddings", "version": MODEL_VERSION, "hub_id": "Qwen/Qwen3-Embedding-0.6B", "revision": "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3"}
    assert table.meta["config_sha256"] == sha256_file(tmp_path / E.CONFIG_REL)
    assert table.vocab == sorted(table.vocab) and len(set(table.vocab)) == count
    assert table.vectors.dtype == np.int8 and int(table.vectors.min()) >= -127 and int(table.vectors.max()) <= 127
    assert (np.abs(table.vectors).max(axis=0) == 127).all()                      # every column uses the full range
    assert (table.scales > 0).all()
    assert int(table.weights.min()) >= 1 and int(table.weights.max()) <= 255
    # rows are unit vectors before quantisation: the decoded rows are close to unit
    assert np.allclose(np.linalg.norm(table.decoded(), axis=1), 1.0, atol=0.05)
    stem_weight = dict(zip(table.vocab, table.weights.tolist()))
    assert stem_weight["task"] < stem_weight["bill"]                              # a frequent general word weighs less; a project stem without a rank 255


def test_weights_follow_the_zipf_formula():
    assert E.weight_of(1, 1000) == max(1, round(255 * math.log(3) / math.log(1002)))
    assert E.weight_of(1000, 1000) == 255
    assert E.weight_of(1, 1_000_000) >= 1
    assert E.weight_of(0, 0) == 255


def test_pca_sign_rule_and_normalisation():
    rng = np.random.default_rng(7)
    x = rng.normal(size=(40, 12))
    y, keep = E.reduce_rows(x, 4)
    assert keep.all() and y.shape == (40, 4)
    assert np.allclose(np.linalg.norm(y, axis=1), 1.0)
    # the sign rule: flipping the input's sign leaves the projection unchanged up to the row signs, not the component signs
    y2, _ = E.reduce_rows(-x, 4)
    assert np.allclose(np.abs(y), np.abs(y2))
    xc = x - x.mean(axis=0)
    _u, _s, vt = np.linalg.svd(xc, full_matrices=False)
    comps = vt[:4].copy()
    for k in range(4):
        j = int(np.argmax(np.abs(comps[k])))
        comps[k] *= 1 if comps[k, j] >= 0 else -1
    proj = xc @ comps.T
    assert np.allclose(y, proj / np.linalg.norm(proj, axis=1, keepdims=True))
    with pytest.raises(E.BuildError, match="only"):
        E.reduce_rows(x, 13)


def test_a_row_with_a_tiny_norm_is_dropped_and_counted():
    x = np.array([[1.0, 0, 0, 0], [-1.0, 0, 0, 0], [0.0, 0, 0, 0]])           # the third row lies on the mean: nothing is left after projecting
    y, keep = E.reduce_rows(x, 1)
    assert keep.tolist() == [True, True, False] and y.shape == (2, 1)


def test_quantisation_scale_rounding_half_to_even_and_zero_columns():
    scales, q = E.quantise(np.array([[0.5, 0.0, -1.0], [0.25, 0.0, 1.0]]))
    assert scales.dtype == np.float32 and scales[1] == 1.0
    assert scales[0] == np.float32(0.5 / 127) and scales[2] == np.float32(1.0 / 127)
    assert q[:, 2].tolist() == [-127, 127] and q[0, 0] == 127 and q[:, 1].tolist() == [0, 0]
    s2, q2 = E.quantise(np.array([[127.0], [63.5], [62.5], [-63.5]]))          # scale exactly 1.0: numpy rounds half to even
    assert s2[0] == 1.0 and q2[:, 0].tolist() == [127, 64, 62, -64]


def test_tables_are_refused_by_the_reader_with_their_reason(tmp_path):
    res = build_table(tmp_path, "en")
    good = res.data

    def bad(data, match, lang="en"):
        with pytest.raises(embtable.TableError, match=match):
            embtable.read_table(data, lang)

    bad(good[:20], "shorter")
    bad(b"NOTATABLE" + good[9:], "magic")
    bad(good[:8] + (2).to_bytes(2, "little") + good[10:], "format_version")
    bad(good, "language", lang="it")
    bad(good + b"\x00", "bytes")
    bad(good[:-1], "bytes")
    # an unsorted vocabulary: swap two stems of equal length
    table = embtable.read_table(good, "en")
    vocab = list(table.vocab)
    i = next(k for k in range(len(vocab) - 1) if len(vocab[k]) == len(vocab[k + 1]))
    vocab[i], vocab[i + 1] = vocab[i + 1], vocab[i]
    swapped = embtable.encode_table("en", table.scales.astype("<f4"), table.meta, vocab, table.weights, table.vectors)
    bad(swapped, "sorted")
    dup = list(table.vocab)
    dup[1] = dup[0]
    bad(embtable.encode_table("en", table.scales.astype("<f4"), table.meta, dup, table.weights, table.vectors), "unique")
    zero_w = table.weights.copy()
    zero_w[3] = 0
    bad(embtable.encode_table("en", table.scales.astype("<f4"), table.meta, table.vocab, zero_w, table.vectors), "weight")
    bad(embtable.encode_table("en", np.zeros(table.dims, dtype="<f4"), table.meta, table.vocab, table.weights, table.vectors), "scale")


def test_the_builder_fails_on_a_quantisation_error_and_on_a_file_above_max_bytes(tmp_path, monkeypatch):
    root = tmp_path / "a"
    root.mkdir()
    materialise(root, "en")
    real = E.quantise
    monkeypatch.setattr(E, "quantise", lambda y: (lambda s, q: (s, np.zeros_like(q)))(*real(y)))
    with pytest.raises(E.BuildError, match="cosine"):
        build_table(root, "en")
    monkeypatch.undo()
    root2 = tmp_path / "b"
    root2.mkdir()
    materialise(root2, "en")
    cfg = json.loads((root2 / E.CONFIG_REL).read_text())
    cfg["table"]["max_bytes"] = 1000
    (root2 / E.CONFIG_REL).write_text(json.dumps(cfg))
    packs = root2 / "packs"
    src = E.read_sources(root2, "en", packs, E.load_config(root2))
    plan = E.plan_stems(src, E.tokenizer_words(vocab_json("en")), 400)
    with pytest.raises(E.BuildError, match="max_bytes"):
        E.build_language(src, plan, E.FakeEncoder(FAKE_DIM, CLUSTERS["en"]), E.load_config(root2), "0" * 64, "1.0.0", {}, "cpu")


# ---------------------------------------------------------------- configuration

def test_configuration_refusals(tmp_path):
    cfg = json.loads((ROOT / E.CONFIG_REL).read_text())
    with pytest.raises(E.BuildError, match="verified"):
        E.check_config(cfg)
    E.check_config(cfg, allow_unverified=True)
    bad = copy.deepcopy(cfg)
    bad["model"]["verified"] = True
    E.check_config(bad)
    bad["model"]["revision"] = "main"
    with pytest.raises(E.BuildError, match="40"):
        E.check_config(bad)
    bad["model"]["revision"] = "97B0C614BE4D77EE51C0CEF4E5F07C00F9EB65B3"
    with pytest.raises(E.BuildError, match="40"):
        E.check_config(bad)


def test_the_shipped_configuration_pins_the_model_of_the_spec():
    cfg = json.loads((ROOT / E.CONFIG_REL).read_text())
    assert cfg["model"]["hub_id"] == "Qwen/Qwen3-Embedding-0.6B"
    assert cfg["model"]["revision"] == "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3"
    assert cfg["model"]["verified"] is False and cfg["model"]["min_transformers"] == "4.51.0"
    assert cfg["table"]["dims"] == 128 and cfg["table"]["max_bytes"] == 8 * 1024 * 1024
    pin = next(ln for ln in (ROOT / "training" / "requirements.txt").read_text().splitlines() if ln.startswith("transformers=="))
    assert E.version_tuple(pin.split("==")[1].split()[0]) >= E.version_tuple(cfg["model"]["min_transformers"])


def test_a_pack_with_another_analyzer_is_refused(tmp_path):
    root, packs, _ = fixture_root(tmp_path, ("en",))
    pack = json.loads((packs / "en" / "pack.json").read_text())
    pack["analyzer"]["version"] = 1
    (packs / "en" / "pack.json").write_text(json.dumps(pack))
    with pytest.raises(E.BuildError, match="another analyzer"):
        E.read_sources(root, "en", packs, E.load_config(root))
    pack["analyzer"] = E.analyzer_block("en")
    pack["analyzer"]["stopwords_sha256"] = SHA64
    (packs / "en" / "pack.json").write_text(json.dumps(pack))
    with pytest.raises(E.BuildError, match="another analyzer"):
        E.read_sources(root, "en", packs, E.load_config(root))


def test_the_analyzer_version_equals_the_one_of_the_typescript_golden_file():
    assert L.ANALYZER_VERSION == json.loads(LEXICAL_GOLDEN.read_text())["analyzer_version"]


# ---------------------------------------------------------------- imports and device

def test_importing_the_builder_and_a_dry_run_import_no_model_library(tmp_path):
    root, packs, vocab = fixture_root(tmp_path)
    code = ("import sys\nfrom training.assistant import embed as E\n"
            f"rc = E.main(['build', '--dry-run', '--allow-unverified', '--root', {str(root)!r}, '--packs', {str(packs)!r}, '--device', 'cpu'])\n"
            "assert rc == 0, rc\nassert 'torch' not in sys.modules and 'transformers' not in sys.modules, sorted(m for m in sys.modules if m.startswith(('torch', 'transformers')))\nprint('ok')\n")
    res = subprocess.run([sys.executable, "-c", code], cwd=ROOT, capture_output=True, text=True)
    assert res.returncode == 0, res.stderr + res.stdout
    assert "project" in res.stdout and "expected file size" in res.stdout and "estimate" in res.stdout
    assert not (root / "training" / "out").exists()                                           # a dry run writes nothing


def test_device_order_is_cuda_then_mps_then_cpu():
    assert E.choose_device("auto", cuda=True, mps=True) == "cuda"
    assert E.choose_device("auto", cuda=False, mps=True) == "mps"
    assert E.choose_device("auto", cuda=False, mps=False) == "cpu"
    assert E.choose_device("cpu", cuda=True, mps=True) == "cpu"


# ---------------------------------------------------------------- build command, run folder, probe

def test_a_build_writes_the_run_folder(tmp_path):
    root, packs, vocab = fixture_root(tmp_path)
    assert build_run(root, packs, vocab) == 0
    run = root / "training" / "out" / "embeddings" / "runs" / "2026-11-20-a"
    assert sorted(p.name for p in run.iterdir()) == ["build-en.json", "build-it.json", "en.bin", "exports.json", "it.bin", "run.json"]
    info = json.loads((run / "run.json").read_text())
    assert info["status"] == "built" and info["model_version"] == MODEL_VERSION and info["verified"] is True
    assert info["base_model"]["revision"] == "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3"
    assert info["distillation"]["dims"] == CONFIG_OVERRIDES["table"]["dims"] and set(info["distillation"]["languages"]) == {"en", "it"}
    assert info["commands"][0]["command"] == "build" and info["commands"][0]["ok"] is True
    ex = json.loads((run / "exports.json").read_text())["exports"][0]
    assert ex["format"] == "help-embeddings" and ex["variant"] == f"int8-{CONFIG_OVERRIDES['table']['dims']}" and ex["runtime"] == "browser"
    assert [f["path"] for f in ex["files"]] == ["en.bin", "it.bin"]
    assert all(f["sha256"] == sha256_file(run / f["path"]) and f["bytes"] == (run / f["path"]).stat().st_size for f in ex["files"])
    report = json.loads((run / "build-en.json").read_text())
    assert len(report["nearest"]) == 10 and all(len(v) == 8 for v in report["nearest"].values()) and report["lowest_row_cosine"] >= 0.99


def test_a_word_both_languages_need_is_encoded_once(tmp_path):
    root, packs, vocab = fixture_root(tmp_path)
    seen: list[str] = []

    class Counting(E.FakeEncoder):
        def encode(self, words):
            seen.extend(words)
            return super().encode(words)

    E.run_build(root, ["en", "it"], packs, "2026-11-20-a", MODEL_VERSION, "cpu", False, False,
                encoder_factory=lambda cfg, dev: Counting(FAKE_DIM, CLUSTERS["en"]), vocab_path=vocab, out=lambda m: None)
    assert len(seen) == len(set(seen))


def test_a_real_build_refuses_an_unverified_model_and_probe_explains_a_retrieval(tmp_path, capsys):
    root, packs, vocab = fixture_root(tmp_path)
    cfg = json.loads((root / E.CONFIG_REL).read_text())
    cfg["model"]["verified"] = False
    (root / E.CONFIG_REL).write_text(json.dumps(cfg))
    with pytest.raises(E.BuildError, match="verified"):
        build_run(root, packs, vocab)
    assert not (root / "training" / "out").exists()
    cfg["model"]["verified"] = True
    (root / E.CONFIG_REL).write_text(json.dumps(cfg))
    assert build_run(root, packs, vocab) == 0
    capsys.readouterr()
    assert E.probe(root, "2026-11-20-a", "en", "rename a job", packs) == 0
    text = capsys.readouterr().out
    assert "query stems" in text and "top 5 lexical" in text and "top 5 by cosine" in text and "top 5 fused" in text and "decision:" in text
    assert E.probe(root, "2026-11-20-a", "en", "zzzz qqqq", packs) == 0
    assert "NOT IN THE TABLE" in capsys.readouterr().out
    assert E.main(["probe", "--run", "2026-11-20-a", "--lang", "en", "--question", "rename a job", "--root", str(root), "--packs", str(packs)]) == 0


# ---------------------------------------------------------------- the port against the TypeScript golden file

EMB = json.loads(EMB_GOLDEN.read_text(encoding="utf-8"))
TABLES = json.loads(GOLDEN_PATH.read_text(encoding="utf-8"))
CASES = [pytest.param(lang, i, id=f"{lang}-{i:02d}") for lang, g in EMB["languages"].items() for i in range(len(g["cases"]))]


def _scorer(lang):
    g = EMB["languages"][lang]
    table = embtable.read_table(base64.b64decode(TABLES["languages"][lang]["table_base64"]), lang)
    index = L.Index(g["sections"], lang, g["glossary"])
    return g, table, index, L.EmbeddingScorer(table, index, EMB["retrieval"]["bm25"]["field_weights"])


def test_the_embedding_golden_file_belongs_to_the_table_golden_file_and_covers_the_rules():
    assert EMB["retrieval"] == json.loads((ROOT / "training/assistant/retrieval.json").read_text())
    for lang, g in EMB["languages"].items():
        assert g["table_sha256"] == TABLES["languages"][lang]["table_sha256"]
    tags = {t for g in EMB["languages"].values() for c in g["cases"] for t in c["tags"]}
    assert {"no_table_stem", "no_lexical_hit", "fused_tie", "depth_cut", "diversify_after_fusion", "semantic_accept", "lexical_accept", "veto", "veto_off",
            "section_without_vector"} <= tags
    assert len(EMB["languages"]["en"]["cases"]) >= 12


@pytest.mark.parametrize("lang", ["en", "it"])
def test_port_section_vectors(lang):
    g, _table, _index, scorer = _scorer(lang)
    assert len(scorer.section_vectors) == len(g["section_vectors"])
    for got, want in zip(scorer.section_vectors, g["section_vectors"]):
        if want is None:
            assert got is None
        else:
            assert np.max(np.abs(got - np.array(want))) <= TOLERANCE


@pytest.mark.parametrize(("lang", "i"), CASES)
def test_port_query_vector_lists_fusion_and_decision(lang, i):
    g, _table, index, scorer = _scorer(lang)
    case = g["cases"][i]
    retrieval = case["retrieval"]
    stems = sorted(L.stem_set(case["query"], lang))
    assert stems == case["stems"]
    assert [s for s in stems if s in scorer.table.index] == case["stems_in_table"]
    q = scorer.query_vector(stems)
    if case["query_vector"] is None:
        assert q is None
    else:
        assert np.max(np.abs(q - np.array(case["query_vector"]))) <= TOLERANCE
    depth = retrieval["fusion"]["depth"]
    emb = scorer.rank(stems, depth)
    assert [e[0] for e in emb] == [e["id"] for e in case["embedding_list"]]
    assert all(abs(e[2] - w["cosine"]) <= TOLERANCE for e, w in zip(emb, case["embedding_list"]))
    assert len(scorer.rank(stems, 1000)) == case["embedding_total"]
    lex = index.lexical_ranked(case["query"], retrieval)
    assert len(lex) == case["lexical_total"]
    assert [x[1] for x in lex[:depth]] == [x["id"] for x in case["lexical_list"]]
    assert all(abs(-x[0] - w["score"]) <= TOLERANCE for x, w in zip(lex[:depth], case["lexical_list"]))
    hits = index.search(case["query"], retrieval, scorer)
    assert [h[0]["id"] for h in hits] == [h["id"] for h in case["fused"]]
    assert all(abs(h[1] - w["score"]) <= TOLERANCE for h, w in zip(hits, case["fused"]))
    d = index.decide(case["query"], retrieval, scorer, hits)
    want = case["decision"]
    assert d["in_scope"] == want["in_scope"] and d["top_id"] == want["top_id"]
    assert abs(d["coverage"] - want["coverage"]) <= TOLERANCE
    if want["cosine"] is None:
        assert d["cosine"] is None
    else:
        assert abs(d["cosine"] - want["cosine"]) <= TOLERANCE


def test_port_without_a_table_is_the_lexical_ranking():
    g = EMB["languages"]["en"]
    index = L.Index(g["sections"], "en", g["glossary"])
    retrieval = EMB["retrieval"]
    for q in ("add a new task", "invitation expires", "weather forecast"):
        assert index.search(q, retrieval) == index.rank(q, retrieval)
        d = index.decide(q, retrieval)
        assert d["cosine"] is None


def test_effective_retrieval_lays_the_pack_params_over_the_defaults():
    base = json.loads((ROOT / "training/assistant/retrieval.json").read_text())
    eff = L.effective_retrieval(base, {"method": "rrf", "depth": 5, "rrf_k": 10, "embedding_weight": 2, "min_cosine": 0.3, "min_cosine_lexical": 0.1})
    assert eff["fusion"] == {"method": "rrf", "depth": 30, "rrf_k": 10, "embedding_weight": 2}
    assert eff["out_of_scope"]["min_cosine"] == 0.3 and eff["out_of_scope"]["min_cosine_lexical"] == 0.1
    assert L.effective_retrieval(base, {"method": "none"})["fusion"]["method"] == "none"
    assert base["fusion"]["method"] == "rrf"                                                  # the input is untouched


# ---------------------------------------------------------------- lock and fetch

NOW = __import__("datetime").datetime(2026, 11, 20, 18, 0, 0, tzinfo=__import__("datetime").timezone.utc)


def lock_root(tmp_path: Path, calibrated=True) -> Path:
    """A repository with a finished embeddings run, its calibration report and the files L09 compares with."""
    root, packs, vocab = fixture_root(tmp_path)
    (root / "docs").mkdir(exist_ok=True)
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": {k: {"version": v} for k, v in
                                                                            {"manual-page": 1, "help-pack": 2, "assistant-protocol": 1, "assistant-retrieval": 3}.items()}}))
    assert build_run(root, packs, vocab) == 0
    run = root / "training" / "out" / "embeddings" / "runs" / "2026-11-20-a"
    if calibrated:
        params = {"method": "rrf", "depth": 30, "rrf_k": 60, "embedding_weight": 1.0, "min_cosine": 0.55, "min_cosine_lexical": -1.0}
        (run / "calibration.json").write_text(json.dumps({
            "schema_version": 1, "kind": "help-embeddings-calibration", "config_sha256": sha256_file(root / E.CONFIG_REL), "retrieval_sha256": SHA64,
            "languages": {lang: {"table_sha256": sha256_file(run / f"{lang}.bin"), "pack_inputs_sha256": SHA64, "calibrated": True,
                                 "items": {"tuning": 1, "held_out": 1}, "params": params, "target_missed": False, "metrics": {}, "notes": []} for lang in ("en", "it")}}))
    return root


def record(root: Path, **kw):
    return LOCK.record("embeddings", "2026-11-20-a", root, artifact_url="https://store.example/embeddings/1.0.0", now=NOW, **kw)


def test_record_writes_the_embeddings_entry_in_the_order_of_the_document_and_copies_the_report(tmp_path):
    root = lock_root(tmp_path)
    entry = record(root)
    data = json.loads((root / "models" / "model.lock").read_text())
    assert data["schema_version"] == 2
    e = data["models"]["embeddings"]
    assert list(e) == ["model_version", "status", "base_model", "distillation", "contracts", "exports", "calibration", "card", "recorded_at"]
    assert e["status"] == "distilled" and e["card"] is None and e["recorded_at"] == "2026-11-20T18:00:00Z"
    assert list(e["base_model"]) == ["hub_id", "revision", "license", "parameters", "dimensions"]
    assert e["contracts"] == {"help-pack": 2, "assistant-retrieval": 3}
    x = e["exports"][0]
    assert x["url"] == "https://store.example/embeddings/1.0.0/" and [f["path"] for f in x["files"]] == ["en.bin", "it.bin"]
    report = root / "models" / "embeddings" / "calibration" / "1.0.0.json"
    assert report.is_file() and e["calibration"]["report_sha256"] == sha256_file(report)
    assert set(e["calibration"]["params"]) == {"en", "it"} and entry == e
    assert LOCK.verify(root, "embeddings") == []


def test_record_refuses_without_writing_anything(tmp_path):
    root = lock_root(tmp_path / "a", calibrated=False)
    with pytest.raises(LOCK.LockError, match="calibration.json"):
        record(root)
    assert not (root / "models" / "model.lock").exists()

    root = lock_root(tmp_path / "b")
    run = root / "training" / "out" / "embeddings" / "runs" / "2026-11-20-a"
    cal = json.loads((run / "calibration.json").read_text())
    for mutate, match in ((lambda c: c["languages"]["en"].update(table_sha256=SHA64), "another table"),
                          (lambda c: c["languages"].pop("it"), "no language it"),
                          (lambda c: c["languages"]["it"].pop("params"), "no params")):
        c = copy.deepcopy(cal)
        mutate(c)
        (run / "calibration.json").write_text(json.dumps(c))
        with pytest.raises(LOCK.LockError, match=match):
            record(root)
        assert not (root / "models" / "model.lock").exists() and not (root / "models" / "embeddings").exists()
    (run / "calibration.json").write_text(json.dumps(cal))
    with pytest.raises(LOCK.LockError, match="artifact store URL"):
        LOCK.record("embeddings", "2026-11-20-a", root)
    info = json.loads((run / "run.json").read_text())
    info["status"] = "failed"
    (run / "run.json").write_text(json.dumps(info))
    with pytest.raises(LOCK.LockError, match="status built"):
        record(root)
    info.update(status="built", verified=False)
    (run / "run.json").write_text(json.dumps(info))
    with pytest.raises(LOCK.LockError, match="verified"):
        record(root)


def test_a_version_1_lock_is_read_and_written_as_version_2(tmp_path):
    root = lock_root(tmp_path)
    (root / "models").mkdir(exist_ok=True)
    (root / "models" / "model.lock").write_text(json.dumps({"schema_version": 1, "models": {}}))
    assert LOCK.read_lock(root / "models" / "model.lock")["models"] == {}
    record(root)
    assert json.loads((root / "models" / "model.lock").read_text())["schema_version"] == 2
    (root / "models" / "model.lock").write_text(json.dumps({"schema_version": 3, "models": {}}))
    assert [f.id for f in LOCK.verify(root)] == ["L01"]


def _edit_entry(root: Path, fn) -> None:
    p = root / "models" / "model.lock"
    d = json.loads(p.read_text())
    fn(d["models"]["embeddings"])
    p.write_text(json.dumps(d))


def _ids(root: Path) -> list[str]:
    return sorted({f.id for f in LOCK.verify(root, "embeddings")})


def test_verify_reports_each_case_of_l09(tmp_path):
    root = lock_root(tmp_path)
    record(root)
    assert _ids(root) == []
    lock_text = (root / "models" / "model.lock").read_text()

    def reset():
        (root / "models" / "model.lock").write_text(lock_text)

    _edit_entry(root, lambda e: e["base_model"].update(revision="main"))
    assert _ids(root) == ["L09"]
    reset()
    _edit_entry(root, lambda e: e["base_model"].update(license=""))
    assert _ids(root) == ["L09"]
    reset()
    _edit_entry(root, lambda e: e["distillation"]["languages"]["en"]["analyzer"].update(version=1))
    assert _ids(root) == ["L09"]
    reset()
    _edit_entry(root, lambda e: e["calibration"].update(report_sha256=SHA64))
    assert _ids(root) == ["L09"]
    reset()
    _edit_entry(root, lambda e: e["calibration"]["params"].pop("it"))
    assert _ids(root) == ["L09"]
    reset()
    (root / "models" / "embeddings" / "calibration" / "1.0.0.json").unlink()
    assert _ids(root) == ["L09"]
    shutil.copy(root / "training/out/embeddings/runs/2026-11-20-a/calibration.json", root / "models" / "embeddings" / "calibration" / "1.0.0.json")
    assert _ids(root) == []
    cfg = json.loads((root / E.CONFIG_REL).read_text())
    cfg["build"]["min_vocab_coverage"] = 0.5
    (root / E.CONFIG_REL).write_text(json.dumps(cfg))
    assert _ids(root) == ["L09"]                                                              # embed.json changed: rebuild
    # a stopword list that is not the one the tables were built with
    cfg["build"]["min_vocab_coverage"] = 0.9
    (root / E.CONFIG_REL).write_text(json.dumps(cfg))
    sw = root / "training" / "assistant" / "config" / "stopwords.en.txt"
    sw.write_text("zzz\n")
    assert _ids(root) == ["L09"]


def test_verify_checks_contracts_weights_and_unlocked_packs(tmp_path):
    root = lock_root(tmp_path)
    record(root)
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": {"help-pack": {"version": 3}, "assistant-retrieval": {"version": 3}}}))
    assert _ids(root) == ["L02"]
    (root / "docs" / "contracts.json").write_text(json.dumps({"contracts": {"help-pack": {"version": 2}, "assistant-retrieval": {"version": 3}}}))
    w = root / "models" / "embeddings" / "weights"
    w.mkdir(parents=True)
    (w / "en.bin").write_bytes(b"damaged")
    assert _ids(root) == ["L08"]
    (w / "en.bin").unlink()
    pack = root / "src" / "frontend" / "public" / "help" / "en"
    pack.mkdir(parents=True)
    (pack / "pack.json").write_text(json.dumps({"assistant": {"compatible": True}, "embeddings": {"locked": False}}))
    assert _ids(root) == ["L07"]


def test_card_refuses_the_embeddings_entry(tmp_path):
    from training.common import card as CARD
    root = lock_root(tmp_path)
    record(root)
    with pytest.raises(LOCK.LockError, match="no card"):
        CARD.refresh("embeddings", root)


def test_fetch_downloads_the_tables_and_checks_them_by_sha256(tmp_path):
    root = lock_root(tmp_path)
    record(root)
    run = root / "training" / "out" / "embeddings" / "runs" / "2026-11-20-a"
    served = {f"https://store.example/embeddings/1.0.0/{lang}.bin": (run / f"{lang}.bin").read_bytes() for lang in ("en", "it")}
    messages: list[str] = []

    def downloader(url, dest):
        dest.write_bytes(served[url])

    assert FETCH.fetch("embeddings", root, downloader=downloader, out=messages.append) == 0
    dest = root / "models" / "embeddings" / "weights"
    assert sorted(p.name for p in dest.iterdir()) == ["en.bin", "it.bin"]
    assert (dest / "en.bin").read_bytes() == served["https://store.example/embeddings/1.0.0/en.bin"]
    assert LOCK.verify(root, "embeddings") == []                                               # L08 passes on the fetched files
    assert FETCH.fetch("embeddings", root, downloader=downloader, out=messages.append) == 0
    assert any("present" in m for m in messages)
    (dest / "it.bin").unlink()
    served["https://store.example/embeddings/1.0.0/it.bin"] = b"tampered"
    assert FETCH.fetch("embeddings", root, downloader=downloader, out=messages.append) == 1
    assert not (dest / "it.bin").exists()
    with pytest.raises(LOCK.LockError):
        FETCH.fetch("investigator", root, downloader=downloader, out=messages.append)
