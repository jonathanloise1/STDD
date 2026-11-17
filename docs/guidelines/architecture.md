# MyApp - Architecture

> **Technical reference document for developers and AI agents**

---

## 1. Overview

MyApp is a multi-tenant SaaS template built with a modern stack. It provides a ready-to-use foundation for building business applications with authentication, task management, team collaboration, and an extensible architecture. This document describes the technical architecture, conventions and integrations.

### 1.1 Technology Stack

| Layer | Technology | Version |
|-------|------------|----------|
| **Frontend** | React + TypeScript, built with Vite | 19 / 5.x / 6.x |
| **Frontend UI** | react-bootstrap on Bootstrap 5 with an own Sass theme, lucide icons, self-hosted Inter; i18next for translations | 2.x / 5.x / 23.x |
| **Backend** | ASP.NET Core Web API on .NET, C# | 10.0 / 14 |
| **API docs** | Swashbuckle (Swagger UI, Development only) | 10.x |
| **Database** | SQL Server (Azure SQL) | - |
| **ORM** | Entity Framework Core | 10.0 |
| **Auth** | Microsoft Entra External ID: Microsoft.Identity.Web in the backend, MSAL in the browser | 4.x / 4.x |
| **Background jobs** | Hosted services (`BackgroundService`) | - |
| **Observability** | Azure Monitor OpenTelemetry to Application Insights; JSON lines log file ([operations.md](operations.md) 3.4) | 1.x |
| **Monitor** | `src/support/MyApp.Monitor`, an ASP.NET Core application with no reference to the backend ([operations.md](operations.md) 14) | 10.0 |
| **E2E tests** | Playwright for .NET with NUnit | 1.x |

---

## 2. System Architecture

### 2.1 Infrastructure Diagram

The repository holds no cloud infrastructure code, so the hosting named here is the reference deployment. What the
code configures is the backend, its database, identity provider and telemetry, and the monitor. The repository does
hold a Docker setup that runs the application, the monitor, the model hosts and the test runs with no cloud service
([section 8.5](#85-docker)); it is the way to run the template on a laptop or on a server of your own.

```
Browser (React application)
   |
   +--> Static web hosting (Azure Blob Storage behind Azure Front Door)
   |
   +--> Microsoft Entra External ID (sign-in)
   |
   +--> Backend API (ASP.NET Core on Azure App Service), with a bearer token
           |
           +--> Azure SQL Database (EF Core, Managed Identity)
           |
           +--> Application Insights (OpenTelemetry)
           |
           +--> JSON lines log file (on-premises and local development)

Monitor (src/support/MyApp.Monitor)
   |
   +--> Reads only: Application Insights or log files, /health, the database through masked tools
```

### 2.2 Environments

| Environment | Backend URL | Frontend URL | Database |
|----------|-------------|--------------|----------|
| **DEV** | `{to be configured}` | `{to be configured}` | Azure SQL Dev |
| **PROD** | `{to be configured}` | `{to be configured}` | Azure SQL Prod |
| **LOCAL** | `https://localhost:7342` | `http://localhost:3147` | Docker SQL Server |
| **DOCKER** | not published (the frontend forwards `/api`) | `http://localhost:3147` | SQL Server container `myapp-sqlserver`, no host port |

### 2.3 Authentication

Microsoft Entra External ID:
- **Tenant**: Dedicated MyApp tenant
- **Flows**: Sign-up/Sign-in, Password reset
- **Token**: JWT Bearer tokens
- **E2E tests**: impersonation, enabled only in Development ([development.md](development.md) 16.1)

---

## 3. Repository Structure

### 3.1 Monorepo Layout

```
{repository root}/
+-- .github/
|   +-- agents/                  # Agent instructions
|
+-- docs/                        # Specifications - CHECK FIRST!
|   +-- overview.md              # What is MyApp
|   +-- roadmap.md               # Features and their status
|   +-- contracts.json           # Current version of every format
|   +-- guidelines/
|   |   +-- architecture.md      # This file
|   |   +-- development.md       # Story, test case and traceability contracts
|   |   +-- frontend.md          # UI/UX guidelines
|   |   +-- manual.md            # User manual and help
|   |   +-- models.md            # Assistant model
|   |   +-- operations.md        # Logging, runbooks, monitor
|   +-- areas/                   # Feature specifications
|   +-- support/                 # Normative files of the monitor
|
+-- models/                      # Model lock file and card
+-- docker/                      # Health check script shared by the images
+-- compose.yaml                 # The whole template in Docker (with compose.gpu.yaml, compose.sqlserver-port.yaml)
+-- .env.example                 # Settings of compose.yaml
+-- LICENSE, THIRD-PARTY-NOTICES.md
+-- tools/
|   +-- MyApp.DocTools/          # Checker of the documentation contracts
+-- training/                    # Dataset, training and evaluation code
|
+-- src/                         # Source Code
|   +-- backend/                 # Backend (.NET 10)
|   |   +-- MyApp.Domain/          # Entities, repository interfaces
|   |   +-- MyApp.Application/     # Services, DTOs, log definitions
|   |   +-- MyApp.Infrastructure/  # EF Core, repositories, jobs, log sinks
|   |   +-- MyApp.WebApi/          # Controllers, auth, middlewares
|   |
|   +-- frontend/                # Frontend (React 19)
|   |   +-- src/
|   |       +-- pages/               # Page components
|   |       +-- services/api/        # API clients
|   |       +-- contexts/            # React contexts
|   |
|   +-- support/
|       +-- MyApp.Monitor/       # Monitor application
|
+-- tests/                       # Tests (development.md section 10)
    +-- e2e/                     # Playwright E2E tests
    +-- unit/                    # Unit and architecture tests
    +-- support/                 # Monitor tests
```

`tests/integration/` holds the API-level tests (108 tests, passing on their own database) and `docs/manual/` the user manual
(English and Italian, 90 pages, 80 screenshots).

### 3.2 Backend - Clean Architecture

```
MyApp.WebApi/                  # Presentation Layer
+-- Controllers/                 # API endpoints
+-- Authorization/               # Policies, handlers
+-- ClientEvents/                # Traces reported by the browser
+-- Configuration/               # Service registration, health checks
+-- Middlewares/                 # Trace context, exception handling, impersonation

MyApp.Application/             # Business Logic Layer
+-- Services/                    # Business services
+-- Interfaces/                  # Service contracts
+-- DTOs/                        # Data transfer objects and hand-written mappings
+-- Exceptions/                  # Outcome exceptions
+-- Logging/                     # Log definition classes
+-- Configuration/               # Service registration

MyApp.Infrastructure/          # Data Access Layer
+-- Persistence/
|   +-- MyAppDbContext.cs
|   +-- Configurations/          # Entity configurations
+-- Repositories/                # Repository implementations
+-- Migrations/                  # EF Core migrations
+-- Services/                    # External service implementations
+-- Jobs/                        # Background jobs
+-- Logging/                     # Log processor and file sink
+-- Seed/                        # Development and test data
+-- Configuration/               # Service registration, startup

MyApp.Domain/                  # Core Layer
+-- Entities/                    # Domain models
+-- Repositories/                # Repository interfaces (I*Repository)
+-- Constants/                   # Enums, constants
```

### 3.3 Frontend Structure

```
src/
+-- ui/                          # UI layer: theme, icons, building blocks (frontend.md 3.1)
+-- shell/                       # Application frame: header, side and bottom navigation, menus
+-- pages/company/{area}/        # One folder per area
+-- help/                        # Manual, help panel and assistant
+-- services/api/                # API clients
|   +-- apiClient.ts             # Axios singleton
+-- contexts/                    # React contexts
+-- components/auth/             # Components of the sign-in flow
+-- hooks/                       # Application hooks
+-- routes/                      # Route definitions
```

---

## 4. Architectural Patterns

### 4.1 Clean Architecture Layers

| Layer | Responsibility | Dependencies |
|-------|----------------|--------------|
| **Domain** | Entities, Repository interfaces | None |
| **Application** | Business logic, DTOs, log definitions | Domain |
| **Infrastructure** | EF Core, External services | Domain, Application |
| **WebApi** | Controllers, Auth, Middleware | Application; Infrastructure only to register its services and run startup |

**Rule**: Dependencies always point inward (toward Domain).

### 4.2 Repository Pattern

```csharp
// Domain: Interface
public interface IExampleRepository
{
    Task<Example?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Example example, CancellationToken ct);
}

// Infrastructure: Implementation
public class ExampleRepository(MyAppDbContext db) : IExampleRepository
{
    public async Task<Example?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Examples.FindAsync([id], ct);
}
```

### 4.3 Service Pattern

```csharp
// Application: Interface
public interface IExampleService
{
    Task<ExampleDto> CreateAsync(Guid userId, CreateExampleRequest req, CancellationToken ct);
}

// Application: Implementation (primary constructor)
public class ExampleService(
    ILogger<ExampleService> logger,
    IExampleRepository repository,
    IMapper mapper) : IExampleService
{
    public async Task<ExampleDto> CreateAsync(...)
    {
        // Business logic
    }
}
```

### 4.4 Primary Constructors (C# 12)

**Parameter order**: Logger -> Options -> Services -> Repositories

```csharp
public class SomeService(
    ILogger<SomeService> logger,           // 1. Logger
    IOptions<SomeOptions> options,         // 2. Options
    ISomeDependency dependency,            // 3. Services
    ISomeRepository repository)            // 4. Repositories
    : ISomeService
{
    private readonly SomeOptions _options = options.Value;
}
```

---

## 5. Database

### 5.1 Migrations

The backend applies pending migrations at start.

```bash
# Create new migration
cd src/backend
dotnet ef migrations add <MigrationName> -p MyApp.Infrastructure -s MyApp.WebApi

# Apply migrations
dotnet ef database update -p MyApp.Infrastructure -s MyApp.WebApi
```

---

## 6. API Design

### 6.1 Response Codes

| Code | Meaning |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 204 | No Content (update success) |
| 400 | Bad Request (validation error) |
| 401 | Unauthorized (no auth) |
| 403 | Forbidden |
| 404 | Not Found |
| 429 | Too Many Requests (rate limit) |
| 500 | Server Error |

Error responses share one shape: [operations.md](operations.md) 4.2.

---

## 7. Pipeline

The template ships no pipeline. A project puts these commands in its own pipeline: unit and architecture tests, the documentation check ([development.md](development.md) 12), frontend checks, monitor tests, training tests, integration tests and E2E tests; the commands are in [CLAUDE.md](../../CLAUDE.md), "Commands".

---

## 8. Development Setup

### 8.1 Prerequisites

- .NET 10 SDK
- Node.js 22.18+ (the manual and help scripts need it)
- Docker Desktop (for local SQL Server)

### 8.2 Local Database

```bash
cp .env.example .env
docker compose -f compose.yaml -f compose.sqlserver-port.yaml up -d sqlserver
# Connection string: Server=localhost,14330;User Id=sa;Password={MSSQL_SA_PASSWORD of .env};TrustServerCertificate=True
```

### 8.3 Backend

```bash
cd src/backend/MyApp.WebApi

dotnet restore
dotnet run --launch-profile https

# Hot reload
dotnet watch run --launch-profile https

# Swagger: https://localhost:7342/swagger
```

### 8.4 Frontend

```bash
cd src/frontend

npm install
npm run dev

# Dev server: http://localhost:3147
```

### 8.5 Docker

`compose.yaml` at the repository root (project `myapp`) runs everything in containers; `compose.gpu.yaml` is an
override for the model hosts. Settings come from `.env`, created from the committed `.env.example`. Every container
and volume is named `myapp-*`; the host ports bound are 3147 (frontend), 5310 (monitor dashboard) and 8091 to 8093
(model hosts), on `127.0.0.1`. SQL Server publishes no port, so it does not meet another SQL Server on 1433.

| Service | Profile | Image and role |
|---------|---------|----------------|
| `sqlserver` | default | SQL Server 2022 Developer edition (the image's default, free for development and test only; production needs a licensed edition or another database), sa password from `.env`, named volume `myapp-sqlserver-data`. `compose.sqlserver-port.yaml` publishes it on `127.0.0.1:14330` |
| `backend` | default | `src/backend/Dockerfile`: .NET 10, plain http on 8080 inside the network, migrations and (Development) seed at start, JSON lines log file on the volume `myapp-logs`. `APP_ENVIRONMENT` selects Development or Production |
| `frontend` | default | `src/frontend/Dockerfile`, built from the repository root (filter: `src/frontend/Dockerfile.dockerignore`): Node builds the help pack from `docs/manual` (`npm run help:build -- --strict`, lexical only, skipped when the manual has no pages) and the application, nginx serves the build and forwards `/api`, `/health` and `/swagger` to the backend, so the browser sees one origin and there is no CORS |
| `monitor`, `monitor-dashboard` | `monitor` | The monitor image with an operations pack built from the repository for the `onprem` profile, reading `myapp-logs`; SQLite store on `myapp-monitor-data`. The dashboard's `local` authentication accepts only a loopback caller, so a forwarder in the monitor's network publishes it on the host's `127.0.0.1` |
| `assistant-model`, `investigator-model`, `teacher` | one each | llama.cpp server (official image), started through `src/support/model-host/start.sh` with `shared-flags.txt`; model file mounted from `MODELS_DIR`; CPU by default, NVIDIA GPU with `compose.gpu.yaml`. Without a model file the container says so and stops. `teacher` is the local OpenAI-compatible endpoint of the training pipeline |
| `integration` | `integration` | The SDK 10 image with the repository copied in, running `tests/integration` against `sqlserver`; the run drops, migrates and seeds `MyApp_Integration_Test` |
| `backend-e2e`, `frontend-e2e`, `e2e` | `e2e` | The E2E run: its own backend (Development, fault switch, impersonation, database `MyApp_E2E`, which the backend drops and seeds at start and only when the name ends in `_E2E` or `_Test`), its own frontend (no mock user, the identity provider stand-in) and the Playwright runner, whose image holds the same help pack, built from `docs/manual` in its own stage. See the E2E README |

**Frontend settings at run time.** Vite bakes `VITE_*` values into the bundle, so one image could serve only one
environment. The settings that differ per environment (API address, sign-in, mock user) are therefore read from
`/config.js`, which the container writes from its environment at start (`src/frontend/docker/40-runtime-config.sh`,
`src/config/runtimeConfig.ts`). A key set there, even empty, wins over the build-time value; the rest (theme, layout)
stays build-time. An empty API address means the page's own origin.

**Local sign-in.** The impersonation of [development.md](development.md) 16.1 exists only in the Development
environment. A compose run in Development with `VITE_DEV_MOCK_USER_ID` set signs the visitor in as the seed's admin;
a Production run needs a real Entra External ID tenant (README "Run with Docker").

**Health routes.** Backend `/health` (also through the frontend), frontend container `/nginx-health`, monitor `/api/health`.

**Health checks.** Every service has one: SQL Server runs `sqlcmd`, the .NET images run `docker/http-check.sh` (they
have neither curl nor wget), nginx answers `/nginx-health`, the model hosts `GET /health`. `depends_on` waits for
`service_healthy`, so the frontend starts after the backend, which starts after the database.

**Secrets.** No committed file holds one: `.env` and `backend.local.env` are git-ignored, and `.env.example` carries
labelled local placeholders that must be changed on any machine other people can reach.

---

## 9. Conventions

### 9.1 Naming

| Type | Convention | Example |
|------|------------|---------|
| **Entity** | PascalCase, singular | `TaskItem`, `Organization` |
| **DTO** | PascalCase + `Dto` or `Request` | `TaskItemDto`, `CreateTaskRequest` |
| **Repository Interface** | `I` + Entity + `Repository` | `ITaskItemsRepository` |
| **Service Interface** | `I` + Feature + `Service` | `ITaskItemService` |
| **Controller** | PascalCase + `Controller` | `TasksController` |
| **Log definition class** | Area in PascalCase + `Log` | `TasksLog` |
| **React Component** | PascalCase | `TaskListPage.tsx` |
| **API Service** | camelCase + `Service` | `taskService.ts` |
| **Hook** | `use` + camelCase | `useNavItems.ts` |

### 9.2 File Organization

**Backend**: services are one class per feature in `Services/`; DTOs are grouped by feature.
```
Services/TaskItemService.cs
DTOs/Tasks/TaskDtos.cs
```

### 9.3 Logging

Log call sites, message templates, levels, correlation and the log file are defined in
[operations.md](operations.md) sections 3 and 4.

---

## 10. Development Workflow

The workflow, the formats of the documentation and the area layout are defined in [development.md](development.md).

---

## 11. Appendix

### 11.1 Useful Commands

```bash
# Backend (from src/backend)
dotnet build MyApp.sln
dotnet ef migrations add <Name> -p MyApp.Infrastructure -s MyApp.WebApi
dotnet ef database update -p MyApp.Infrastructure -s MyApp.WebApi

# Unit and architecture tests, documentation check (from the repository root)
dotnet test tests/unit/MyApp.UnitTests
dotnet run --project tools/MyApp.DocTools -- check

# Frontend (from src/frontend)
npm run dev
npm run build
npm run lint:fix
```

### 11.2 Related Documents

| Document | Purpose |
|----------|---------|
| [overview.md](../overview.md) | What is MyApp |
| [development.md](development.md) | Development workflow & documentation |
| [frontend.md](frontend.md) | UI and UX rules |
| [manual.md](manual.md) | User manual and help |
| [models.md](models.md) | Assistant model |
| [operations.md](operations.md) | Logging, runbooks, monitor |
| [areas/](../areas/) | Feature specifications |
