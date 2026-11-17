# US-TASK-04: Delete a task

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-TASK-01 |
| User input | No |

## Story

**As a** team member
**I want** to remove a task I no longer need
**So that** the list only shows work that matters

## Acceptance Criteria

1. For Admins and Editors, each row of the Tasks page has a "Delete" button.
2. Clicking the "Delete" button opens the "Delete Task" dialog with the question "Are you sure you want to delete this task?"
3. Clicking "Delete" in the dialog closes it and the task is no longer in the list.
4. Clicking "Cancel" in the dialog closes it and the task stays in the list.
5. When the last task is deleted, the page shows "No tasks yet".
6. Viewers do not see the "Delete" button.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | No |

## Business Rules

None.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| Another member deleted the task before the deletion was confirmed | "This task no longer exists." | Continue with the updated list, where the task is no longer shown |

## Non-Goals

- Restoring a deleted task.
- Deleting several tasks at once.

## Questions users ask

### en

1. How do I get rid of a job nobody needs any more?
2. I deleted the wrong item by mistake. Can I get it back?
3. Which button throws away an entry from the team's list?
4. Why does the app ask me if I am sure?

### it

1. Come tolgo dall'elenco un lavoro che non serve più?
2. Ho cancellato per sbaglio un'attività, si può recuperare?
3. Quale pulsante elimina una voce dalla lista del team?
4. Perché mi chiede se sono sicuro prima di cancellare?
