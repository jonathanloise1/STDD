"""Manual pages of a synthetic domain: page parsing, sections (manual.md 6.1) and the question split (6.2).

This is the Python counterpart of the pack builder, used for the synthetic domains only. For a real project the
TypeScript pack builder is the reference (``npm run help:build``).
"""
from __future__ import annotations

import hashlib
import re
import unicodedata
from dataclasses import dataclass, field
from typing import Any

MAX_SECTION_CHARS = 1200
DRAFT_MARKER = "<!-- draft: written by manual:generate, remove after review -->"

# 2.4 headings per key and language
HEADINGS = {
    "en": {"before": "Before you start", "steps": "Steps", "result": "What you will see", "rules": "Good to know",
           "problems": "If something goes wrong", "related": "Related pages"},
    "it": {"before": "Prima di iniziare", "steps": "Passaggi", "result": "Cosa vedrai", "rules": "Da sapere",
           "problems": "Se qualcosa non va", "related": "Pagine correlate"},
}
KEY_ORDER = ["before", "steps", "result", "rules", "problems", "related"]
PROBLEM_COLUMNS = {"en": ("What you see", "What to do"), "it": ("Cosa vedi", "Cosa fare")}


@dataclass
class Page:
    title: str
    intro: str
    sections: list[tuple[str, list[str]]] = field(default_factory=list)   # (key, body lines)
    body_lines: int = 0


def strip_front_matter(md: str) -> tuple[dict[str, str], str]:
    md = md.replace("\r\n", "\n")
    if not md.startswith("---\n"):
        return {}, md
    end = md.find("\n---\n", 4)
    if end < 0:
        return {}, md
    fm: dict[str, str] = {}
    for line in md[4:end].split("\n"):
        if ": " in line:
            k, v = line.split(": ", 1)
            fm[k] = v
        elif line.endswith(":"):
            fm[line[:-1]] = ""
    return fm, md[end + 5:]


def parse_page(md: str, lang: str) -> Page:
    """Parse a page body of the 2.5 subset. Raises ``ValueError`` on a structure that sections cannot be built from."""
    _fm, body = strip_front_matter(md)
    lines = [l for l in body.split("\n")]
    if lines and lines[0].strip() == DRAFT_MARKER:
        lines = lines[1:]
    rev = {v: k for k, v in HEADINGS[lang].items()}
    title, intro_lines, current, sections = None, [], None, []
    for line in lines:
        if line.startswith("# ") and title is None:
            title = line[2:].strip()
        elif line.startswith("## "):
            key = rev.get(line[3:].strip())
            if key is None:
                raise ValueError(f"unknown heading: {line}")
            current = (key, [])
            sections.append(current)
        elif current is not None:
            current[1].append(line)
        elif title is not None:
            intro_lines.append(line)
    if title is None:
        raise ValueError("missing H1")
    return Page(title, "\n".join(intro_lines).strip(), [(k, b) for k, b in sections], sum(1 for l in lines if l.strip()))


_BOLD = re.compile(r"\*\*(.+?)\*\*")
_IMAGE = re.compile(r"!\[([^\]]*)\]\([^)]*\)")
_LINK = re.compile(r"\[([^\]]+)\]\([^)]*\)")


def inline_text(text: str) -> str:
    text = _IMAGE.sub(lambda m: m.group(1), text)
    text = _LINK.sub(lambda m: m.group(1), text)
    return _BOLD.sub(lambda m: m.group(1), text).strip()


def body_items(lines: list[str]) -> list[str]:
    """Plain-text items of a body: paragraph lines, list items and table rows, as in 6.1."""
    items: list[str] = []
    header: list[str] | None = None
    para: list[str] = []

    def flush() -> None:
        if para:
            items.append(" ".join(para))
            para.clear()

    for raw in lines:
        line = raw.rstrip()
        if not line.strip():
            flush()
            header = None
            continue
        if line.startswith("|"):
            flush()
            cells = [inline_text(c) for c in line.strip().strip("|").split("|")]
            if all(re.fullmatch(r"-{3,}", c.strip()) for c in cells):
                continue
            if header is None:
                header = cells
                continue
            items.append(" ".join(f"{h}: {c}." for h, c in zip(header, cells)))
        elif re.match(r"^\d+\. ", line):
            flush()
            n, rest = line.split(". ", 1)
            items.append(f"{n}. {inline_text(rest)}")
        elif line.startswith("- "):
            flush()
            items.append(f"- {inline_text(line[2:])}")
        elif line.startswith("   ") and line.strip().startswith("!["):
            flush()
            alt = inline_text(line.strip())
            if alt:
                items.append(alt)
        else:
            para.append(inline_text(line))
    flush()
    return [unicodedata.normalize("NFC", i) for i in items]


def split_items(items: list[str], limit: int = MAX_SECTION_CHARS) -> list[str]:
    parts, cur = [], ""
    for it in items:
        if len(it) > limit:
            raise ValueError(f"a single item is longer than {limit} characters")
        nxt = it if not cur else cur + "\n" + it
        if len(nxt) > limit:
            parts.append(cur)
            cur = it
        else:
            cur = nxt
    if cur:
        parts.append(cur)
    return parts or [""]


def page_sections(page_id: str, md: str, lang: str, *, area: str, story: str | None) -> list[dict[str, Any]]:
    page = parse_page(md, lang)
    out: list[dict[str, Any]] = []

    def add(key: str, title: str, items: list[str]) -> None:
        for n, text in enumerate(split_items(items), start=1):
            if not text.strip():
                continue
            sid = f"{page_id}#{key}" + (f"-{n}" if n > 1 else "")
            out.append({"id": sid, "page_id": page_id, "key": key, "area": area, "story": story,
                        "title": title + (f" ({n})" if n > 1 else ""), "text": text, "questions": []})

    add("overview", page.title, body_items(page.intro.split("\n")))
    for key, lines in page.sections:
        if key == "related":
            continue
        add(key, f"{page.title} - {HEADINGS[lang][key]}", body_items(lines))
    return out


def normalise_question(q: str) -> str:
    return " ".join(unicodedata.normalize("NFC", q).split())


def split_questions(story_id: str, lang: str, questions: list[str]) -> tuple[list[str], list[str]]:
    """The deterministic split of manual.md 6.2: returns ``(bench, indexed)``."""
    keyed = sorted(((hashlib.sha256(f"{story_id}\n{lang}\n{normalise_question(q)}".encode("utf-8")).hexdigest(), normalise_question(q))
                    for q in questions))
    cut = len(keyed) // 2
    return [q for _k, q in keyed[:cut]], [q for _k, q in keyed[cut:]]
