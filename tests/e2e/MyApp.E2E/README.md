# MyApp E2E Tests

> End-to-End tests with Playwright for .NET

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Backend running at `https://localhost:7342` (see [backend README](../../../src/backend/README.md))
- Frontend running at `http://localhost:3147` (see [frontend README](../../../src/frontend/README.md))

### Run tests

```bash
cd tests/e2e/MyApp.E2E
dotnet test
```

---

## Configuration

### appsettings.e2e.json

This file is **tracked by git** and contains sensible defaults for local development:

```json
{
  "E2E": {
    "BaseUrl": "http://localhost:3147",
    "ApiBaseUrl": "https://localhost:7342",
    "Timeout": 30000,
    "AutoStartApps": true,
    "StopAppsAfterTests": true,
    "StartupTimeoutSeconds": 300,
    "ImpersonationHeaderName": "X-MyApp-Impersonate-UserId"
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `http://localhost:3147` | Frontend URL |
| `ApiBaseUrl` | `https://localhost:7342` | Backend URL |
| `Timeout` | `30000` | Default timeout in ms |
| `AutoStartApps` | `true` | Auto-start BE+FE (set `false` if you run them manually) |
| `StopAppsAfterTests` | `true` | Auto-stop BE+FE after tests |
| `StartupTimeoutSeconds` | `300` | App startup timeout |
| `ImpersonationHeaderName` | `X-MyApp-Impersonate-UserId` | Header for backend impersonation |

> **Important**: the `ImpersonationHeaderName` must match the value in the backend's `appsettings.Development.json` → `MyApp.Impersonation.HeaderName`.

### .runsettings (Playwright browser settings)

Two presets are provided:

| File | Headless | SlowMo | Use for |
|------|----------|--------|---------|
| `.runsettings` | `false` | `500ms` | Local dev — visible browser, slow for debugging |
| `.runsettings.ci` | `true` | `0ms` | CI/CD — headless, full speed |

```bash
# Local dev (default)
dotnet test

# CI/CD
dotnet test --settings .runsettings.ci
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `E2E_BASE_URL` | Override BaseUrl |
| `E2E_API_URL` | Override ApiBaseUrl |
| `E2E_AUTO_START_APPS` | `true` to auto-start BE+FE |
| `E2E_STOP_APPS_AFTER_TESTS` | `true` to stop apps after tests |
| `HEADED` | `1` to enable debug mode (visible typing) |

---

## Running Tests

```bash
# All tests
dotnet test

# By category
dotnet test --filter "Category=Auth"
dotnet test --filter "Category=Organization"

# Single test
dotnet test --filter "FullyQualifiedName~LoginTests.Login_WithValidUser"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# In CI
dotnet test --settings .runsettings.ci
```

---

## Authentication (Impersonation Pattern)

### The Problem

Azure Entra External ID (CIAM) uses email OTP — unsuitable for automated testing:
- Requires manual email code entry
- Rate limiting on logins
- No service principal support for consumer users

### The Solution

```
┌─────────────────────────────────────────────────────────────────────┐
│                   E2E IMPERSONATION ARCHITECTURE                    │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────┐      localStorage          ┌──────────────────┐   │
│  │  Playwright  │  ──────────────────────>   │    Frontend      │   │
│  │    Test      │  E2E_MOCK_USER json        │  (authContext)   │   │
│  └─────────────┘                             │  Skips MSAL      │   │
│         │                                    └──────────────────┘   │
│         │  X-MyApp-Impersonate-UserId                              │
│         │  header on all /api/** requests                           │
│         ▼                                                           │
│  ┌──────────────────┐                        ┌──────────────────┐   │
│  │     Backend       │  ──────────────────>  │ ClaimsPrincipal  │   │
│  │ Impersonation     │  Creates identity     │  (User.Identity) │   │
│  │   Middleware      │  from DB user         └──────────────────┘   │
│  └──────────────────┘                                               │
│                                                                     │
│  SECURITY: Only works when MyApp:Impersonation:Enabled = true     │
│            NEVER enable in production!                               │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Strategy

| Test Type | Approach | Base Class |
|-----------|----------|------------|
| Login/Logout tests | Real login via `AuthStateManager` | `TestBase` |
| Feature tests (95%) | Impersonation | `AuthenticatedTestBase` |
| API-only tests | Impersonation header only | — |

### Writing Authenticated Tests

```csharp
[TestFixture]
[Category("Organization")]
public class DashboardTests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    public async Task Dashboard_ShouldLoad()
    {
        await NavigateToAsync("/dashboard");

        await Assertions.Expect(Page.Locator("#dashboard-layout"))
            .ToBeVisibleAsync();
    }
}
```

---

## Project Structure

```
MyApp.E2E/
├── Infrastructure/             # Setup and utilities
│   ├── ApplicationManager.cs   # Auto-start BE+FE
│   ├── TestBase.cs             # Base class (unauthenticated)
│   ├── TestConfiguration.cs    # Configuration reader
│   └── TestResultTracker.cs    # Test run summary & export
│
├── Auth/                       # Authentication
│   ├── AuthStateManager.cs     # Real login (Entra OTP)
│   ├── AuthenticatedTestBase.cs# Base class for authenticated tests
│   ├── E2EImpersonation.cs     # Impersonation setup
│   └── TestUsers.cs            # Test user definitions
│
├── PageObjects/                # Page Object Model
│   └── Common/
│       ├── BasePage.cs
│       └── SidebarPage.cs
│
├── Tests/                      # Test classes by feature
│
├── appsettings.e2e.json        # Config (tracked)
├── .runsettings                # Local dev: headed, SlowMo
├── .runsettings.ci             # CI: headless, fast
└── .gitignore                  # Excludes bin/, obj/, .auth/, TestResults/
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Backend won't start | Check port `7299`: `netstat -an \| findstr 7299` |
| Frontend won't start | Run `npm ci` in `src/frontend`, check port `3100` |
| Tests timeout | Increase `StartupTimeoutSeconds` in `appsettings.e2e.json` |
| Auth not working | Verify `ImpersonationHeaderName` matches between E2E and backend configs |

---

## Conventions

### Test Naming

```
File:   [Feature][Action]Tests.cs
Method: [Scenario]_[ExpectedResult]

Example:
  CostEntryCreateTests.cs
    → CeCreate001_CreateMinimal_ShouldSucceed()
```

### Test Categories

```csharp
[TestFixture]
[Category("CostEntries")]
public class CostEntryListTests : AuthenticatedTestBase { }
```
