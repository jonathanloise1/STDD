"""The fixture of the embedding table: inputs, a fake encoder and the golden file of the table (manual.md 6.7.9, models.md 7.8).

The inputs are small and written in code: a pack with a few sections and indexed questions, a glossary, locale strings and a
byte-level vocabulary. ``build_golden`` materialises them as the files the builder reads, builds each language with ``FakeEncoder``
and returns what ``src/frontend/scripts/help/fixtures/embeddings.table.golden.json`` holds: the inputs, the table's bytes (base64)
and the decoded vectors. ``tests/test_embed.py`` rebuilds it and compares byte for byte; ``scripts/help/golden.ts`` reads it.

Regenerate after an intended change of the builder, the fake encoder or these inputs, with the repository root as working folder:

    python training/tests/embed_fixture.py --write

then regenerate ``embeddings.golden.json`` with ``node --disable-warning=ExperimentalWarning scripts/help/golden.ts --write`` in
``src/frontend`` and run both test suites.
"""
from __future__ import annotations

import base64
import json
import sys
import tempfile
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import numpy as np  # noqa: E402

from training.assistant import embed as E, embtable, lexical as L  # noqa: E402
from training.common.hashing import sha256_bytes, sha256_file  # noqa: E402

GOLDEN_PATH = ROOT / "src" / "frontend" / "scripts" / "help" / "fixtures" / "embeddings.table.golden.json"
MODEL_VERSION = "1.0.0"
FAKE_DIM = 32

CONFIG_OVERRIDES = {"table": {"dims": 16, "max_stems": 400, "max_bytes": 1 << 20, "batch_size": 8, "max_tokens": 16, "min_row_cosine": 0.99}}

# sections: (id, title, text); the story of a section is the part of the page id after the area, or null.
SECTIONS = {
    "en": [
        ("tasks/US-TASK-01#overview", "Create a task", "Use this page to add a task with a title. The team can see what needs to be done."),
        ("tasks/US-TASK-01#steps", "Create a task - Steps", "1. Open the Tasks page.\n2. Click \"New Task\".\n3. In the \"Title\" field, type a value.\n4. Click \"Create Task\"."),
        ("tasks/US-TASK-01#problems", "Create a task - If something goes wrong", "\"Enter a title.\". What to do: Type a title in the Title field."),
        ("tasks/US-TASK-02#overview", "Edit a task", "Use this page to change the title or the status of a task."),
        ("tasks/US-TASK-02#steps", "Edit a task - Steps", "1. Open the Tasks page.\n2. Click the pencil next to the task.\n3. Change the title or the status.\n4. Click \"Save\"."),
        ("team/US-TEAM-01#overview", "Invite a colleague", "Send an invitation by e-mail so that a colleague can join your organization."),
        ("team/US-TEAM-01#rules", "Invite a colleague - Good to know", "- An invitation expires after seven days.\n- Only an admin can invite people."),
        ("home#overview", "MyApp", "Use this manual to find out how to use MyApp."),
        ("home#blank", "A", "the and of"),                                    # no stem at all: a section without a vector
        ("home#rare", "Zyzzyva", "Zyzzyva zyzzyva"),   # its stem is removed from the table below
    ],
    "it": [
        ("tasks/US-TASK-01#overview", "Creare un compito", "Usa questa pagina per aggiungere un compito con un titolo. Il team vede cosa c'è da fare."),
        ("tasks/US-TASK-01#steps", "Creare un compito - Passaggi", "1. Apri la pagina Compiti.\n2. Clicca \"Nuovo compito\".\n3. Nel campo \"Titolo\" scrivi un valore.\n4. Clicca \"Crea compito\"."),
        ("tasks/US-TASK-02#overview", "Modificare un compito", "Usa questa pagina per cambiare il titolo o lo stato di un compito."),
        ("tasks/US-TASK-02#steps", "Modificare un compito - Passaggi", "1. Apri la pagina Compiti.\n2. Clicca la matita accanto al compito.\n3. Cambia il titolo o lo stato.\n4. Clicca \"Salva\"."),
        ("team/US-TEAM-01#overview", "Invitare un collega", "Invia un invito per e-mail così che un collega possa entrare nell'organizzazione."),
        ("team/US-TEAM-01#rules", "Invitare un collega - Da sapere", "- Un invito scade dopo sette giorni.\n- Solo un amministratore può invitare le persone."),
        ("home#overview", "MyApp", "Usa questo manuale per scoprire come usare MyApp."),
    ],
}
QUESTIONS = {
    "en": [("US-TASK-01", "How do I add something new to the team's to-do list?"), ("US-TASK-01", "Why does the form refuse to save when the name is blank?"),
           ("US-TASK-02", "Can I fix a typo in a task?")],
    "it": [("US-TASK-01", "Come aggiungo un lavoro da fare per il mio team?"), ("US-TASK-02", "Posso correggere un errore di battitura?")],
}
GLOSSARY_MD = """# Help glossary

## en

| Term | Users also say |
|------|----------------|
| task | job, to-do item |
| organization | company |
| due date | deadline |
| manual | guide |

## it

| Term | Users also say |
|------|----------------|
| compito | lavoro, attività da fare |
| organizzazione | azienda |
| manuale | guida |
"""
LOCALES = {
    "en": {"common.json": {"save": "Save", "cancel": "Cancel", "greeting": "Hello {{name}}, <b>welcome</b>", "menu": {"signOut": "Sign out", "language": "Language"}}},
    "it": {"common.json": {"save": "Salva", "cancel": "Annulla", "greeting": "Ciao {{name}}, <b>benvenuto</b>", "menu": {"signOut": "Esci", "language": "Lingua"}}},
}
# Tokens of a byte-level vocabulary, in id order. ``Ġ`` is the space marker; ``PerchÃ©`` is the byte-level image of "perché".
VOCAB_WORDS = {
    "en": ["the", "task", "weather", "forecast", "rain", "invoice", "payment", "pay", "billing", "colleague", "team", "job", "item", "work"],
    "it": ["il", "compito", "tempo", "pioggia", "fattura", "pagamento", "collega", "squadra", "lavoro"],
}
VOCAB_EXTRA = ["ĠperchÃ©", "Ġдом", "Ġx", "Ġa1b", "task", "ĠNeverUsed9"]
CLUSTERS = {
    "en": [["task", "tasks", "job", "jobs", "item", "items", "work", "todo", "create", "add", "edit", "title", "status", "new"],
           ["invitation", "invite", "invit", "colleague", "colleagues", "organization", "company", "team", "people", "admin", "join"],
           ["weather", "forecast", "rain", "sunny"], ["invoice", "payment", "pay", "billing", "money"],
           ["manual", "guide", "page", "pages"]],
    "it": [["compito", "compiti", "lavoro", "attività", "titolo", "stato", "nuovo", "crea", "creare", "modifica", "modificare"],
           ["invito", "inviti", "invitare", "collega", "organizzazione", "azienda", "team", "persone", "amministratore"],
           ["tempo", "pioggia"], ["fattura", "pagamento", "pagare"], ["manuale", "guida", "pagina"]],
}
# a stem that the table must not hold, so that a section has no vector for it
REMOVED = {"en": ["zyzzyva"], "it": []}


def vocab_json(lang: str) -> dict[str, int]:
    tokens = ["Ġ" + w for w in VOCAB_WORDS[lang]] + VOCAB_EXTRA
    return {t: i for i, t in enumerate(tokens)}


def pack_of(lang: str) -> dict[str, Any]:
    story = lambda sid: sid.split("#")[0].split("/")[1] if "/" in sid.split("#")[0] else None   # noqa: E731
    return {
        "analyzer": E.analyzer_block(lang),
        "build": {"commit": None, "inputs_sha256": sha256_bytes(f"fixture {lang}".encode())},
        "sections": [{"id": i, "title": t, "text": x, "story": story(i)} for i, t, x in SECTIONS[lang]],
        "questions": [{"story": s, "text": q} for s, q in QUESTIONS[lang]],
    }


def materialise(root: Path, lang: str, packs_root: Path | None = None) -> tuple[Path, Path]:
    """Writes the files of the fixture below ``root`` and returns ``(packs folder, vocab.json)``."""
    packs = packs_root or root / "packs"
    (packs / lang).mkdir(parents=True, exist_ok=True)
    (packs / lang / "pack.json").write_text(json.dumps(pack_of(lang)), encoding="utf-8")
    g = root / "docs" / "manual"
    g.mkdir(parents=True, exist_ok=True)
    (g / "glossary.md").write_text(GLOSSARY_MD, encoding="utf-8")
    loc = root / "src" / "frontend" / "public" / "locales" / lang
    loc.mkdir(parents=True, exist_ok=True)
    for name, body in LOCALES[lang].items():
        (loc / name).write_text(json.dumps(body, ensure_ascii=False), encoding="utf-8")
    cfg = json.loads((ROOT / E.CONFIG_REL).read_text(encoding="utf-8"))
    cfg["model"]["verified"] = True
    cfg["table"] = CONFIG_OVERRIDES["table"]
    cdir = root / "training" / "assistant" / "config"
    cdir.mkdir(parents=True, exist_ok=True)
    (cdir / "embed.json").write_text(json.dumps(cfg, indent=2), encoding="utf-8")
    (cdir / ".." / "retrieval.json").resolve().write_bytes((ROOT / E.RETRIEVAL_REL).read_bytes())
    vocab = root / f"vocab.{lang}.json"
    vocab.write_text(json.dumps(vocab_json(lang), ensure_ascii=False), encoding="utf-8")
    return packs, vocab


def build_table(root: Path, lang: str, cache: dict | None = None, version: str = MODEL_VERSION) -> E.LangBuild:
    packs, vocab = materialise(root, lang)
    cfg = E.load_config(root)
    E.check_config(cfg)
    src = E.read_sources(root, lang, packs, cfg)
    words = E.tokenizer_words(json.loads(vocab.read_text(encoding="utf-8")))
    plan = E.plan_stems(src, words, cfg["table"]["max_stems"])
    plan.stems = [s for s in plan.stems if s.stem not in REMOVED[lang]]
    enc = E.FakeEncoder(FAKE_DIM, CLUSTERS[lang])
    return E.build_language(src, plan, enc, cfg, sha256_file(E.config_path(root)), version, {} if cache is None else cache, "cpu")


def build_golden() -> dict[str, Any]:
    out: dict[str, Any] = {"schema_version": 1, "kind": "help-embeddings-golden", "model_version": MODEL_VERSION, "languages": {}}
    for lang in ("en", "it"):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            res = build_table(root, lang)
            gloss = E.parse_glossary(GLOSSARY_MD, lang)
        table = embtable.read_table(res.data, lang)
        out["languages"][lang] = {
            "inputs": {"pack": pack_of(lang), "glossary": gloss, "locales": LOCALES[lang], "vocab": vocab_json(lang), "clusters": CLUSTERS[lang],
                       "fake_dim": FAKE_DIM, "removed": REMOVED[lang], "config": CONFIG_OVERRIDES["table"]},
            "table_base64": base64.b64encode(res.data).decode("ascii"),
            "table_sha256": res.sha256,
            "decoded": {"dims": table.dims, "count": table.count, "vocab": table.vocab, "weights": [int(w) for w in table.weights],
                        "scales": [float(x) for x in table.scales], "vectors": table.decoded().tolist()},
        }
    return out


def serialise(golden: dict[str, Any]) -> str:
    return json.dumps(golden, indent=1, ensure_ascii=False) + "\n"


if __name__ == "__main__":
    text = serialise(build_golden())
    if "--write" in sys.argv:
        GOLDEN_PATH.write_text(text, encoding="utf-8")
        print(f"wrote {GOLDEN_PATH}")
    else:
        print("golden file is up to date" if GOLDEN_PATH.read_text(encoding="utf-8") == text else "golden file differs: run with --write")
