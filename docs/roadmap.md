# MyApp - Roadmap

Delivery status of the features of MyApp. A feature groups the stories that name it in their `Feature` field; the
rules for this file are in [development.md section 3.3](guidelines/development.md#33-roadmap-features). For the
product see [overview.md](overview.md).

Status values: `Done` (every item of the definition of done is met, development.md 2.2), `In Progress` (the stories are written
and the code exists, but the definition of done is not met yet), `Planned` (specified, not yet implemented), `Backlog` (defined,
not scheduled).

As of 2026-11-16 every feature below is `Done`: its E2E tests pass (178 tests, all areas green; the help area 49 of 49 in its last run), the integration tests pass (108 of 108), and the manual is written in English and Italian (90 pages, 80 screenshots, strict check passing). Not yet true: CI has never run on GitHub, nothing is committed, no model has been trained or downloaded, and the help retrieval is below its bench gates without the embedding table (recall at 3: 0.58 English, 0.61 Italian).

## Features

| ID | Feature | Area | Status |
|----|---------|------|--------|
| F-AUD-01 | View and filter the audit trail | audit | Done |
| F-AUD-02 | See what changed in an audit entry | audit | Done |
| F-AUTH-01 | Sign in with a code, session and sign out | authentication | Done |
| F-AUTH-02 | First sign-in and invitations | authentication | Done |
| F-AUTH-03 | Navigation, role-based access and keyboard shortcuts | authentication | Done |
| F-AUTH-04 | Language selection | authentication | Done |
| F-DASH-01 | Task counts, latest tasks and link to the task list | dashboard | Done |
| F-DASH-02 | Dashboard greeting | dashboard | Done |
| F-HELP-01 | Read and search the manual | help | Done |
| F-HELP-02 | Help for the current screen | help | Done |
| F-HELP-03 | In-page assistant | help | Done |
| F-HELP-04 | Unanswered questions export | help | Done |
| F-ONBOARD-01 | Organization setup for new users | onboarding | Done |
| F-ORG-01 | Organization details and logo | organizations | Done |
| F-ORG-02 | Team members: view, invite, change role, remove | organizations | Done |
| F-TASK-01 | Create, edit and delete tasks | tasks | Done |
| F-TASK-02 | Change the status of a task | tasks | Done |
| F-TASK-03 | Task list | tasks | Done |
