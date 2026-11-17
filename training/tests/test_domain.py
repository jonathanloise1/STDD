"""Story contract (V02), page structure (V03 built-in part), domain stage parsing and the written tree."""
import json

import pytest

from training.assistant import domain as D
from training.assistant.sections import DRAFT_MARKER

STORY = """# US-TASK-01: Create a task

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-TASK-01 |
| User input | Yes |

## Story

**As a** team member
**I want** to add a task to the list
**So that** I remember to do it

## Acceptance Criteria

1. The user sees the "New Task" button.
2. The task appears in the list.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Viewer | No |

## Business Rules

None.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| Access was removed | "You no longer have access to this organization." | Ask an admin |

## Non-Goals

- Bulk changes are not supported.

## Questions users ask

### en

1. how do i put a new thing on the team list
2. my list is empty
3. where do i write what needs doing

### it

1. come aggiungo una cosa da fare
2. la mia lista è vuota
3. dove scrivo cosa devo fare
"""


def test_valid_story_passes():
    assert D.check_story(STORY, "US-TASK-01") == []
    assert D.story_questions(STORY)["it"][1] == "la mia lista è vuota"


@pytest.mark.parametrize("mutate,fragment", [
    (lambda s: s.replace("## Non-Goals\n\n- Bulk changes are not supported.\n\n", ""), "headings"),
    (lambda s: s.replace("# US-TASK-01:", "# US-TASK-02:"), "first line"),
    (lambda s: s.replace("| Priority | High |", "| Priority | Urgent |"), "Priority"),
    (lambda s: s.replace("| Feature | F-TASK-01 |", "| Feature | F-OTHR-01 |"), "Feature"),
    (lambda s: s.replace("**So that**", "**Because**"), "Story lines"),
    (lambda s: s.replace("1. The user sees", "x. The user sees").replace("2. The task appears in the list.\n", ""), "acceptance criteria"),
    (lambda s: s.replace("| Admin | Yes |", "| Boss | Yes |"), "Access Control row"),
    (lambda s: s.replace("3. where do i write what needs doing\n", ""), "2 questions under en"),
    (lambda s: s.replace("### it", "### fr"), "### en then ### it"),
    (lambda s: s + "\n" * 0 + "x\n" * 80, "lines"),
])
def test_story_violations(mutate, fragment):
    problems = D.check_story(mutate(STORY), "US-TASK-01")
    assert problems and any(fragment in p for p in problems), problems


def test_story_with_more_than_five_questions_fails():
    s = STORY.replace("3. where do i write what needs doing\n", "3. a\n4. b\n5. c\n6. d\n")
    assert any("6 questions under en" in p for p in D.check_story(s, "US-TASK-01"))


PAGE = """# Create a task

Use this page to add a task to the list.

## Steps

1. Open the Tasks page.
2. Click "New Task".

## If something goes wrong

| What you see | What to do |
|---|---|
| "No access." | Ask an admin |
"""


def test_valid_page_passes_and_kinds():
    assert D.check_page(PAGE, "en", "story") == []
    assert D.check_page("# Home\n\nWelcome.\n", "en", "home") == []
    assert D.check_page(DRAFT_MARKER + "\n" + PAGE, "en", "story") == []


@pytest.mark.parametrize("page,kind,fragment", [
    (PAGE.replace("## Steps", "## Passi"), "story", "unknown heading"),
    (PAGE.replace("## Steps\n\n1. Open the Tasks page.\n2. Click \"New Task\".\n\n", ""), "story", "missing Steps"),
    (PAGE + "\n<b>bold</b>\n", "story", "HTML"),
    (PAGE + "\nSee https://example.org\n", "story", "external URL"),
    (PAGE + "\n### Sub\n", "story", "H3"),
    (PAGE.replace("| What you see | What to do |", "| Seen | Do |"), "story", "columns"),
    ("# Home\n\nWelcome.\n\n## Steps\n\n1. x\n", "home", "only the H1"),
    ("Welcome.\n", "home", "missing H1"),
    (PAGE.replace("2. Click \"New Task\".", "3. Click \"New Task\"."), "story", "sequence"),
])
def test_page_violations(page, kind, fragment):
    problems = D.check_page(page, "en", kind)
    assert problems and any(fragment in p for p in problems), problems


def test_page_with_thirteen_steps_fails():
    steps = "\n".join(f"{i}. Do {i}." for i in range(1, 14))
    assert any("expected 1 to 12" in p for p in D.check_page(f"# T\n\nIntro.\n\n## Steps\n\n{steps}\n", "en", "story"))


DOMAIN_JSON = {"app_name": "Quill", "roles": ["Admin", "Bogus"], "areas": [
    {"folder": "tasks", "code": "TASK", "title": "Tasks", "overview": "o", "business_rules": [], "ui_flow": [],
     "stories": [{"title": "Create a task", "priority": "High", "user_input": "Yes", "goal": "g", "screen": "Tasks", "route": "tasks"},
                 {"title": "Edit a task", "priority": "Bogus", "user_input": "No", "goal": "g", "screen": "Tasks", "route": "/tasks"}]}]}
CFG = {"id": "d99", "sector": "s", "split": "train"}


def test_domain_response_parsing():
    d = D.parse_domain_response(json.dumps(DOMAIN_JSON), CFG, 1, 2)
    assert d.roles == ["Admin"] and [s.id for s in d.stories()] == ["US-TASK-01", "US-TASK-02"]
    assert d.stories()[1].priority == "Medium" and d.stories()[0].route == "/tasks"
    fenced = "```json\n" + json.dumps(DOMAIN_JSON) + "\n```"
    assert D.parse_domain_response(fenced, CFG, 1, 2).app_name == "Quill"


@pytest.mark.parametrize("data,areas,stories", [("not json", 1, 2), (json.dumps(DOMAIN_JSON), 2, 2), (json.dumps(DOMAIN_JSON), 1, 3)])
def test_domain_response_errors(data, areas, stories):
    with pytest.raises(D.DomainError):
        D.parse_domain_response(data, CFG, areas, stories)


def test_domain_response_rejects_bad_codes():
    bad = json.loads(json.dumps(DOMAIN_JSON))
    bad["areas"][0]["code"] = "t"
    with pytest.raises(D.DomainError, match="bad area id"):
        D.parse_domain_response(json.dumps(bad), CFG, 1, 2)


def test_json_list_parsing():
    assert D.parse_json_list('["a  b", "c"]', 2) == ["a b", "c"]
    for bad, n in (('["a"]', 2), ('[1]', 1), ('{"a":1}', 1), ('["", "x"]', 2), ("nope", 1)):
        with pytest.raises(D.DomainError):
            D.parse_json_list(bad, n)


def test_write_tree(tmp_path):
    d = D.parse_domain_response(json.dumps(DOMAIN_JSON), CFG, 1, 2)
    d.stories()[0].md = STORY
    d.pages = {"en": {"home": "# Quill\n\nWelcome.\n", "tasks": "# Tasks\n\nThe area.\n", "tasks/US-TASK-01": PAGE}}
    d.bench_oos = {"en": ["What is the capital of Australia?"], "it": []}
    D.write_tree(d, tmp_path)
    assert (tmp_path / "docs/areas/registry.md").exists()
    assert "[US-TASK-01](user-stories/US-TASK-01.md)" in (tmp_path / "docs/areas/tasks/userstories.md").read_text()
    page = (tmp_path / "docs/manual/en/tasks/US-TASK-01.md").read_text()
    assert page.startswith("---\nschema_version: 1\nkind: story\nlang: en\npage_id: tasks/US-TASK-01\nsource: docs/areas/tasks/user-stories/US-TASK-01.md\nsource_hash: ")
    assert "routes: [/tasks]" in page and "# Create a task" in page
    assert (tmp_path / "docs/manual/en/index.md").read_text().startswith("---\nschema_version: 1\nkind: home")
    bench = (tmp_path / "docs/manual/bench.md").read_text()
    assert "1. What is the capital of Australia?" in bench and "### it\n\nNone." in bench
