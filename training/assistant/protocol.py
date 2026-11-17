"""Python renderer and parser of the assistant protocol (models.md section 3).

``protocol.json`` is the single definition; this module only implements the rules of 3.2 to 3.7. All lengths are
counted in Unicode code points. Other runtimes (TypeScript, C#) must reproduce ``protocol.golden.json`` byte for byte.
"""
from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Mapping, Sequence

from training.common.hashing import sha256_bytes
from training.common.jsonio import load_json

PROTOCOL_PATH = Path(__file__).with_name("protocol.json")

# Whitespace of the normalisation rules: the exact set of the JavaScript ``\s`` class, so that implementations agree.
WS_CLASS = "\t\n\u000b\u000c\r    -     　﻿"
_WS_RUN = re.compile(f"[{WS_CLASS}]+")
_WS_END = re.compile(f"[{WS_CLASS}]+$")
_WS_START = re.compile(f"^[{WS_CLASS}]+")
_PLACEHOLDER = re.compile(r"\{(app_name|refusal|language_name|sections|n|title|text|question)\}")
_SOURCE_LINE = re.compile(r"^Source: \[[1-9]\](?:, \[[1-9]\])*$")


def load_protocol(path: str | Path = PROTOCOL_PATH) -> dict[str, Any]:
    return load_json(path)


def protocol_sha256(path: str | Path = PROTOCOL_PATH) -> str:
    """SHA-256 of the file bytes (what the lock and the pack record)."""
    return sha256_bytes(Path(path).read_bytes())


# ---------------------------------------------------------------- normalisation (3.7)

def normalise_text(value: str) -> str:
    """Unicode NFC and ``\\r\\n`` to ``\\n``."""
    return unicodedata.normalize("NFC", value).replace("\r\n", "\n")


def normalise_question(protocol: Mapping[str, Any], question: str) -> str:
    """NFC, CRLF, trim, collapse whitespace runs to one space, cut at ``max_question_chars`` code points."""
    q = normalise_text(question)
    q = _WS_START.sub("", q)
    q = _WS_END.sub("", q)
    q = _WS_RUN.sub(" ", q)
    return q[: int(protocol["context"]["max_question_chars"])]


def substitute(template: str, values: Mapping[str, str]) -> str:
    """One left-to-right pass; substituted values are not scanned again."""
    return _PLACEHOLDER.sub(lambda m: values[m.group(1)] if m.group(1) in values else m.group(0), template)


# ---------------------------------------------------------------- rendering (3.2)

def render_section(protocol: Mapping[str, Any], n: int, title: str, text: str) -> str:
    return substitute(protocol["section"], {"n": str(n), "title": normalise_text(title), "text": normalise_text(text)})


def render_messages(protocol: Mapping[str, Any], lang: str, app_name: str, sections: Sequence[Mapping[str, str]],
                    question: str) -> tuple[str, str]:
    """Return the ``(system, user)`` messages. ``sections`` are in context order, each with ``title`` and ``text``."""
    lang_cfg = protocol["languages"][lang]
    system = substitute(protocol["system"], {"app_name": normalise_text(app_name), "refusal": lang_cfg["refusal"],
                                             "language_name": lang_cfg["language_name"]})
    rendered = protocol["section_separator"].join(
        render_section(protocol, i, s["title"], s["text"]) for i, s in enumerate(sections, start=1))
    user = substitute(protocol["user"], {"sections": rendered, "question": normalise_question(protocol, question)})
    return system, user


# ---------------------------------------------------------------- context selection (3.5)

def select_sections(protocol: Mapping[str, Any], ranked: Sequence[Mapping[str, Any]],
                    max_sections: int | None = None) -> list[Mapping[str, Any]]:
    """Whole sections only, in rank order, within the budget. ``max_sections`` may lower (never raise) the limit,
    which training uses to draw K."""
    ctx = protocol["context"]
    limit = ctx["max_sections"] if max_sections is None else min(max_sections, ctx["max_sections"])
    selected: list[Mapping[str, Any]] = []
    total = 0
    for s in ranked[0: ctx["scan_limit"]]:
        if len(selected) == limit:
            break
        n = len(normalise_text(s["text"]))
        if total + n > ctx["max_context_chars"]:
            continue
        selected.append(s)
        total += n
    return selected


def context_chars(sections: Sequence[Mapping[str, Any]]) -> int:
    return sum(len(normalise_text(s["text"])) for s in sections)


# ---------------------------------------------------------------- parsing (3.3, 3.4)

@dataclass
class ParseResult:
    outcome: str                      # "refused" | "answered" | "invalid"
    cited: list[int] = field(default_factory=list)
    body: str | None = None
    reason: str | None = None         # why invalid (diagnostics only, not part of the golden contract)


def parse_answer(protocol: Mapping[str, Any], lang: str, output: str, k: int) -> ParseResult:
    """Parse a model output against ``k`` sections in the context."""
    ans = protocol["answer"]
    text = _WS_END.sub("", output.replace("\r\n", "\n"))
    if text == protocol["languages"][lang]["refusal"]:
        return ParseResult("refused")
    if text.startswith(protocol["languages"][lang]["refusal"]):
        return ParseResult("invalid", reason="refusal sentence followed by other text")
    lines = text.split("\n")
    if len(lines) < 2:
        return ParseResult("invalid", reason="no body and source line")
    last = lines[-1]
    if not _SOURCE_LINE.match(last) or not last.startswith(ans["source_prefix"]):
        return ParseResult("invalid", reason="last line is not a source line")
    refs = [int(x) for x in re.findall(r"\[([1-9])\]", last)]
    if any(b <= a for a, b in zip(refs, refs[1:])):
        return ParseResult("invalid", reason="references are not distinct and ascending")
    if refs[-1] > k:
        return ParseResult("invalid", reason="reference beyond the context")
    body_lines = lines[:-1]
    body = "\n".join(body_lines)
    if not (1 <= len(body_lines) <= ans["max_body_lines"]):
        return ParseResult("invalid", reason="body line count")
    if len(body) > ans["max_body_chars"]:
        return ParseResult("invalid", reason="body too long")
    if not _WS_RUN.sub("", body):
        return ParseResult("invalid", reason="empty body")
    if any(l.startswith("Source:") for l in body_lines):
        return ParseResult("invalid", reason="a body line starts with Source:")
    return ParseResult("answered", refs, body)


def format_answer(protocol: Mapping[str, Any], body: str, cited: Sequence[int]) -> str:
    """Build the target ``{body}\\nSource: [a], [b]`` (cited must be ascending and distinct)."""
    return body + "\n" + protocol["answer"]["source_prefix"] + ", ".join(f"[{n}]" for n in cited)
