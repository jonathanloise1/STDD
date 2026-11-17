# MyApp E2E Tests

End-to-end tests with Playwright for .NET. They drive the UI in a browser through page objects and assert only
what the user sees. A test that calls the API without a page is an integration test and lives in
`tests/integration/MyApp.IntegrationTests/` (development.md 10.1).

The rules these tests follow are in `docs/guidelines/development.md`: sections 8 (test cases), 10 (levels and
structure), 11.1 (tags), 12 (rules R16 to R22) and 16 (switches).

---

## Quick Start

Running the E2E tests needs the approval of the repository owner (`CLAUDE.md`). Build without approval:

```bash
cd tests/e2e/MyApp.E2E
dotnet build
```

### Prerequisites for the first run

In this order:

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Node.js.
2. Install the Playwright browsers once: `dotnet build`, then `pwsh bin/Debug/net10.0/playwright.ps1 install`.
3. Run `npm ci` in `src/frontend`.
4. Build the manual and the help pack the help tests read: `npm run help:build` in `src/frontend`.
5. Give the run a SQL Server. Set the connection string in the environment (never in a tracked file):

   ```bash
   export E2E_CONNECTION_STRING="Server=localhost,1433;User Id=...;Password=...;TrustServerCertificate=True;"
   ```

   Its `Database` part is ignored. The run uses its own database, `MyApp_E2E` (`E2E_DATABASE_NAME` changes the
   name; it must end in `_E2E` or `_Test`). Without the connection string the run stops and names the setting.
6. Make sure nothing answers on the backend port (`7342`): the run starts the backend itself, because only a
   backend it started can reset the database.
7. Ask for the approval to run (`CLAUDE.md`), then `dotnet test`.

**The run never touches another database.** The backend drops only the database named by `E2E_DATABASE_NAME`,
and only when all of these hold: the environment is Development, `MyApp:Testing:ResetDatabaseOnStart` is `true`
(the run sets it), and the name ends in `_E2E` or `_Test`. Otherwise it stops at start. The SQL Server may be
shared with other projects; their databases are not reached.

### Run tests (with approval)

```bash
cd tests/e2e/MyApp.E2E
dotnet test
```

With `AutoStartApps` on (the default) the run starts the backend and the frontend itself:

- the backend starts in the **Development** environment with the **fault switch enabled**
  (`MyApp__Testing__FaultInjection__Enabled=true`), on the E2E database (`ConnectionStrings__DefaultConnection`)
  with `MyApp__Testing__ResetDatabaseOnStart=true`, and, when `FixedUtcNow` is set, the fixed clock;
- before it answers, the backend **drops the E2E database, migrates it and loads the seed data**, so every run starts
  from a fresh seed and a test that consumes seed data (removing a member, finishing a setup) can run again;
- the frontend starts with `npm start` and these settings, given by the run so the owner's `.env` files do not
  matter: no `VITE_DEV_MOCK_USER_ID` (a mock user would make every visitor signed in), and an identity provider on
  the made-up host `login.e2e.test`, which the visitor's browser context answers itself
  (`Infrastructure/IdentityProviderStandIn.cs`). No tenant is needed for "the sign-in page opens".

With `AutoStartApps` off, start both yourself and reset the database first, by hand:

1. Use a database whose name ends in `_E2E` or `_Test`, for example `MyApp_E2E`.
2. Start the backend from `src/backend/MyApp.WebApi` with `dotnet run --launch-profile https` and these
   environment variables: `ASPNETCORE_ENVIRONMENT=Development`, `MyApp__Testing__FaultInjection__Enabled=true`,
   `ConnectionStrings__DefaultConnection=<server, Database=MyApp_E2E>` and
   `MyApp__Testing__ResetDatabaseOnStart=true`. It resets that database on its way up. Restart it before every run:
   a second run on the same data fails.
3. Start the frontend from `src/frontend` with `VITE_DEV_MOCK_USER_ID=` (empty) and the identity provider settings
   of `FrontendEnvironment()` in `Infrastructure/ApplicationManager.cs`, then `npm start`.

### Run in Docker (profile `e2e`, with approval)

`compose.yaml` has an `e2e` profile that runs the same tests with nothing installed but Docker. It is ready to use and,
like any E2E run, needs the owner's approval before it is started. It uses the compose `sqlserver` (no host port, so
it never meets another SQL Server) and its own copies of the applications:

| Service | Role |
|---------|------|
| `backend-e2e` | The backend image in Development with the fault switch and the impersonation on, on the database `E2E_DATABASE_NAME` (default `MyApp_E2E`) with `MyApp__Testing__ResetDatabaseOnStart=true`: at start it drops that database, migrates it and loads the seed, and only when the name ends in `_E2E` or `_Test` (the guard above) |
| `frontend-e2e` | The frontend image with no mock user and the identity provider stand-in of `FrontendEnvironment()`, given as run time settings |
| `e2e` | `Dockerfile` of this folder: the official Playwright .NET image (browsers included), the test project built into it, `AutoStartApps` off. It shares the frontend's network so the browser opens `http://localhost:8080`, a secure context that the sign-in library needs |

```bash
cp .env.example .env            # once; the E2E database name is E2E_DATABASE_NAME
# 1. Start fresh applications: this resets the database. Do it before every run, a second run on the same data fails.
docker compose --profile e2e up -d --build --force-recreate backend-e2e frontend-e2e
# 2. Run the tests; arguments after "e2e" go to dotnet test
docker compose --profile e2e run --rm e2e
docker compose --profile e2e run --rm e2e --filter "FullyQualifiedName~TaskCreate005"
# 3. Remove the E2E containers (the SQL Server of the default profile stays)
docker compose --profile e2e rm -sf e2e frontend-e2e backend-e2e
```

The trx file and the failure screenshots are copied to the volume `myapp-e2e-results`:
`docker run --rm -v myapp-e2e-results:/r -v "$PWD":/out alpine cp -R /r/. /out/e2e-results/`. The capture classes for the
manual are not part of this profile (they write into `docs/manual` of the working tree; see "Manual screenshots").

---

## Configuration

### appsettings.e2e.json

Tracked by git, with defaults for local development.

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `http://localhost:3147` | Frontend URL |
| `ApiBaseUrl` | `https://localhost:7342` | Backend URL; used to wait for the backend and to scope the sign-in route. Test classes never call it |
| `Timeout` | `30000` | Default timeout in ms |
| `AutoStartApps` | `true` | Start the backend and frontend (set `false` to run them yourself) |
| `StopAppsAfterTests` | `true` | Stop them after the run |
| `StartupTimeoutSeconds` | `300` | Startup timeout |
| `ConnectionString` | empty | SQL Server connection string; give it in `E2E_CONNECTION_STRING`, not in this file. Its `Database` part is ignored |
| `DatabaseName` | `MyApp_E2E` | The dedicated database, reset at the start of every run. Must end in `_E2E` or `_Test` |
| `ImpersonationHeaderName` | `X-MyApp-Impersonate-UserId` | Must equal `MyApp:Impersonation:HeaderName` of the backend |
| `FaultHeaderName` | `X-MyApp-Fault` | Header of the fault switch (read only by `FaultSwitch`) |
| `FixedUtcNow` | empty | ISO 8601 instant the backend clock starts at; empty is the real clock |

`FixedUtcNow` is empty for the tests on purpose: the clock would restart at the same instant on every run, and a
test that creates data and reads a "newest first" list should see the real time. Capture classes for the manual
(development.md 10.4) need dates that never change: the run that captures sets it, in the environment
(`E2E_FIXED_UTC_NOW=2026-01-15T09:00:00Z dotnet test --filter ...`) or in this file. The run hands it to the backend
as `MyApp__Testing__FixedUtcNow`, which also fixes the dates of the seed data, so the same value gives the same
screens. It is read once, when the backend starts: one run has one clock.

### Environment variables

| Variable | Description |
|----------|-------------|
| `E2E_BASE_URL` | Override `BaseUrl` |
| `E2E_API_URL` | Override `ApiBaseUrl` |
| `E2E_AUTO_START_APPS` | `true` or `false` |
| `E2E_STOP_APPS_AFTER_TESTS` | `true` or `false` |
| `E2E_FIXED_UTC_NOW` | Override `FixedUtcNow` |
| `E2E_CONNECTION_STRING` | Override `ConnectionString` (required with `AutoStartApps`) |
| `E2E_DATABASE_NAME` | Override `DatabaseName` |
| `HEADED` | `1` shows the browser and types key by key |

### .runsettings (Playwright browser settings)

| File | Headless | SlowMo | Use for |
|------|----------|--------|---------|
| `.runsettings` | `true` | `0` | Default of the project |
| `.runsettings.headless` | `true` | `0` | Headless runs (no visible browser) |

Set `HEADED=1` (or edit `.runsettings`) to watch a run.

---

## Signing in and the two switches

Microsoft Entra sign-in needs an emailed code, which cannot be automated. Test sessions skip it:

```
Test setup  -> localStorage "E2E_MOCK_USER" (JSON)  -> frontend skips MSAL
Browser     -> header X-MyApp-Impersonate-UserId    -> ImpersonationMiddleware sets the user
```

`E2EImpersonation` installs both on a browser context. `AuthenticatedTestBase` does it for the user a test
class names, so a test class never deals with sign-in. Both switches exist only in the Development
environment; never enable them anywhere else.

### Users

`Auth/TestUsers.cs` holds every user of the seed data. The ids, names and emails are those of `SeedDataSet` in
the backend, and each area's `testsuite.md` says which of them its test cases use.

| `TestUsers` member | Name | Role and organization |
|--------------------|------|-----------------------|
| `AdminUser` | Dev User | Admin of "MyApp Dev Org" (its only Admin) |
| `MemberUser` | Dev Member | Editor of "MyApp Dev Org" |
| `ViewerUser` | Dev Viewer | Viewer of "MyApp Dev Org" |
| `SpareMember`, `LeavingMember`, `FormerMember` | Spare / Leaving / Former Member | Viewers of "MyApp Dev Org", for team tests |
| `InviteAdmin` | Invite Admin | Admin of "E2E Invited Org" |
| `UserC` | Test UserC | No organization; pending invitation to "E2E Invited Org" as Editor |
| `StatsAdmin`, `StatsViewer` | Stats Admin / Viewer | Admin / Viewer of "Stats Org" (12 tasks) |
| `EmptyAdmin` | Empty Admin | Only member of "Empty Org" (no tasks) |
| `NewUser`, `SetupUser` | New User, Setup User | No organization; setup not completed |

### Seed data

The seed data is the deterministic dataset the run starts from. The backend drops the E2E database and loads it
again when it starts, so every run begins with it; test
classes never load it, never read it from the database and never create their preconditions through the API
(development.md 10.3). A precondition of a test case is one of:

- a fact of the seed data (the "Seed Data" section of the area's `testsuite.md` lists them);
- a step of the test itself, done through the UI;
- "The service does not answer when ...", provided by the fault switch (below).

A test whose precondition none of these can provide is a problem to report, not a reason to call the API.

A test that changes data uses unique values (`Unique("...")`, below) or restores the state through the UI, so the
tests of one run do not disturb each other. Between runs the reset does the restoring: a test may consume seed data
(remove the seed members, finish the setup of Setup User), and the next run finds it again. The tasks area does both: titles come from
`UniqueTitle(...)`, and `TasksTestBase` deletes the created tasks through the Tasks page afterwards, which keeps
the demo tasks inside the 50 newest that the list shows. A test that changes a seed user (for example a role)
puts it back with the steps of its own test case.

### Fault switch

The precondition `The service does not answer when {situation}.` is provided by the fault switch
(development.md 16.2). In a test class it is one line, before the first step:

```csharp
// The service does not answer when a task is saved.
ServiceDoesNotAnswer();
```

The base class reads the Log key from the Observable trace of the test case the class is tagged with
(`TestCaseDocs`), and `FaultSwitch` makes the browser context send it in the fault header. A test class never
names a key, a route or a header; a page object never does either. The switch affects only that session and
only the operation with that key.

### Second session

`A second session is signed in as {Role} of "{Organization}".` is:

```csharp
var admin = await StartSecondSession(TestUsers.AdminUser);
var adminTasks = new TasksPage(admin);   // steps that start with "As Admin:" use the second page objects
```

---

## How to add a test

You add a test for a test case that exists in `docs/areas/{area}/test-cases/`. Stories and test cases come
first (`CLAUDE.md`).

1. **Read the test case.** Note its id, its User Story, the first precondition (the user), the other
   preconditions, the Steps and the Expected Results. Do not change the test case to suit the test; if the
   steps cannot be automated as written, report it.
2. **Check the page objects** under `PageObjects/{AreaPascal}/`. Every screen the steps touch has one
   `{Screen}Page.cs`. If a step or a result has no method yet, add it (see "Page objects"). Never write a
   locator in a test class.
3. **Create the class** `Tests/{AreaPascal}/{TestCasePascal}Tests.cs`, one class per test case. The class name
   is the test case id in PascalCase plus `Tests`: `TASK-CREATE-005` becomes `TaskCreate005Tests`.
4. **Tag the class** with exactly these attributes, on the class and never on a method (development.md 11.1):

   ```csharp
   [TestFixture]
   [Category("tasks")]                       // the area folder
   [Property("TestCase", "TASK-CREATE-005")]
   [Property("UserStory", "US-TASK-01")]     // the User Story of the test case
   public class TaskCreate005Tests : TasksTestBase
   ```

5. **Choose the base class.** `AuthenticatedTestBase` signs in as the user of the first precondition
   (`protected override TestUser GetTestUser() => TestUsers.MemberUser;`). An area may add a base class for
   what all its tests share (`Tests/Tasks/TasksTestBase.cs`).
6. **Write one test method that follows the steps literally.** One comment line per step with the step's number
   and text, the page object call below it. Put each Expected Result after the step it follows, or at the end.
   Do not add steps, and do not leave out a result.

   ```csharp
   [Test]
   public async Task SavingAfterTheRoleBecameViewerShowsAMessage()
   {
       var title = UniqueTitle("Prepare the quarterly report");
       var tasks = new TasksPage(Page);
       var admin = await StartSecondSession(TestUsers.AdminUser);   // "A second session is signed in as Admin"

       // 1. Open the Tasks page.
       await tasks.Open();
       ...
       // 8. Click the "Create Task" button.
       await tasks.ClickCreateTask();
       // After step 8, the message "Your role does not allow this change." appears.
       await Expect(tasks.Message("Your role does not allow this change.")).ToBeVisibleAsync();
   }
   ```

7. **Assert what the user sees**, with `Expect(pageObject.State)`: `ToBeVisibleAsync`, `ToHaveTextAsync`,
   `ToHaveCountAsync(0)` for "does not show". Never assert a status code, stored data or a log entry; that is
   an integration test.
8. **Build** (`dotnet build`), then run `dotnet run --project tools/MyApp.DocTools -- sync-automated-by` and
   `dotnet run --project tools/MyApp.DocTools -- check` from the repository root. The first writes the
   `Automated by` field of the test case; the second must show no finding for your files (rules R16, R17, R18,
   R20, R22). Running the test needs the owner's approval.

### What a test class may not contain (rule R20)

`APIRequest`, `HttpClient`, a database type, or a call of `Locator`, `GetByRole`, `GetByText`, `GetByLabel`,
`GetByTestId`, `GetByPlaceholder`, `GetByAltText`, `GetByTitle` or `FrameLocator`. It must use at least one page
object (a class named `...Page` from `PageObjects/`).

### Page objects

A page object is the only place that holds locators (development.md 10.2).

- One `{Screen}Page.cs` per screen in `PageObjects/{AreaPascal}/`; parts shared by every screen in
  `PageObjects/Common/` (`SidebarPage`, `HeaderPage`). Derive from `BasePage`; give a screen its `PagePath`.
- **Actions read like the steps**: `ClickNewTask()`, `TypeTitle("...")`, `ClickEdit(title)`,
  `SelectStatus("Cancelled")`. The name is the step's verb and the element's English label. An action that
  changes data returns when the request has finished, so the next step, or the other session, starts from a
  settled state.
- **State is exposed as locators** that the test hands to `Expect`: `RowFor(title)`, `StatusOf(title)`,
  `FieldMessage("Title")`, `Message("...")`, `EditButtons`. Name them as the test case words the result.
- **Locate by role, label and text.** Use a test id only when the element has no accessible name, and list the
  element as a testability finding. Never locate by CSS class when a role, label or text exists.
- A step that names an icon-only button uses its accessible name (`Edit`, `Change status`, `Delete`).
- Keep what belongs to another area out: the tasks tests use `TeamPage` (in `PageObjects/Organizations/`) only
  for changing a role. The area that owns a screen owns its page object.

### Unique values

`Unique("text")` appends a short run suffix. Use it for every title or name a test creates. `TasksTestBase`
offers `UniqueTitle(...)`, which also registers the title for clean-up.

---

## Project Structure

```
MyApp.E2E/
├── Infrastructure/
│   ├── ApplicationManager.cs   # Starts the backend (Development, fault switch, E2E database) and the frontend
│   ├── IdentityProviderStandIn.cs # The identity provider of a visitor's browser (no tenant needed)
│   ├── HelpStandIns.cs         # Routes and a fake device for the help area (no model needed)
│   ├── FaultSwitch.cs          # The fault switch for a browser context (development.md 16.2)
│   ├── TestCaseDocs.cs         # Reads the Log key of the running test case
│   ├── TestBase.cs             # Base class for tests that are not signed in
│   ├── TestConfiguration.cs    # Settings
│   └── TestResultTracker.cs    # Run summary and export
│
├── Auth/
│   ├── AuthenticatedTestBase.cs# Base class: signed-in session, second session, fault, Unique
│   ├── E2EImpersonation.cs     # Impersonation (and the fault header) on a browser context
│   ├── VisitorTestBase.cs      # Base class for "Not signed in": a session with no user until "Sign in as ..."
│   └── TestUsers.cs            # The users of the seed data
│
├── PageObjects/
│   ├── Common/                 # BasePage, SidebarPage, HeaderPage
│   ├── Organizations/          # TeamPage
│   └── Tasks/                  # TasksPage
│
├── Tests/
│   └── Tasks/                  # TaskCreate001Tests ... one class per test case, and TasksTestBase
│
├── Manual/                     # Capture classes for the manual (development.md 10.4), when they exist
├── appsettings.e2e.json        # Settings (tracked)
├── .runsettings                # Playwright and NUnit settings
└── .runsettings.headless       # Headless settings
```

---

## Manual screenshots (capture classes)

The screenshots of the user manual come from the capture classes in `Manual/{AreaPascal}/{Screen}Capture.cs`,
not from the tests (development.md 10.4, manual.md 4). One class drives one page object; one `[Test]` takes one
image, and the method name is the state: `TasksCapture.NewTaskFormOpen` writes
`docs/manual/{lang}/tasks/images/tasks-new-task-form-open.png`. Classes carry the area category, the category
`manual-capture` and one or more `UserStory` tags, and never a `TestCase` tag (rules R17, R21).

**Capture runs are never part of a normal run**, in two ways:

- `ManualCaptureBase` skips every capture (`Assert.Ignore`) while `E2E__ManualLanguage` is not set, so a plain
  `dotnet test` reports them as skipped and starts no browser for them.
- To leave them out altogether: `dotnet test --filter "TestCategory!=manual-capture"`.

Running a capture is an E2E run and regenerates the manual, so it needs the owner's approval every time
(`CLAUDE.md`). Images from a machine other than the pinned Playwright container are for review only.

### Prerequisites

- Everything under "Prerequisites" above, with the Playwright browsers installed.
- A database in the state of the seed data, with no changes left by earlier runs: the Audit Log and the lists show
  what the database holds. The captures themselves never save anything.
- Node 22.18 or later and `npm ci` in `src/frontend`, for the manual commands.
- Run the whole `manual-capture` category for each language, never a selection: the first shot of a run replaces
  the manifest entries of that language, and `manual:check --strict` expects every image to be there.
- The server clock and the browser clock are fixed at `2030-01-10T09:00:00Z`. The base class refuses to run
  unless `E2E_FIXED_UTC_NOW` is that instant; with `AutoStartApps` on, the run starts the backend with it, with it
  off start the backend with `MyApp__Testing__FixedUtcNow` set to the same value.

### Commands, in order

```bash
# 1. Capture, once per language (from tests/e2e/MyApp.E2E)
cd tests/e2e/MyApp.E2E
dotnet test --filter "TestCategory=manual-capture" --environment E2E__ManualLanguage=en --environment E2E_FIXED_UTC_NOW=2030-01-10T09:00:00Z
dotnet test --filter "TestCategory=manual-capture" --environment E2E__ManualLanguage=it --environment E2E_FIXED_UTC_NOW=2030-01-10T09:00:00Z

# 2. The rest runs in src/frontend
cd ../../../src/frontend
npm run manual:generate                 # creates the missing pages as drafts, lists stale pages
# 3. Write the pages: replace every draft in docs/manual/en and docs/manual/it, put each image under its step
#    (manual.md 4.5), then mark each reviewed story or area as accepted:
npm run manual:generate -- --accept {story id or area folder}
npm run manual:check -- --strict        # 4. orphans, manifest, parity, drafts
npm run help:build -- --strict          # 5. bundle, packs, manifest
```

Output: the images under `docs/manual/{lang}/{area}/images/` and the manifest `docs/manual/captures.json`
(entries sorted by path, so a changed screen is a changed line). The Italian run starts the application in
Italian by answering the account request with that language; nothing is written to the user's account.

### How the Italian run finds its labels

The page objects find labels in English. In an Italian run `PageText.Language` is `it`, and the helpers of
`BasePage` (`Button`, `Link`, `Field`, `ExactText`, `Named`, `NamedIn`) accept the English text or any Italian text
the frontend translation files hold for it. A page object that locates by a label must use these helpers, or the
screen cannot be captured in Italian.

### Images the capture does not produce

The sign-in page belongs to the identity provider and cannot be driven, so there is no image of it (US-AUTH-01,
US-AUTH-02). Stories without a screen of their own (US-AUTH-04) use the images of the screen they lead to.

---

## Running a selection (with approval)

```bash
dotnet test                                              # everything
dotnet test --filter "Category=tasks"                    # one area
dotnet test --filter "TestCategory!=manual-capture"      # everything except the manual captures
dotnet test --filter "FullyQualifiedName~TaskCreate005"  # one test case
dotnet test --logger "console;verbosity=detailed"        # verbose
dotnet test --settings .runsettings.headless              # headless settings
```

Failed tests save a screenshot under `screenshots/` of the working directory. The run summary is exported at
the end of the run.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| The run stops at start and names `E2E:ConnectionString` | Set `E2E_CONNECTION_STRING` (prerequisite 5) |
| The run stops: a backend already answers | Stop it; the run starts its own so it can reset the database |
| The backend stops saying `ResetDatabaseOnStart` is set but the database is not reset | The database name does not end in `_E2E` or `_Test`, or the environment is not Development |
| Backend does not start | Check the connection string of `E2E_CONNECTION_STRING` (server, user, password), and port `7342` is free |
| Frontend does not start | Run `npm ci` in `src/frontend`, check port `3147` is free |
| Tests time out at start | Increase `StartupTimeoutSeconds` |
| Every test is redirected or gets 401 | `ImpersonationHeaderName` differs from the backend's `MyApp:Impersonation:HeaderName`, or the backend is not in Development |
| A "service does not answer" test never sees the message | The backend runs without `MyApp__Testing__FaultInjection__Enabled=true` (only possible with `AutoStartApps` off) |
| A test that reads the demo tasks cannot find one | With `AutoStartApps` off, the database was not reset before this run; reset it (see above) |
| The help tests of the browser tier fail at once | The pack names no assistant model: rebuild it with `npm run help:build` and the project's model lock |
