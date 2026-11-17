# MyApp - Development Guidelines

> The STDD contracts of this template: how features are specified, tested, traced and checked.
> This document is the single definition of every format below. Current format versions are in
> [docs/contracts.json](../contracts.json).

**Two readers.** A developer new to the method should read sections 1, 2 and 14 first, then the section for the
file being written. An AI agent applies sections 3 to 13 literally: headings, field names and vocabularies are
exact strings. Where this document says "exactly", a validator checks it.

---

## 1. Principles

### 1.1 Philosophy

> "Specs and tests are the asset. Code is a commodity."

User stories say what the user sees and does. Test cases say how that is verified. Code is replaceable as long
as it passes the tests that come from the test cases. Everything downstream (user manual, help, operations
signatures) is generated from the stories and test cases, so they must be regular enough to be parsed.

| Goal | Means |
|------|-------|
| Traceability | Every test case links to one story; every test and code unit carries the ids it serves |
| Automation | Test cases become E2E tests through page objects |
| Living documentation | Stories and test cases describe the current behaviour, never a past or planned one |
| Machine-checkable | Fixed headings, fixed field names, closed vocabularies, architecture tests (section 12) |

### 1.2 Single source of truth

| Information | Where it lives |
|-------------|----------------|
| Tech stack, naming conventions, cross-cutting technical constraints | `docs/guidelines/architecture.md` |
| UI and UX rules | `docs/guidelines/frontend.md` |
| User manual, screenshots, help pack, in-page assistant, bench | `docs/guidelines/manual.md` |
| Assistant protocol, evaluation, training, model lock | `docs/guidelines/models.md` |
| Log call sites, signatures, runbooks, monitor, investigator | `docs/guidelines/operations.md` |
| Formats, ids, test levels, traceability rules | This document |
| Current format versions | `docs/contracts.json` |
| Features and their status | `docs/roadmap.md` |
| What a feature does for the user | `docs/areas/{area}/user-stories/US-{AREA}-NN.md` |
| How it is verified | `docs/areas/{area}/test-cases/{AREA}-{ACTION}-NNN.md` |
| How it is built | The codebase |

Reference other documents; never copy their content.

### 1.3 Documentation does not describe code

- No code blocks in area documents. Templates in this guideline are the only exception.
- No source file paths, class names, endpoints or table names in area documents. The link runs the other way: code and
  tests carry the story and test case ids (section 11), so a search for an id finds the implementation.

### 1.4 Language and encoding

- Documentation, code, comments, log messages and commit messages are in English.
- The application ships in English and Italian. UI text lives in the locale files.
- Documentation holds Italian in three places only: the `it` block of "Questions users ask" in a story
  (section 6.4), the user-facing pages under `docs/manual/it/`, and the bench questions in `docs/manual/bench.md`.
- UI text quoted in documents (labels, messages) is the English UI text, in double quotes, exactly as shown.
- Test cases about language name the English text and state that it is shown in Italian; elements are always named
  by their English label.
- Every Markdown file is UTF-8 without byte order mark, with LF line endings. Use `->` for arrows; do not paste
  arrow characters, check marks or box-drawing characters (rule R03).

---

## 2. Workflow and definition of done

### 2.1 Activity order

| # | Step | Output | Rule |
|---|------|--------|------|
| 1 | Requirements | Roadmap feature id; story files; area index updated | Sections 3, 5, 6, 7 |
| 2 | Test specification | Test case files; test suite updated | Sections 8, 9 |
| 3 | Implementation | Backend, frontend, page objects, tests, with story and test case tags; one log call site per log key | Sections 10, 11 |
| 4 | Verification | Unit and architecture tests, integration tests, E2E tests green (E2E runs need approval, see `CLAUDE.md`) | Sections 10, 12 |
| 5 | Review | Code review, manual QA, stakeholder acceptance | - |
| 6 | Manual and support readiness | Manual pages current, help questions answered, runbook per log key | `docs/guidelines/manual.md`, `docs/guidelines/models.md`, `docs/guidelines/operations.md` |

Do not implement behaviour that has no story and no test case. Do not change behaviour without changing the
story and test cases first.

### 2.2 Definition of done (per feature)

- [ ] Every story of the feature has a story file that passes the rules of section 12.
- [ ] Every story meets the coverage rules (section 8.6).
- [ ] Every test case is automated by at least one E2E test (`Automated by` has an `e2e/` item).
- [ ] Every Observable trace has a log call site in the backend and a runbook in the area's `operations.md`.
- [ ] Architecture tests pass with no new baseline entries (section 12.3).
- [ ] Code review, manual QA and stakeholder acceptance are done.
- [ ] Manual pages for the stories are regenerated and not stale; the help bench passes for the stories'
      questions (rules in `manual.md` and `models.md`).
- [ ] The roadmap status of the feature is `Done`.

---

## 3. Identifiers and registries

### 3.1 Id grammar

| Id | Pattern | Example | Rule |
|----|---------|---------|------|
| Area folder | `^[a-z]+(-[a-z]+)*$` | `tasks` | Listed in the area registry (3.2) |
| Area code | `^[A-Z]{2,8}$` | `TASK` | One per area, unique, never changed |
| Feature | `^F-[A-Z]{2,8}-\d{2}$` | `F-TASK-01` | Area code of the owning area; numbered from `01` per area |
| User story | `^US-[A-Z]{2,8}-\d{2}$` | `US-TASK-01` | Area code of the folder it lives in; numbered from `01` per area |
| Test case | `^[A-Z]{2,8}-[A-Z]{2,12}-\d{3}$` | `TASK-CREATE-001` | `{AREA}-{ACTION}-NNN`; `{AREA}` is the area code; `{ACTION}` names the user action in one English word, upper case (`CREATE`, `EDIT`, `LIST`); numbered from `001` per `{AREA}-{ACTION}` |
| Log key | `^[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(_[a-z]+)*$` | `task.create.not_allowed` | `{area}.{action}.{outcome}`: the area code and action of the defining test case in lower case, then the outcome in snake case (1 to 4 words) |
| Runbook anchor | log key with `.` and `_` replaced by `-` | `task-create-not-allowed` | Written as the H2 text of the runbook in `docs/areas/{area}/operations.md` (`operations.md` 5.4), so the GitHub anchor equals it. The heading is never the log key: GitHub drops the dots, and the anchor could no longer be derived |

- Numbers are assigned in sequence and never reused, even after a story or test case is deleted.
- A test case belongs to exactly one story. Several stories may use the same `{ACTION}`.
- A log key is defined by exactly one test case (its Observable trace) and emitted by exactly one log call site.

### 3.2 Area registry

Every folder under `docs/areas/` has one row. A project adds a row when it adds an area.

| Area folder | Area code | Title |
|-------------|-----------|-------|
| `audit` | `AUD` | Audit Trail |
| `authentication` | `AUTH` | Authentication |
| `dashboard` | `DASH` | Dashboard |
| `help` | `HELP` | Help |
| `onboarding` | `ONBOARD` | Onboarding |
| `organizations` | `ORG` | Organizations |
| `tasks` | `TASK` | Tasks |

### 3.3 Roadmap features

A feature is a deliverable in `docs/roadmap.md`. It groups one or more stories; each story names exactly one
feature in its `Feature` field. The roadmap does not list stories (the relation is derived from the stories).

The roadmap holds one table under the heading `## Features` with exactly these columns:

| ID | Feature | Area | Status |
|----|---------|------|--------|
| F-TASK-01 | Create, edit and delete tasks | tasks | Done |

`Area` is an area folder. `Status` is one of `Done`, `In Progress`, `Planned`, `Backlog`.

### 3.4 Role registry

The roles that may appear in an Access Control table, in this order:

| Role | Meaning |
|------|---------|
| `Visitor` | Not signed in |
| `Signed-in user` | Signed in, whatever their role in the current organization, or with no organization |
| `Admin` | Organization role: full access, manages the organization and its team |
| `Editor` | Organization role: creates, edits and deletes data |
| `Viewer` | Organization role: read-only access |

### 3.5 Closed vocabularies

| Field | Values |
|-------|--------|
| Priority | `Critical`, `High`, `Medium`, `Low` |
| User input | `Yes`, `No` |
| Allowed | `Yes`, `No` |
| Type | `Happy path`, `Validation`, `Business rule`, `Edge case`, `Error handling`, `UX`, `Security` |
| Severity | `Information`, `Warning`, `Error`, `Critical` |
| Roadmap status | `Done`, `In Progress`, `Planned`, `Backlog` |
| Step verbs | `Open`, `Click`, `Type`, `Clear`, `Select`, `Check`, `Uncheck`, `Upload`, `Drag`, `Press`, `Reload`, `Sign in`, `Sign out` |
| UI element names | `page`, `form`, `dialog`, `button`, `link`, `field`, `list`, `table`, `menu`, `tab`, `message` |

---

## 4. Area layout

```
docs/areas/{area}/
  userstories.md              # area index (section 5)
  testsuite.md                # test index (section 9)
  operations.md               # runbooks, format defined in docs/guidelines/operations.md (optional)
  user-stories/
    US-{AREA}-NN.md           # one file per story (section 6)
  test-cases/
    {AREA}-{ACTION}-NNN.md    # one file per test case (section 8)
```

- No other files or folders are allowed in an area.
- One file per story because each manual page stores a hash of its source story file. Any edit to a story
  file, including whitespace, makes its manual page stale.
- Every file in the area is linked from an index: story files from the Stories table, test case files from the
  Coverage Matrix, `testsuite.md` and `operations.md` from the Files table.

---

## 5. Area index

File: `docs/areas/{area}/userstories.md`. Exactly these headings, in this order. A section with nothing to say
contains the single line `None.`

```markdown
# {Area title} - User Stories

## Overview

{Two to four sentences: what the area lets users do and for whom.}

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-{AREA}-01](user-stories/US-{AREA}-01.md) | {Story title} | {Priority} |

## Business Rules

1. {Rule that applies to more than one story of the area.}

## UI Flow

1. {Screen name as shown in the UI}: {what the user does there and which screen comes next.}

## Access Control

| Role | Allowed |
|------|---------|
| {Role} | {Yes or No} |

## Open Questions

1. {Assumption or question still to be confirmed by the owner, with the story id it affects.}

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
```

- `{Area title}` is the Title from the area registry.
- Stories table: one row per story file, in id order; Title and Priority equal those in the story file.
- Access Control: who may enter the area at all. A role with `No` here must not be `Yes` in any story.
- Files table: the `operations.md` row is present only when the file exists.

---

## 6. User story file

File: `docs/areas/{area}/user-stories/US-{AREA}-NN.md`.

### 6.1 Template

Exactly these headings, in this order. `Business Rules`, `Error States` and `Non-Goals` contain `None.` when empty.

```markdown
# US-{AREA}-NN: {Title}

| Field | Value |
|-------|-------|
| Priority | {Priority} |
| Feature | F-{AREA}-NN |
| User input | {Yes or No} |

## Story

**As a** {role or persona}
**I want** {what the user wants to do}
**So that** {the benefit for the user}

## Acceptance Criteria

1. {One observable behaviour per item.}

## Access Control

| Role | Allowed |
|------|---------|
| {Role} | {Yes or No} |

## Business Rules

1. {A rule the user can notice through its effect.}

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| {What went wrong, in user terms} | "{Exact English message}" | {Next step for the user} |

## Non-Goals

- {Something a reader might expect that this story does not do.}

## Questions users ask

### en

1. {Question as an end user would type it.}

### it

1. {Domanda come la scriverebbe un utente.}
```

### 6.2 Field and section rules

| Element | Rule |
|---------|------|
| Title | Sentence case, starts with a verb, at most 60 characters, names the user goal ("Create a task") |
| Priority | Closed vocabulary (3.5) |
| Feature | An id in the roadmap Features table |
| User input | `Yes` when the user types or chooses values that the app checks; drives the Validation coverage rule |
| Story | Three lines, starting exactly with `**As a**` (or `**As an**`), `**I want**`, `**So that**` |
| Acceptance Criteria | Numbered, 1 to 8 items, each one sentence a non-technical user can verify on screen. Input validation messages belong here |
| Access Control | Rows are roles from the registry (3.4), in registry order; list only roles that matter for the story; at least one row |
| Business Rules | Numbered. A rule is a condition that changes what the user can do or see. Rules shared by several stories go in the area index |
| Error States | Situations where the action cannot be completed for a reason other than the user's input in the form (access lost, item removed, limit reached, service unavailable). Message is the exact English text in double quotes. Each row is covered by an Observable trace (8.5) |
| Non-Goals | Bullets. What is out of scope and might otherwise be assumed |
| Questions users ask | See 6.4 |

### 6.3 Size limits

| Limit | Value |
|-------|-------|
| Lines in the file | At most 80 |
| Acceptance criteria | 1 to 8 |
| Questions | 3 to 5 under `en`, 3 to 5 under `it` |

A story that exceeds a limit covers more than one user goal. Split it into stories that each cover one goal a
user completes in one sitting (for example "Manage tasks" becomes "Create a task", "Edit a task", "Delete a
task"); move rules shared by the new stories to the area index. Never drop observable behaviour to fit a limit.

### 6.4 Questions users ask

This block captures the user's vocabulary. Its questions feed help retrieval and the evaluation bench through a
computed split, per story and language, defined in `manual.md` 6.2: a question is either indexed (it enters the help
pack) or kept for the bench (it never does), so the bench does not measure a question retrieval has already seen.
The split is computed, never stored in the area. Neither set is ever training data.

- Three to five questions per language, under the exact sub-headings `### en` and `### it`.
- Phrase each question as an end user would ask it, in different words from the story: synonyms, symptoms,
  goals ("Why can't I save?" rather than "Title is required").
- No question contains the story title.
- Write the Italian questions as an Italian user would ask them; they are not translations of the English ones.
- Each question must be answerable from the story alone.

---

## 7. User story style guide

### 7.1 The rule

> If a non-technical user cannot observe it on screen, it is not an acceptance criterion.

Write what the user sees, clicks, types and reads. Describe consequences, never mechanisms.

### 7.2 User vocabulary

| Instead of | Write |
|------------|-------|
| authenticate, login API, session token | sign in, stay signed in |
| redirect, route | the {Screen} page opens |
| persist, store, save to the database | is saved, is kept |
| record, entity, row | the domain noun: task, organization, member |
| toast, snackbar, alert | message |
| modal, popup | dialog |
| 401 / 403 / 404 | the user is asked to sign in / sees "You don't have access to this page." / sees "This task no longer exists." |
| validation error | the {Field} field shows "{message}" |
| enum value `InProgress` | the label shown in the UI, "In progress" |

### 7.3 Banned content

These are checked by rule R08 in story files, area indexes and test case files.

| Banned | Detected by |
|--------|-------------|
| Code spans and code blocks | Any backtick (test cases: allowed only in the Log key value) |
| Endpoints and URL paths of the API | `/api/`, or `GET`, `POST`, `PUT`, `PATCH`, `DELETE` followed by a space and `/` |
| HTTP status codes | A three-digit number followed by a status phrase (`401 Unauthorized`), or the words "status code" |
| Type and layer names | PascalCase words of two or more capitalised parts ending in `Dto`, `Request`, `Response`, `Controller`, `Service`, `Repository`, `Validator`, `Middleware`, `Interceptor`, `Entity` |
| Source files | Words ending in `.cs`, `.ts`, `.tsx`, `.json`, `.sql` |
| Technical words | Whole words, case-insensitive: `API`, `endpoint`, `DTO`, `JSON`, `HTTP`, `JWT`, `token`, `claim`, `GUID`, `database`, `localStorage`, `backend`, `frontend`, `payload`, `middleware`, `migration` |
| Implementation sections | Any heading not in the template, such as "Implementation References" or "API Contract" |

A message that needs a banned word is a defect of the message: change the message.

### 7.4 Good and bad

| Bad | Good |
|-----|------|
| After login the frontend calls the sync endpoint. | After signing in, the user lands on the dashboard of their organization. |
| Returns 401 if the token is missing. | A visitor who opens any page of the app is asked to sign in first. |
| A new User record is created with the object identifier from the claims. | The first time someone signs in, they can use the app right away; there is no separate registration. |
| Tasks are sorted by creation date descending. | The newest task is at the top of the list. |
| The language is stored in the user's PreferredLanguage field. | The chosen language is kept the next time the user signs in, on any device. |
| Sync is idempotent. | Signing in again does not create a second account or duplicate the user's organizations. |
| Title max length 200 (validator). | A title longer than 200 characters cannot be saved; the Title field shows "Use at most 200 characters." |

### 7.5 Where technical constraints go instead

A technical constraint that matters is recorded, but not in a story. If the user can notice its effect, write the
effect in the story and the mechanism in the place below.

| Constraint | Where it goes |
|------------|---------------|
| API contract: routes, payloads, status codes | Integration tests (section 10) |
| Data model, tables, columns | The code; `architecture.md` when cross-cutting |
| Security mechanism: identity provider, tokens, impersonation | `architecture.md` |
| Rate limits, timeouts, sizes the user can hit | Effect in Error States or Business Rules; value in code and integration tests |
| Log keys, severity, runbooks | Observable trace of a test case (8.5) |
| Location of the implementation | Nowhere in docs: code carries the story tags (11.2) |
| Decisions not yet taken | Open Questions in the area index |

---

## 8. Test case file

File: `docs/areas/{area}/test-cases/{AREA}-{ACTION}-NNN.md`.

### 8.1 Template

Exactly these headings, in this order. `## Observable trace` is present if and only if Type is `Edge case` or
`Error handling`. `Test Data` contains `None.` when the test types nothing.

```markdown
# {AREA}-{ACTION}-NNN: {Title}

| Field | Value |
|-------|-------|
| User Story | US-{AREA}-NN |
| Type | {Type} |
| Priority | {Priority} |
| Automated by | {None or derived list, see 8.4} |

## Description

{One or two sentences: the behaviour this test case verifies.}

## Preconditions

- Signed in as {Role} of "{Organization}".
- {A fact provided by the seed data, in user terms.}

## Test Data

| Field | Value |
|-------|-------|
| {Field label as shown in the UI} | "{Value typed or chosen}" |

## Steps

1. {Verb} {target}.

## Expected Results

- {An outcome the user can see.}

## Observable trace

| Field | Value |
|-------|-------|
| Message | "{Exact English message}" |
| Log key | `{area}.{action}.{outcome}` |
| Severity | {Severity} |
| Runbook | [{anchor}](../operations.md#{anchor}) |
```

### 8.2 Types

| Type | Use when | Observable trace |
|------|----------|------------------|
| `Happy path` | The main flow of the story succeeds | No |
| `Validation` | The user's input is rejected with a field message, including length and format boundaries | No |
| `Business rule` | A rule of the story or the area changes the outcome | No |
| `Edge case` | The system meets an unusual condition that it handles and records: a limit is reached, data changed under the user, a dependency answers late | Required |
| `Error handling` | The action cannot be completed for a reason other than the user's input: access lost, item removed, service unavailable | Required |
| `UX` | Screen behaviour with no business outcome: loading indicator, empty state, confirmation dialog, focus, layout on small screens | No |
| `Security` | A role or visitor without access cannot see or do the action | No |

If a situation produces no log entry, it is not an Edge case: use `UX` (empty states, display limits) or
`Validation` (input limits).

### 8.3 Section rules

| Section | Rule |
|---------|------|
| Title | Sentence case, at most 70 characters, states the behaviour ("Saving without a title shows a field message") |
| Description | One or two sentences |
| Preconditions | Bullets. The first is `Signed in as {Role} of "{Organization}".`, `Signed in as "{Person}", who belongs to no organization.` or `Not signed in.` A second actor is declared as `A second session is signed in as {Role} of "{Organization}".` A service failure is declared as `The service does not answer when {situation}.` and provided by the fault switch (16.2). Every other bullet is a fact the seed data provides (section 10.3) |
| Test Data | Table with `Field` = UI label and `Value` = quoted text, or `None.` |
| Steps | Numbered. One user action per step, starting with a step verb (3.5). Steps of a second actor start with `As {Role}: ` followed by the verb. Name UI elements by their English label in double quotes and an element name from 3.5 (`Click the "Save" button.`). An icon-only button is named by its accessible name, which its story states; `Click the "Edit" button.` is then correct |
| Expected Results | Bullets. Only what the user sees: messages, screens, list contents, field states. "Nothing is saved" is verified on screen ("The task list does not show ..."). Results seen by a second actor start with `As {Role}: ` |

There is no API Calls section. Requests, status codes and stored data are checked by integration tests.

### 8.4 Automated by

`Automated by` is derived from the test tags (section 11.1); it is never written by judgement.

- Value: `None`, or a comma-separated list of `{level}/{ClassName}` items, where `{level}` is `e2e` or
  `integration`, for every test class whose `TestCase` tag equals this test case id.
- Order: `e2e` items first, then `integration` items; within a level, ordinal order of class name.
- Example: `e2e/TaskCreate005Tests, integration/TaskCreateApiTests`.
- A script writes the value; until it exists, authors write the value the rule derives. Rule R18 fails on any
  difference.

### 8.5 Observable trace

The bridge to operations: signatures and runbooks are generated from this block.

| Field | Value |
|-------|-------|
| Message | The exact English message the user sees, in double quotes; `None` only for an `Edge case` where the user sees no message |
| Log key | The stable key of the log entry written when the situation occurs, in backticks; grammar in 3.1 |
| Severity | `Information`: expected outcome caused by the user, no action. `Warning`: unexpected but handled; investigate if the rate rises. `Error`: the user could not complete the action. `Critical`: many users affected or data at risk |
| Runbook | Link to the runbook heading in the area's `operations.md`; link text and anchor are the runbook anchor (3.1) |

- Every Error States row of the story has its Message equal to the Message of at least one Observable trace of a
  test case of that story.
- The log call site convention and the runbook format are defined in `docs/guidelines/operations.md`.

### 8.6 Coverage rules per story

| When the story has | It needs at least |
|--------------------|-------------------|
| Always | One `Happy path` |
| `User input` = `Yes` | One `Validation` |
| Business Rules other than `None.` | One `Business rule` |
| Error States other than `None.` | One `Edge case` or `Error handling` per Error States row (matched by Message) |
| An Access Control row with `Allowed` = `No` | One `Security` |

Rule R11 checks the rows decided by Type. Rule R12 checks the Error States row, which is decided by Message: only
`Edge case` and `Error handling` test cases have an Observable trace (R13), so a matching trace is a test of the right
type.

---

## 9. Test suite

File: `docs/areas/{area}/testsuite.md`. Exactly these headings, in this order. It holds no run evidence: no
pass or fail status, no automation status, no dates, no totals.

```markdown
# {Area title} - Test Suite

## Coverage Matrix

| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [{AREA}-{ACTION}-NNN](test-cases/{AREA}-{ACTION}-NNN.md) | {Title} | US-{AREA}-NN | {Type} | {Priority} |

## Seed Data

- {What the area's part of the seed data contains, in user terms.}
```

- One row per test case file, ordered by User Story, then test case id.
- Title, User Story, Type and Priority equal those in the test case file.
- Seed Data contains `None.` when the area adds nothing to the shared seed data.

---

## 10. Test levels

### 10.1 Levels

| Level | Location | Drives | May | May not |
|-------|----------|--------|-----|---------|
| Unit | `tests/unit/MyApp.UnitTests/` | A class or function in isolation | Use test doubles; architecture tests read repository files (section 12) | Use network, database, browser or a running app |
| Integration | `tests/integration/MyApp.IntegrationTests/` | The backend over HTTP, with a real SQL Server (`IntegrationTests__ConnectionString`), on its own database `MyApp_Integration_Test` that the run drops, migrates and seeds at start (same guard as 10.3); tests never change the seed data | Use the impersonation header; assert status codes, payloads, stored state and log keys | Use a browser |
| E2E | `tests/e2e/MyApp.E2E/` | The UI in a browser, through page objects | Assert only what the user sees | Call the API, read or write the database, use locators outside page objects |
| Monitor | `tests/support/MyApp.Monitor.IntegrationTests/` | The monitor application (`operations.md` 14): classes in isolation under `Unit/`, its files, store and tools under `Integration/` | Use local files, a local store and listener, `world` fixtures, and the docs of this repository | Use a browser, a running backend or a real log source |

A test that calls the API without driving a page is an integration test, whatever folder it is in.

Unit, integration and E2E tests verify the product and trace to its stories. Monitor tests verify the operations
tooling, which no user story describes: they carry no story or test case tags (11.1) and never appear in
`Automated by` (8.4).

### 10.2 E2E structure

| Element | Location and rule |
|---------|-------------------|
| Page object | `PageObjects/{AreaPascal}/{Screen}Page.cs`, one per screen; exposes user actions and readable screen state; holds every locator |
| Shared page parts | `PageObjects/Common/` (base page, navigation) |
| Test class | `Tests/{AreaPascal}/{TestCasePascal}Tests.cs`; one class per test case; class name is the test case id in PascalCase plus `Tests` (`TASK-CREATE-005` -> `TaskCreate005Tests`) |
| Capture class | `Manual/{AreaPascal}/{Screen}Capture.cs`; reuses page objects to take the manual's screenshots |

`{AreaPascal}` is the area folder in PascalCase (`tasks` -> `Tasks`). Locators prefer accessible role, label
and text; use a test id only when the element has no accessible name.

### 10.3 Preconditions in E2E tests

- Preconditions come from the seed data, from UI steps of the test itself or, for
  `The service does not answer when {situation}.`, from the Development-only fault switch (16.2). A test never
  creates them through the API or the database.
- The seed data is the deterministic dataset every E2E run starts from. The run uses a dedicated database (name
  ending in `_E2E` or `_Test`) that the backend drops, migrates and seeds at start, only in Development and only
  with `MyApp:Testing:ResetDatabaseOnStart=true`; test classes never load it. It contains one user per organization role of "MyApp Dev Org", plus the
  users named in each area's Seed Data (`Auth/TestUsers.cs`), and the data each area describes in the Seed Data
  section of its `testsuite.md`.
- A test that changes data uses unique values (for example a title with a unique suffix) or restores the state
  through the UI.

### 10.4 Screenshots for the manual

Screenshots come from capture classes, not from tests. Capture classes reuse the E2E page objects, run with
seed data and a fixed clock, and skip themselves in a normal run, where `E2E__ManualLanguage` is not set. Running them
regenerates the manual and needs approval (`CLAUDE.md`). Their rules are in `docs/guidelines/manual.md`; the commands are in
`tests/e2e/MyApp.E2E/README.md`.

---

## 11. Traceability tags in code and tests

### 11.1 Test tags (C#, NUnit)

Tags are NUnit attributes on the test class. Tags on methods are not allowed. Ids in names or descriptions are
not read by any tool.

| Attribute | Unit | Integration | E2E | Capture | Monitor |
|-----------|------|-------------|-----|---------|---------|
| `[Property("UserStory", "US-{AREA}-NN")]` | Optional, any number | Required, one or more | Required, exactly one: the test case's story | Required, one or more | Not allowed |
| `[Property("TestCase", "{AREA}-{ACTION}-NNN")]` | Not allowed | Optional, any number | Required, exactly one | Not allowed | Not allowed |
| `[Category("{area folder}")]` | Optional | Required, one or more | Required, exactly one: the test case's area | Required, one or more, plus `[Category("manual-capture")]` | Not allowed |

A `Category` is a tag only when it names an area folder. Any other category (`Smoke`) is a selection label: it is
allowed at every level, on classes and methods, and no rule reads it. `manual-capture` is reserved for capture
classes, because it takes a test out of the normal run (10.4).

```csharp
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-005")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate005Tests : AuthenticatedTestBase
```

### 11.2 Story-id comments in application code

- Form: a line comment holding only story ids, comma-separated: `// US-TASK-01` or `// US-TASK-01, US-TASK-02`.
  In JSX: `{/* US-TASK-01 */}`. Nothing else on the line: titles drift, ids do not.
- Placement: the line immediately above the declaration. Attributes belong to the declaration, so the comment may
  stand above them; an XML doc comment goes above the story comment. In JSX, the line immediately above the element.
- Backend: controller actions, public service methods, validators, background jobs.
- Frontend: page components, API service functions, submit handlers.
- Story ids appear in `src/` only in this form. The XML form `<userstory ref="..." />` is replaced by it.
- Under `tests/`, a story id may appear in any form and must name a story file.
- Test data and tooling are exempt from both, because their ids are data, sometimes invalid on purpose: `*.test.ts`
  and `*.test.tsx` files, `Fixtures/` and `fixtures/` folders, `src/frontend/scripts/`,
  `tests/unit/MyApp.UnitTests/Rules/` and `tests/support/`.
- Log call sites carry their key as a constant in `MyApp.Application/Logging/{AreaPascal}Log.cs` (`operations.md`
  3.1). Where that class name collides with a domain type, the class takes the area title in PascalCase instead
  (`audit` -> `AuditTrailLog.cs`). The constant is the only string literal of the key under `src/backend/`; the call
  site calls the generated method and any other code refers to the constant (rule R15).

---

## 12. Traceability rules and baseline

### 12.1 Where they run

The rules are architecture tests in `tests/unit/MyApp.UnitTests/Architecture/ArchitectureTests.cs`, one test per rule, named
after the rule id. They read the repository files only. Rule ids are stable: a removed rule's id is never reused.
`tools/MyApp.DocTools` implements them; its `check` command runs them all from the command line
(`dotnet run --project tools/MyApp.DocTools -- check`), and `sync-automated-by` rewrites the field that R18 checks. The operations
checks O01 to O10 are separate (`operations.md` 16).

### 12.2 Rules

Each finding has a subject: a path relative to the repository root, with `/` separators. A defect belongs to one
rule: R13 owns the Observable trace table and R06 every other table; R12 owns the Error States row of 8.6 and R11
the other rows.

| Id | Name | Check | Subject |
|----|------|-------|---------|
| R01 | Area registry | The folders under `docs/areas/` and the rows of the area registry (3.2) are the same set | Area folder |
| R02 | Allowed files | An area contains only the files of section 4 | File |
| R03 | Encoding | Every `docs/**/*.md` file is UTF-8 without BOM, has LF line endings, no U+FFFD character and none of the symbols banned by 1.4: arrow characters (U+2190 to U+21FF), box-drawing characters (U+2500 to U+257F), check and cross marks | File |
| R04 | Required headings | Area index, story, test case and test suite have exactly their headings, in order (sections 5, 6, 8, 9); the roadmap has a `## Features` section holding the table of 3.3 | File |
| R05 | Ids and file names | The H1 id matches its grammar (3.1), equals the file name, and its area code is the registry code of the folder | File |
| R06 | Fields and vocabularies | Every table except the Observable trace (R13) has exactly its columns or fields in order: story and test case fields, Access Control, Stories, Files, Coverage Matrix, Error States, Test Data, roadmap Features. Values are in the closed vocabularies (3.5); roles follow the registry and its order; `Feature` exists in the roadmap; a role with `No` in the area index is not `Yes` in a story. Steps start with a step verb; the first precondition, a second session and a service failure have the forms of 8.3 | File |
| R07 | Story size | At most 80 lines; 1 to 8 acceptance criteria; 3 to 5 questions per language; no question contains the story title | Story file |
| R08 | Banned content | No banned content (7.3) in area indexes, story files and test case files | File |
| R09 | Indexes complete | Stories table rows equal the story files, Coverage Matrix rows equal the test case files, with equal titles, stories, types and priorities; the Files table lists `testsuite.md` and, if present, `operations.md`; every link resolves | Index file |
| R10 | Test case to story | `User Story` names a story file in the same area | Test case file |
| R11 | Coverage | Each story meets the coverage rules of 8.6 decided by Type: `Happy path`, `Validation`, `Business rule`, `Security` | Story file |
| R12 | Error States covered | The Error States rule of 8.6: each Error States Message equals the Message of an Observable trace of a test case of the same story | Story file |
| R13 | Observable trace complete | Present if and only if Type is `Edge case` or `Error handling`; columns `Field`, `Value`; exactly the four fields in order, each valid; log key area and action equal the test case's; log key unique across all test cases; Runbook link target is `../operations.md#{anchor derived from the log key}` | Test case file |
| R14 | Runbook exists | The area's `operations.md` exists and has an H2 whose text is the runbook anchor (3.1) | Test case file |
| R15 | Log key emitted | Comments do not count. The log key appears as a string literal at exactly one place under `src/backend/`, a constant in the area's log class (11.2); a `LoggerMessage` method of that class has the constant as its `EventName`; that method is called at exactly one place under `src/backend/`, or is marked `[ClientReported]` and called nowhere, the dispatcher being its call site (`operations.md` 3.6) | Test case file |
| R16 | Test tags resolve | Every `TestCase` tag names a test case file; every `UserStory` tag names a story file; an E2E test's `UserStory` equals its test case's User Story; no tags on methods (a `Category` is a tag only when it names an area folder) | Test file |
| R17 | Tag multiplicity and naming | Tags follow the table of 11.1 for the test's level, counting as `Category` tags only those that name an area folder; `manual-capture` is only on capture classes; E2E class name and folder follow 10.2 | Test file |
| R18 | Automated by derived | `Automated by` equals the value derived from the tags (8.4) | Test case file |
| R19 | Done features automated | Every test case of a story whose feature is `Done` in the roadmap has at least one `e2e/` item in `Automated by` | Test case file |
| R20 | E2E drives the UI | Every E2E test class uses at least one page object; it does not call the API (`APIRequest`, `HttpClient`), the database, or locator methods directly | Test file |
| R21 | Capture classes | Every class under `Manual/` uses at least one page object, has no `TestCase` tag, and has the `manual-capture` category | Test file |
| R22 | Story ids in code | Outside test data (11.2), every story id under `src/` and `tests/` names a story file; under `src/` story ids appear only in the comment form of 11.2, and the next line starts a declaration: it is not blank, a comment, an import, a statement or a plain call | Source file |
| R23 | Contract versions | `docs/contracts.json` parses, lists exactly the formats of section 13, each with an integer version, and each `spec` anchor resolves to a heading of the guideline it names | `docs/contracts.json` |

### 12.3 Baseline

The rules are switched on before the content is perfect. Known findings are recorded in
`tests/unit/MyApp.UnitTests/Architecture/baseline.json`, and the baseline may only shrink.

```json
{
  "findings": [
    { "rule": "R14", "subject": "docs/areas/tasks/test-cases/TASK-CREATE-005.md" }
  ]
}
```

| Rule | Effect |
|------|--------|
| A finding listed in the baseline | Reported, does not fail |
| A finding not listed | Fails |
| A baseline entry with no matching finding | Fails as stale: remove the entry in the same change that fixed it |
| An entry not present in the target branch's baseline | Fails when the check names that branch, unless section 12.2 of that branch does not list its rule yet |

Entries are sorted by rule, then subject. Never add an entry to make a change pass.

To compare with a target branch, pass it: `dotnet run --project tools/MyApp.DocTools -- check --target-ref origin/{branch}`. The
command reads that branch's baseline and section 12.2 with read-only git commands. A branch with no baseline file
is a first adoption and is not compared.

---

## 13. Contract versions

Every format has an integer version. `docs/contracts.json` is the one machine-readable place that states the current
versions, and it lists exactly the formats below (rule R23). The guideline named in the table defines each one.

| Format key | Defined in |
|------------|------------|
| `identifiers` | Section 3 (ids, registries, vocabularies) |
| `roadmap-features` | Section 3.3 |
| `area-layout` | Section 4 |
| `area-index` | Section 5 |
| `user-story` | Sections 6 and 7.3 |
| `test-case` | Section 8 |
| `test-suite` | Section 9 |
| `code-tags` | Section 11 |
| `baseline` | Section 12.3 |
| `manual-page` | `manual.md` section 2 |
| `manual-captures` | `manual.md` section 4 |
| `help-pack` | `manual.md` section 6 |
| `assistant-retrieval` | `manual.md` section 6.5 |
| `help-gap` | `manual.md` section 7.4 |
| `help-bench` | `manual.md` section 8 |
| `assistant-protocol` | `models.md` section 3 |
| `assistant-example` | `models.md` section 5.9 |
| `eval-report` | `models.md` section 4.6 |
| `model-lock` | `models.md` section 8 |
| `model-card` | `models.md` section 8.3 |
| `log-call-site` | `operations.md` section 3 |
| `log-file` | `operations.md` section 3.4 |
| `error-response` | `operations.md` section 4.2 |
| `runbook` | `operations.md` section 5.4 |
| `signature-record` | `operations.md` section 5.3 |
| `tool-catalog` | `operations.md` section 7.1 |
| `entity-binding` | `operations.md` section 7.2 |
| `masking-policy` | `operations.md` section 8.1 |
| `constraints` | `operations.md` section 9 |
| `incident-report` | `operations.md` section 10.1 |
| `investigator-protocol` | `operations.md` section 11.3 |
| `profile` | `operations.md` section 12.1 |
| `ops-pack` | `operations.md` section 13 |

Every repository file always conforms to the current version; files do not carry a version of their own, except
where the defining guideline gives a generated artifact or a file under `docs/support/` a `schema_version` field.

**Changing a format is a breaking change** for generators, validators and the models trained on these formats.
A change that only rewords a guideline without changing what is valid is not a format change. For a format
change, in one pull request:

1. Bump the format's version in `docs/contracts.json` and update the guideline that defines it.
2. Migrate every file in the repository to the new format.
3. Update the architecture tests, generators and validators; regenerate derived artifacts (manual, packs).
4. Record the change for the model owners: a format change is a retraining trigger, and the evaluation must be
   run again before release (`docs/guidelines/models.md`).

---

## 14. Worked example

A format model, copied from the Tasks area. The files under `docs/areas/tasks/` are the authority if they have moved
on since.

### 14.1 Story file: `docs/areas/tasks/user-stories/US-TASK-01.md`

```markdown
# US-TASK-01: Create a task

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-TASK-01 |
| User input | Yes |

## Story

**As a** team member
**I want** to add a task with a title
**So that** the team can see what needs to be done

## Acceptance Criteria

1. On the Tasks page, Admins and Editors see the "New Task" button; it opens a form above the list.
2. The form has the fields Title, Due Date and Description; only Title is required.
3. Clicking "Create Task" with the Title field empty keeps the form open and the Title field shows "Enter a title."
4. The Title field accepts at most 200 characters and the Description field at most 2000; further typing is ignored.
5. After saving, the form closes and the new task is first in the list with status "Todo".
6. Clicking "Cancel" closes the form and no task is added.
7. Viewers do not see the "New Task" button.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | No |

## Business Rules

1. A new task always starts with status "Todo".

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The user's role was changed to Viewer while the form was open | "Your role does not allow this change." | Ask an admin of the organization for the Editor role |
| The service did not answer when the task was saved | "The service did not answer. Try again in a few minutes." | Click "Create Task" again later; the form keeps what was typed |

## Non-Goals

- Choosing who the task is assigned to.
- Creating several tasks at once.
- Attaching files to a task.

## Questions users ask

### en

1. How do I add something new to the team's to-do list?
2. Why won't the form let me save when the name is blank?
3. I can't find the button to add work. What am I missing?
4. Can I set a deadline when I make a new item?

### it

1. Come aggiungo un lavoro da fare per il mio team?
2. Perché non mi salva se non scrivo il titolo?
3. Non vedo il pulsante per inserire una nuova attività, perché?
4. Posso indicare entro quando va fatta?
```

The story needs at least: a Happy path (always), a Validation (User input is `Yes`), a Business rule (it has
rules), a Security test (Viewer is `No`), and for each Error States row an Edge case or Error handling test with the
same Message: "Your role does not allow this change." and "The service did not answer. Try again in a few minutes."
The test suite of the area therefore contains:

```markdown
| Test Case | Title | User Story | Type | Priority |
|-----------|-------|------------|------|----------|
| [TASK-CREATE-001](test-cases/TASK-CREATE-001.md) | Create a task with a title | US-TASK-01 | Happy path | Critical |
| [TASK-CREATE-002](test-cases/TASK-CREATE-002.md) | Saving without a title shows a field message | US-TASK-01 | Validation | High |
| [TASK-CREATE-003](test-cases/TASK-CREATE-003.md) | A new task starts with status Todo | US-TASK-01 | Business rule | Medium |
| [TASK-CREATE-004](test-cases/TASK-CREATE-004.md) | A title made only of spaces shows a field message | US-TASK-01 | Validation | Medium |
| [TASK-CREATE-005](test-cases/TASK-CREATE-005.md) | Saving after the role became Viewer shows a message | US-TASK-01 | Error handling | High |
| [TASK-CREATE-006](test-cases/TASK-CREATE-006.md) | Cancelling closes the form without adding a task | US-TASK-01 | UX | Low |
| [TASK-CREATE-007](test-cases/TASK-CREATE-007.md) | A viewer cannot create tasks | US-TASK-01 | Security | High |
| [TASK-CREATE-008](test-cases/TASK-CREATE-008.md) | Saving when the service does not answer shows a message | US-TASK-01 | Error handling | High |
```

### 14.2 Error test case: `docs/areas/tasks/test-cases/TASK-CREATE-005.md`

```markdown
# TASK-CREATE-005: Saving after the role became Viewer shows a message

| Field | Value |
|-------|-------|
| User Story | US-TASK-01 |
| Type | Error handling |
| Priority | High |
| Automated by | None |

## Description

An editor whose role is changed to Viewer while the New Task form is open cannot save the task and is told why.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- A second session is signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Prepare the quarterly report" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Prepare the quarterly report" in the "Title" field.
4. As Admin: Click the "Team" link in the menu.
5. As Admin: Click the "Edit" button on the row of "Dev Member".
6. As Admin: Check the "Viewer" field.
7. As Admin: Click the "Save" button.
8. Click the "Create Task" button.
9. As Admin: Click the "Edit" button on the row of "Dev Member".
10. As Admin: Check the "Editor" field.
11. As Admin: Click the "Save" button.
12. As Admin: Click the "Tasks" link in the menu.

## Expected Results

- After step 8, the message "Your role does not allow this change." appears.
- As Admin: the Tasks page does not list "Prepare the quarterly report".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "Your role does not allow this change." |
| Log key | `task.create.not_allowed` |
| Severity | Warning |
| Runbook | [task-create-not-allowed](../operations.md#task-create-not-allowed) |
```

---

## 15. Documenting existing code

When an area exists in code but not in the docs:

1. **Use** the application as each role would; list the screens, actions and messages.
2. **Read** the code (controller -> service -> repository) to confirm the rules and find the error paths and log
   call sites.
3. **Write** the stories in user terms (sections 6 and 7). Error paths a user can reach become Error States.
4. **Write** the test cases (section 8), then add the story-id comments (11.2) to the code you read.

Do not copy code or file references into the docs, do not invent rules the code does not enforce, and do not
document behaviour no user can reach: it belongs to integration tests or `architecture.md`. Behaviour that looks
wrong is recorded under Open Questions in the area index, not silently documented as intended.

---

## 16. E2E test infrastructure

Two switches let E2E tests reach what the UI alone cannot, and two settings make a run repeatable (16.3). **Security:** never enable
any of them outside the Development environment. With `AutoStartApps` on, the run sets them on the backend it starts, in the
Development environment.

### 16.1 Impersonation

E2E tests bypass the Microsoft Entra sign-in with an impersonation pattern:

```
Test setup  -> localStorage "E2E_MOCK_USER" (JSON)  -> frontend skips MSAL
Browser     -> header X-MyApp-Impersonate-UserId    -> ImpersonationMiddleware sets the user
```

- It is on when `MyApp:Impersonation:Enabled` is `true`, which only `appsettings.Development.json` and the E2E run set. The middleware
  reads that setting alone, so the setting is the safeguard.
- The header name is configured in `MyApp:Impersonation:HeaderName` and must match `E2E:ImpersonationHeaderName`
  in the E2E settings.

### 16.2 Fault switch

The precondition `The service does not answer when {situation}.` (8.3) is provided by a fault switch, so one test can
meet a service failure while the backend keeps serving the others:

```
Test setup  -> Playwright route adds the header to the browser's API calls
Browser     -> header X-MyApp-Fault: {log key}     -> the operation with that log key answers 503 and does not run
```

- The switch is off by default and can only be on in the Development environment:
  `MyApp:Testing:FaultInjection:Enabled` (environment variable `MyApp__Testing__FaultInjection__Enabled=true`).
- `{log key}` is the Log key in the test case's Observable trace; several keys are comma-separated. The operation
  writes the keyed Error log and returns the problem response with the service message, exactly as a real failure.
- It affects only the browser session whose context carries the route, and only the operations with that key. The
  frontend knows nothing about it.
- The test case states the situation in user terms. Test infrastructure under `tests/e2e/MyApp.E2E/Infrastructure/`
  takes the key from the test case and adds the route; test classes and page objects never name a route or a header.

### 16.3 Database reset and fixed clock

| Setting | Effect | Guard |
|---------|--------|-------|
| `MyApp:Testing:ResetDatabaseOnStart` | The backend drops, migrates and seeds its database when it starts | Only in Development, and only when the database name ends in `_E2E` or `_Test`; otherwise it stops at start |
| `MyApp:Testing:FixedUtcNow` | The backend clock and the dates of the seed data start at this instant (`E2E_FIXED_UTC_NOW` in a run) | Only in Development |

---

## 17. Maintenance

| Event | Action |
|-------|--------|
| New feature | Roadmap feature, stories and test cases before any code (section 2) |
| Behaviour change | Change the story and test cases first, then the code |
| Bug fix | Add or correct the test case that would have caught it; for a new failure mode, an Error handling or Edge case test case with its Observable trace |
| Refactoring | No doc change; keep the story-id comments on the moved code |
| UI label or message change | Update the stories and test cases that quote it, in the same change |
| Format change | Section 13 |

---

## 18. Related documents

| Document | Purpose |
|----------|---------|
| [overview.md](../overview.md) | What MyApp is |
| [roadmap.md](../roadmap.md) | Features and status |
| [architecture.md](architecture.md) | Technical architecture and conventions |
| [frontend.md](frontend.md) | UI and UX rules |
| [contracts.json](../contracts.json) | Current format versions |
| [manual.md](manual.md) | User manual, screenshots, help pack, in-page assistant, project bench |
| [models.md](models.md) | Assistant protocol, evaluation, training, model lock |
| [operations.md](operations.md) | Log call sites, signatures, runbooks, monitor, investigator |
