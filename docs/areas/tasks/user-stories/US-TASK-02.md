# US-TASK-02: Edit a task

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-TASK-01 |
| User input | Yes |

## Story

**As a** team member
**I want** to change the details of a task
**So that** the list stays correct when plans change

## Acceptance Criteria

1. On the Tasks page, the "Edit" button of a row opens the form filled with the values of that task.
2. The form has the fields Title, Due Date, Status and Description.
3. The Status field offers "Todo", "In Progress", "Done" and "Cancelled".
4. Clicking "Update" with the Title field empty keeps the form open and the Title field shows "Enter a title."
5. After updating, the form closes and the row shows the new values.
6. Clicking "Cancel" closes the form and the task keeps its previous values.
7. Viewers do not see the "Edit" button.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | No |

## Business Rules

1. Choosing a status in the form changes the status shown on the row after updating.
2. Updating changes only what was changed in the form; every other detail of the task is kept.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| Another member deleted the task while the form was open | "This task no longer exists." | Continue with the updated list, where the task is no longer shown |
| The user's role was changed to Viewer while the form was open | "Your role does not allow this change." | Ask an admin of the organization for the Editor role |

## Non-Goals

- Changing who the task is assigned to.
- Editing several tasks at once.
- Seeing the history of changes of a task; Admins find it in the Audit Log.

## Questions users ask

### en

1. How can I rename something that is already on the list?
2. Where do I move the deadline of a job?
3. Why did my change disappear after I closed the form?
4. How do I mark a job as abandoned?

### it

1. Come cambio il nome di un'attività già inserita?
2. Posso spostare la scadenza di un lavoro?
3. Ho chiuso il modulo e le modifiche sono sparite, perché?
4. Come segno un lavoro come annullato?
