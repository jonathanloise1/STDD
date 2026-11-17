# Audit Trail - User Stories

## Overview

The audit trail lets the admins of an organization read the history of changes made to its data: who changed what and when, and the values before and after each change. It is read-only. Editors and viewers cannot open it and do not see its menu link.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-AUD-01](user-stories/US-AUD-01.md) | Open the audit trail | High |
| [US-AUD-02](user-stories/US-AUD-02.md) | Filter the audit trail | Medium |
| [US-AUD-03](user-stories/US-AUD-03.md) | See what changed in an entry | High |

## Business Rules

1. Only admins of the organization can open the audit trail; anyone else does not see the menu link and is taken to the Dashboard page with the message "You don't have access to this page."
2. Every creation, change and deletion of the organization, its team members, its tasks and its users is recorded with the name of the person who made it.
3. The history of an organization is kept for one year; older changes are no longer listed.
4. Nobody can edit or delete a recorded change.
5. Types, fields and values are shown with the names used elsewhere in the app ("Task", "Team member", "Title", "In Progress"), and dates and times in the time zone of the user's device.

## UI Flow

1. Audit Log: the admin opens it from the "Audit Log" menu link, reads the table of changes, optionally filters by type and date, and clicks "Details" on a row to open the "Details" dialog.
2. Details: the dialog shows the values before and after the change; "Cancel" returns to the Audit Log page.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Open Questions

None.

## Files

| File | Purpose |
|------|---------|
| [testsuite.md](testsuite.md) | Test index |
| [operations.md](operations.md) | Runbooks |
