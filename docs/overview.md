# MyApp - Product Overview

> What MyApp is at the product level. The behaviour of each area is specified by its stories in
> [docs/areas](areas/). For the architecture see [architecture.md](guidelines/architecture.md); for delivery
> status see [roadmap.md](roadmap.md).

---

## 1. What is MyApp

MyApp is a multi-tenant web application for small teams that keep a shared list of tasks. It is also the sample
application of the STDD template: every part of the method is exercised on it, and a project that adopts the
template replaces its domain with its own.

A person signs in with their email address and a one-time code, creates an organization (or joins one they were
invited to) and works with the other members of that organization.

| Aspect | Detail |
|--------|--------|
| Primary users | Small teams that share tasks |
| Languages | English and Italian; English is the default until the user chooses |
| Roles | Admin, Editor and Viewer, assigned per organization |
| Methodology | STDD (Story-Test Driven Development) |

### 1.1 Roles

| Role | What the role allows |
|------|----------------------|
| Admin | Everything an Editor does, plus the organization details, the team and the audit trail |
| Editor | Creates, edits, changes the status of and deletes tasks |
| Viewer | Reads tasks and the dashboard; changes nothing |

An organization always has at least one Admin.

### 1.2 Multi-tenancy

Every organization is isolated from the others: its members, tasks and audit entries are visible only to its own
members. A new user who belongs to no organization is asked to create one before using anything else; invited
people join the organization that invited them and skip that step.

---

## 2. Domain Model

| Concept | Description |
|---------|-------------|
| User | A person with an account, signed in by email and one-time code, with a preferred language |
| Organization | The workspace that owns members and data; has a profile, billing details and a logo |
| Membership | The link between a user and an organization, with the user's role |
| Task | A work item with a title, a description, a status (Todo, In Progress, Done, Cancelled) and an optional due date |
| Audit entry | An append-only record of a change to the organization's data: who, when, and the values before and after |

---

## 3. Functional areas

| Area | What users can do | Roles |
|------|-------------------|-------|
| [Authentication](areas/authentication/userstories.md) | Sign in with an email and a code, move between sections, change language, sign out | Everyone |
| [Onboarding](areas/onboarding/userstories.md) | Create their organization on first use | New users |
| [Tasks](areas/tasks/userstories.md) | Create, edit, change the status of, delete and list tasks | Admin, Editor; Viewer reads |
| [Dashboard](areas/dashboard/userstories.md) | See the task counts per status and the latest tasks | All members |
| [Organizations](areas/organizations/userstories.md) | Edit the organization profile and logo; invite, re-role and remove team members | Admin |
| [Audit](areas/audit/userstories.md) | Read and filter the history of changes | Admin |
| [Help](areas/help/userstories.md) | Read and search the manual, get help for the current screen, ask questions | All members |

See the [roadmap](roadmap.md) for the status of every feature. The Help area is built, and shows nothing until a project has written its
manual pages ([manual.md](guidelines/manual.md) 1.3).

---

## 4. Design principles

| Principle | Decision |
|-----------|----------|
| Same words everywhere | A failure gets the same message in every screen; the stories fix the wording |
| Roles shown, not only enforced | A user sees only the menu entries and buttons their role allows |
| Flat task list | One list with statuses; no hierarchy |
| Multilingual by default | The interface follows the user's language |
| Audit everything | Every data change is recorded for accountability |

---

## 5. Out of scope

- Project management features (Gantt charts, dependencies, sprints)
- File and document management
- Integrations with external systems; the only outside service is the sign-in provider
- Billing and marketplace
- A public API

---

## 6. After release

The product is more than its code. Each release also ships what lets people use and support it; the rules live in
the guidelines and are checked like the rest of the documentation.

| What | What it gives | Guideline |
|------|---------------|-----------|
| User manual | Pages generated from the stories and screenshots of the running application, one set per language | [manual.md](guidelines/manual.md) |
| In-page help | A Help panel on every screen and an assistant that answers from the manual, with sources | [manual.md](guidelines/manual.md), [models.md](guidelines/models.md) |
| Monitor | Watches the logs, matches each failure to its runbook and prepares an incident report | [operations.md](guidelines/operations.md) |
| Models | The assistant and the investigator, trained on the template's formats and never on a project's content. Optional: without them the assistant works on retrieval and the monitor on rules | [models.md](guidelines/models.md) |

---

## 7. Architecture summary

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, Clean Architecture (Domain, Application, Infrastructure, WebApi) |
| Frontend | React 19, TypeScript, Vite, Bootstrap 5.3 |
| Database | SQL Server |
| Sign-in | Microsoft Entra External ID, MSAL, JWT Bearer |
| Hosting | Azure App Service |

For details see [architecture.md](guidelines/architecture.md).
