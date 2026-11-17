"""Synthetic domains: data model, the story contract (V02), the page structure check (V03, built-in part) and the
repository tree written for each domain (models.md 5.2)."""
from __future__ import annotations

import json
import re
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from training.assistant.sections import DRAFT_MARKER, HEADINGS, KEY_ORDER, PROBLEM_COLUMNS, parse_page, strip_front_matter
from training.common.hashing import git_blob_id
from training.common.jsonio import atomic_write_text

ROLES = ["Visitor", "Signed-in user", "Admin", "Editor", "Viewer"]
PRIORITIES = ["Critical", "High", "Medium", "Low"]
STORY_HEADINGS = ["Story", "Acceptance Criteria", "Access Control", "Business Rules", "Error States", "Non-Goals", "Questions users ask"]
AREA_HEADINGS = ["Overview", "Stories", "Business Rules", "UI Flow", "Access Control", "Open Questions", "Files"]
AREA_FOLDER_RE = re.compile(r"^[a-z]+(-[a-z]+)*$")
AREA_CODE_RE = re.compile(r"^[A-Z]{2,8}$")
STORY_ID_RE = re.compile(r"^US-[A-Z]{2,8}-\d{2}$")
MAX_STORY_LINES = 80


class DomainError(ValueError):
    pass


@dataclass
class StorySpec:
    id: str
    area: str            # area folder
    title: str
    priority: str
    user_input: str
    goal: str
    screen: str
    route: str
    md: str | None = None            # the story file, once written by the teacher and valid
    questions: dict[str, list[str]] = field(default_factory=dict)


@dataclass
class AreaSpec:
    folder: str
    code: str
    title: str
    overview: str
    business_rules: list[str]
    ui_flow: list[str]
    stories: list[StorySpec] = field(default_factory=list)


@dataclass
class Domain:
    id: str
    sector: str
    split: str
    app_name: str
    roles: list[str]
    areas: list[AreaSpec]
    pages: dict[str, dict[str, str]] = field(default_factory=dict)   # lang -> page_id -> markdown body (no front matter)
    bench_oos: dict[str, list[str]] = field(default_factory=dict)    # lang -> out-of-scope questions for docs/manual/bench.md

    def stories(self) -> list[StorySpec]:
        return [s for a in self.areas for s in a.stories]

    def area_of(self, story: StorySpec) -> AreaSpec:
        return next(a for a in self.areas if a.folder == story.area)


# ---------------------------------------------------------------- the domain stage response

def parse_domain_response(text: str, domain_cfg: dict[str, Any], n_areas: int, n_stories: int) -> Domain:
    """Parse and check the JSON answer of the ``domain`` stage. Raises ``DomainError``."""
    try:
        data = json.loads(_strip_fence(text))
    except json.JSONDecodeError as exc:
        raise DomainError(f"not JSON: {exc}") from exc
    if not isinstance(data, dict):
        raise DomainError("not an object")
    app = str(data.get("app_name", "")).strip()
    if not app or len(app) > 60:
        raise DomainError("app_name missing or too long")
    roles = [r for r in data.get("roles", []) if r in ROLES]
    if not roles:
        raise DomainError("no valid roles")
    areas: list[AreaSpec] = []
    seen_codes: set[str] = set()
    for a in data.get("areas", []):
        folder, code = str(a.get("folder", "")), str(a.get("code", ""))
        if not AREA_FOLDER_RE.match(folder) or not AREA_CODE_RE.match(code) or code in seen_codes:
            raise DomainError(f"bad area id: {folder}/{code}")
        seen_codes.add(code)
        area = AreaSpec(folder, code, str(a.get("title", "")).strip(), str(a.get("overview", "")).strip(),
                        [str(x) for x in a.get("business_rules", [])], [str(x) for x in a.get("ui_flow", [])])
        if not area.title or not area.overview:
            raise DomainError(f"area {folder}: title or overview missing")
        for n, s in enumerate(a.get("stories", []), start=1):
            prio = s.get("priority", "Medium")
            area.stories.append(StorySpec(
                id=f"US-{code}-{n:02d}", area=folder, title=str(s.get("title", "")).strip(),
                priority=prio if prio in PRIORITIES else "Medium",
                user_input="Yes" if s.get("user_input") == "Yes" else "No",
                goal=str(s.get("goal", "")).strip(), screen=str(s.get("screen", "")).strip(),
                route="/" + str(s.get("route", folder)).strip().lstrip("/")))
            if not area.stories[-1].title:
                raise DomainError(f"area {folder}: a story has no title")
        areas.append(area)
    if len(areas) != n_areas:
        raise DomainError(f"expected {n_areas} areas, got {len(areas)}")
    total = sum(len(a.stories) for a in areas)
    if total != n_stories:
        raise DomainError(f"expected {n_stories} stories, got {total}")
    return Domain(domain_cfg["id"], domain_cfg["sector"], domain_cfg["split"], app, roles, areas)


def _strip_fence(text: str) -> str:
    t = text.strip()
    if t.startswith("```"):
        t = t.split("\n", 1)[1] if "\n" in t else ""
        if t.rstrip().endswith("```"):
            t = t.rstrip()[:-3]
    return t.strip()


def parse_json_list(text: str, count: int) -> list[str]:
    """A JSON array of ``count`` non-empty strings (question stages)."""
    try:
        data = json.loads(_strip_fence(text))
    except json.JSONDecodeError as exc:
        raise DomainError(f"not JSON: {exc}") from exc
    if not isinstance(data, list) or not all(isinstance(x, str) and x.strip() for x in data):
        raise DomainError("not a list of non-empty strings")
    if len(data) != count:
        raise DomainError(f"expected {count} items, got {len(data)}")
    return [unicodedata.normalize("NFC", " ".join(x.split())) for x in data]


# ---------------------------------------------------------------- V02: the story contract

def check_story(md: str, story_id: str, title: str | None = None) -> list[str]:
    """Problems of a story file against development.md 6 (headings, fields, 1 to 8 criteria, 3 to 5 questions per
    language, at most 80 lines). An empty list means the story is valid."""
    problems: list[str] = []
    md = md.replace("\r\n", "\n").rstrip("\n")
    lines = md.split("\n")
    if len(lines) > MAX_STORY_LINES:
        problems.append(f"{len(lines)} lines, at most {MAX_STORY_LINES}")
    m = re.match(r"^# (US-[A-Z]{2,8}-\d{2}): (.+)$", lines[0] if lines else "")
    if not m or m.group(1) != story_id:
        problems.append("first line must be '# {story id}: {Title}'")
    elif len(m.group(2)) > 60:
        problems.append("title longer than 60 characters")
    h2 = [l[3:].strip() for l in lines if l.startswith("## ")]
    if h2 != STORY_HEADINGS:
        problems.append(f"headings are {h2}, expected {STORY_HEADINGS}")
        return problems
    sections = _split_h2(lines)
    fields = dict(re.findall(r"^\| (Priority|Feature|User input) \| (.+?) \|$", "\n".join(lines[:sections['Story'][0] - 1]), re.M))
    if fields.get("Priority") not in PRIORITIES:
        problems.append("Priority missing or outside the closed vocabulary")
    code = story_id.split("-")[1]
    if not re.fullmatch(rf"F-{code}-\d{{2}}", fields.get("Feature", "")):
        problems.append("Feature missing or malformed")
    if fields.get("User input") not in ("Yes", "No"):
        problems.append("User input must be Yes or No")
    story = "\n".join(sections["Story"][1])
    if not re.search(r"^\*\*As an? \*\*|^\*\*As an?\*\* ", story, re.M) or "**I want**" not in story or "**So that**" not in story:
        problems.append("Story lines must start with **As a**, **I want**, **So that**")
    crit = [l for l in sections["Acceptance Criteria"][1] if re.match(r"^\d+\. ", l)]
    if not 1 <= len(crit) <= 8:
        problems.append(f"{len(crit)} acceptance criteria, expected 1 to 8")
    ac_rows = [l for l in sections["Access Control"][1] if l.startswith("|") and not re.match(r"^\|[-| ]+\|$", l)][1:]
    if not ac_rows:
        problems.append("Access Control has no rows")
    for r in ac_rows:
        cells = [c.strip() for c in r.strip("|").split("|")]
        if len(cells) != 2 or cells[0] not in ROLES or cells[1] not in ("Yes", "No"):
            problems.append(f"Access Control row invalid: {r}")
    qs = sections["Questions users ask"][1]
    h3 = [l[4:].strip() for l in qs if l.startswith("### ")]
    if h3 != ["en", "it"]:
        problems.append("Questions users ask needs ### en then ### it")
    else:
        cur, counts = None, {"en": 0, "it": 0}
        for l in qs:
            if l.startswith("### "):
                cur = l[4:].strip()
            elif cur and re.match(r"^\d+\. .+", l):
                counts[cur] += 1
        for lang, n in counts.items():
            if not 3 <= n <= 5:
                problems.append(f"{n} questions under {lang}, expected 3 to 5")
    return problems


def _split_h2(lines: list[str]) -> dict[str, tuple[int, list[str]]]:
    out: dict[str, tuple[int, list[str]]] = {}
    cur = None
    for i, l in enumerate(lines):
        if l.startswith("## "):
            cur = l[3:].strip()
            out[cur] = (i + 1, [])
        elif cur is not None:
            out[cur][1].append(l)
    return out


def story_questions(md: str) -> dict[str, list[str]]:
    """The ``Questions users ask`` of a valid story, by language."""
    res: dict[str, list[str]] = {"en": [], "it": []}
    sec = _split_h2(md.replace("\r\n", "\n").split("\n")).get("Questions users ask", (0, []))[1]
    cur = None
    for l in sec:
        if l.startswith("### "):
            cur = l[4:].strip()
        elif cur in res and re.match(r"^\d+\. ", l):
            res[cur].append(l.split(". ", 1)[1].strip())
    return res


# ---------------------------------------------------------------- V03 (built-in part): page structure

_FORBIDDEN = [(re.compile(r"<[a-zA-Z/!][^>]*>"), "HTML"), (re.compile(r"`"), "code"), (re.compile(r"https?://|www\."), "external URL"),
              (re.compile(r"^### ", re.M), "H3"), (re.compile(r"(?<![*\w])_[^_\s][^_]*_(?!\w)"), "italics")]


def check_page(md: str, lang: str, kind: str) -> list[str]:
    """Structure of a page body against manual.md 2.4 and 2.5 (the subset of M03/M04 that needs no frontend)."""
    problems: list[str] = []
    _fm, body = strip_front_matter(md)
    clean = body.replace(DRAFT_MARKER, "")
    for rx, what in _FORBIDDEN:
        if rx.search(clean):
            problems.append(f"{what} is outside the Markdown subset")
    try:
        page = parse_page(md, lang)
    except ValueError as exc:
        return problems + [str(exc)]
    if not page.intro:
        problems.append("missing introduction")
    keys = [k for k, _b in page.sections]
    if kind in ("home", "area"):
        if keys:
            problems.append("home and area pages have only the H1 and the introduction")
        return problems
    order = [KEY_ORDER.index(k) for k in keys]
    if order != sorted(order) or len(set(keys)) != len(keys):
        problems.append("sections out of order or repeated")
    if "steps" not in keys:
        problems.append("missing Steps section")
    if page.body_lines > 150:
        problems.append("more than 150 body lines")
    for k, body_lines in page.sections:
        if k == "steps":
            items = [l for l in body_lines if re.match(r"^\d+\. ", l)]
            if not items or len(items) > 12:
                problems.append(f"steps has {len(items)} items, expected 1 to 12")
            if [int(l.split('.')[0]) for l in items] != list(range(1, len(items) + 1)):
                problems.append("steps numbers are not in sequence")
        if k == "problems":
            hdr = next((l for l in body_lines if l.startswith("|")), "")
            cols = tuple(c.strip() for c in hdr.strip("|").split("|"))
            if cols != PROBLEM_COLUMNS[lang]:
                problems.append(f"problems table columns are {cols}")
    return problems


# ---------------------------------------------------------------- the repository tree of a domain (5.2)

def front_matter(kind: str, lang: str, page_id: str, source: str, source_md: str | None, routes: list[str]) -> str:
    sh = git_blob_id(source_md.encode("utf-8")) if source_md is not None else "none"
    return "\n".join(["---", "schema_version: 1", f"kind: {kind}", f"lang: {lang}", f"page_id: {page_id}", f"source: {source}",
                      f"source_hash: {sh}", "steps_from: none", "steps_hash: none", f"routes: [{', '.join(routes)}]",
                      "contexts: []", "screenshots: []", "---", ""])


def area_index_md(domain: Domain, area: AreaSpec) -> str:
    rows = "\n".join(f"| [{s.id}](user-stories/{s.id}.md) | {s.title} | {s.priority} |" for s in area.stories)
    rules = "\n".join(f"{i}. {r}" for i, r in enumerate(area.business_rules, 1)) or "None."
    flow = "\n".join(f"{i}. {r}" for i, r in enumerate(area.ui_flow, 1)) or "None."
    access = "\n".join(f"| {r} | Yes |" for r in domain.roles)
    return (f"# {area.title} - User Stories\n\n## Overview\n\n{area.overview}\n\n## Stories\n\n| ID | Title | Priority |\n|----|-------|----------|\n{rows}\n\n"
            f"## Business Rules\n\n{rules}\n\n## UI Flow\n\n{flow}\n\n## Access Control\n\n| Role | Allowed |\n|------|---------|\n{access}\n\n"
            f"## Open Questions\n\nNone.\n\n## Files\n\nNone.\n")


def bench_md(domain: Domain) -> str:
    """``docs/manual/bench.md`` of manual.md 8.2: the out-of-scope questions of the domain, no extras."""
    def block(lang: str) -> str:
        qs = domain.bench_oos.get(lang, [])[:12]
        return "\n".join(f"{i}. {q}" for i, q in enumerate(qs, 1)) or "None."
    return f"# Help bench\n\n## Out of scope\n\n### en\n\n{block('en')}\n\n### it\n\n{block('it')}\n\n## Extra\n\nNone.\n"


def write_tree(domain: Domain, root: Path) -> None:
    """Write ``docs/areas`` and ``docs/manual`` of the domain under ``root`` (no images, no test cases)."""
    reg = "\n".join(f"| `{a.folder}` | `{a.code}` | {a.title} |" for a in domain.areas)
    atomic_write_text(root / "docs" / "areas" / "registry.md",
                      f"# Area registry\n\n| Area folder | Area code | Title |\n|-------------|-----------|-------|\n{reg}\n")
    for a in domain.areas:
        atomic_write_text(root / "docs" / "areas" / a.folder / "userstories.md", area_index_md(domain, a))
        for s in a.stories:
            if s.md is not None:
                atomic_write_text(root / "docs" / "areas" / a.folder / "user-stories" / f"{s.id}.md", s.md.rstrip("\n") + "\n")
    atomic_write_text(root / "docs" / "manual" / "bench.md", bench_md(domain))
    for lang, pages in sorted(domain.pages.items()):
        for page_id, body in sorted(pages.items()):
            _fm, text = strip_front_matter(body)
            if page_id == "home":
                kind, rel, src, smd, routes = "home", "index.md", "none", None, []
            elif "/" in page_id:
                area_folder, sid = page_id.split("/", 1)
                story = next((s for s in domain.stories() if s.id == sid), None)
                kind, rel = "story", f"{area_folder}/{sid}.md"
                src, smd, routes = f"docs/areas/{area_folder}/user-stories/{sid}.md", story.md if story else None, [story.route] if story else []
            else:
                kind, rel, src, smd, routes = "area", f"{page_id}/index.md", f"docs/areas/{page_id}/userstories.md", None, []
                a = next((x for x in domain.areas if x.folder == page_id), None)
                smd = area_index_md(domain, a) if a else None
            atomic_write_text(root / "docs" / "manual" / lang / rel, front_matter(kind, lang, page_id, src, smd, routes) + text.strip("\n") + "\n")
