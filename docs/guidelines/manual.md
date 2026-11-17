# MyApp - User Manual and Help Guidelines

> The end-user side of STDD: the user manual, its screenshots, the manual inside the application, the help pack,
> the in-page assistant and the project bench. Formats defined here are registered in
> [docs/contracts.json](../contracts.json). Story and test case formats are in [development.md](development.md);
> the model behind the assistant is in [models.md](models.md).

**How to read this.** Section 1 lists every artifact and command. Each later section is a specification: field
names, headings, file names and thresholds are exact strings, and the check that enforces each rule is named
next to it.

---

## 1. Overview

### 1.1 Artifacts

| Artifact | Location | Written by | Committed |
|----------|----------|------------|-----------|
| Manual pages | `docs/manual/{lang}/...` (section 2) | `manual:generate` drafts, then an author or agent | Yes |
| Screenshots | `docs/manual/{lang}/{area}/images/*.png` (section 4) | Capture classes | Yes |
| Capture manifest | `docs/manual/captures.json` (4.6) | Capture classes | Yes |
| Project bench extras | `docs/manual/bench.md` (8.2) | Author | Yes |
| Manual bundle | `src/frontend/public/help/{lang}/manual.json` and `images/` (5.1) | `help:build` | No (git-ignored) |
| Help pack | `src/frontend/public/help/{lang}/pack.json` (section 6) | `help:build` | No (git-ignored) |
| Help manifest | `src/frontend/public/help/manifest.json` (6.8) | `help:build` | No (git-ignored) |
| Embedding table | `src/frontend/public/help/{lang}/embed.bin` (6.7.2); optional, absent in a lexical-only build | `help:build` copies it from `models/embeddings/weights/` or from `--embeddings {dir}` | No (git-ignored) |
| Table source and calibration | `models/embeddings/weights/{lang}.bin` (fetched; never committed), `models/embeddings/calibration/{version}.json` (6.7.7) | `python -m training.assistant.embed build`, `help:bench -- --calibrate`, `lock --record --model embeddings` (models.md 7.8, 9.5) | Calibration report: yes; tables: no |
| Documentation gaps | Backend store, exported as JSON (7.4) | Backend | No |

`{lang}` is `en` or `it`. `{area}` is an area folder of the registry (development.md 3.2).

### 1.2 Commands

All `npm run` commands run in `src/frontend`. Scripts live in `src/frontend/scripts/manual/` and
`src/frontend/scripts/help/`; they are TypeScript with erasable syntax only, run directly by Node 22.18 or
later (no build step, no new dependency), and import the runtime modules of `src/frontend/src/help/` with
explicit `.ts` extensions. Choice: one implementation of parsing, analysis and ranking shared by the build, the
bench and the browser, so they cannot disagree.

| Command | What it does | Approval |
|---------|--------------|----------|
| `npm run manual:generate` | Creates missing pages as drafts, refreshes derived front matter, reports stale pages with the story diff (3.1) | Yes (regenerates the manual) |
| `npm run manual:generate -- --accept {story id or area folder}` | Marks the pages of one story or area as reviewed against the current sources (3.2) | Yes |
| `npm run manual:check` | Runs checks M01 to M16; staleness and drafts are warnings (3.3) | No |
| `npm run manual:check -- --strict` | Same, every finding fails; used by a pipeline and by release | No |
| `npm run manual:check -- --root {dir} --no-images` | Checks a docs tree outside this repository (synthetic domains, models.md 5.7) | No |
| `npm run manual:generate -- --root {dir}`, `npm run manual:check -- --root {dir}` | The same on another docs tree | As above |
| `cd tests/e2e/MyApp.E2E && dotnet test --filter "TestCategory=manual-capture" --environment E2E__ManualLanguage={lang} --environment E2E_FIXED_UTC_NOW=2030-01-10T09:00:00Z` | Captures the screenshots of one language (4.7) | Yes (E2E run and manual regeneration) |
| `npm run help:build` | Builds manual bundles, help packs and manifest from `docs/` (5.1, 6) | No |
| `npm run help:build -- --strict` | Same, fails on any manual check finding or model incompatibility (6.8) | No |
| `npm run help:build -- --embeddings {dir} [--unlocked]` | Builds with the tables of `{dir}` instead of `models/embeddings/weights/`; `--unlocked` uses a table that `models/model.lock` does not record and is a usage error with `--strict` (6.7.4, 6.7.8). Without a table the pack is lexical only | No |
| `npm run help:build -- --root {dir} --out {dir} [--no-images]` | Builds packs for a docs tree outside this repository | No |
| `npm run help:retrieve -- --pack {file} --in {jsonl} --out {jsonl}` | Runs retrieval for a list of questions (8.4, models.md 4); uses the `embed.bin` next to the pack when the pack names one and it passes the checks of 6.7.5 | No |
| `npm run help:bench` | Retrieval bench for both languages, on the fused configuration when a table is used; exit code 1 below threshold (8.4) | No |
| `npm run help:bench -- --compare [--embeddings {dir} --unlocked]` | The bench on the fused configuration (which gates) beside the same items with `fusion.method` = `none`, with the difference per metric and language; an unlocked table is labelled "not a release result" (6.7.8) | No |
| `npm run help:bench -- --calibrate [--embeddings {dir} --unlocked]` | Calibrates `rrf_k`, `embedding_weight` and the two cosine thresholds on the indexed questions only and writes `{dir}/calibration.json` (6.7.7) | No |
| `npm run help:bench -- --indexed` | Retrieval metrics of every indexed question asked without itself; never gates (6.9) | No |
| `npm run help:bench -- --export {jsonl}` | Writes the bench items for the answer-level evaluation (models.md 4.2) | No |
| `npm run help:bench -- --answers {report} [--require-answers]` | Adds the answer gates of 8.3 from the report of `python -m training.assistant.eval --suite project`; `--require-answers` fails when a model is locked and no report is given | No |
| `npm run help:gaps -- --in {export.json}` | Prints the documentation gap triage report to standard output (7.4) | No |

`package.json` has no `prebuild` or `predev` step: run `npm run help:build` before `npm run dev` or `npm run build`, once pages exist.
Without pages (a tree that has not written its manual yet) the manual and help surfaces have nothing to show.

### 1.3 Producing the manual the first time

The template ships the manual of the sample application (English and Italian, 90 pages, 80 screenshots; the strict check passes), produced by the
steps below. A project that replaces the domain starts again from `bench.md`, and `help:build` fails with `no manual pages` until the pages exist. A project
produces them once, in this order. Steps 2, 3 and 6 need the owner's approval (`CLAUDE.md`); subagents never run them.

| # | Step | Command | Who | Result |
|---|------|---------|-----|--------|
| 1 | Prepare | Everything under "Prerequisites" of `tests/e2e/MyApp.E2E/README.md`, the database in the state of the seed data, Node 22.18 or later, `npm ci` in `src/frontend` | Owner | Ready to capture |
| 2 | Capture both languages, whole category | The capture command of 1.2, once with `en` and once with `it`. For committed images use the pinned container (4.3) | Owner, after approval | `docs/manual/{lang}/{area}/images/*.png`, `docs/manual/captures.json` |
| 3 | Create the drafts | `npm run manual:generate` | Owner, after approval | One draft page per story and area, and the home page, in both languages |
| 4 | Write the pages | No command. The generator writes only drafts: **an author, or a coding agent the owner directs, writes the text** of every page in both languages, following 2.4 and 2.6, sets `routes` and `contexts` by hand, places each image under its step (4.5), and removes the draft marker | Author or agent | Finished pages |
| 5 | Check | `npm run manual:check -- --strict` (`--strict` fails on any draft marker; M13 and M14 catch messages that differ from the UI) | Anyone | No finding |
| 6 | Accept each story and area | `npm run manual:generate -- --accept {story id or area folder}`, which refuses a page that still has a draft marker | Owner, after approval | `source_hash` and `steps_hash` set: the author has read the pages against the stories |
| 7 | Build the bundle and packs | `npm run help:build -- --strict` | Anyone | `src/frontend/public/help/` (git-ignored) |
| 8 | Measure retrieval | `npm run help:bench` (needs `docs/manual/bench.md` written for the project, 8.2) | Anyone | Retrieval gates of 8.3 |
| 9 | Commit | Pages, images and `captures.json`, with approval | Owner | The manual is part of the repository |

After that, the E2E tests of the help area can run, because they read the bundle and packs of step 7. Later changes follow 3.2.

---

## 2. Manual pages

### 2.1 Layout

```
docs/manual/
  captures.json                     # screenshot manifest (4.6)
  bench.md                          # project bench extras (8.2)
  glossary.md                       # words users say for the manual's words (6.9)
  en/
    index.md                        # home page (kind home)
    {area}/
      index.md                      # area page (kind area)
      US-{AREA}-NN.md               # one page per story (kind story)
      images/
        {screen}-{state}.png        # screenshots (4.2)
  it/
    (same tree, same file names)
```

- No other files are allowed under `docs/manual/` (check M01).
- The `it` tree mirrors the `en` tree file for file, including image file names (check M11).

### 2.2 Page kinds and mapping

| Kind | File | Source | Page id |
|------|------|--------|---------|
| `story` | `docs/manual/{lang}/{area}/US-{AREA}-NN.md` | `docs/areas/{area}/user-stories/US-{AREA}-NN.md` | `{area}/US-{AREA}-NN` |
| `area` | `docs/manual/{lang}/{area}/index.md` | `docs/areas/{area}/userstories.md` | `{area}` |
| `home` | `docs/manual/{lang}/index.md` | none | `home` |

- Exactly one story page per story file and language; no story page without a story file (M02).
- Exactly one area page per area folder and language (M02).
- A story page maps to application screens through its `routes` and `contexts` front matter fields (2.3, 5.4).
  Routes are not in stories, because stories carry no implementation detail (development.md 1.3), and not in
  code, because story ids may appear in `src/` only as comments (development.md R22).
- Order inside an area: story id order. Order of areas on the home page: area registry order.

### 2.3 Front matter

The first line of a page is `---`; front matter ends at the next `---`. One `key: value` per line, in the order
below. Lists use the flow form `[a, b]` with unquoted items; an empty list is `[]`. No other YAML syntax.

```markdown
---
schema_version: 1
kind: story
lang: en
page_id: tasks/US-TASK-01
source: docs/areas/tasks/user-stories/US-TASK-01.md
source_hash: 3b18e512dba79e4c8300dd08aeb37f8e728b8dad
steps_from: TASK-CREATE-001
steps_hash: 9f2a6c0b51d1e2a3f4b5c6d7e8f90a1b2c3d4e5f
routes: [/tasks]
contexts: [task-form]
screenshots: [images/tasks-default.png, images/tasks-new-task-form-open.png]
---
```

| Field | Rule | Set by |
|-------|------|--------|
| `schema_version` | Equals the `manual-page` version in `docs/contracts.json` | Generator |
| `kind` | `story`, `area` or `home` | Generator |
| `lang` | `en` or `it`; equals the language folder | Generator |
| `page_id` | As in 2.2 | Generator |
| `source` | Repository-relative path of the source file; `none` for `home` | Generator |
| `source_hash` | Git blob id of the source file: lower-case hex SHA-1 of `blob {byte length}\0{bytes}` over the file as stored (UTF-8, LF). `none` for `home` | `--accept` only |
| `steps_from` | Story pages: id of the lowest-numbered `Happy path` test case of the story. Other kinds: `none` | Generator |
| `steps_hash` | Git blob id of the `steps_from` test case file; `none` when `steps_from` is `none` | `--accept` only |
| `routes` | Story pages: one or more route patterns of the frontend router, with a leading `/` and `:param` segments (`/tasks`, `/settings/team`). Other kinds: `[]` | Author |
| `contexts` | Zero or more context keys matching `^[a-z]+(-[a-z]+)*$`, for help opened from inside a screen (5.4) | Author |
| `screenshots` | The image paths referenced in the body, in order of first appearance, relative to the page | Generator |

Choice: the git blob id rather than SHA-256, because `git diff {old} {new}` can then show exactly what changed in
the story since the page was last reviewed, and it is computable without git.

### 2.4 Body

After the front matter: an optional draft marker line (3.1), then exactly one H1, then an introduction, then the
H2 sections below, in this order. Headings are exact strings per language. Optional sections are omitted, not
left empty.

| Key | `en` heading | `it` heading | Required | Content | Source material |
|-----|--------------|--------------|----------|---------|-----------------|
| `overview` | H1: the page title | H1: the page title | Yes | One to three sentences under the H1: what the page is for, in the user's words | Story `I want` and `So that`; for `area` the Overview of the area index |
| `before` | `Before you start` | `Prima di iniziare` | No | Who can do this and what must exist first | Story Access Control (only when a role has `No`); happy-path Preconditions other than the sign-in line |
| `steps` | `Steps` | `Passaggi` | Story pages | Numbered list, one user action per item, optional image under an item | Steps of `steps_from`, without second-actor steps |
| `result` | `What you will see` | `Cosa vedrai` | No | Bullets: what the screen shows when it worked | Expected Results of `steps_from`; Acceptance Criteria |
| `rules` | `Good to know` | `Da sapere` | No | Bullets: rules the user notices, and what the feature does not do | Story Business Rules and Non-Goals |
| `problems` | `If something goes wrong` | `Se qualcosa non va` | Story pages with Error States or `User input` = `Yes` | Table with columns `What you see` / `What to do` (`it`: `Cosa vedi` / `Cosa fare`) | Error States rows; field messages of the story's `Validation` test cases |
| `related` | `Related pages` | `Pagine correlate` | No | Bullets, each one link to another page of the same language | Other stories of the area |

- Page title: `en` equals the story Title (area: the area Title from the registry); `it` is its Italian
  rendering, at most 70 characters.
- Every Error States Message of the story appears verbatim, in double quotes, in the `What you see` column of
  the `en` page (M13).
- UI labels and messages are quoted in double quotes exactly as the UI shows them in the page's language (M14).
- Home and area pages have only the H1 and the introduction; the list of areas and pages is rendered by the
  application from the front matter of the pages.
- Size: at most 150 body lines; `steps` at most 12 items.

### 2.5 Markdown subset

Pages are parsed by the project's own parser (`src/frontend/src/help/markdown/`), which turns them into a block
tree. Choice: a small closed subset parsed by our code instead of a Markdown library, because the repository has
none, the output must be deterministic, and the frontend then renders blocks with React components (no raw HTML).

| Allowed | Form |
|---------|------|
| Headings | `# ` once, `## ` with the fixed headings of 2.4; no other levels |
| Paragraph | Lines of text separated by a blank line |
| Ordered list | `1. ` items, one level; numbers in sequence |
| Bullet list | `- ` items, one level |
| Image | `![{alt}](images/{file}.png)` on its own line, either as a paragraph or indented three spaces under a list item; `alt` is required, in the page language, at most 120 characters |
| Table | Pipe table with a header row and a `|---|` separator row; cells hold inline content only |
| Bold | `**text**` |
| Link | `[{text}]({relative path to a page}.md)`, to a page of the same language; no anchors, no external links |
| Draft marker | The single line `<!-- draft: written by manual:generate, remove after review -->` |

Anything else (HTML, code, italics, nested lists, external URLs, H3) fails M04.

### 2.6 Italian pages and parity

The manual is user-facing content: `docs/manual/it/` and the `it` part of `docs/manual/bench.md` are the
sanctioned places for Italian text in documentation, alongside the `it` block of stories.

| Rule | Check |
|------|-------|
| Every `en` page has an `it` page with the same path, and the reverse | M11 |
| Same `kind`, `page_id`, `source`, `source_hash`, `steps_from`, `steps_hash`, `routes`, `contexts` | M11 |
| Same H2 keys in the same order; same number of `steps` items; same number of `problems` rows | M11 |
| Same image file names, referenced under the same step numbers | M11 |
| Language: outside double-quoted strings, the share of tokens that are stopwords of the other language is at most 0.10 (stopword lists of 6.4; words in both lists are ignored) | M12 |
| Each Error States Message: the `it` page contains its Italian UI text when the lookup of 3.1 finds a unique one | M13 |

The `it` page is written as an Italian user reads it, not as a word-for-word translation, but it carries the same
facts, steps and messages.

---

## 3. Generator and checks

### 3.1 `manual:generate`

Deterministic: the same repository state produces the same bytes. Inputs: story files, test case files, area
indexes, `src/frontend/public/locales/{lang}/*.json`.

| Situation | Effect |
|-----------|--------|
| A story or area has no page in a language | Creates the page: front matter (with `source_hash` and `steps_hash` set), draft marker, H1, every section of 2.4 filled with draft content from the table below |
| A page exists | Rewrites only `schema_version`, `kind`, `lang`, `page_id`, `source`, `steps_from`, `screenshots`; never touches the body, `routes`, `contexts` or the hashes |
| A page is stale (M05) | Prints the page path and the source diff (`git diff {stored blob} {current blob}`; when the stored blob is not in the repository, prints "previous version not in git history" and the full current source) |
| A page has no source any more | Prints it; deleting the page is the author's decision |

Draft content:

| Section | `en` draft | `it` draft |
|---------|-----------|-----------|
| H1 | Story Title | Story Title followed by ` (English, to translate)` |
| Introduction | `Use this page to {I want}. {So that, first letter upper case}.`, with first-person words (I, my, me) turned into second person (you, your) | Same English text |
| `steps` | Each step of `steps_from`, transformed by the step table below | The step table's `it` column where the pattern matches; otherwise the English step |
| `result` | Expected Results of `steps_from`, verbatim | Same English text |
| `rules` | Business Rules, then each Non-Goal as `This feature does not include {Non-Goal, first letter lower case unless a name}.` | Same English text |
| `problems` | One row per Error States row (Message, What the user can do), then one row per field message of the story's `Validation` test cases, found in Expected Results as `The "{F}" field shows "{message}"` (What to do: `Correct the "{F}" field and try again.`) or `The message "{message}" appears.` (What to do: `Correct what you entered and try again.`); other quoted strings (labels, test data) never give a row | Messages replaced by their Italian UI text when the lookup is unique; What to do: `Correggi il campo "{F_it}" e riprova.` / `Correggi i dati inseriti e riprova.` |

Step transformation (patterns are exact; `{X}` is a quoted label or a screen name):

| Test case step | `en` draft | `it` draft |
|----------------|-----------|-----------|
| `Open the {X} page.` | `Open the {X} page.` | `Apri la pagina {X_it}.` |
| `Click the "{X}" {element}[ in the menu\|dialog\|header].` | unchanged | `Fai clic su "{X_it}"[ nel menu\|nella finestra di dialogo\|nell'intestazione].` |
| `Click the "{X}" button on the first row of the table.` or `... on the row of "{R}".` | unchanged | `Fai clic su "{X_it}" sulla prima riga della tabella.` / `... sulla riga di "{R}".` |
| `Clear the "{F}" field.` | unchanged | `Svuota il campo "{F_it}".` |
| `Type "{v}" in the "{F}" field.` | `In the "{F}" field, type a value such as "{v}".` | `Nel campo "{F_it}" scrivi un valore come "{v}".` |
| `Select "{v}" in the "{F}" field.` | `In the "{F}" field, choose "{v}".` | `Nel campo "{F_it}" scegli "{v_it}".` |
| `As {Role}: ...` | dropped | dropped |
| any other | unchanged | unchanged (English) |

UI text lookup (`{X_it}`): flatten every `en` locale file to `namespace.key -> value`; collect the keys whose
English value equals the string exactly; the Italian text is the `it` value of those keys if all of them give the
same value. Values containing `{{` are skipped. No unique value: keep the English text.

The `before` draft leaves out a precondition that quotes a string which is not a UI text (it names test data).

The draft marker stays until an author or agent has rewritten the page; `--strict` fails while it is there.

### 3.2 `--accept`

`npm run manual:generate -- --accept US-TASK-01` (or an area folder) sets `source_hash` and `steps_hash` of both
language pages to the current blob ids. It refuses, changing nothing, when either page has a draft marker or a
finding other than M05. Accepting declares that a person or agent has re-read both pages against the current
story and test case; the pull request diff shows the story change and the page change side by side.

Workflow when a story changes: edit the story (and test cases) -> `manual:check` reports the pages stale ->
update both page bodies -> `--accept` -> commit stories and pages together.

### 3.3 Checks

`manual:check` reads `docs/` and `src/frontend/`. Each finding prints `{check id} {path}: {message}`.

| Id | Check | Non-strict |
|----|-------|------------|
| M01 | `docs/manual/` contains only the files of 2.1 | Fail |
| M02 | One story page per story file and language, one area page per area and language, one home page per language; no page without a source | Fail |
| M03 | Front matter fields present, in order, valid (2.3); `screenshots` equals the image references of the body | Fail |
| M04 | Body uses only the subset of 2.5; headings are the exact strings of 2.4 for the page language, in order; required sections present; size limits | Fail |
| M05 | Stale: `source_hash` differs from the current blob id of `source`, or `steps_hash` from that of `steps_from`, or `steps_from` is no longer the lowest-numbered `Happy path` test case | Warning |
| M06 | Draft marker present | Warning |
| M07 | Every link resolves to an existing page of the same language | Fail |
| M08 | Every referenced image exists under the page's `images/` folder, is PNG, and has an entry in `captures.json` with equal SHA-256 | Fail |
| M09 | Orphans: an image file not referenced by any page; a `captures.json` entry with no file | Fail |
| M10 | Every route in `routes` appears as a `path` string literal in `src/frontend/src/routes/*.tsx` (compared after adding a leading `/`); every story page has at least one route | Fail |
| M11 | Parity of `en` and `it` pages (2.6) | Fail |
| M12 | Language share (2.6) | Fail |
| M13 | Error States coverage (2.4, 2.6) | Fail |
| M14 | Every double-quoted string in `steps` and `problems` is a value of the page language's locale files, or a Test Data value of `steps_from` | Warning |
| M15 | Every context key used in `src/frontend/src/` (`openHelp({ context: '...' })` or `<HelpButton context="..." />`) is declared by exactly one story page per language | Fail |
| M16 | `docs/manual/bench.md` follows 8.2 | Fail |

### 3.4 Strict mode

`--strict` turns every warning into a failure. A project's pipeline runs `npm run manual:check -- --strict` once `docs/manual/` has pages, and skips it with a notice before that (`MANUAL_REQUIRED` makes the absence fail). Locally the default
is non-strict so a story can be edited before its pages are.

---

## 4. Screenshots

### 4.1 Capture classes

| Element | Rule |
|---------|------|
| File | `tests/e2e/MyApp.E2E/Manual/{AreaPascal}/{Screen}Capture.cs` (development.md 10.2) |
| Base class | `Manual/ManualCaptureBase.cs`; sets up language, clock, viewport and the manifest writer |
| Tags | `[Category("{area folder}")]`, `[Category("manual-capture")]`, one or more `[Property("UserStory", ...)]`; no `TestCase` (development.md 11.1, R21) |
| Methods | One `[Test]` per image; the method name is the state in PascalCase (`NewTaskFormOpen`) |
| Actions | Only through page objects; the page object of the screen exposes the actions and the locators |
| Normal runs | The base class calls `Assert.Ignore` when `E2E__ManualLanguage` is not set, so a plain `dotnet test` reports captures as skipped |
| Clock | The base refuses to run unless `E2E_FIXED_UTC_NOW` is `2030-01-10T09:00:00Z` |
| Data changes | A capture never saves: it may type into a form, and stops before the request that would change anything, or at a request the fault switch makes fail |

### 4.2 Image names

`docs/manual/{lang}/{area}/images/{screen}-{state}.png`, where `{screen}` is the capture class name without `Capture`
in kebab case (`TasksCapture` -> `tasks`) and `{state}` is the method name in kebab case
(`NewTaskFormOpen` -> `new-task-form-open`). The capture class `TasksCapture` drives `TasksPage`.

- Names are fixed: no dates, counters or language suffixes. A new capture run overwrites the same file, so a
  changed screen shows as a changed file in the pull request and an unchanged screen shows no change.
- The same name exists in both language folders (M11).

### 4.3 Environment

| Setting | Value |
|---------|-------|
| Browser | Chromium of the Playwright version in `MyApp.E2E.csproj`, headless |
| Machine | Committed images come from the Playwright container image of the same version (`mcr.microsoft.com/playwright/dotnet:v{version}-noble`), so fonts and rendering are stable |
| Viewport | 1280 x 800, device scale factor 1; a narrow variant 390 x 844 only for a state whose name ends in `Mobile` |
| Language | One run per language: `E2E__ManualLanguage=en` or `it`. Browser context `Locale` `en-US` or `it-IT`. In an Italian run the base answers the account request with Italian, without saving anything. Before each shot the language button must show the run language; if not, the base chooses it through the language menu. A screen with no language button is not checked |
| Time zone | `UTC` |
| Browser clock | Fixed with Playwright `Clock.SetFixedTimeAsync` at `2030-01-10T09:00:00Z` |
| Server clock | The backend honours `MyApp:Testing:FixedUtcNow` (read through `TimeProvider`) only in the Development environment; `E2E_FIXED_UTC_NOW` makes the run start it with that value (with `AutoStartApps` off, start the backend with the same value) |
| Seed data | The E2E seed data (development.md 10.3), loaded when the run's backend starts; a capture run never changes it |
| Rendering | `Animations = Disabled`, `Caret = Hide`, wait for `document.fonts.ready` and network idle before the shot |
| Shot | Viewport screenshot by default; `ShootElementAsync(locator)` for a dialog or form; never full-page |

### 4.4 Masking

Seed data and fixed clocks remove most volatile content. What they cannot fix is masked with Playwright `Mask`
and `MaskColor = "#D9D9D9"`. A page object may override `VolatileRegions` (locators, empty by default); the base masks them on
every shot of that screen. Typical: values with a generated unique suffix,
avatars loaded from an external service, the application version in the footer. Masking anything else (to hide a
layout bug, for example) is not allowed.

### 4.5 Referencing an image

In the page body, under the step it illustrates, indented three spaces:

```markdown
2. Click "New Task".

   ![The New Task form above the list of tasks](images/tasks-new-task-form-open.png)
```

`manual:generate` updates `screenshots` from the references. A page references only images of its own
language folder and area.

### 4.6 Capture manifest

`docs/manual/captures.json`, written by the capture run (the base class replaces the entries of the run's
language and keeps the others), entries sorted by `path`:

```json
{
  "schema_version": 1,
  "images": [
    {
      "path": "docs/manual/en/tasks/images/tasks-new-task-form-open.png",
      "lang": "en",
      "capture": "Tasks/TasksCapture.NewTaskFormOpen",
      "viewport": "1280x800",
      "sha256": "{lower-case hex of the file bytes}"
    }
  ]
}
```

The manifest proves that every image came from a capture (M08) and makes an image change visible as a one-line
text diff.

### 4.7 Running a capture

Running captures is an E2E run and regenerates the manual: it needs the user's approval every time
(`CLAUDE.md`). Subagents never run it. It runs after approval, by hand, from `tests/e2e/MyApp.E2E`:

```bash
dotnet test --filter "TestCategory=manual-capture" --environment E2E__ManualLanguage=en --environment E2E_FIXED_UTC_NOW=2030-01-10T09:00:00Z
dotnet test --filter "TestCategory=manual-capture" --environment E2E__ManualLanguage=it --environment E2E_FIXED_UTC_NOW=2030-01-10T09:00:00Z
```

Prerequisites and troubleshooting are in `tests/e2e/MyApp.E2E/README.md`. Run the whole category for each language, never a selection: the first
shot of a run replaces the manifest entries of that language. Images from a machine other than the pinned container are for review only, not for
commit.

After a run: `npm run manual:check -- --strict` must pass (orphans, manifest, parity) once the pages exist.

---

## 5. The manual inside the application

### 5.1 Build output

`npm run help:build` writes, from `docs/manual/` and `docs/areas/`:

| File | Content |
|------|---------|
| `src/frontend/public/help/manifest.json` | 6.8 |
| `src/frontend/public/help/{lang}/manual.json` | Manual bundle: `{ "schema_version", "lang", "project_version", "pages": [ { "page_id", "kind", "area", "title", "source_id", "routes", "contexts", "blocks" } ], "routes": { "{pattern}": ["{page_id}"] }, "contexts": { "{key}": "{page_id}" } }`. `blocks` is the block tree of 2.5; image sources are rewritten to `/help/{lang}/images/{area}/{file}`; links to `/manual/{page path}` |
| `src/frontend/public/help/{lang}/images/{area}/*.png` | Copies of the referenced images |
| `src/frontend/public/help/{lang}/pack.json` | Help pack (section 6) |

`src/frontend/public/help/` is git-ignored. The backend reads the same files from `MyApp:Help:PackDirectory`
(models.md 7.7).

### 5.2 Manual section

| Route | Shows |
|-------|-------|
| `/manual` | Home page and the list of areas |
| `/manual/{area}` | Area page and its story pages in story id order |
| `/manual/{area}/{story id}` | One story page; `#steps` and the other H2 keys of 2.4 scroll to a section |
| `/manual/search?q={text}` | Search results (5.3) |

The section uses the UI language; changing the language reloads the bundle. Blocks are rendered by React
components in `src/frontend/src/help/components/`; no HTML string from the bundle is inserted into the page. UI
text of the section lives in a new locale namespace `help` in both languages.

### 5.3 Search

Search uses the retrieval of section 6 on the pack of the UI language, without the out-of-scope decision: up to
10 sections, grouped by page in rank order of their best section. Each result shows the page title, the section
heading and the first 160 characters of the section text, with words whose stem matches a query stem in bold.
An empty result shows a link to the manual home.

### 5.4 Contextual help

- Every screen has a help button with the accessible name "Help" in the page header, so page
  objects can reach it.
- The button opens a side panel (the `SidePanel` block of `src/ui`) with two tabs: "This page" and "Ask".
- "This page" lists the story pages whose `routes` contain the current route pattern. The pattern comes from
  React Router `matchRoutes` on the current location (the route definition's `path`, with a leading `/`). No
  match: the panel shows the area page of the first path segment if one exists, otherwise the home page.
- A component inside a screen (a form, a dialog) opens help for itself with `openHelp({ context: 'task-form' })`
  or `<HelpButton context="task-form" />`; the panel then shows the single page that declares that context first,
  followed by the other pages of the route. Context keys are checked by M15.
- "Ask" is the assistant (section 7). Its retrieval is not limited to the current route; sections of the
  current route's pages get no boost. Choice: a boost would hide answers that live on another screen, which is
  where users get lost most.
These are user-facing behaviours: they are specified as stories of a help area before they are built
(development.md 2.1).

---

## 6. Help pack

The knowledge artifact the in-page assistant and search work from. Built at every build and release from the
manual pages and the stories' questions by `npm run help:build`; one pack per language. It contains no source code, no test cases and no
personal data.

### 6.1 Sections

| Rule | Value |
|------|-------|
| Unit | One section per H1 introduction (key `overview`) and per H2 section of 2.4, except `related` |
| Pages included | Every page of the language (home, area, story) |
| Section id | `{page_id}#{key}`; a split part adds `-{part}` from 2 (`tasks/US-TASK-01#steps-2`) |
| Title | `{page title}` for `overview`; `{page title} - {heading}` otherwise; part 2 onward adds ` ({part})` |
| Text | Plain text: paragraphs joined by `\n`; list items as `{n}. {text}` or `- {text}`; table rows as `{col 1}: {cell 1}. {col 2}: {cell 2}.`; bold markers removed; images and links reduced to their text; Unicode NFC; no trailing spaces |
| Maximum | 1,200 characters of text. A longer section is split at list item or paragraph boundaries into parts of at most 1,200 characters; numbering of steps continues across parts. A single item longer than 1,200 characters fails the build |

### 6.2 Questions in the pack and the bench split

The stories' "Questions users ask" (development.md 6.4) serve two purposes that would leak into each other: a
question indexed next to its section cannot measure the retrieval of that section. They are split
deterministically, per story and language:

1. Normalise each question: Unicode NFC, trim, collapse whitespace to one space.
2. Key: `sha256("{story id}\n{lang}\n{normalised question}")` as lower-case hex.
3. Sort the story's questions of that language by key, ascending.
4. The first `floor(n / 2)` questions are **bench** questions (n = 3 gives 1; n = 4 or 5 gives 2); the rest are
   **indexed** questions.

- Indexed questions go into the pack, in the `questions` field of every section of their story's page, and
  bridge the user's vocabulary to the page.
- Bench questions never enter the pack. `help:build` fails if a bench question's normalised text equals any
  indexed question or appears inside any section text.
- No file is added to an area (development.md R02): the split is computed, not stored.
- Editing a question can move it between the two sets; that is visible in the bench report.
- Neither set is ever training data (development.md 6.4, models.md 5). Bench questions never enter the embedding
  table either; indexed questions reach it only through the pack (6.7.3).

The single implementation of the split is in `src/frontend/src/help/bench/split.ts`; Python code that needs bench
items reads them from `help:bench -- --export` (8.4), never recomputes them.

### 6.3 Pack format

`src/frontend/public/help/{lang}/pack.json`, UTF-8, keys in the order shown, no insignificant whitespace.

```json
{
  "schema_version": 2,
  "kind": "help-pack",
  "lang": "en",
  "project": { "name": "MyApp", "version": "0.1.0" },
  "contracts": { "manual-page": 1, "help-pack": 2, "assistant-protocol": 1, "assistant-retrieval": 3 },
  "build": { "commit": "{git commit or null}", "inputs_sha256": "{hex}" },
  "assistant": {
    "protocol": { "sha256": "{hex}", "content": { } },
    "retrieval": { "sha256": "{hex}", "content": { } },
    "model": { "id": "assistant", "version": "1.0.0", "protocol_sha256": "{hex}", "retrieval_sha256": "{hex}" },
    "compatible": true
  },
  "analyzer": { "id": "snowball-en", "version": 1, "stopwords_sha256": "{hex}" },
  "sections": [
    {
      "id": "tasks/US-TASK-01#steps",
      "page_id": "tasks/US-TASK-01",
      "key": "steps",
      "area": "tasks",
      "story": "US-TASK-01",
      "title": "Create a task - Steps",
      "text": "1. Open the Tasks page.\n2. Click \"New Task\".",
      "url": "/manual/tasks/US-TASK-01#steps",
      "routes": ["/tasks"]
    }
  ],
  "questions": [
    { "story": "US-TASK-01", "text": "Can I set a deadline when I make a new item?" }
  ],
  "index": {
    "fields": ["title", "text", "questions"],
    "doc_count": 0,
    "avg_length": [0, 0, 0],
    "lengths": [[0, 0, 0]],
    "terms": { "{stem}": [{df}, [{doc}, {field}, {tf}], [{doc}, {field}, {tf}]] }
  },
  "embeddings": null
}
```

| Field | Rule |
|-------|------|
| `project.name` | The H1 of `docs/manual/{lang}/index.md`: the product name as users see it. It fills `{app_name}` in the prompt (models.md 3.2) |
| `project.version` | Environment variable `HELP_PROJECT_VERSION` when set (release pipeline), otherwise `version` of `src/frontend/package.json` |
| `contracts` | Current versions from `docs/contracts.json` |
| `build.commit` | Environment variable `HELP_BUILD_COMMIT` or `null`; never read from git implicitly, so local builds are byte-identical |
| `build.inputs_sha256` | SHA-256 of the lines `{path} {git blob id}` of every input file, sorted by path, joined with `\n` |
| `assistant.protocol`, `assistant.retrieval` | Byte copies (parsed then re-serialised in key order) of `training/assistant/protocol.json` and `training/assistant/retrieval.json` with the SHA-256 of the file bytes (models.md 3) |
| `assistant.model` | From `models/model.lock`; `null` when the lock has no `assistant` entry |
| `assistant.compatible` | `true` when `model` is not `null` and both hashes equal the current files; otherwise `false`, and the runtime uses retrieval only (models.md 7.5) |
| `story` | Story id for story pages; `null` for home and area pages |
| `index.terms` | For each stem: document frequency, then postings `[section index, field index, term frequency]`, sorted by section index then field index; terms sorted by code point |
| `index.lengths` | Per section, token count per field after analysis |
| `embeddings` | `null` when no table is used (6.7.4), otherwise the block of 6.7.4 |

Size target: under 1 MB per language for a project of 100 stories, plus the embedding table when one is used (at most
8 MiB, 6.7.2). The browser loads the pack, and its table, the first time the help panel or search opens.

### 6.4 Analyzer

The same code (`src/frontend/src/help/lexical/`) analyses sections at build time and queries at run time.

1. Unicode NFC, then lower case.
2. Split on every character that is not a letter or a digit (`[^\p{L}\p{N}]+`, Unicode aware); an apostrophe
   splits (`l'elenco` -> `l`, `elenco`).
3. Drop tokens of one character and tokens in the stopword list of the language
   (`src/frontend/src/help/lexical/stopwords.{lang}.txt`, one word per line, sorted, shipped with the template;
   `training/assistant/config/` keeps byte copies for the Python port). Each list is the Snowball stop list of the
   language; the Italian list adds the forms of *potere* and *dovere* and the Italian counterparts of the English
   entries that Snowball's Italian list lacks (`solo`, `ogni`, `altro`, `ancora`, `molto`, `qui`, `dopo`, ...), so
   both languages drop the same kinds of words.
4. Stem with the Snowball stemmer of the language (`english`, `italian`). The stemmers are the JavaScript files
   generated by the Snowball compiler, vendored under `src/frontend/src/help/lexical/snowball/` with their
   licence; no package is added.
5. Remove diacritics from the stem (NFD, drop combining marks).

`analyzer.id` is `snowball-{lang}`; `analyzer.version` changes with any change to these steps or the lists.

### 6.5 Ranking

Parameters live in `training/assistant/retrieval.json` (copied into the pack); defaults:

```json
{
  "schema_version": 3,
  "bm25": { "k1": 1.2, "b": 0.75, "field_weights": { "title": 2.0, "text": 1.0, "questions": 1.5 } },
  "candidates": 10,
  "diversify": "page",
  "out_of_scope": { "min_coverage": 0.40, "min_cosine": 0.55, "min_cosine_lexical": -1.0, "min_query_terms": 1 },
  "fusion": { "method": "rrf", "depth": 30, "rrf_k": 60, "embedding_weight": 1.0 }
}
```

`schema_version` is the `assistant-retrieval` version of `docs/contracts.json`. `fusion.method` is `rrf` (fuse when the
pack carries a usable embedding table, 6.7.4; lexical ranking otherwise) or `none` (lexical ranking even with a table).
The four values `fusion.rrf_k`, `fusion.embedding_weight`, `out_of_scope.min_cosine` and `out_of_scope.min_cosine_lexical`
are defaults: when the pack's `embeddings.params` exists, its values replace them (calibration, 6.7.7).

Lexical score of section `d` for the set of distinct query stems `Q` (BM25F):

```
N        = doc_count
idf(t)   = ln(1 + (N - df(t) + 0.5) / (df(t) + 0.5))        df(t) = 0 when t is not in the index
tf'(t,d) = sum over fields f of  w_f * tf(t,d,f) / (1 - b + b * len(d,f) / avg_len(f))
score(d) = sum over t in Q of  idf(t) * tf'(t,d) * (k1 + 1) / (tf'(t,d) + k1)
```

Order the sections by score, ties broken by section id in code point order; sections with score 0 are not
returned. With `diversify` = `page`, the best section of every page (the part of the id before `#`) comes first, in
that order, then the remaining sections in that order; the first section never changes, so neither does the
out-of-scope decision. Return the first `candidates` of that order. `diversify` absent means `none` (packs built before
it existed). The answer of a question is usually one page and the panel shows three cards: three sections of one
page would hide the second and third pages.

**Fusion.** Applies when `fusion.method` is `rrf` and the pack has a usable table (6.7.4); `rrf_k` and
`embedding_weight` are the effective values above.

1. Lexical list: the sections ordered by BM25F score as above (score 0 excluded, ties by section id), **before**
   diversification, cut to the first `fusion.depth`.
2. Embedding list: the sections ordered by cosine to the query vector (6.7.6), cosine greater than 0 only, ties by
   section id, cut to the first `fusion.depth`. A query with no stem in the table gives an empty list.
3. Fused score of a section: `1/(rrf_k + rank_lex) + embedding_weight/(rrf_k + rank_emb)`, ranks from 1; a section
   absent from a list contributes 0 for that list. Order by fused score, ties by section id in code point order.
4. Apply `diversify` to the fused order, then return the first `candidates`.

With one list empty, the result is the other list's order (diversified). The first section can now differ from the
lexical first section, so the out-of-scope decision (6.6) is taken on the fused first section.

Choice: reciprocal rank fusion, not a weighted sum of scores. BM25F scores are unbounded and their scale changes with
the query length and the pack, so a weighted sum needs a per-query normalisation (min-max is unstable with two or three
hits); ranks need none. RRF has two parameters, which a few hundred indexed questions can calibrate without
overfitting (6.7.7), and when the lexical list is empty (the vocabulary gap the table exists for) the embedding order
passes through unchanged.

### 6.6 Out-of-scope decision

Applied by the assistant (section 7), not by search.

```
idf_unknown  = ln(1 + (N + 0.5) / 0.5)                       idf of a stem absent from the index
coverage     = sum of idf(t) for t in Q found in any field of the top section
               / sum of idf(t) for t in Q (unknown stems counted with idf_unknown)
cosine       = cosine of the query vector and the top section's vector (6.7.6); 0 when the top section has no
               vector; null when fusion does not apply or the query has no stem in the table

without fusion:
in scope     = |Q| >= min_query_terms and coverage >= min_coverage

with fusion:
in scope     = |Q| >= min_query_terms
               and (   cosine >= min_cosine                                          semantic acceptance
                    or (coverage >= min_coverage
                        and (cosine is null or cosine >= min_cosine_lexical)))       lexical acceptance, semantic veto
```

The top section is the first section of the returned order (fused when fusion applies). `min_cosine` and
`min_cosine_lexical` are the effective values of 6.5. The veto exists because acceptance by cosine alone can only
accept more questions: without a way for the vector to reject a question that shares words with the manual but asks for
something else, out-of-scope rejection could only fall. With `min_cosine_lexical` = -1.0 (the uncalibrated default) the
veto is off and the rule is "coverage or cosine".

`bm25`, `candidates`, `diversify`, `min_coverage` and `min_query_terms` are set by the platform evaluation (models.md 4)
and change only through `training/assistant/retrieval.json`, which the lock records. The fusion values and the two
cosine thresholds depend on the project's table and are calibrated per project on its indexed questions (6.7.7). No
value is ever tuned on the project bench, since the bench would then no longer measure it.

### 6.7 Static embeddings

**Status.** Implemented on 2026-11-16 (the builder `training/assistant/embed.py`, the reader and vectors in
`src/frontend/src/help/lexical/embeddings.ts`, fusion and decision in `ranking.ts`, `help:build`, `help:bench`, the lock) and
tested with a fake encoder; it has never run with the real embedding model, so no table exists yet. Until one is built,
calibrated and recorded (models.md 9.5), every build is lexical only, which is the state today. Without the table the bench is below its
gates (2026-11-16: recall at 3 is 0.58 in English and 0.61 in Italian).

Users write symptoms and goals ("I can't find what I did yesterday"); the manual uses the words of the screens. Lexical
ranking alone stays below the gates of 8.3 for that reason. A **static embedding table** closes part of the gap: one
vector per stem, distilled once at build time from a local Qwen embedding model (models.md 7.8). At query time no model
runs: the query vector is a weighted mean of vectors looked up in the table, so it works in every browser, on the CPU,
in a few milliseconds. The table is optional: without one, retrieval is lexical exactly as 6.5 and 6.6 describe.

| Piece | Where | Defined in |
|-------|-------|------------|
| Embedding model, distillation, lock entry, commands | `training/assistant/embed.py`, `training/assistant/config/embed.json`, `models/model.lock` | models.md 7.8, 8.1, 9.5 |
| Table file | `{lang}.bin` (6.7.2), at most 8 MiB per language | Here |
| Vocabulary rule | What enters the table and what never does (6.7.3) | Here |
| Pack block, manifest entry, build states | `help:build` (6.7.4) | Here |
| Loading, query and section vectors | Browser, bench and Python port (6.7.5, 6.7.6) | Here |
| Fusion and out-of-scope decision | 6.5, 6.6 | 6.5, 6.6 |
| Calibration | `help:bench -- --calibrate` (6.7.7) | Here |

#### 6.7.1 Lifecycle in one view

1. `help:build` builds the packs (lexical only).
2. `python -m training.assistant.embed build` reads the packs and the other sources of 6.7.3, runs the embedding model
   once per stem, and writes `{lang}.bin` into a run folder (models.md 7.8).
3. `help:build -- --embeddings {run folder} --unlocked`, then `help:bench -- --calibrate` writes `calibration.json` into
   the run folder (6.7.7), and `help:bench -- --compare` shows the gain over lexical ranking.
4. `lock --record --model embeddings` records the tables, their hashes, the model and its licence, and the calibrated
   values (models.md 8.1); the tables go to the artifact store like weights.
5. Every later build fetches the tables (`fetch --model embeddings`) and `help:build` uses them only when they match the
   lock (6.7.4).

#### 6.7.2 Table file

Binary, little-endian, one file per language. The writer is the Python builder only; TypeScript and Python both read it.

| Offset | Size (bytes) | Type | Field |
|--------|--------------|------|-------|
| 0 | 8 | ASCII | Magic `HELPEMB` followed by one zero byte |
| 8 | 2 | uint16 | `format_version`, `1` |
| 10 | 2 | ASCII | Language, `en` or `it` |
| 12 | 2 | uint16 | `dims` (d), default 128 |
| 14 | 2 | uint16 | Reserved, 0 |
| 16 | 4 | uint32 | `count` (N), number of stems |
| 20 | 4 | uint32 | `meta_bytes` (M) |
| 24 | 4 | uint32 | `vocab_bytes` (V) |
| 28 | 4 | uint32 | Reserved, 0 |
| 32 | 4 x d | float32[d] | `scales`: one quantisation scale per dimension, each greater than 0 |
| 32 + 4d | M | UTF-8 JSON | `meta` (below) |
| 32 + 4d + M | V | UTF-8 | Vocabulary: N stems, each followed by one byte 0x0A |
| 32 + 4d + M + V | N | uint8[N] | Weights, 1 to 255, in vocabulary order |
| 32 + 4d + M + V + N | N x d | int8[N x d] | Vectors, row-major in vocabulary order, values -127 to 127 |

- The file size is exactly `32 + 4d + M + V + N + N x d`; the scales start at a 4-byte boundary so a reader can view them
  as a `Float32Array`. At most 8 MiB (`table.max_bytes` of `embed.json`): the builder fails above.
- Vocabulary: stems as the analyzer of 6.4 produces them (NFC, diacritics removed), unique, sorted by code point, none
  empty, none containing 0x0A, every character in the Basic Multilingual Plane (so code point order and the UTF-16
  order of JavaScript agree; the builder checks it).
- Decoding stem t, dimension j: `v_t[j] = vector[t][j] x scales[j]`, computed in float64 (a float32 read into a
  JavaScript number or a numpy float64 is exact). Decoded rows are not renormalised.
- `meta`: compact JSON, keys in this order:

```json
{"kind":"help-embeddings","format_version":1,"lang":"en","model":{"id":"embeddings","version":"1.0.0","hub_id":"Qwen/Qwen3-Embedding-0.6B","revision":"97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3"},"analyzer":{"id":"snowball-en","version":2,"stopwords_sha256":"{hex}"},"config_sha256":"{hex}","inputs_sha256":"{hex}","sources":{"project":0,"wordlist":0,"tokenizer":0}}
```

| Field | Rule |
|-------|------|
| `model.id` | Always `embeddings`, the lock entry's id (models.md 8.1) |
| `model.version` | The table version the lock records (`--model-version` of the builder, default `1.0.0`) |
| `model.hub_id`, `model.revision` | The embedding model and its pinned 40-character commit (models.md 7.8) |
| `analyzer` | The analyzer the stems were made with; equal to the pack's `analyzer` block or the table is not used (6.7.4) |
| `config_sha256` | SHA-256 of the bytes of `training/assistant/config/embed.json` |
| `inputs_sha256` | SHA-256 of the lines `pack {build.inputs_sha256 of the pack}` and `{path} {SHA-256 of the file bytes}` for every other source file of 6.7.3, sorted, joined with `\n` |
| `sources` | Number of stems from each source group of 6.7.3 |

No timestamp and no machine name are written, so the same inputs, model, device and library versions give the same
bytes.

#### 6.7.3 What enters the table, and what never does

Per language, stems come from these sources, in this priority:

| Group | Source | What is taken |
|-------|--------|---------------|
| `project` | The built pack, `src/frontend/public/help/{lang}/pack.json` (or `--packs`) | Section titles, section texts and the indexed questions (`questions`) |
| `project` | `docs/manual/glossary.md`, section `## {lang}` (6.9) | Terms and aliases |
| `project` | `src/frontend/public/locales/{lang}/*.json` | Every string value, recursively, with `{{...}}` placeholders and `<...>` tags removed: the words of the screens |
| `wordlist` | `training/assistant/config/words.{lang}.txt`, only when `embed.json` names it with its licence | One word per line; none ships with the template |
| `tokenizer` | The embedding model's tokenizer at the pinned revision | Whole-word tokens, in token id order (models.md 7.8) |

- Each text goes through steps 1 to 5 of the analyzer (6.4); every token that survives gives one stem and one surface
  word for that stem.
- Every `project` stem is in the table. Then `wordlist` stems, then `tokenizer` stems by rank, until `table.max_stems`
  (default 40,000) stems are reached.
- Why general vocabulary: the bench questions use words the manual does not use. A user's word that occurs nowhere in
  the project's text has no vector unless the table holds general vocabulary; with project words only, the table could
  relate manual words only to other manual words. A general word that also appears in a bench question is fair: it was
  not taken from the bench.
- **Never** read by the builder: `docs/manual/bench.md` (out-of-scope questions and the Extra table), the bench
  questions of 6.2 (the builder takes questions only from the pack, which never holds them, and never reads story
  files), gap exports (7.4), source code other than the locale files, and any personal data.
- Only single words reach the embedding model, one stem at a time, never a sentence of the project.

#### 6.7.4 `help:build` and the pack block

`help:build` looks for `{dir}/{lang}.bin`, where `{dir}` is `--embeddings {dir}` or, by default,
`models/embeddings/weights/` (filled by `python -m training.common.fetch --model embeddings`, models.md 9.5). It reads
the `embeddings` entry of `models/model.lock` and `training/assistant/config/embed.json`. Per language, the first
matching row decides:

| State | Condition | Pack `embeddings` | Without `--strict` | With `--strict` |
|-------|-----------|-------------------|--------------------|-----------------|
| Off | `fusion.method` is `none`, or the effective `params.method` is `none` (6.7.7) | `null`, no file copied | Info line | Passes |
| None | No `{lang}.bin` | `null` | Info line "lexical ranking only" | Passes |
| Invalid | A check of 6.7.5 fails | `null` | Warning naming the check | Fails |
| Analyzer mismatch | `meta.analyzer` differs from the pack's `analyzer` | `null` | Warning "the table was built with another analyzer: rebuild it" | Fails |
| Unlocked | No lock entry `embeddings`, or the file's SHA-256 differs from the lock's file for the language, or `meta.model.version` or `meta.model.revision` differs from the entry | `null`; with `--unlocked`, the block with `locked: false` | Warning naming the difference | Fails; `--strict` with `--unlocked` is a usage error (exit 2) |
| Locked | Everything above holds | The block, `locked: true` | Used | Used |

For a used table, `help:build` also computes `vocab_coverage`: the sum of `tf` over all postings of the pack's index
(every field, glossary gains included) whose stem is in the table, divided by the sum of `tf` over all postings, rounded
to 4 decimals. Below `build.min_vocab_coverage` of `embed.json` (default 0.90) it warns, and `--strict` fails: the manual
gained words the table lacks, so the table is rebuilt (models.md 11). The table is still used meanwhile; a missing stem
only contributes nothing.

Effective parameters (`params`), in this order: the lock entry's `calibration.params.{lang}` for a locked table;
`languages.{lang}.params` of `{dir}/calibration.json` for an unlocked table used with `--unlocked`; otherwise the
defaults of `retrieval.json` with `calibrated: false`. `depth` always comes from `retrieval.json`.

Pack block, keys in this order:

```json
"embeddings": {
  "file": "embed.bin",
  "sha256": "{hex}",
  "bytes": 0,
  "format_version": 1,
  "dims": 128,
  "count": 0,
  "model": { "id": "embeddings", "version": "1.0.0", "hub_id": "Qwen/Qwen3-Embedding-0.6B", "revision": "97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3" },
  "locked": true,
  "vocab_coverage": 0.9731,
  "calibrated": true,
  "params": { "method": "rrf", "depth": 30, "rrf_k": 60, "embedding_weight": 1.0, "min_cosine": 0.55, "min_cosine_lexical": -1.0 }
}
```

`help:build` copies the table's bytes to `src/frontend/public/help/{lang}/embed.bin` and adds it to the manifest (6.8).
The pack is under 1 MB without the table; the table is a separate file so the pack stays small to parse and the
table's hash can be checked on its own.

#### 6.7.5 Loading

The browser loader (`src/frontend/src/help/runtime/loader.ts`) and the scripts (`scripts/help/pack.ts`) load a table
when the pack's `embeddings` is not `null`, from the manifest's `embeddings.{lang}.file`, and check in this order:

1. SHA-256 of the bytes equals `embeddings.sha256` of the pack, and of the manifest where one is read (in the browser with
   `crypto.subtle.digest`; when `crypto.subtle` is unavailable, the table is not used).
2. Size of at least 32 bytes, magic, `format_version` not higher than the reader knows (1), language equal to the
   pack's, `dims` and `count` equal to the block's, the exact size of 6.7.2.
3. `meta` parses, `meta.model` equals the block's `model`, `meta.analyzer` equals the pack's `analyzer`.
4. The vocabulary has `count` stems, sorted and unique; every weight is at least 1.

Any failure: retrieval runs lexical only for that pack, the reason is kept for the panel's status line and logged once
to the console; help never becomes unavailable because of a table. Search and the Ask tab wait until the table is
loaded or has failed before the first query, so the results of a question never change while the user reads them.

#### 6.7.6 Vectors at query time

`u_t` is the weight of stem t divided by 255; `v_t` its decoded vector (6.7.2). All arithmetic is float64.

```
section vector  s_d = normalise( sum over stems t in the table of
                                 (sum over fields f of w_f * tf(t,d,f)) * u_t * v_t )
query vector    q   = normalise( sum over the distinct query stems t in the table of u_t * v_t )
cosine(q, d)        = q . s_d
normalise(x)        = x / |x|; when |x| = 0 there is no vector
```

- `tf(t,d,f)` comes from the postings of the pack's index (glossary gains included), `w_f` from
  `bm25.field_weights`; the section vectors are computed once when the pack loads, from the pack alone, so the pack
  carries no vectors.
- The query stems are those of `stems()` in ranking (6.5): distinct, after stopwords. Stems absent from the table are
  skipped. A query with none in the table has no vector: the embedding list is empty and `cosine` is `null` (6.6).
- A section with no stem in the table has no vector and never enters the embedding list.

#### 6.7.7 Calibration

`rrf_k`, `embedding_weight`, `min_cosine` and `min_cosine_lexical` are calibrated per language, on the project's
**indexed questions only**, never on bench, out-of-scope or Extra questions.

```
cd src/frontend && npm run help:bench -- --calibrate --embeddings {run folder} --unlocked
```

It writes `{run folder}/calibration.json` and prints a summary. It runs in TypeScript because the numbers must come from
the same ranking code the browser runs (1.2).

1. **Items.** The pack's indexed questions of the language. Each is asked against an index built without that one
   question, as `--indexed` does (6.9); its section vectors are recomputed from that index.
2. **Slices.** Key `sha256("calibration\n{lang}\n{story}\n{normalised question}")` (normalised as in 6.2), sorted
   ascending; the first `ceil(held_out_share x n)` items (default 0.30) are the **held-out** slice, the rest the
   **tuning** slice. Fewer than `min_items` (default 30) indexed questions in a language: nothing is tuned,
   `calibrated: false`, `params` are the `retrieval.json` defaults, and the summary says so.
3. **Fusion grid**, on the tuning slice: every pair of `rrf_k` in `[10, 20, 40, 60]` and `embedding_weight` in
   `[0.5, 0.75, 1.0, 1.25, 1.5, 2.0]` (lists in `embed.json`). Keep the pair with the highest Recall@3; ties by MRR@10,
   then Recall@1, then the smaller `embedding_weight`, then the larger `rrf_k`. Metrics as in 8.3, with the item's story
   as the expected story.
4. **Veto threshold**, with that pair, on the tuning slice: `min_cosine_lexical` is the largest value of the grid 0.00,
   0.01, ..., 0.99 for which the share of tuning items with `coverage >= min_coverage` and a non-null `cosine` below the
   value is at most `max_veto_loss` (default 0.02); -1.0 (veto off) when no value of the grid qualifies.
5. **Semantic threshold**: `min_cosine` is the largest value of the same grid for which the false no-match of the
   tuning slice, under the full rule of 6.6, is at most `target_false_no_match` (default 0.05, half the gate of 8.3). If
   no value reaches the target: the value with the lowest false no-match, the largest on ties, and `target_missed: true`.
6. **Guard**, on the held-out slice: when fused Recall@3 is below lexical Recall@3, or fused false no-match is above
   lexical false no-match, `params.method` is `none` for the language (the table is not used, 6.7.4) and the reason is
   written.

Out-of-scope questions cannot be used for calibration (the only ones are in the bench), so the thresholds are set from
in-scope questions alone: each is the strictest value that keeps the in-scope losses within a stated budget. The bench
then measures out-of-scope rejection honestly.

`calibration.json`, keys in this order:

```json
{
  "schema_version": 1,
  "kind": "help-embeddings-calibration",
  "config_sha256": "{hex of embed.json}",
  "retrieval_sha256": "{hex of retrieval.json}",
  "languages": {
    "en": {
      "table_sha256": "{hex}",
      "pack_inputs_sha256": "{hex}",
      "calibrated": true,
      "items": { "tuning": 0, "held_out": 0 },
      "params": { "method": "rrf", "depth": 30, "rrf_k": 60, "embedding_weight": 1.0, "min_cosine": 0.55, "min_cosine_lexical": -1.0 },
      "target_missed": false,
      "metrics": {
        "lexical": { "tuning": { "recall_at_1": 0, "recall_at_3": 0, "mrr_at_10": 0, "false_no_match": 0 }, "held_out": { } },
        "fused": { "tuning": { }, "held_out": { } }
      },
      "notes": []
    }
  }
}
```

`lock --record --model embeddings` refuses a run without this file, or whose `table_sha256` differs from the run's
table (models.md 8.1). Calibrate again after any change of the table, of `retrieval.json` or of `embed.json`; a release
whose bench fails false no-match recalibrates before anything else.

#### 6.7.8 Commands

All in `src/frontend`, none needs approval (the builder of models.md 9.5 does):

| Command | What it does |
|---------|--------------|
| `npm run help:build -- --embeddings {dir} [--unlocked]` | Builds with the tables of `{dir}` instead of `models/embeddings/weights/`; `--unlocked` uses a table the lock does not record (6.7.4) |
| `npm run help:bench -- --compare` | The bench of 8.4 on the fused configuration (which gates) and, beside it, the same items with `fusion.method` = `none` and the difference per metric and language |
| `npm run help:bench -- --embeddings {dir} --unlocked --compare` | The same on a table not yet recorded; the report says "unlocked table: not a release result" |
| `npm run help:bench -- --calibrate --embeddings {dir} --unlocked` | 6.7.7 |
| `npm run help:bench -- --indexed` | As in 6.9; with a table, on the fused configuration |

With a usable table, `help:bench`, `--export` (8.4) and `help:retrieve` use the fused ranking and the decision of 6.6,
as the browser does.

#### 6.7.9 Tests

| Test | Where | Proves |
|------|-------|--------|
| Shared golden cases | `src/frontend/scripts/help/fixtures/embeddings.table.golden.json`, written by the Python builder from a fake encoder: inputs, the table's bytes (base64) and the decoded vectors. `src/frontend/scripts/help/fixtures/embeddings.golden.json`, written by `scripts/help/golden.ts --write` from that table and a small pack: section vectors, query vectors, embedding lists, lexical lists, fused lists and decisions for at least 12 queries | Python writes byte for byte what the golden says; TypeScript decodes it; the Python port (`training/assistant/lexical.py`) gives the same query vectors, section vectors, cosine order, fused order and decisions as TypeScript (tolerance 1e-9 on numbers, exact on orders) |
| Golden coverage | Same files | Cases for: a query with no stem in the table; a query with no lexical hit; ties on fused score; `depth` cutting both lists; diversification after fusion; semantic acceptance; lexical acceptance; the veto; `min_cosine_lexical` = -1 equal to the "coverage or cosine" rule; a section without vector |
| Loader | `src/frontend/src/help/lexical` and `src/help/runtime` tests | Every check of 6.7.5 fails closed to lexical ranking with its reason; search waits for the table |
| No table, no change | Existing lexical tests | With `embeddings: null` every result and decision equals today's |
| Build states | `scripts/help/help.test.ts` | Each row of 6.7.4, `vocab_coverage`, the parameter order, the pack block's key order, the manifest entry, byte-identical builds with a table |
| Calibration | `scripts/help/help.test.ts` | Slices are deterministic; only `pack.questions` are asked; grid tie-breaks; both threshold rules; the guard; too few items; the file's format |
| Bench | `scripts/help/help.test.ts` | `--compare` prints both configurations and gates on the fused one; an unlocked table is labelled |
| Builder | `training/tests/test_embed.py` | models.md 7.8 |

### 6.8 Versioning and determinism

- `schema_version` of pack, bundle and manifest is the `help-pack` version of `docs/contracts.json`. The runtime
  refuses a pack with a higher `schema_version` than it knows and falls back to "help is unavailable" with a link
  to the manual section.
- `help:build` twice on the same tree, with the same tables, gives byte-identical files (test in
  `src/frontend/scripts/help/help.test.ts`).
- `src/frontend/public/help/manifest.json`:

```json
{
  "schema_version": 2,
  "project_version": "0.1.0",
  "languages": ["en", "it"],
  "packs": { "en": { "file": "en/pack.json", "sha256": "{hex}" }, "it": { "file": "it/pack.json", "sha256": "{hex}" } },
  "bundles": { "en": { "file": "en/manual.json", "sha256": "{hex}" }, "it": { "file": "it/manual.json", "sha256": "{hex}" } },
  "embeddings": { "en": { "file": "en/embed.bin", "sha256": "{hex}" }, "it": null },
  "assistant_compatible": true
}
```

`embeddings` has one key per language: the table's file and SHA-256 when the pack uses one (6.7.4), otherwise `null`.

- `--strict` fails when any `manual:check` finding exists, when a bench question leaks (6.2), when
  `assistant.compatible` is `false` while `models/model.lock` has an `assistant` entry, or on a table state of 6.7.4
  that fails in strict mode.
- `help-pack` version 2 (2026-11-16): the `embeddings` block of 6.7.4 replaces the unimplemented block of version 1,
  the table file of 6.7.2 and the manifest's `embeddings` map are added. A version 1 pack has `embeddings: null` and
  reads unchanged.

### 6.9 Glossary

Users name things with their own words ("job", "company", "log in"); the manual uses the words of the screens ("task",
"organization", "sign in"). The indexed questions bridge that gap for their own story only; the glossary bridges it
for every page that uses the word. It is project content, kept in `docs/manual/glossary.md`, optional, and an input
of the pack (`build.inputs_sha256`).

```markdown
# Help glossary

## en

| Term | Users also say |
|------|----------------|
| task | job, item, work |

## it

| Term | Users also say |
|------|----------------|
| attività | lavoro |
```

| Rule | Value |
|------|-------|
| Term | A word or phrase the manual uses: a label of the screens or a domain noun |
| Users also say | Comma-separated words or phrases users say for the term |
| Evidence | Every alias appears in an indexed question (6.2) or a locale label of the language. Never take an alias from a bench question, an out-of-scope question or the Extra table: the bench would stop measuring retrieval |
| Expansion | At build time, for each section, the title and the text fields gain the stems of the aliases of every row whose term stems occur in the field as a contiguous run: each stem once per field, term frequency + 1. The questions field gains nothing. Field lengths (`index.lengths`) stay the token counts of the analysed text. The query is not expanded, so the runtime and the pack format are unchanged |
| Errors | `help:build` fails on a section other than `## en` or `## it`, on another table header, on a row without an alias, and on a row whose term or aliases are stopwords only |

Glossary terms and aliases are also vocabulary of the embedding table (6.7.3): a new alias reaches the table at its
next build.

Measure a glossary or parameter change with `npm run help:bench -- --indexed`: every indexed question is asked
against an index without that question, as the bench asks its held-out questions; it reports the retrieval metrics
and never gates. Judge the change on `help:bench` once, at the end.

---

## 7. In-page assistant

### 7.1 Tiers

| Tier | Available when | What answers |
|------|----------------|--------------|
| `retrieval` | Always (a pack is loaded) | Retrieval of section 6: the best sections and links to the manual |
| `browser` | The pack is `compatible`, the device passes the WebGPU check and the weights are cached or downloaded (models.md 7.2, 7.3) | The assistant model in the browser, on top of retrieval |
| `server` | The pack is `compatible` and the backend reports the model host available (models.md 7.4) | The same model on the server, on top of retrieval |

The order of `browser` and `server` is the profile setting `MyApp:Assistant:Order` (models.md 7.7). Retrieval
always runs first in the browser; the generation tiers receive the question and the ids of the selected sections.

### 7.2 Panel behaviour

The "Ask" tab: a text box (at most 300 characters) and an "Ask" button. UI texts below are the English ones; their
Italian counterparts are written in the `help` locale namespace and checked by `translate:validate`. On submit:

1. The panel shows the retrieved sections at once, before any model answers: up to 3 cards with page title,
   section heading, the first 160 characters, and "Open in the manual" linking to the section.
2. If the question is out of scope (6.6): the panel shows "The manual has nothing on this yet." with links to the
   manual home and to the search page for the same text, records a gap with outcome `no_match`, and stops. No
   model is called.
3. If a generation tier is available: an answer card appears above the section cards, labelled "Answer from the
   manual", and fills as tokens arrive. The source line of the answer is not shown as text; it is turned into
   source chips (`[1]`, `[2]`) that link to the cited sections, and the cited sections move to the top of the
   cards. A footer line says "Answered on this device" or "Answered by the server".
4. If only retrieval is available: the header of the cards reads "Best matches in the manual". Nothing suggests
   that a model answered.
5. Under every result: "Did this help?" with "Yes" and "No". "No" records a gap with outcome `not_helpful`.

Sources are always shown: an answer without a valid source line is never displayed (7.3).

While the browser weights download (first use), the panel shows "Preparing the assistant on this device
({done} of {total} MB)" and meanwhile answers with the next tier.

### 7.3 Refusal and fallback

| Model output | The user sees | Gap outcome |
|--------------|---------------|-------------|
| A valid answer (models.md 3.4) | Answer, source chips, section cards | none |
| The refusal sentence of the language | "The assistant could not answer this from the manual. These sections are the closest:" and the section cards | `refused` |
| Invalid format, or a source number outside the context | Same as refusal | `invalid` |
| Timeout or error of the tier | The next tier is tried for this question; when none is left, the retrieval result with the header of 7.2 step 4 | `timeout` only when every tier failed |

The raw refusal sentence is never shown: the panel replaces it with the message above, so the user always gets the
retrieved sections.

### 7.4 Documentation gaps

Every question that the documentation did not answer becomes a gap record that returns to the stories and the
manual.

Record (sent by the panel to the backend, which stores it):

```json
{
  "schema_version": 1,
  "recorded_at": "2026-11-06T10:15:00Z",
  "lang": "en",
  "route": "/tasks",
  "question": "can i get an email when someone assigns me a task",
  "outcome": "no_match",
  "tier": "retrieval",
  "pack": { "project_version": "0.1.0", "sha256": "{hex}" },
  "top_sections": ["tasks/US-TASK-01#overview", "tasks/US-TASK-02#overview"],
  "coverage": 0.22
}
```

| Rule | Value |
|------|-------|
| `outcome` | `no_match`, `refused`, `invalid`, `not_helpful`, `timeout` |
| Masking | Before storing, the backend replaces e-mail addresses with `[email]`, URLs with `[url]`, and any run of 4 or more digits (with spaces, dots or dashes inside) with `[number]`; the panel applies the same rules before sending |
| Identity | No user id, user name or organization is stored |
| Size | `question` at most 300 characters after masking |
| Retention | `MyApp:Help:Gaps:RetentionDays` (default 90); `MyApp:Help:Gaps:Enabled` (default `true`) switches recording off |
| Export | Admins download the records of a period as a JSON array (the behaviour is a story of the help area) |

Triage: `npm run help:gaps -- --in {export.json}` prints, per route, clusters of questions (two questions are in
the same cluster when the Jaccard index of their stem sets is at least 0.5), with counts and the most frequent top
section. Each cluster gets one of these outcomes, done as a normal change:

| Finding | Action |
|---------|--------|
| The manual answers it, retrieval missed | Add a question in the user's words to the story ("Questions users ask"); it lands in the indexed or bench set by 6.2 |
| The app does it, the docs do not say | Update the story (criteria, rules or non-goals), then the pages |
| The app does not do it | Add the question to `docs/manual/bench.md` under Out of scope; if users keep asking, propose a feature in the roadmap |
| The manual answers it and the model refused | Add the question to the Extra table of `docs/manual/bench.md` with its story; it measures the model at the next release |

---

## 8. Project bench

The questions that measure retrieval and answers for this project at each release. It is never training data.

### 8.1 Sources

| Set | Source | Used for |
|-----|--------|----------|
| In-scope, from stories | The bench questions of 6.2 (every story, both languages) | Retrieval and answers |
| In-scope, extra | The Extra table of `docs/manual/bench.md` | Retrieval and answers |
| Out of scope | The Out of scope lists of `docs/manual/bench.md` | Out-of-scope decision and refusals |

`docs/manual/bench.md` sits outside `docs/areas/`, so the area layout (development.md R02) is unchanged.

### 8.2 `docs/manual/bench.md`

Exactly these headings. A list or table with nothing in it contains `None.`

```markdown
# Help bench

## Out of scope

### en

1. Can I pay my invoice from here?

### it

1. Posso pagare la fattura da qui?

## Extra

| Question | Language | Story |
|----------|----------|-------|
| "Where do I see what's due this week?" | en | US-TASK-05 |
```

| Rule | Value |
|------|-------|
| Out of scope | Questions an end user of this application could plausibly ask that no page answers: features the product does not have, other software, general knowledge. At least 10 per language, and at least 20% of the number of in-scope bench questions of that language |
| Extra | Questions with the story whose page answers them; `Language` is `en` or `it`; `Story` names a story file. A question that equals an indexed question fails M16 |
| Italian | Written as an Italian user asks, not translated |

### 8.3 Metrics and thresholds

Retrieval metrics are computed per language on the pack of that language. A hit for an in-scope question is a
returned section whose `story` equals the expected story.

| Metric | Definition | Gate per language |
|--------|------------|-------------------|
| Recall@1 | Share of in-scope questions with a hit at rank 1 | >= 0.60 |
| Recall@3 | Share with a hit in the top 3 | >= 0.80 |
| MRR@10 | Mean of 1 / rank of the first hit (0 when none in the top 10) | Reported |
| False no-match | Share of in-scope questions judged out of scope (6.6) | <= 0.10 |
| Out-of-scope rejection | Share of out-of-scope questions judged out of scope | >= 0.70 |

Answer metrics apply when the shipped profile enables a generation tier; they are defined in models.md 4.3 and
measured end to end (retrieval as at run time) on the same bench items:

| Metric | Gate per language |
|--------|-------------------|
| Faithful (of answered) | >= 0.95 |
| Useful (of in-scope) | >= 0.65 |
| Wrongful refusal (of in-scope with a hit in the context) | <= 0.15 |
| Out-of-scope refusal (no-match or model refusal) | >= 0.90 |
| Correct source (of answered) | >= 0.85 |

A language with fewer than 20 in-scope bench items reports its metrics but does not gate; the report says so.

A release fails when any gate fails. The thresholds change only in this document, in a reviewed change.

### 8.4 Runner

| Command | Where | Gates |
|---------|-------|-------|
| `npm run help:bench` | Release; no pipeline runs it yet. With a usable table (6.7.4) the items go through the fused ranking and the decision of 6.6, exactly as in the browser; without one, through the lexical ranking | Retrieval metrics |
| `npm run help:bench -- --compare` | Release, and the decision of models.md 9.5 step 5: the fused report gates, the lexical one is printed beside it, and an unlocked table is labelled "not a release result" (6.7.8) | Retrieval metrics of the fused configuration |
| `npm run help:bench -- --calibrate --embeddings {run folder} --unlocked` | After a table build (models.md 9.5 step 4); indexed questions only, never bench questions | None: it writes `calibration.json` (6.7.7) |
| `npm run help:bench -- --export {jsonl}`, then `python -m training.assistant.eval --suite project --items {jsonl} --export gguf --run {run_id} --approved-estimate {id}`, then `npm run help:bench -- --answers {report}` | Release when a generation tier ships (models.md 12) | Answer metrics |

`help:bench` prints a table per language (metric, value, threshold, pass) and the in-scope misses with their top 3
sections, and exits with code 1 when a gate fails. The export writes one line per item:
`{ "id", "lang", "question", "kind": "in_scope|out_of_scope", "story", "source": "story|extra|oos", "retrieved": [section ids], "context": [section ids selected by models.md 3.5], "in_scope_decision": true }`.

---

## 9. Release summary

| Step | Command | Fails the release when |
|------|---------|------------------------|
| 1 | `npm run manual:check -- --strict` | Any finding |
| 2 | `npm run help:build -- --strict` | Any finding, leak or incompatibility |
| 3 | `npm run help:bench` | A retrieval gate fails |
| 4 | Answer bench and lock checks | See models.md 12 |

Screenshots are regenerated before a release only when screens changed, through the approved capture run of 4.7.

---

## 10. Formats defined here

| Format key | Section |
|------------|---------|
| `manual-page` | 2 (layout, front matter, body, Markdown subset, parity) |
| `manual-captures` | 4.2, 4.6 (image names and capture manifest) |
| `help-pack` | 5.1, 6 (manual bundle, pack, manifest, analyzer, split) |
| `assistant-retrieval` | 6.5, 6.6 (`training/assistant/retrieval.json`) |
| `help-gap` | 7.4 |
| `help-bench` | 8.2 |

A change to any of them follows development.md section 13; `manual-page`, `help-pack` and `assistant-retrieval`
are also retraining or re-evaluation triggers for the assistant (models.md 11).
