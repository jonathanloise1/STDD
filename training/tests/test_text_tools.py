"""Analyzer, overlap, retrieval, sections and the bench-style question split."""
import hashlib

import pytest

from training.assistant import lexical as L, sections as S


def test_stopword_lists_are_sorted_and_loaded():
    for lang in ("en", "it"):
        words = L.stopwords_path(lang).read_text(encoding="utf-8").split()
        assert words == sorted(words) and len(words) > 80
    assert "the" in L.stopwords("en") and "della" in L.stopwords("it")


def test_analyzer_steps():
    assert L.tokens("Il L'elenco, e-mail: 3 x", "it") == ["elenco", "mail"]            # apostrophe splits, one-character tokens dropped
    assert L.tokens("Where IS the Button", "en") == ["button"]
    assert L.analyze("Creating tasks", "en") == L.analyze("create task", "en")
    assert L.analyze("perché è già", "it") == []                                          # accented stopwords
    assert L.strip_diacritics("città") == "citta"
    assert L.analyze("Università", "it") == L.analyze("universita", "it")


def test_overlap_and_buckets():
    sec = {"title": "Create a task - Steps", "text": "1. Open the Tasks page.\n2. Click \"New Task\"."}
    assert L.overlap("create task", [sec], "en") == 1.0
    assert L.overlap("banana smoothie recipe", [sec], "en") == 0.0
    assert L.overlap("create banana", [sec], "en") == 0.5
    assert L.overlap("the of", [sec], "en") == 0.0                                       # no content stems
    other = {"title": "Banana", "text": "smoothie"}
    assert L.overlap("create banana", [sec, other], "en") == 1.0                        # union of sections
    assert [L.overlap_bucket(x) for x in (0.0, 0.15, 0.1501, 0.35, 0.36, 1.0)] == ["distant", "distant", "mid", "mid", "near", "near"]


def test_consecutive_copy_and_jaccard():
    text = "Open the Tasks page and click the New Task button to start"
    assert L.consecutive_stem_copy("please open tasks page click new", text, "en") is True
    assert L.consecutive_stem_copy("open page tasks click", text, "en") is False
    assert L.jaccard({"a", "b"}, {"a", "b"}) == 1.0 and L.jaccard({"a"}, {"b"}) == 0.0


def test_stopword_share_ignores_quotes_and_shared_words():
    assert L.stopword_share("how do i create a task in the page", "en", "it") > 0.4
    assert L.stopword_share("come posso creare un compito nella pagina", "en", "it") < 0.1
    assert L.stopword_share('"how do i create the task" fai clic sul pulsante', "en", "it") < 0.1


def secs():
    mk = lambda i, title, text, q=(): {"id": f"s{i}", "title": title, "text": text, "questions": list(q)}   # noqa: E731
    return [mk(1, "Create a task - Steps", "Open the Tasks page. Click New Task."),
            mk(2, "Delete a task - Steps", "Open the Tasks page. Click Delete."),
            mk(3, "Billing", "Invoices are monthly.", ["where do I find my receipts"]),
            mk(4, "Other", "Nothing relevant here")]


RETRIEVAL = {"bm25": {"k1": 1.2, "b": 0.75, "field_weights": {"title": 2.0, "text": 1.0, "questions": 1.5}}, "candidates": 2}


def test_bm25_ranking():
    idx = L.Index(secs(), "en")
    ranked = idx.rank("create new task", RETRIEVAL)
    assert [s["id"] for s, _ in ranked][0] == "s1" and len(ranked) == 2                  # candidates cap
    assert idx.rank("xyzzy", RETRIEVAL) == []                                            # score 0 is not returned
    assert [s["id"] for s, _ in idx.rank("receipts", RETRIEVAL)] == ["s3"]               # the indexed question bridges vocabulary
    assert [s["id"] for s, _ in idx.rank("create new task", RETRIEVAL, exclude={"s1"})][0] != "s1"


def test_bm25_ties_break_by_section_id():
    same = [{"id": "b", "title": "x", "text": "alpha", "questions": []}, {"id": "a", "title": "x", "text": "alpha", "questions": []}]
    assert [s["id"] for s, _ in L.Index(same, "en").rank("alpha", RETRIEVAL)] == ["a", "b"]


def test_idf_formula():
    idx = L.Index(secs(), "en")
    import math
    assert idx.idf("zzz") == pytest.approx(math.log(1 + (4 + 0.5) / 0.5))
    assert idx.coverage("task", 0) == pytest.approx(1.0)


def test_top_tfidf_stems_are_deterministic_and_limited():
    s = secs()
    a = L.top_tfidf_stems(s[0], s, "en", 3)
    assert a == L.top_tfidf_stems(s[0], s, "en", 3) and len(a) == 3


# ---- sections (manual.md 6.1)

PAGE = """---
schema_version: 1
---
<!-- draft: written by manual:generate, remove after review -->
# Create a task

Use this page to add **work** to the list.

## Steps

1. Open the Tasks page.
2. Click "New Task".

## If something goes wrong

| What you see | What to do |
|---|---|
| "No access." | Ask an [Admin](../x.md) |

## Related pages

- [Other](other.md)
"""


def test_page_sections():
    out = S.page_sections("tasks/US-TASK-01", PAGE, "en", area="tasks", story="US-TASK-01")
    assert [s["id"] for s in out] == ["tasks/US-TASK-01#overview", "tasks/US-TASK-01#steps", "tasks/US-TASK-01#problems"]      # related is not a section
    assert out[0]["title"] == "Create a task" and out[1]["title"] == "Create a task - Steps"
    assert out[0]["text"] == "Use this page to add work to the list."
    assert out[1]["text"] == '1. Open the Tasks page.\n2. Click "New Task".'
    assert out[2]["text"] == 'What you see: "No access.". What to do: Ask an Admin.'


def test_long_section_is_split_at_item_boundaries():
    steps = "\n".join(f"{i}. " + "word " * 40 for i in range(1, 16))
    md = f"# T\n\nIntro.\n\n## Steps\n\n{steps}\n"
    out = S.page_sections("a/US-A-01", md, "en", area="a", story="US-A-01")
    parts = [s for s in out if s["key"] == "steps"]
    assert len(parts) >= 2 and all(len(p["text"]) <= 1200 for p in parts)
    assert parts[1]["id"] == "a/US-A-01#steps-2" and parts[1]["title"] == "T - Steps (2)"
    assert parts[1]["text"].startswith(str(int(parts[0]["text"].splitlines()[-1].split(".")[0]) + 1) + ".")        # numbering continues
    with pytest.raises(ValueError):
        S.split_items(["x" * 1300])


def test_italian_headings():
    md = "# Crea\n\nIntro.\n\n## Passaggi\n\n1. Apri la pagina.\n"
    assert [s["key"] for s in S.page_sections("a/US-A-01", md, "it", area="a", story="US-A-01")] == ["overview", "steps"]
    with pytest.raises(ValueError):
        S.parse_page("# Crea\n\n## Steps\n\n1. x\n", "it")


def test_question_split_rule():
    qs = ["Can I set a deadline when I make a new item?", "How do I add a task", "Where is the new button", "q4", "q5"]
    for n, bench in ((3, 1), (4, 2), (5, 2)):
        b, i = S.split_questions("US-TASK-01", "en", qs[:n])
        assert len(b) == bench and len(i) == n - bench and not set(b) & set(i)
    keyed = sorted((hashlib.sha256(f"US-TASK-01\nen\n{q}".encode()).hexdigest(), q) for q in qs)
    assert S.split_questions("US-TASK-01", "en", qs)[0] == [q for _k, q in keyed[:2]]          # the first floor(n/2) by key are bench
    assert S.split_questions("US-TASK-01", "en", qs) == S.split_questions("US-TASK-01", "en", list(reversed(qs)))
    assert S.split_questions("US-TASK-01", "it", qs) != S.split_questions("US-TASK-01", "en", qs)    # the language is part of the key
    assert S.split_questions("US-TASK-01", "en", ["  Can I   set a deadline when I make a new item? ", *qs[1:]])[0] == S.split_questions("US-TASK-01", "en", qs)[0]
