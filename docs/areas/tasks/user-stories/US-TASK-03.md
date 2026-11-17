# US-TASK-03: Change the status of a task

| Field | Value |
|-------|-------|
| Priority | Medium |
| Feature | F-TASK-02 |
| User input | No |

## Story

**As a** team member
**I want** to move a task forward with one click
**So that** everyone sees how far the work has come

## Acceptance Criteria

1. For Admins and Editors, each row of the Tasks page starts with a "Change status" button.
2. Clicking the "Change status" button changes the status shown on the row at once.
3. A task with status "Todo" moves to "In Progress", then to "Done"; a task with status "Done" moves back to "Todo".
4. A task with status "Cancelled" moves to "Todo".
5. Changing the status keeps the title, description and due date of the task.
6. Viewers see the status of each task but no "Change status" button.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | No |

## Business Rules

1. The order of the statuses is "Todo", "In Progress", "Done", then "Todo" again.
2. A cancelled task is brought back to "Todo" by the button, never to "In Progress" or "Done" directly.
3. The button changes only the status; every other detail of the task is kept.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| Another member deleted the task before the button was clicked | "This task no longer exists." | Continue with the updated list, where the task is no longer shown |

## Non-Goals

- Choosing any status directly from the list; use the edit form for that.
- Cancelling a task with the button.

## Questions users ask

### en

1. How do I tell the team that I started a job?
2. What happens when I click the little circle next to a job?
3. Can I reopen a job that I marked as finished?
4. The status went back to the beginning after I clicked it, why?

### it

1. Come segnalo al team che ho iniziato un lavoro?
2. Cosa succede se clicco sul cerchietto vicino a un'attività?
3. Posso riaprire un lavoro che avevo segnato come finito?
4. Ho cliccato e lo stato è tornato all'inizio, come mai?
