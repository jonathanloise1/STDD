# Tasks - User Stories

## Overview

The Tasks area lets the members of an organization keep a shared list of work. Admins and Editors create tasks, edit them, move them forward through their statuses and delete them. Viewers read the same list without changing it.

## Stories

| ID | Title | Priority |
|----|-------|----------|
| [US-TASK-01](user-stories/US-TASK-01.md) | Create a task | Critical |
| [US-TASK-02](user-stories/US-TASK-02.md) | Edit a task | High |
| [US-TASK-03](user-stories/US-TASK-03.md) | Change the status of a task | Medium |
| [US-TASK-04](user-stories/US-TASK-04.md) | Delete a task | High |
| [US-TASK-05](user-stories/US-TASK-05.md) | See the list of tasks | Critical |

## Business Rules

1. A title made only of spaces counts as empty and cannot be saved.
2. A task has one of four statuses, shown as "Todo", "In Progress", "Done" and "Cancelled".
3. The list shows the tasks of the current organization only.
4. Admins and Editors create, edit, change the status of and delete tasks; Viewers only read them and do not see the buttons for those actions.

## UI Flow

1. Tasks: the user opens the page from the "Tasks" menu item and sees the table of tasks.
2. Tasks, New Task form: the "New Task" button opens a form above the table; saving or cancelling closes it and the table is shown again.
3. Tasks, edit form: the "Edit" button of a row opens the same form filled with the task and a Status field; saving or cancelling closes it.
4. Tasks, row buttons: the "Change status" button changes the status without leaving the page; the "Delete" button opens the "Delete Task" dialog.
5. Delete Task dialog: "Delete" removes the task and "Cancel" keeps it; both close the dialog.

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
