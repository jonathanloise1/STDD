# MyApp - Product Overview

> This document defines **what MyApp is** at the product level.
> For technical architecture see [architecture.md](guidelines/architecture.md).
> For implementation status see [roadmap.md](roadmap.md).

---

## 1. What is MyApp

MyApp is a **multi-tenant SaaS template** built with a modern stack. It provides a ready-to-use foundation for building business applications with authentication, task management, team collaboration, and an extensible architecture.

Out-of-the-box the template includes:

- Passwordless authentication (Microsoft Entra External ID)
- Multi-tenant organization management
- Task management with statuses (Todo, In Progress, Done)
- Dashboard with KPI cards and recent activity
- Audit logging
- Multilingual UI (IT, EN, DE, FR)

### 1.1 Target

| Aspect | Detail |
|--------|--------|
| **Purpose** | Reusable SaaS starter template |
| **Primary users** | Small teams and organisations managing tasks and projects |
| **Languages** | Italian, English, German, French - multilingual from day one |
| **Methodology** | STDD (Story-Test Driven Development) |

### 1.2 Multi-Tenancy

- Each user gets an **automatic tenant** (Organization) created at first login.
- The invite/role mechanism exists in the codebase for team management.

---

## 2. Domain Model

### Core Concepts

| Concept | Description |
|---------|-------------|
| **User** | A person with an account. Authenticated via email + OTP. Has a preferred language. |
| **Organization** | Technical tenant for data isolation. Auto-created at first login. |
| **Task** | A work item with title, description, status (Todo / InProgress / Done), and optional due date. |
| **AuditLog** | Append-only record of data changes for accountability. |

---

## 3. Functional Areas

| Area | Description | Status |
|------|-------------|--------|
| **Authentication** | Passwordless login via Microsoft Entra External ID (email + OTP), multilingual profile | Done |
| **Onboarding** | Signup, organisation creation, redirect to dashboard | Done |
| **Tasks** | Create, update, delete tasks with status tracking | Done |
| **Dashboard** | Task KPIs, recent tasks, greeting | Done |
| **Audit** | Append-only change tracking via EF Core interceptor (Admin only) | Done |
| **Settings** | Organization profile and team management (Admin only) | Done |

---

## 4. Design Principles

| Principle | Decision |
|-----------|----------|
| **Zero-config onboarding** | Signup then start creating tasks. No wizards. |
| **Flat task list** | Simple list with status filters. No complex hierarchy. |
| **Multilingual by default** | UI responds in the user's preferred language. |
| **Audit everything** | Every data change is logged for accountability. |

---

## 5. What is NOT in Scope (Template)

- No complex project management (Gantt, dependencies, sprints)
- No file/document management
- No external integrations
- No marketplace or billing
- No public API

---

## 6. Architecture Summary

| Layer | Technology |
|-------|-----------|
| **Backend** | .NET 10, Clean Architecture (Domain / Application / Infrastructure / WebApi) |
| **Frontend** | React 19, TypeScript, Vite, Bootstrap 5.3 |
| **Database** | SQL Server (Azure SQL in production, LocalDB locally) |
| **Auth** | Microsoft Entra External ID, MSAL, JWT Bearer |
| **Hosting** | Azure App Service |
| **Methodology** | STDD (Story-Test Driven Development) |

For full architecture details, see [architecture.md](guidelines/architecture.md).