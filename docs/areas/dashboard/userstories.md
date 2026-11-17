# Dashboard - User Stories

## Overview

The Dashboard is the first page a signed-in member of an organization sees. It greets the user, shows how many tasks the organization has in each status and lists the most recently created tasks, with a way to go to the full task list. Admins, Editors and Viewers see the same figures.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-DASH-01](user-stories/US-DASH-01.md) | See my task counts | High |
| [US-DASH-02](user-stories/US-DASH-02.md) | See my latest tasks | High |
| [US-DASH-03](user-stories/US-DASH-03.md) | Open the task list from the dashboard | Medium |
| [US-DASH-04](user-stories/US-DASH-04.md) | Read a greeting on the dashboard | Low |

## Business Rules

1. The Dashboard page opens after signing in for a user who belongs to an organization.
2. A user who belongs to no organization sees the "Set Up Your Organization" page instead (Onboarding area).
3. The figures always describe the organization currently selected, and count all its tasks.
4. A user who opens a page their role does not allow is brought to the Dashboard page with the message "You don't have access to this page." (Authentication area).

## UI Flow

1. Dashboard: the user reads the greeting, the four task cards and the "Recent Tasks" table; "View All" or a click on a row opens the Tasks page.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Open Questions

None.

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
