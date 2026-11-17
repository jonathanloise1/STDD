# CLAUDE.md

Rules for working in this repository. They apply to every session and to every subagent a session starts.

## What this repository is

A project template built on Story-Test Driven Development (STDD), with a sample application (MyApp) that
proves every part of the method. Projects start by copying this repository, so everything in it must be
generic: never bring client names, paths or content from other projects into it.

## Before any change

1. Read `docs/guidelines/development.md`, `docs/guidelines/architecture.md`, `docs/guidelines/frontend.md`,
   `docs/overview.md` and `docs/roadmap.md`.
2. Read the area you are touching under `docs/areas/{area}/`.
3. Follow the STDD order: user stories, then test cases, then implementation, then automated tests, then the
   user manual. Do not implement a behaviour that has no user story and no test case.

Where existing content contradicts the rules in this file, the rules win: fix the content, do not imitate it.

## Execution plan (mandatory)

Before starting any activity that changes files, create `executionplan_temp_{activity-name}.md` in the
repository root. Read-only questions and analysis do not need one.

The file has four sections:

| Section | Content |
|---------|---------|
| Overview | Goal, deliverables, what is out of scope |
| Problem | The verified facts that motivate the activity |
| Solution | The approach, the decisions taken, the open decisions |
| Tasks | Tasks in groups; each task has an id, the model that carries it out (`opus`, `sonnet` or `haiku`), its dependencies and a status |

Task status is `todo`, `in progress` or `completed`. Set `in progress` before starting a task and `completed`
only after its result has been verified. Keep the file current while you work.

Summarise the plan to the user when you create it. Delete the file when every task is completed and the user
has confirmed the activity is closed.

## Actions that always need the user's approval

| Action | Notes |
|--------|-------|
| Running E2E tests | Any run, full or filtered |
| Regenerating the user manual | Screenshot capture and page generation |
| `git commit` | |
| `git push` | |
| Creating a branch | |
| Running integration tests | They use a database |
| Screenshot capture for the manual | Any run; it is also an E2E run |
| `python -m training.assistant.embed build` | It downloads (the first time) and runs the embedding model; `--dry-run` needs no approval |

- Ask every time. An approval covers one action and does not carry over to the next.
- The user usually makes many changes and runs the whole suite once at the end. Do not ask after each change:
  finish the work that does not depend on the answer, then ask.
- Build, type-check, lint, unit tests and architecture tests can run without asking.
- Subagents never perform these actions. Only the main session does, after approval.

## Delegation

| Model | Use for |
|-------|---------|
| `opus` | Design, contracts, evaluation design, review |
| `sonnet` | Implementation against a defined design, documentation rewrites, tests |
| `haiku` | Mechanical work: cleanup, renames, inventories, bulk reformatting to a fixed template |

Give each subagent a precise scope and the files to read. A subagent must not perform the actions listed above,
must not write outside its scope, and must not leave scratch files. The main session checks a subagent's key
claims before relying on them.

Subagents never use git commands that change the working tree: other agents' uncommitted work would be destroyed.

## Repository rules

- **English only** for documentation, code, comments and commit messages. The application UI ships in English
  and Italian.
- **One source of truth.** Reference the guidelines; do not copy them into other files.
- **User stories describe what the user sees and does.** No endpoints, DTOs, status codes or table names.
- **E2E tests drive the UI through page objects.** A test that only calls the API is an integration test.
- **Traceability.** Code and tests carry the user story id; test cases link to their story; tests carry the
  test case id.
- **Follow the patterns already in the code.** Do not add libraries or layers the repository does not use.
- **Never commit** secrets, model weights, generated datasets or temporary plans without approval.

## Models and knowledge (design constraints)

- Models are trained on the template's formats and protocol, never on a project's content. A project ships a
  knowledge pack generated from `docs/` with the released model.
- The formats of stories, test cases, manual pages, packs, tool catalog and incident report are contracts.
  Changing one is a breaking change: bump its version and re-run the evaluation.
- Deterministic first: retrieval before generation, rules before the model, masking in the tool layer before
  any data reaches a model.
- Every model-backed feature runs on a GPU when one is present, falls back to CPU, and ends in a tier that
  works without a model.

## Commands

| What | Command |
|------|---------|
| Backend | `cd src/backend/MyApp.WebApi && dotnet run --launch-profile https` |
| Frontend | `cd src/frontend && npm install && npm run dev` |
| Frontend checks | `cd src/frontend && npx tsc -b && npm run lint && npm run translate:validate && npx vitest run` |
| Unit and architecture tests | `dotnet test tests/unit/MyApp.UnitTests` |
| Documentation and traceability rules | `dotnet run --project tools/MyApp.DocTools -- check`; `sync-automated-by` after adding a test class |
| Monitor tests | `dotnet test tests/support/MyApp.Monitor.IntegrationTests` |
| Monitor | `dotnet run --project src/support/MyApp.Monitor -- {command}` (`operations.md` 14.4) |
| Training tests | `python -m pytest training/tests` |
| Manual check | `cd src/frontend && npm run manual:check` |
| Help pack and retrieval bench | `cd src/frontend && npm run help:build && npm run help:bench` (need the manual pages); `npm run help:bench -- --compare` shows the embedding table's gain; `-- --calibrate --embeddings {run folder} --unlocked` calibrates it (`manual.md` 6.7.8) |
| Embedding table: dry run, probe, lock check | `python -m training.assistant.embed build --dry-run`; `python -m training.assistant.embed probe --run {run_id} --lang {lang} --question TEXT`; `python -m training.common.lock --verify` (`models.md` 7.8, 8.2) |
| Embedding table build (approval required: it downloads and runs a model) | `python -m training.assistant.embed build`; then `lock --record --model embeddings`, `fetch --model embeddings` (`models.md` 9.5) |
| Integration tests (approval required) | `dotnet test tests/integration/MyApp.IntegrationTests`, with `IntegrationTests__ConnectionString` |
| E2E tests (approval required) | `cd tests/e2e/MyApp.E2E && dotnet test`, with `E2E_CONNECTION_STRING`. The run uses its own database, `MyApp_E2E`, which it drops and seeds at start |
| Manual capture and generation (approval required) | `tests/e2e/MyApp.E2E/README.md` "Manual screenshots"; `manual.md` 1.3 |
| Model training (approval required for each `[COST]` step) | `models.md` section 9 |
| Everything in Docker | `cp .env.example .env`, then `docker compose up -d --build` (application on http://localhost:3147); profiles `monitor`, `assistant-model`, `investigator-model`, `teacher`, `integration` and `e2e` (both need approval); the frontend image builds the help pack from `docs/manual`; GPU with `-f compose.yaml -f compose.gpu.yaml`. README "Run with Docker" |

No part of the template calls a cloud AI service: teacher, judge and assistant models run on local hosts.

## Related files

`.github/agents/myapp.agent.md` is the counterpart of this file for GitHub Copilot. Keep the two aligned.
