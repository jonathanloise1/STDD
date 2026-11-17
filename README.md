# STDD Template

A project template built on Story-Test Driven Development (STDD), with a complete sample application (MyApp) that
shows every part of the method working. Copy it, replace the example domain with yours, and keep the method.

MyApp is a small multi-tenant task manager: .NET 10 Web API, React 19 and TypeScript frontend, SQL Server, a user
manual in English and Italian, an in-app help assistant, and a monitor that reads the logs and prepares incident
reports. Everything runs on one machine with no cloud service and no GPU.

---

## Quick start

You need Docker with Compose v2. Nothing else.

```bash
cp .env.example .env
docker compose up -d --build
```

The first build takes about 3 minutes (with the base images already pulled). Then open <http://localhost:3147>.
You see the application already signed in as the example admin user, with seed data, the user manual and the Help
panel. The `.env` values are local placeholders; change the two secrets before any machine other people can reach
runs it.

Optional, the monitor dashboard (two containers and a small relay image):

```bash
docker compose --profile monitor up -d
```

It serves <http://localhost:5310>. Stop everything with `docker compose down`; add `-v` to delete the data too.
More on Docker, profiles and sign-in: [Run with Docker](#run-with-docker).

Expected noise: the frontend build prints a chunk size warning, and the backend logs data-protection key warnings
at start. Both are harmless here.

SQL Server in compose is the Developer edition: free for development and test only. A production deployment needs a
licensed edition or another database.

---

## Status

As of 2026-11-17, stated as facts:

| Item | State |
|------|-------|
| E2E tests | 178 of 178 pass on the current frontend |
| Integration tests | 108 of 108 pass, on their own database |
| Unit and architecture tests | 598 |
| Monitor tests | 834 |
| Frontend tests | 533 |
| Training pipeline tests | 621 (light dependencies only) |
| User manual | English and Italian, 90 pages, 80 screenshots |
| Help retrieval | Lexical. Below its bench gates until the embedding table is built: recall at 3 is 0.58 (English) and 0.61 (Italian) against 0.80 |
| Dependency audit | `npm audit`: 0 vulnerabilities. NuGet (`dotnet list package --vulnerable --include-transitive`): none |
| Models | None downloaded, none trained |
| CI pipeline | None, by choice (architecture.md section 7) |

---

## Why STDD

AI coding agents improvise: given a vague prompt they write code that looks right and drifts from the intent.
Spec Driven Development answered with a written specification before the code. A specification says what to build,
but not when the result has gone wrong.

STDD makes **test cases a first-class artifact**, written with the user stories before any code exists:

> Specs define the intent. Tests enforce the boundary.

```
SDD:   Spec -> Code -> (hope it is right)
STDD:  Spec + Tests -> Code -> Verify -> Repeat until the tests pass
```

Stories capture the what, test cases capture how to verify it, and the code can be regenerated as long as the
tests pass. A checker (`tools/MyApp.DocTools`) enforces the formats and the traceability from story to test case
to test to code.

### The workflow

Every feature follows the same sequence; the rules of each step are in
[development.md](docs/guidelines/development.md).

1. Requirements: one story file per story, with acceptance criteria, access control and error messages.
2. Test specification: test cases for the happy path, validation, business rules, edge cases, errors and security.
3. Implementation: backend, frontend, page objects and tests, tagged with story and test case ids.
4. Verification: unit, architecture, integration and E2E tests until green.
5. Review: code review, manual QA, stakeholder acceptance.
6. Manual and support readiness: user manual regenerated, help questions answered, one runbook per log key.

Each area has a predictable structure:

```
docs/areas/{area}/
  userstories.md           Area index: overview, rules, access, story list
  testsuite.md             Coverage matrix: story to test cases
  operations.md            Runbooks, one per log key (when the area has any)
  user-stories/US-{AREA}-NN.md
  test-cases/{AREA}-{ACTION}-NNN.md
```

### After release

| Part | What it adds | Defined in |
|------|--------------|------------|
| User manual | Pages generated from the stories and screenshots of the running application, English and Italian | [manual.md](docs/guidelines/manual.md) |
| In-page assistant | A Help panel on every screen that answers from the manual and shows its sources | [manual.md](docs/guidelines/manual.md) |
| Monitor | Reads the logs, matches each failure to its runbook, prepares an incident report | [operations.md](docs/guidelines/operations.md) |
| Trained models | The assistant and the incident investigator, trained on the template's formats, never on a project's content | [models.md](docs/guidelines/models.md) |

Models are optional. Without one, the assistant shows the best sections of the manual and the monitor runs on
rules. No model has been trained for this template yet.

---

## What is included

| Feature | Description |
|---------|-------------|
| Authentication | Passwordless sign-in by email and one-time code (Microsoft Entra External ID); a local sign-in for development |
| Multi-tenancy | Organizations with Admin, Editor and Viewer roles; invitations; team management |
| Task management | Create, edit, change the status of and delete tasks; Viewers read only |
| Dashboard | Task counts per status, latest tasks, greeting |
| Audit trail | Append-only change tracking, readable and filterable by Admins |
| Multilingual | English and Italian |

Stack: ASP.NET Core 10 with Clean Architecture, Entity Framework Core 10, FluentValidation, hand-written mapping and
SQL Server; React 19, TypeScript, Vite, react-bootstrap on Bootstrap 5, lucide icons, self-hosted Inter and MSAL.
Details in [architecture.md](docs/guidelines/architecture.md).

## Repository structure

```
.env.example                   Settings of compose.yaml (copy to .env)
compose.yaml                   The whole template in Docker, by profile
compose.gpu.yaml               Override: NVIDIA GPU for the model hosts
compose.sqlserver-port.yaml    Override: publishes SQL Server on 127.0.0.1:14330
docker/                        Health check script shared by the images
docs/
  overview.md, roadmap.md      Product overview and feature status
  contracts.json               Versions of the STDD formats
  guidelines/                  architecture, development, frontend, manual, models, operations
  areas/                       Stories, test cases, runbooks per area
    audit/ authentication/ dashboard/ help/ onboarding/ organizations/ tasks/
  support/                     Contracts of the monitor: tools, masking, profiles
  manual/                      Generated user manual (English and Italian, with screenshots)
src/
  backend/                     .NET 10 Web API: Domain, Application, Infrastructure, WebApi
  frontend/                    React 19 and Vite
  support/
    MyApp.Monitor/             The monitor application
    model-host/                Launchers of the local model host (also the Docker entry point)
tests/
  unit/                        Unit and architecture tests
  integration/                 API-level tests with a real database
  e2e/                         E2E tests driving the UI through page objects
  support/                     Tests of the monitor
tools/MyApp.DocTools/          Documentation checker and generators
training/                      Training pipeline for the models
models/                        Model lock file, cards, evaluation reports
.github/agents/                Agent instructions for GitHub Copilot
CLAUDE.md                      Rules for AI agents working in this repository
LICENSE, THIRD-PARTY-NOTICES.md
```

---

## Run with Docker

Everything is in [compose.yaml](compose.yaml) (project `myapp`); every container and volume is named `myapp-*`. Only
the frontend publishes a port (`127.0.0.1:3147`). SQL Server publishes none, so it never meets another SQL Server on
port 1433. To reach it from the host (for example from a database client), add the override, which publishes
`127.0.0.1:14330`; the sa password is `MSSQL_SA_PASSWORD` of `.env`:

```bash
docker compose -f compose.yaml -f compose.sqlserver-port.yaml up -d
```

| Profile | Adds | Start |
|---------|------|-------|
| (default) | `sqlserver`, `backend`, `frontend` (nginx serves the build and forwards `/api`, so there is no CORS) | `docker compose up -d` |
| `monitor` | The monitor, reading the backend's log volume; dashboard on `http://localhost:5310` | `docker compose --profile monitor up -d` |
| `assistant-model`, `investigator-model`, `teacher` | A llama.cpp server on `127.0.0.1:8091`, `8092` or `8093`. Needs a model file | `docker compose --profile teacher up` |
| `integration` | The integration tests against the compose SQL Server (owner's approval) | `docker compose --profile integration run --rm integration` |
| `e2e` | The E2E runner with its own backend, frontend and database (owner's approval) | See the [E2E README](tests/e2e/MyApp.E2E/README.md) |

**Health routes.** Backend: `/health` (also through the frontend on port 3147). Frontend container:
`/nginx-health`. Monitor: `/api/health`.

**Local sign-in.** A real sign-in needs a Microsoft Entra External ID tenant. Without one, the backend runs in
`Development` (`APP_ENVIRONMENT` in `.env`) and the frontend gets `VITE_DEV_MOCK_USER_ID`, the seed's admin user, set
in `.env.example`. That skips the sign-in page and works only in Development. For a real sign-in set
`APP_ENVIRONMENT=Production`, empty `VITE_DEV_MOCK_USER_ID`, the `VITE_AZURE_AD_B2C_*` and `VITE_API_*` values in
`.env`, and the backend's `AzureAd__*` settings in `backend.local.env` (one `KEY=value` per line, git-ignored).
Production loads no seed data and shows no Swagger. The frontend settings are read when the container starts, so a
change needs `docker compose up -d`, not a rebuild.

**Help pack.** The frontend image builds the help pack from `docs/manual`, so the Manual page and the Help panel work
on a fresh copy.

**Models.** Nothing is downloaded. Put GGUF files in a folder and point `MODELS_DIR` and the `*_MODEL_FILE` values of
`.env` at them. With an NVIDIA GPU on Linux or Windows (WSL 2) add `compose.gpu.yaml`:
`docker compose -f compose.yaml -f compose.gpu.yaml --profile teacher up`. Docker on macOS cannot use the GPU.

**Apple silicon.** The SQL Server image is amd64 only and runs emulated: enable "Use Rosetta for x86_64/amd64
emulation" in Docker Desktop.

---

## Run without Docker

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), [Node.js](https://nodejs.org/) 22.18 or
later, and a SQL Server. The simplest SQL Server is the compose one with its port published. Run every block from the
repository root.

**1. SQL Server** (needs Docker and a `.env`; the password in the connection string below must match
`MSSQL_SA_PASSWORD`):

```bash
cp .env.example .env
docker compose -f compose.yaml -f compose.sqlserver-port.yaml up -d sqlserver
```

It listens on `localhost,14330`. A SQL Server you already have works too.

**2. Backend.** Trust the development certificate once, create the settings file, then edit
`ConnectionStrings:DefaultConnection` in it (server `localhost,14330`, user `sa`, your password):

```bash
dotnet dev-certs https --trust
cp src/backend/MyApp.WebApi/appsettings.Development.json.example src/backend/MyApp.WebApi/appsettings.Development.json
```

```bash
cd src/backend/MyApp.WebApi
dotnet run --launch-profile https
```

The backend migrates the database and, in Development, loads the seed data at start. API: `https://localhost:7342`,
Swagger at `/swagger`, health at `/health`. In Development the API accepts the header `X-MyApp-Impersonate-UserId`
with a user id from the seed, as a stand-in for a bearer token (the seed's admin is
`00000000-0000-0000-0000-000000000001`).

**3. Frontend** (in a second terminal, from the repository root). To sign in locally without an identity provider,
uncomment `VITE_DEV_MOCK_USER_ID` and `VITE_DEV_MOCK_USER_EMAIL` in `.env.development.local`:

```bash
cd src/frontend
npm install
cp .env.development.local.example .env.development.local
```

Edit `.env.development.local` as said, then:

```bash
cd src/frontend
npm run help:build
npm run dev
```

The app is at <http://localhost:3147>. `npm run help:build` builds the pack the in-app help reads from `docs/manual`;
without it the Help panel and the Manual page have nothing to show.

---

## Tests

Checks that need no database and no approval:

```bash
dotnet run --project tools/MyApp.DocTools -- check        # documentation and traceability rules
dotnet test tests/unit/MyApp.UnitTests                    # unit and architecture tests
dotnet test tests/support/MyApp.Monitor.IntegrationTests  # the monitor
```

The training tests need a Python environment (3.11 or 3.12 recommended), created once:

```bash
python -m venv .venv && . .venv/bin/activate
pip install -r training/requirements-light.txt
python -m pytest training/tests
```

The frontend checks need `npm install` first:

```bash
cd src/frontend
npm install
npx tsc -b && npm run lint && npm run translate:validate && npx vitest run
```

In this repository's rules ([CLAUDE.md](CLAUDE.md)) these need the owner's approval for every run, because they use a
database or a browser: E2E tests, integration tests, the manual regeneration and the screenshot capture. Their setup is
in [tests/e2e/MyApp.E2E/README.md](tests/e2e/MyApp.E2E/README.md) (the E2E run uses its own database, `MyApp_E2E`, which it
drops and seeds at start; `dotnet test --settings .runsettings.headless` runs without a visible browser) and
[tests/integration/MyApp.IntegrationTests/README.md](tests/integration/MyApp.IntegrationTests/README.md).

---

## Adopting this template

In this order. Each step is useful without the next one.

1. **Replace the domain.** Read [development.md](docs/guidelines/development.md), study `docs/areas/`, then write your
   areas: stories, test cases, page objects, E2E tests, capture classes, backend, frontend and seed data. Keep the
   structure, change the domain. Rename `MyApp` in the solution, the projects and the compose files.
2. **Regenerate the derived documents.** `dotnet run --project tools/MyApp.DocTools -- sync-automated-by`, then `check`
   until it reports nothing. Write `docs/manual/bench.md`, the runbooks in each `operations.md`, and
   `docs/support/entities.json` and `masking-policy.json` for your data.
3. **Run the tests.** Unit, architecture and frontend checks first, then integration and E2E. Until the manual exists,
   leave out the help area: `dotnet test --filter "TestCategory!=help"` in `tests/e2e/MyApp.E2E`.
4. **Generate the manual**, then run the help tests. The steps are in
   [manual.md 1.3](docs/guidelines/manual.md#13-producing-the-manual-the-first-time).
5. **Choose: retrieval only, or a first training.** Retrieval only is a valid way to ship: the assistant shows the best
   manual sections with links and the monitor runs on rules. Note that retrieval is below its bench gates until the
   embedding table is built. To train, use a GPU machine and follow
   [models.md section 9](docs/guidelines/models.md#9-first-training-for-an-adopting-project) and
   [training/WINDOWS-CHECKLIST.md](training/WINDOWS-CHECKLIST.md).
6. **Set up the monitor profile.** Adapt `docs/support/profiles/onprem.yaml` or `cloud.yaml`, then
   `dotnet run --project src/support/MyApp.Monitor -- pack build --profile {name}` and `run --profile {name}`
   ([operations.md](docs/guidelines/operations.md) 12 and 14). Without a recorded investigator it runs on rules.

---

## Documentation

| Document | Description |
|----------|-------------|
| [Product Overview](docs/overview.md) | What MyApp does |
| [Roadmap](docs/roadmap.md) | Status of every feature |
| [Architecture](docs/guidelines/architecture.md) | Tech stack, infrastructure, conventions |
| [Development Guidelines](docs/guidelines/development.md) | STDD workflow, formats, test strategy, traceability |
| [Frontend Guide](docs/guidelines/frontend.md) | Design system, UI patterns, component standards |
| [Manual Guidelines](docs/guidelines/manual.md) | User manual generation, in-page help and assistant |
| [Model Guidelines](docs/guidelines/models.md) | Training, evaluation and runtime of the models |
| [Operations Guidelines](docs/guidelines/operations.md) | Logging, runbooks, monitor and incident reports |

---

## Licence

MIT, see [LICENSE](LICENSE). Third-party material and licences: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
