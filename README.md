# STDD Template

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/React-19-61DAFB?style=flat-square&logo=react" alt="React 19" />
  <img src="https://img.shields.io/badge/TypeScript-5.x-3178C6?style=flat-square&logo=typescript" alt="TypeScript" />
  <img src="https://img.shields.io/badge/Vite-6.x-646CFF?style=flat-square&logo=vite" alt="Vite" />
  <img src="https://img.shields.io/badge/Azure-Cloud-0078D4?style=flat-square&logo=microsoftazure" alt="Azure" />
</p>

<p align="center">
  A production-ready <strong>multi-tenant SaaS starter template</strong><br/>
  built with the <strong>Story-Test Driven Development (STDD)</strong> methodology.
</p>

---

## What is this?

This is a **ready-to-clone template** for anyone starting a new full-stack project with frontend and backend. Instead of starting from scratch, you get:

- A working multi-tenant SaaS application (authentication, tasks, dashboard, audit)
- A complete documentation structure (user stories, test cases, architecture guidelines)
- A proven development workflow designed for **AI-assisted engineering**

Clone it, replace the example domain with yours, and start building.

---

## Why STDD?

### The problem with AI-assisted development

AI coding agents are powerful, but they have a fundamental flaw: **they improvise**. Give an LLM a vague prompt and it will happily generate code that looks correct but doesn't match your intent. The more complex the feature, the more it drifts.

### Spec Driven Development (SDD) was a first answer

[Spec Driven Development](https://en.wikipedia.org/wiki/Spec-driven_development) formalized the idea that a **machine-readable specification** should be the single source of truth, written *before* implementation. The spec drives everything: code, tests, docs. This was a major step forward — instead of "vibe coding", you have a structured contract.

But specs alone aren't enough. A specification tells the AI *what* to build, but it doesn't tell it when it has gone wrong. An AI agent can read a perfect spec and still produce code that subtly violates it. There's no feedback loop.

### STDD adds the missing piece: tests as a perimeter

**Story-Test Driven Development (STDD)** evolves SDD by making **test cases a first-class artifact**, written alongside user stories *before* any code exists.

The core insight:

> **Specs define the intent. Tests enforce the boundary.**

Where SDD says "here's what to build", STDD says "here's what to build — and here's exactly how we'll verify it's correct". The test cases create an **executable perimeter** that constrains AI agents. If the generated code drifts from the spec, the tests catch it immediately.

```
SDD:   Spec → Code → (hope it's right)
STDD:  Spec + Tests → Code → Verify → Repeat until tests pass
```

### The STDD philosophy

> "Specs and tests are the asset. Code is a commodity."

In a world where AI can generate code in seconds, the **hard part** is knowing *what* to build and *how to verify* it works. User stories capture the "what". Test cases capture the "how to verify". The code itself is replaceable — it can be regenerated, refactored, or rewritten as long as it passes the tests.

---

## How STDD works

### The workflow

Every feature follows a strict sequence:

```
┌─────────────────────────────────────────────────────┐
│  1. REQUIREMENTS                                    │
│     Write user stories with acceptance criteria     │
│     and business rules (userstories.md)             │
├─────────────────────────────────────────────────────┤
│  2. TEST SPECIFICATION                              │
│     Write test cases: happy path, error cases,      │
│     edge cases (test-cases/*.md)                    │
├─────────────────────────────────────────────────────┤
│  3. IMPLEMENTATION                                  │
│     Implement backend, frontend, and E2E tests      │
│     (human or AI — doesn't matter)                  │
├─────────────────────────────────────────────────────┤
│  4. VERIFY                                          │
│     Run tests. Fix. Repeat until green.             │
├─────────────────────────────────────────────────────┤
│  5. REVIEW                                          │
│     Human code review + manual QA                   │
└─────────────────────────────────────────────────────┘
```

### The documentation structure

Each feature area has a predictable structure:

```
docs/areas/{feature}/
├── userstories.md        # WHAT it does (stories, business rules, access control)
├── testsuite.md          # Mapping: user story → test cases
└── test-cases/
    ├── FEAT-CREATE-001.md   # Individual test case (preconditions, steps, expected result)
    ├── FEAT-CREATE-002.md
    └── ...
```

This structure is **the spec** that drives implementation. An AI agent (or a human developer) reads the user stories to understand the feature, reads the test cases to understand the acceptance criteria, and implements until all tests pass.

### Why this matters for AI agents

| Without STDD | With STDD |
|-------------|-----------|
| "Build a task management feature" | User stories define exactly what "task management" means |
| AI invents its own business rules | Business rules are explicit in userstories.md |
| No way to verify correctness | Test cases define pass/fail criteria before code exists |
| Drift accumulates silently | Tests catch drift immediately |
| Every prompt is ad-hoc | Predictable structure = predictable AI output |

---

## What's included

This template ships with a working **task management SaaS** as the example domain:

| Feature | Description |
|---------|-------------|
| **Authentication** | Passwordless login via Microsoft Entra External ID (email + OTP) |
| **Multi-tenancy** | Auto-tenant creation at first login, team management, roles |
| **Task management** | Create, update, delete tasks with status tracking (Todo / InProgress / Done) |
| **Dashboard** | Task KPIs, recent tasks, greeting |
| **Audit log** | Append-only change tracking via EF Core interceptor |
| **Multilingual** | Italian, English, German, French from day one |

### Architecture

```
┌──────────────────────────────┬────────────────────────────────┐
│         Backend              │          Frontend              │
│    (.NET 10 Web API)         │   (React 19 + TypeScript)      │
├──────────────────────────────┼────────────────────────────────┤
│  Clean Architecture (DDD)    │  Component-based UI            │
│  Entity Framework Core 10    │  React Router v6               │
│  FluentValidation            │  React Context (state)         │
│  AutoMapper                  │  Formik (forms)                │
│  JWT Bearer auth             │  MSAL (Entra External ID)      │
│  SQL Server / Azure SQL      │  Vite 6 + Bootstrap 5.3        │
└──────────────────────────────┴────────────────────────────────┘
```

---

## Repository structure

```
├── docs/                          # Specifications & guidelines
│   ├── overview.md                # Product overview
│   ├── roadmap.md                 # Feature status tracking
│   ├── guidelines/
│   │   ├── architecture.md        # Tech stack, conventions, infrastructure
│   │   ├── development.md         # STDD workflow, test strategy
│   │   ├── frontend.md            # UI/UX design system
│   │   └── wireframes.md          # ASCII wireframes
│   └── areas/                     # Feature specifications
│       ├── authentication/        #   User stories + test cases
│       ├── dashboard/
│       ├── onboarding/
│       └── audit/
│
├── src/
│   ├── backend/                   # .NET 10 Web API
│   │   ├── MyApp.Domain/          #   Entities, repositories (interfaces)
│   │   ├── MyApp.Application/     #   Services, DTOs, business logic
│   │   ├── MyApp.Infrastructure/  #   EF Core, migrations, external services
│   │   └── MyApp.WebApi/          #   Controllers, middleware, auth
│   │
│   └── frontend/                  # React 19 + Vite
│       └── src/
│
├── tests/
│   ├── e2e/                       # Playwright E2E tests
│   └── unit/                      # Unit tests
│
└── scripts/                       # DevOps utilities
```

---

## Getting started

### Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | Backend |
| [Node.js](https://nodejs.org/) | 18+ | Frontend |
| [npm](https://www.npmjs.com/) | 9+ | Package manager |
| [SQL Server](https://www.microsoft.com/sql-server) | 2019+ | Docker locally, Azure SQL in prod |
| [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) | 10.0+ | `dotnet tool install -g dotnet-ef` |

### 1. Backend

```bash
cd src/backend
dotnet restore
dotnet ef database update -p MyApp.Infrastructure -s MyApp.WebApi
cd MyApp.WebApi
dotnet run --launch-profile https
```

API: `https://localhost:7342` — Swagger: `https://localhost:7342/swagger`

### 2. Frontend

```bash
cd src/frontend
npm install
npm run dev
```

App: `http://localhost:3147`

### 3. E2E Tests

```bash
cd tests/e2e/MyApp.E2E
dotnet test
```

---

## Using this template

1. **Clone** this repository
2. **Read** `docs/guidelines/development.md` to understand the STDD workflow
3. **Study** the example feature in `docs/areas/` to see the spec → test → code pattern
4. **Replace** the task management domain with your own:
   - Write user stories in `docs/areas/{your-feature}/userstories.md`
   - Write test cases in `docs/areas/{your-feature}/test-cases/`
   - Implement backend + frontend + E2E tests
   - Repeat for each feature

The documentation structure, guidelines, and development workflow are designed to be **domain-agnostic**. Keep them, change the domain.

---

## Documentation

| Document | Description |
|----------|-------------|
| [Product Overview](docs/overview.md) | What MyApp does as a template |
| [Architecture](docs/guidelines/architecture.md) | Tech stack, infrastructure, conventions |
| [Development Guidelines](docs/guidelines/development.md) | STDD workflow, test strategy, documentation rules |
| [Frontend Guide](docs/guidelines/frontend.md) | Design system, UI patterns, component standards |
| [Wireframes](docs/guidelines/wireframes.md) | ASCII wireframes for key screens |

---

## License

MIT
