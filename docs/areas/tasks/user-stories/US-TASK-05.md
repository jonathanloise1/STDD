# US-TASK-05: See the list of tasks

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-TASK-03 |
| User input | No |

## Story

**As a** team member
**I want** to see all the tasks of my organization
**So that** I know what the team is working on

## Acceptance Criteria

1. The "Tasks" menu item opens the Tasks page.
2. The page shows a table with the columns Title, Status and Due Date; Admins and Editors also see the column Actions.
3. Each row shows the task title, its description in smaller text below the title, and its status as a label.
4. A task without a due date shows "-" in the Due Date column.
5. When the organization has no tasks, the page shows "No tasks yet"; Admins and Editors also read "Create your first task to get started."
6. While the tasks are loading, a spinner is shown instead of the table.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Business Rules

1. The newest task is at the top of the list.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the tasks were loaded | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Searching or filtering the list.
- Paging: the list shows the 50 newest tasks.
- Seeing who a task is assigned to.
- Seeing tasks of another organization.

## Questions users ask

### en

1. Where can I see everything the team has to do?
2. Why is my list empty?
3. Why can I read the jobs but not change them?
4. I created a job yesterday and do not see it at the top, where is it?

### it

1. Dove vedo tutto quello che il team deve fare?
2. Perché l'elenco è vuoto?
3. Perché vedo i lavori ma non posso modificarli?
4. Non vedo in alto l'attività che ho aggiunto, dove si trova?
