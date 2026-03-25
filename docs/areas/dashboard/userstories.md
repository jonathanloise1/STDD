# Dashboard — User Stories

## Overview

The Dashboard is the landing page after login. It provides a quick overview of the user's tasks and recent activity, with calls-to-action to drive engagement.

| ID | Title | Priority |
|---|---|---|
| US-DASH-01 | View task overview summary | High |
| US-DASH-02 | See recent tasks | High |
| US-DASH-03 | Quick actions from dashboard | Medium |

---

### US-DASH-01 — View Task Overview Summary

**As a** user
**I want to** see a summary of my tasks on the dashboard
**So that** I have a quick overview of my workload

#### Acceptance Criteria

- Dashboard shows KPI cards with: total tasks, todo count, in-progress count, done count.
- KPI cards are displayed in a horizontal row (scrollable carousel on mobile).
- If no tasks exist, an empty state shows with a CTA: "Create your first task".
- Data loads automatically when the page is visited.

#### Access Control

- Any authenticated member of the organization.

#### Business Rules

1. Counts include all tasks in the user's organization.
2. Task statuses: Todo, InProgress, Done.

### Implementation References

| Layer | File |
|-------|------|
| Frontend | `src/frontend/src/pages/company/dashboards/DashboardView.tsx` |

---

### US-DASH-02 — See Recent Tasks

**As a** user
**I want to** see my most recent tasks on the dashboard
**So that** I can quickly review what needs attention

#### Acceptance Criteria

- Dashboard shows a "Recent Tasks" section listing the most recent tasks.
- Each entry shows: task title, status badge, due date.
- Status badges are color-coded (green=Done, yellow=InProgress, red=Todo).
- A "View All" link navigates to the full task list.
- If no tasks exist, a message "No tasks yet" is shown with a CTA to create one.

#### Business Rules

1. Tasks are sorted by creation date descending (most recent first).
2. Maximum 5 entries shown to keep the dashboard compact.

---

### US-DASH-03 — Quick Actions from Dashboard

**As a** user
**I want to** access common actions directly from the dashboard
**So that** I can quickly do what I need without navigating through menus

#### Acceptance Criteria

- Dashboard shows a "View All Tasks" link that navigates to the task list.
- Quick actions are always visible, even when the dashboard has data.

---

### Non-Goals

- Coverage/portfolio analysis (not applicable).
- Expiration tracking (not applicable).
- AI conversation list on dashboard (not applicable).

---

*Last updated: March 2026*