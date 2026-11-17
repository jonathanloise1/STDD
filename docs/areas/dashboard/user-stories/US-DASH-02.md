# US-DASH-02: See my latest tasks

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-DASH-01 |
| User input | No |

## Story

**As a** team member
**I want** to see the most recently created tasks on the dashboard
**So that** I can review what needs attention without opening the task list

## Acceptance Criteria

1. The Dashboard page has a "Recent Tasks" section with a table with the columns "Title", "Status" and "Due Date".
2. The status is shown as a colored label reading "Todo", "In Progress", "Done" or "Cancelled".
3. A task without a due date shows "-" in the "Due Date" column.
4. The table shows at most five tasks, the most recently created first.
5. When the organization has no tasks, Admins and Editors read "No tasks yet. Go to Tasks to create your first one." and Viewers read "No tasks yet" instead of the table.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Business Rules

1. The five tasks shown are the five most recently created among all the tasks of the organization, whatever their status.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the recent tasks were loaded | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Choosing how many tasks to show.
- Changing a task from the dashboard.

## Questions users ask

### en

1. Where do I see the newest work items without going to the full list?
2. Why is only a handful of items listed on my home page?
3. What do the colored labels next to each item mean?
4. My home page says there is nothing yet. What should I do?

### it

1. Dove vedo le ultime attività senza aprire l'elenco completo?
2. Perché nella pagina iniziale ci sono solo poche voci?
3. Cosa indicano le etichette colorate accanto a ogni voce?
4. La pagina iniziale dice che non c'è ancora niente, cosa devo fare?
