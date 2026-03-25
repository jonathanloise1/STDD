# MyApp - Architecture

> **Technical reference document for developers and AI agents**

---

## 1. Overview

MyApp is a multi-tenant SaaS template built with a modern stack. It provides a ready-to-use foundation for building business applications with authentication, task management, team collaboration, and an extensible architecture. This document describes the technical architecture, conventions and integrations.

### 1.1 Technology Stack

| Layer | Technology | Version |
|-------|------------|----------|
| **Frontend** | React + TypeScript | 19 / 5.x |
| **Backend** | .NET + C# | 10.0 / 13.0 |
| **Database** | SQL Server (Azure SQL) | - |
| **ORM** | Entity Framework Core | 10.0 |
| **Auth** | Microsoft Entra External ID | - |
| **Storage** | Azure Blob Storage | - |
| **Observability** | Application Insights + OpenTelemetry | - |

---

## 2. System Architecture

### 2.1 Infrastructure Diagram

```
+-----------------------------------------------------------------------------+
�                              AZURE CLOUD                                     �
+-----------------------------------------------------------------------------�
�                                                                             �
�  +-----------------+         +-----------------+                           �
�  �  Azure Front    �         �  Azure App      �                           �
�  �  Door (CDN)     �--------?�  Service        �                           �
�  �                 �         �  (Backend API)  �                           �
�  +-----------------+         +-----------------+                           �
�           �                           �                                     �
�           ?                           � Managed Identity                    �
�  +-----------------+                  ?                                     �
�  �  Blob Storage   �         +-----------------+                           �
�  �  (Static Web)   �         �   Azure SQL     �                           �
�  �  Frontend React �         �   Database      �                           �
�  +-----------------+         +-----------------+                           �
�                                                                             �
�  +-----------------+                                                       �
�  �  Blob Storage   �                                                       �
�  �  (Media/Files)  �                                                       �
�  +-----------------+                                                       �
�                                                                             �
�  +-----------------+                                                       �
�  �  Microsoft      �                                                       �
�  �  Entra External �  (Authentication)                                     �
�  �  ID             �                                                       �
�  +-----------------+                                                       �
�                                                                             �
�  +-----------------+                                                       �
�  �  Application    �  (Monitoring + OpenTelemetry)                         �
�  �  Insights       �                                                       �
�  +-----------------+                                                       �
�                                                                             �
+-----------------------------------------------------------------------------+
```

### 2.2 Environments

| Environment | Backend URL | Frontend URL | Database |
|----------|-------------|--------------|----------|
| **DEV** | `{to be configured}` | `{to be configured}` | Azure SQL Dev |
| **PROD** | `{to be configured}` | `{to be configured}` | Azure SQL Prod |
| **LOCAL** | `localhost:5001` | `localhost:3000` | Docker SQL Server |

### 2.3 Authentication

Microsoft Entra External ID:
- **Tenant**: Dedicated MyApp tenant
- **Flows**: Sign-up/Sign-in, Password reset
- **Token**: JWT Bearer tokens

---

## 3. Repository Structure

### 3.1 Monorepo Layout

```
WebApp/
+-- .github/
�   +-- workflows/               # CI/CD pipelines
�
+-- docs/                        # ?? Specifications - CHECK FIRST!
�   +-- overview.md              # What is MyApp
�   +-- roadmap.md               # Implementation roadmap
�   +-- guidelines/
�   �   +-- architecture.md      # This file
�   �   +-- development.md       # How to develop + test strategy
�   �   +-- frontend.md          # UI/UX guidelines
�   +-- areas/                   # Feature specifications
�
+-- scripts/                     # Utility scripts (DB, migrations)
�
+-- src/                         # ?? Source Code
�   �
�   +-- backend/                 # ?? Backend (.NET 10)
�   �   +-- MyApp.Domain/          # Entities, Interfaces
�   �   +-- MyApp.Application/     # Services, DTOs
�   �   +-- MyApp.Infrastructure/  # EF Core, External services
�   �   +-- MyApp.WebApi/          # Controllers, Auth
�   �
�   +-- frontend/                # ?? Frontend (React 19)
�       +-- src/
�           +-- pages/                   # Page components
�           +-- services/api/            # API clients
�           +-- contexts/                # React contexts
�
+-- tests/                       # ?? Tests
    +-- e2e/                         # Playwright E2E tests
    +-- unit/                        # Unit tests
```

### 3.2 Backend - Clean Architecture

```
MyApp.WebApi/                  # Presentation Layer
+-- Controllers/                 # API endpoints
+-- Authorization/               # Policies, handlers
+-- Configuration/               # Service registration
+-- Middlewares/                 # Exception handling

MyApp.Application/             # Business Logic Layer
+-- Services/                    # Business services
+-- Interfaces/                  # Service contracts
+-- DTOs/                        # Data transfer objects
+-- Validators/                  # FluentValidation

MyApp.Infrastructure/          # Data Access Layer
+-- Persistence/
�   +-- MyAppDbContext.cs
+-- Repositories/                # Repository implementations
+-- Migrations/                  # EF Core migrations
+-- Services/                    # External service implementations

MyApp.Domain/                  # Core Layer
+-- Entities/                    # Domain models
+-- Repositories/                # Repository interfaces (I*Repository)
+-- Constants/                   # Enums, constants
+-- ValueObjects/                # Value objects
```

### 3.3 Frontend Structure

```
src/
+-- pages/                       # Page components
+-- services/api/                # API clients
�   +-- apiClient.ts             # Axios singleton
+-- contexts/                    # React contexts
+-- components/                  # Reusable components
+-- hooks/                       # Custom hooks
+-- routes/                      # Route definitions
```

---

## 4. Architectural Patterns

### 4.1 Clean Architecture Layers

| Layer | Responsibility | Dependencies |
|-------|----------------|--------------|
| **Domain** | Entities, Repository interfaces | None |
| **Application** | Business logic, DTOs, Validators | Domain |
| **Infrastructure** | EF Core, External services | Domain, Application |
| **WebApi** | Controllers, Auth, Middleware | Application |

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

**Parameter order**: Logger ? Options ? Services ? Repositories

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
| 500 | Server Error |

---

## 7. CI/CD

### 7.1 Branching Strategy

```
main ------------------------------------? PROD
  �
  +-- dev -------------------------------? DEV
        �
        +-- feature/xyz --? merge to dev
        +-- fix/abc ------? merge to dev
```

---

## 8. Development Setup

### 8.1 Prerequisites

- .NET 10 SDK
- Node.js 18+
- Docker Desktop (for local SQL Server)
- VS Code with recommended extensions

### 8.2 Local Database

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPassword123!" \
    -p 1433:1433 --name sqlserver \
    -d mcr.microsoft.com/mssql/server:2022-latest
```

### 8.3 Backend

```bash
cd src/backend/MyApp.WebApi

dotnet restore
dotnet run

# Hot reload
dotnet watch run

# Swagger: https://localhost:5001/swagger
```

### 8.4 Frontend

```bash
cd src/frontend

npm install
npm start

# Dev server: http://localhost:3000
```

---

## 9. Conventions

### 9.1 Naming

| Type | Convention | Example |
|------|------------|---------|
| **Entity** | PascalCase, singular | `TaskItem`, `Organization` |
| **DTO** | PascalCase + `Dto` | `TaskItemDto`, `CreateTaskRequest` |
| **Validator** | PascalCase + `Validator` | `CreateTaskRequestValidator` |
| **Repository Interface** | `I` + Entity + `Repository` | `ITaskItemsRepository` |
| **Service Interface** | `I` + Feature + `Service` | `ITaskItemService` |
| **Controller** | PascalCase + `Controller` | `TasksController` |
| **React Component** | PascalCase | `TaskCard.tsx` |
| **API Service** | camelCase + `Service` | `taskService.ts` |
| **Hook** | `use` + PascalCase | `useTasks.ts` |

### 9.2 File Organization

**Backend**: Group by feature, not by type
```
Services/Tasks/TaskItemService.cs
Services/Organizations/OrganizationsService.cs
```

### 9.3 Logging

```csharp
// Use structured logging
logger.LogInformation("Activity {ActivityId} created by {UserId}", activity.Id, userId);

// Log levels
LogDebug    ? Verbose debugging
LogInformation ? Normal operations
LogWarning  ? Unexpected but handled
LogError    ? Exceptions
```

---

## 10. Development Workflow

> **?? Full guidelines**: [development.md](development.md)

### 10.1 Key Principles

1. **Documentation as index**: references to files/classes, not inline code
2. **Single Source of Truth**: tech stack lives here, not in individual feature docs
3. **Workflow**: Requirements ? Tests ? Implementation ? Test/Dev Cycle ? Human Review

### 10.2 Feature Docs Structure

```
docs/areas/{area}/
+-- userstories.md        # WHAT: User stories, business rules, UI flows
+-- testsuite.md          # Mapping US ? Test Cases
+-- test-cases/           # Specifiche per E2E tests
    +-- {CODE}.md
    +-- ...
```

---

## 11. Appendix

### 11.1 Useful Commands

```bash
# Backend
dotnet build MyApp.sln
dotnet test
dotnet ef migrations add <Name> -p MyApp.Infrastructure -s MyApp.WebApi
dotnet ef database update -p MyApp.Infrastructure -s MyApp.WebApi

# Frontend
npm start
npm run build
npm run lint:fix
```

### 11.2 Related Documents

| Document | Purpose |
|----------|---------|
| [overview.md](../overview.md) | What is MyApp |
| [development.md](development.md) | Development workflow & documentation |
| [areas/](../areas/) | Feature specifications |

---

*Last updated: March 2026*
