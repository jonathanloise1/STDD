"""The renderer and parser against the golden cases and the rules of models.md 3.2 to 3.7."""
import json
import re
from pathlib import Path

import pytest

from training.assistant import golden as golden_mod, protocol as P

ROOT = Path(__file__).resolve().parents[2]


def test_golden_file_is_current():
    assert golden_mod.main(["--check"]) == 0


def test_golden_has_enough_cases_and_every_outcome(golden):
    assert len(golden["render"]) >= 12
    assert {c["expected"]["outcome"] for c in golden["parse"]} == {"refused", "answered", "invalid"}
    ids = " ".join(c["id"] for c in golden["render"])
    for needle in ("accents", "braces", "crlf", "over-long"):
        assert needle in ids
    assert {len(c["sections"]) for c in golden["render"]} >= {1, 2, 3, 4}


def test_golden_records_the_protocol_hash(golden):
    assert golden["protocol_sha256"] == P.protocol_sha256()


def test_render_matches_golden(golden, protocol):
    for c in golden["render"]:
        system, user = P.render_messages(protocol, c["lang"], c["app_name"], c["sections"], c["question"])
        assert system == c["expected"]["system"], c["id"]
        assert user == c["expected"]["user"], c["id"]
        assert P.normalise_question(protocol, c["question"]) == c["expected"]["question"], c["id"]


def test_parse_matches_golden(golden, protocol):
    for c in golden["parse"]:
        r = P.parse_answer(protocol, c["lang"], c["output"], c["k"])
        assert (r.outcome, r.cited) == (c["expected"]["outcome"], c["expected"]["cited"]), c["id"]


def test_select_matches_golden(golden, protocol):
    for c in golden["select"]:
        chosen = P.select_sections(protocol, c["ranked"], c["max_sections"])
        assert [s["id"] for s in chosen] == c["expected"]["selected_ids"], c["id"]


def test_system_prompt_is_the_text_of_the_document(protocol):
    system, _ = P.render_messages(protocol, "en", "TaskFlow", [{"title": "t", "text": "x"}], "q")
    assert system.startswith("You answer questions from users of TaskFlow. Use only the numbered manual sections in the user message.\nRules:\n1. ")
    assert "3. If no section states the answer, reply with exactly this sentence and nothing else: I can't find this in the manual.\n4. Answer in English." in system
    assert system.endswith("for example: Source: [2]")
    it, _ = P.render_messages(protocol, "it", "X", [{"title": "t", "text": "x"}], "q")
    assert "Non trovo questa informazione nel manuale." in it and "Answer in Italian." in it


def test_protocol_json_equals_the_document():
    doc = ROOT / "docs" / "guidelines" / "models.md"
    if not doc.exists():
        pytest.skip("models.md not available")
    text = doc.read_text(encoding="utf-8")
    block = re.search(r"### 3\.1 Single definition.*?```json\n(.*?)\n```", text, re.S).group(1)
    assert json.loads(block) == P.load_protocol()


def test_protocol_values_are_the_defaults_of_the_document(protocol):
    assert protocol["context"] == {"max_sections": 4, "max_section_chars": 1200, "max_context_chars": 4000, "max_question_chars": 300, "scan_limit": 8}
    assert protocol["answer"] == {"source_prefix": "Source: ", "max_body_chars": 600, "max_body_lines": 7}
    assert protocol["decoding"] == {"temperature": 0, "max_new_tokens": 200, "reasoning": "off"}


# ---- normalisation and substitution

def test_question_normalisation(protocol):
    n = lambda q: P.normalise_question(protocol, q)  # noqa: E731
    assert n("  how\tdo\r\n  i   ") == "how do i"
    assert n("Perché?") == "Perché?"                 # NFD to NFC
    assert n("a  b　c") == "a b c"
    assert len(n("x " * 400)) == 300
    assert len(n("a" * 299 + "\U0001F600\U0001F600")) == 300     # code points, not UTF-16 units
    assert n("") == ""


def test_substitution_is_single_pass(protocol):
    _, user = P.render_messages(protocol, "en", "A", [{"title": "T {question}", "text": "see {sections}"}], "what is {question}?")
    assert "[1] T {question}\nsee {sections}" in user
    assert user.endswith("Question: what is {question}?")


def test_unknown_placeholders_stay_literal():
    assert P.substitute("{unknown} {n}", {"n": "1"}) == "{unknown} 1"


def test_crlf_in_sections_is_normalised(protocol):
    _, user = P.render_messages(protocol, "en", "A", [{"title": "T", "text": "a\r\nb"}], "q")
    assert "\r" not in user


# ---- context selection

def sec(i, n):
    return {"id": f"s{i}", "title": "t", "text": "a" * n}


def test_selection_skips_sections_over_budget_and_never_cuts(protocol):
    ranked = [sec(1, 1200), sec(2, 1200), sec(3, 1200), sec(4, 1000), sec(5, 300)]
    chosen = P.select_sections(protocol, ranked)
    assert [s["id"] for s in chosen] == ["s1", "s2", "s3", "s5"]
    assert all(len(s["text"]) in (1200, 300) for s in chosen)
    assert P.context_chars(chosen) == 3900


def test_selection_respects_scan_limit_and_lower_limit(protocol):
    assert [s["id"] for s in P.select_sections(protocol, [sec(i, 10) for i in range(1, 10)])] == ["s1", "s2", "s3", "s4"]
    assert len(P.select_sections(protocol, [sec(i, 10) for i in range(1, 10)], max_sections=2)) == 2
    assert len(P.select_sections(protocol, [sec(i, 10) for i in range(1, 10)], max_sections=9)) == 4     # can lower, never raise
    ranked = [sec(i, 1200) for i in range(1, 9)] + [sec(9, 1)]
    assert [s["id"] for s in P.select_sections(protocol, ranked)] == ["s1", "s2", "s3"]               # s9 is beyond the scan limit


# ---- parsing

def parse(protocol, out, k=2, lang="en"):
    return P.parse_answer(protocol, lang, out, k)


def test_parse_outcomes(protocol):
    assert parse(protocol, "I can't find this in the manual.").outcome == "refused"
    assert parse(protocol, "Do it.\nSource: [1]").cited == [1]
    assert parse(protocol, "Do it.\nSource: [1], [2]").cited == [1, 2]
    assert parse(protocol, "Do it.\nSource: [3]").outcome == "invalid"                   # beyond K
    assert parse(protocol, "Do it.\nSource: [2], [1]").outcome == "invalid"              # not ascending
    assert parse(protocol, "Do it.\nSource: [1], [1]").outcome == "invalid"              # not distinct
    assert parse(protocol, "I can't find this in the manual. Sorry.").outcome == "invalid"
    assert parse(protocol, "I can't find this in the manual.\nSource: [1]").outcome == "invalid"
    assert parse(protocol, "Non trovo questa informazione nel manuale.", lang="it").outcome == "refused"
    assert parse(protocol, "Non trovo questa informazione nel manuale.", lang="en").outcome == "invalid"


def test_parse_body_limits(protocol):
    assert parse(protocol, "x" * 600 + "\nSource: [1]").outcome == "answered"
    assert parse(protocol, "x" * 601 + "\nSource: [1]").outcome == "invalid"
    assert parse(protocol, "\n".join("l" for _ in range(7)) + "\nSource: [1]").outcome == "answered"
    assert parse(protocol, "\n".join("l" for _ in range(8)) + "\nSource: [1]").outcome == "invalid"
    assert parse(protocol, "Source: [1]\nDo it.\nSource: [1]").outcome == "invalid"
    assert parse(protocol, "Do it.\r\nSource: [1]\r\n  ").outcome == "answered"


def test_format_answer_round_trips(protocol):
    text = P.format_answer(protocol, "Do it.", [1, 3])
    assert text == "Do it.\nSource: [1], [3]"
    assert parse(protocol, text, k=3).cited == [1, 3]
