"""Golden cases of the protocol (models.md 3.7): ``python -m training.assistant.golden --write | --check``.

The inputs below are written by hand; the expected values are produced by ``protocol.py`` and reviewed once. The output
``protocol.golden.json`` is language-neutral: every implementation renders, selects and parses each case and compares
the result byte for byte.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any

from training.assistant import protocol as P
from training.common.jsonio import dumps_pretty, load_json

GOLDEN_PATH = Path(__file__).with_name("protocol.golden.json")

S_CREATE = {"title": "Create a task - Steps", "text": "1. Open the Tasks page.\n2. Click \"New Task\".\n3. In the \"Title\" field, type the title.\n4. Click \"Create Task\"."}
S_WRONG = {"title": "Create a task - If something goes wrong", "text": "What you see: \"You no longer have access to this organization.\". What to do: Ask an admin of the organization to add them back."}
S_IT1 = {"title": "Registrare un prestito - Passaggi", "text": "1. Apri la pagina Prestiti.\n2. Fai clic su \"Nuovo prestito\".\n3. Nel campo \"Titolo\" scrivi il titolo del libro."}
S_IT2 = {"title": "Registrare un prestito - Da sapere", "text": "- Un socio può avere al massimo cinque prestiti.\n- Perché è già in prestito? Il libro è già fuori."}
S_IT3 = {"title": "Restituire un libro - Passaggi", "text": "1. Apri la scheda del socio.\n2. Fai clic su \"Restituisci\"."}
S_IT4 = {"title": "Pagina iniziale", "text": "Usa questa pagina per vedere i prestiti in scadenza oggi."}
S_BRACES = {"title": "Export {format} - Steps", "text": "1. Type {name} in the \"Template\" field.\n2. Click \"Export {format}\"."}

RENDER_CASES: list[dict[str, Any]] = [
    {"id": "r01-one-section-en", "description": "One section, English", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE], "question": "how do i make a task"},
    {"id": "r02-two-sections-en", "description": "Two sections, the example of models.md 3.2", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE, S_WRONG], "question": "how do i put a new thing on the team list"},
    {"id": "r03-three-sections-it-accents", "description": "Three sections, Italian accents and an apostrophe", "lang": "it", "app_name": "Biblioteca Viva", "sections": [S_IT1, S_IT2, S_IT3], "question": "Perché non riesco a registrare un prestito? L'elenco è vuoto"},
    {"id": "r04-four-sections-it", "description": "Four sections, Italian", "lang": "it", "app_name": "Biblioteca Viva", "sections": [S_IT1, S_IT2, S_IT3, S_IT4], "question": "dove vedo i prestiti di oggi"},
    {"id": "r05-braces-in-question", "description": "Braces in the question stay literal and are not substituted again", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE], "question": "what does {question} or {sections} or {n} mean in {app_name}?"},
    {"id": "r06-quotes-and-backslash", "description": "Quotes and a backslash in the question", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE], "question": "Where is the \"New Task\" button? I typed C:\\tasks\\ and 'it' failed"},
    {"id": "r07-crlf-input", "description": "CRLF in the question and in a section text", "lang": "en", "app_name": "TaskFlow", "sections": [{"title": "Create a task - Steps", "text": "1. Open the Tasks page.\r\n2. Click \"New Task\"."}], "question": "how do i\r\nmake a task\r\n"},
    {"id": "r08-over-long-question", "description": "A question of 400 characters is cut at 300 code points", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE], "question": "how do i make a task " * 20},
    {"id": "r09-braces-in-app-name", "description": "Braces in the application name and in section titles and texts stay literal", "lang": "en", "app_name": "{sections} Pro", "sections": [S_BRACES], "question": "how do i export"},
    {"id": "r10-nfd-input", "description": "Decomposed input (e + combining acute) is normalised to NFC", "lang": "it", "app_name": "Biblioteca Viva", "sections": [{"title": "Perche\u0301 - Passaggi", "text": "1. Apri la pagina Prestiti."}], "question": "Perche\u0301 non vedo i libri"},
    {"id": "r11-whitespace-runs", "description": "Tabs, non-breaking spaces and runs of spaces collapse to one space", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE], "question": "  how\tdo   i\u00a0\u00a0make\u2003a task  "},
    {"id": "r12-astral-cut", "description": "The cut counts code points, not UTF-16 units: an emoji straddles the limit", "lang": "en", "app_name": "TaskFlow", "sections": [S_CREATE], "question": "a" * 299 + "\U0001F600\U0001F600"},
]

LONG_BODY_LINES = "\n".join(f"Line {i}" for i in range(1, 9))

PARSE_CASES: list[dict[str, Any]] = [
    {"id": "p01-refused-en", "lang": "en", "k": 2, "output": "I can't find this in the manual."},
    {"id": "p02-refused-it", "lang": "it", "k": 3, "output": "Non trovo questa informazione nel manuale."},
    {"id": "p03-refused-trailing-whitespace", "lang": "en", "k": 2, "output": "I can't find this in the manual.\n\n  \n"},
    {"id": "p04-answered-one", "lang": "en", "k": 2, "output": "Click \"New Task\" on the Tasks page, type a title and click \"Create Task\".\nSource: [1]"},
    {"id": "p05-answered-two", "lang": "it", "k": 4, "output": "Apri la scheda del socio e fai clic su \"Restituisci\".\nSource: [2], [3]"},
    {"id": "p06-answered-crlf", "lang": "en", "k": 2, "output": "1. Open the Tasks page.\r\n2. Click \"New Task\".\r\nSource: [1]\r\n"},
    {"id": "p07-answered-max-lines", "lang": "en", "k": 4, "output": "\n".join(f"{i}. Step {i}." for i in range(1, 7)) + "\nSource: [1], [2], [3], [4]"},
    {"id": "p08-invalid-refusal-plus-text", "lang": "en", "k": 2, "output": "I can't find this in the manual.\nSource: [1]"},
    {"id": "p09-invalid-refusal-other-language", "lang": "it", "k": 2, "output": "I can't find this in the manual."},
    {"id": "p10-invalid-no-source", "lang": "en", "k": 2, "output": "Click \"New Task\"."},
    {"id": "p11-invalid-source-only", "lang": "en", "k": 2, "output": "Source: [1]"},
    {"id": "p12-invalid-reference-beyond-context", "lang": "en", "k": 2, "output": "Click \"New Task\".\nSource: [3]"},
    {"id": "p13-invalid-descending", "lang": "en", "k": 3, "output": "Click \"New Task\".\nSource: [2], [1]"},
    {"id": "p14-invalid-duplicate", "lang": "en", "k": 3, "output": "Click \"New Task\".\nSource: [1], [1]"},
    {"id": "p15-invalid-body-too-long", "lang": "en", "k": 2, "output": "x" * 601 + "\nSource: [1]"},
    {"id": "p16-answered-body-exactly-600", "lang": "en", "k": 2, "output": "x" * 600 + "\nSource: [1]"},
    {"id": "p17-invalid-too-many-lines", "lang": "en", "k": 2, "output": LONG_BODY_LINES + "\nSource: [1]"},
    {"id": "p18-invalid-source-line-in-body", "lang": "en", "k": 2, "output": "Source: [1]\nClick \"New Task\".\nSource: [1]"},
    {"id": "p19-invalid-source-with-extra-text", "lang": "en", "k": 2, "output": "Click \"New Task\".\nSource: [1] and more"},
    {"id": "p20-invalid-source-lowercase", "lang": "en", "k": 2, "output": "Click \"New Task\".\nsource: [1]"},
    {"id": "p21-invalid-source-double-digit", "lang": "en", "k": 4, "output": "Click \"New Task\".\nSource: [10]"},
    {"id": "p22-invalid-source-no-space", "lang": "en", "k": 3, "output": "Click \"New Task\".\nSource: [1],[2]"},
]


def _sec(i: int, chars: int) -> dict[str, Any]:
    return {"id": f"s{i}", "title": f"Section {i}", "text": "a" * chars}


SELECT_CASES: list[dict[str, Any]] = [
    {"id": "c01-four-fit", "description": "Four short sections: all four are selected", "max_sections": None, "ranked": [_sec(i, 200) for i in range(1, 7)]},
    {"id": "c02-skip-over-budget", "description": "A section that would pass 4,000 characters is skipped, later smaller ones are taken", "max_sections": None, "ranked": [_sec(1, 1200), _sec(2, 1200), _sec(3, 1200), _sec(4, 1000), _sec(5, 300), _sec(6, 100)]},
    {"id": "c03-scan-limit", "description": "Only the first 8 ranked sections are scanned", "max_sections": None, "ranked": [_sec(i, 1200) for i in range(1, 5)] + [_sec(i, 1200) for i in range(5, 9)] + [_sec(9, 10)]},
    {"id": "c04-lower-limit", "description": "A lower max_sections (training draws K from 2 to 4)", "max_sections": 2, "ranked": [_sec(i, 100) for i in range(1, 5)]},
    {"id": "c05-never-cut", "description": "Exactly 4,000 characters fit: the budget is inclusive and no section is cut", "max_sections": None, "ranked": [_sec(1, 1200), _sec(2, 1200), _sec(3, 1200), _sec(4, 400), _sec(5, 1)]},
]


def build() -> dict[str, Any]:
    proto = P.load_protocol()
    render = []
    for c in RENDER_CASES:
        system, user = P.render_messages(proto, c["lang"], c["app_name"], c["sections"], c["question"])
        render.append({**c, "expected": {"question": P.normalise_question(proto, c["question"]), "system": system, "user": user}})
    parse = []
    for c in PARSE_CASES:
        r = P.parse_answer(proto, c["lang"], c["output"], c["k"])
        parse.append({**c, "expected": {"outcome": r.outcome, "cited": r.cited}})
    select = []
    for c in SELECT_CASES:
        chosen = P.select_sections(proto, c["ranked"], c["max_sections"])
        select.append({**c, "expected": {"selected_ids": [s["id"] for s in chosen]}})
    return {
        "schema_version": 1,
        "protocol_sha256": P.protocol_sha256(),
        "format": {
            "description": "Language-neutral golden cases of the assistant protocol (models.md 3.7). Every implementation loads protocol.json, then for each case checks that its output equals `expected` exactly.",
            "render": "render_messages(protocol, lang, app_name, sections, question) -> (system, user). `expected.question` is the normalised question (also the value that is substituted into `user`). Compare strings code point for code point.",
            "parse": "parse_answer(protocol, lang, output, k) -> outcome in refused|answered|invalid, and `cited` (the cited positions; empty unless answered).",
            "select": "select_sections(protocol, ranked, max_sections) -> ids of the selected sections in order. `max_sections` null means the protocol value; a value can only lower it.",
            "lengths": "All lengths (question cut, body characters, section characters) are counted in Unicode code points. Implementations whose strings are UTF-16 (TypeScript, C#) must count code points, not code units; case r12-astral-cut checks it.",
            "whitespace": "Whitespace for trimming and collapsing is exactly this set: U+0009 to U+000D, U+0020, U+00A0, U+1680, U+2000 to U+200A, U+2028, U+2029, U+202F, U+205F, U+3000, U+FEFF.",
            "normalisation": "Unicode NFC, then CRLF to LF, for the question, the application name and every section title and text. Question: then trim, collapse whitespace runs to one space, cut at context.max_question_chars. Parsing: CRLF to LF and trailing whitespace trimmed before any test.",
            "substitution": "One left-to-right pass over a template; only the placeholders of models.md 3.2 are replaced; substituted values are never scanned again.",
        },
        "render": render,
        "parse": parse,
        "select": select,
    }


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.assistant.golden", description="Write or check protocol.golden.json.")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--write", action="store_true")
    g.add_argument("--check", action="store_true", help="exit 1 when the file differs from the regenerated cases")
    args = ap.parse_args(argv)
    data = build()
    if args.write:
        GOLDEN_PATH.write_text(dumps_pretty(data), encoding="utf-8", newline="\n")
        print(f"wrote {GOLDEN_PATH} ({len(data['render'])} render, {len(data['parse'])} parse, {len(data['select'])} select cases)")
        return 0
    if not GOLDEN_PATH.exists() or load_json(GOLDEN_PATH) != data:
        print("protocol.golden.json is stale: run python -m training.assistant.golden --write", file=sys.stderr)
        return 1
    print("protocol.golden.json is current")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
