"""Every validator of models.md 5.7 with a passing and a failing example."""
import copy
from pathlib import Path

import pytest

from training.assistant import lexical as L, textchecks, validators as V

S1 = {"id": "tasks/US-TASK-01#steps", "title": "Create a task - Steps",
      "text": "1. Open the Tasks page.\n2. Click \"New Task\".\n3. In the \"Title\" field, type the title.\n4. Click \"Create Task\"."}
S2 = {"id": "tasks/US-TASK-01#problems", "title": "Create a task - If something goes wrong",
      "text": "What you see: \"You no longer have access to this organization.\". What to do: Ask an admin."}
BLOCK = textchecks.load_blocklist()


def rec(**kw):
    q = kw.pop("question", "where can i add one more item for my group")
    r = {"schema_version": 1, "id": "d01-en-000001", "split": "train", "domain": "d01", "lang": "en", "app_name": "Quill", "class": "pos_distant",
         "style": "goal", "question": q, "context": [dict(S1), dict(S2)], "gold": [1],
         "target": "Click \"New Task\" on the Tasks page and type the title.\nSource: [1]", "overlap": 0.0, "seed": 1}
    r.update(kw)
    golds = [r["context"][g - 1] for g in r["gold"] if 1 <= g <= len(r["context"])]
    r["overlap"] = kw.get("overlap", L.overlap(q, golds, "en") if golds else 0.0)
    return r


VERDICT_OK = {"supported": True, "answers_question": True}


def test_a_good_record_passes_everything(protocol):
    assert V.run_item_validators(rec(), protocol, BLOCK, VERDICT_OK) == {}


def test_v01(protocol):
    assert V.v01(rec()) == []
    assert V.v01({**rec(), "extra": 1})
    assert V.v01(rec(**{"class": "pos_bogus"}))
    assert V.v01(rec(gold=[3]))
    assert V.v01(rec(gold=[]))                                           # a positive needs gold
    assert V.v01(rec(**{"class": "ref_oos"}))                            # a refusal has no gold
    assert V.v01(rec(target=None)) and V.v01(rec(split="dev")) and V.v01(rec(id="x"))
    assert V.v01(rec(**{"class": "pos_multi"}))                          # two golds needed
    assert V.v01(rec(overlap=1.5)) is not None


def test_v04():
    assert V.v04(rec(), BLOCK) == []
    assert V.v04(rec(question="mail me at jo@example.org"), BLOCK)
    assert V.v04(rec(question="call 555 123 4567 now"), BLOCK)
    assert V.v04(rec(question="see www.example.com for more"), BLOCK)
    assert V.v04(rec(question="can i use Google for this"), BLOCK)
    assert V.v04(rec(), ["Tasks"])                                       # a blocklisted name in the context


def test_v05():
    assert V.v05(rec()) == []
    assert V.v05(rec(question="come posso creare un nuovo compito nella pagina"))
    assert V.v05(rec(lang="it", question="how do i create the task in the page"))


def test_v06(protocol):
    assert V.v06(rec(), protocol) == []
    assert V.v06(rec(target="no source line"), protocol)
    ref = rec(**{"class": "ref_oos"}, gold=[], target="I can't find this in the manual.")
    assert V.v06(ref, protocol) == []
    assert V.v06({**ref, "target": "I can't find this in the manual. Sorry."}, protocol)
    assert V.v06({**ref, "target": "Click.\nSource: [1]"}, protocol)


def test_v07(protocol):
    assert V.v07(rec(), protocol) == []
    assert V.v07(rec(target="Do it.\nSource: [2]"), protocol)           # cites the wrong section
    multi = rec(**{"class": "pos_multi"}, gold=[1, 2], target="Do it.\nSource: [1], [2]")
    assert V.v07(multi, protocol) == []
    assert V.v07({**multi, "target": "Do it.\nSource: [1]"}, protocol)   # pos_multi must cite every gold position
    assert V.v07(rec(target="Do it.\nSource: [1], [2]"), protocol) == []  # a single-gold class may cite more
    assert V.v07(rec(**{"class": "ref_oos"}, gold=[], target="I can't find this in the manual."), protocol) == []


def test_v08(protocol):
    assert V.v08(rec(), protocol, VERDICT_OK) == []
    assert V.v08(rec(target="Press the blue lever to launch rockets into orbit.\nSource: [1]"), protocol, VERDICT_OK)       # ungrounded stems
    assert any("number 42" in p for p in V.v08(rec(target="Click \"New Task\" and wait 42 seconds for the Tasks page title.\nSource: [1]"), protocol, VERDICT_OK))
    assert any("quoted string" in p for p in V.v08(rec(target="Click \"Make Task\" on the Tasks page and type the title.\nSource: [1]"), protocol, VERDICT_OK))
    assert V.v08(rec(), protocol, {"supported": False, "answers_question": True})
    assert V.v08(rec(), protocol, {"supported": True, "answers_question": False})
    assert V.v08(rec(), protocol, None)


def test_v09_bucket_per_class():
    assert V.v09(rec(overlap=0.1)) == [] and V.v09(rec(overlap=0.5))
    assert V.v09(rec(**{"class": "pos_mid"}, overlap=0.2)) == [] and V.v09(rec(**{"class": "pos_mid"}, overlap=0.15))
    assert V.v09(rec(**{"class": "pos_near"}, overlap=0.5)) == [] and V.v09(rec(**{"class": "pos_near"}, overlap=0.35))
    assert V.v09(rec(**{"class": "pos_multi"}, overlap=0.9)) == []        # no bucket


def test_v10():
    assert V.v10(rec()) == []
    assert V.v10(rec(question="please open the tasks page click new task now"))
    near = rec(**{"class": "pos_near"}, question='where do i find "open the tasks page click new" please')
    assert V.v10(near) == []                                             # a quoted UI label is allowed in pos_near
    assert V.v10(rec(question='where do i find "open the tasks page click new" please'))     # but not in other classes


def test_v11():
    ref = rec(**{"class": "ref_near_miss"}, gold=[], target="I can't find this in the manual.")
    assert V.v11(ref, {"answered": False}) == []
    assert V.v11(ref, {"answered": True}) and V.v11(ref, None)
    assert V.v11(rec(), None) == []


def test_v12():
    a, b = rec(id="d01-en-000001"), rec(id="d01-en-000002")
    assert V.v12([a, b]) == {"d01-en-000002": "stem set too close to an earlier question"}
    c = rec(id="d02-en-000001", domain="d02")
    assert V.v12([a, c]) == {}                                           # different domain
    assert V.v12([a, rec(id="d01-en-000003", question="invoices are monthly banana")]) == {}


def test_v13(protocol):
    def mk(k, pos, n):
        return [rec(id=f"d01-en-{i:06d}", gold=[pos]) | {"context": [dict(S1)] * 1 + [dict(S2)] * (k - 1)} for i in range(n)]
    balanced = [r for p in (1, 2) for r in mk(2, p, 50)]
    assert V.v13(balanced, balanced, protocol, min_sample=10) == []
    skewed = mk(2, 1, 90) + mk(2, 2, 10)
    assert any("K=2 position 1" in f for f in V.v13(skewed, skewed, protocol, min_sample=10))
    assert V.v13(skewed, skewed, protocol) == []                         # below the default sample size: not tested
    fat = rec()
    fat["context"][0] = {**fat["context"][0], "text": "x" * 4100}
    assert any("exceeds the context budget" in f for f in V.v13([], [fat], protocol))


def test_v14():
    shares = {"pos_distant": 0.5, "ref_oos": 0.5}
    def mk(cls, lang, n, overlap, offset=0):
        return [rec(id=f"d01-{lang}-{offset + i:06d}", lang=lang, **{"class": cls}) | {"overlap": overlap} for i in range(n)]
    ok = []
    for lang in ("en", "it"):
        for b, ov in (("d", 0.0), ("m", 0.2), ("n", 0.5)):
            ok += mk("pos_distant", lang, 6, ov, hash(b) % 1000 * 10) + mk("ref_oos", lang, 4, ov, 5000 + hash(b) % 1000 * 10)
    assert V.v14(ok, {"pos_distant": 0.6, "ref_oos": 0.4}) == []
    assert any("class pos_distant" in f for f in V.v14(ok, shares))
    assert any("language it" in f for f in V.v14([r for r in ok if r["lang"] == "en"] + [r for r in ok if r["lang"] == "it"][:10], {"pos_distant": 0.6, "ref_oos": 0.4}))
    all_pos = [r for r in ok if r["class"] == "pos_distant"]
    fails = V.v14(all_pos, {"pos_distant": 1.0})
    assert any("refusal share 0.000" in f for f in fails)
    assert V.v14([], shares)


def test_v15(tmp_path):
    domains = [{"id": "d01", "sector": "farm", "split": "train"}, {"id": "d02", "sector": "vet clinic", "split": "test"}]
    r1, r2 = rec(id="d01-en-000001"), rec(id="d02-en-000001", domain="d02", split="test")
    assert V.v15([r1, r2], domains, tmp_path) == []
    assert any("appears in splits" in f for f in V.v15([r1, {**r2, "domain": "d01"}], domains, tmp_path))
    assert any("has split" in f for f in V.v15([{**r1, "split": "test"}], domains, tmp_path))
    assert any("also a train sector" in f for f in V.v15([r1], domains + [{"id": "d03", "sector": "Farm", "split": "validation"}], tmp_path))
    docs = tmp_path / "docs" / "areas" / "x"
    docs.mkdir(parents=True)
    (docs / "userstories.md").write_text(S1["title"] + "\n" + S1["text"], encoding="utf-8")
    fails = V.v15([r1], domains, tmp_path / "docs")
    assert any("8-stem sequence" in f for f in fails)
    clean = rec(id="d01-en-000009", question="where can i add one more item for my group")
    clean["context"] = [{"id": "x", "title": "Other", "text": "Invoices are monthly and receipts are emailed."}] * 2
    clean["target"] = "Invoices are monthly.\nSource: [1]"
    assert V.v15([clean], domains, tmp_path / "docs") == []
