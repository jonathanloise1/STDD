# US-TASK-01: Create a task

| Field | Value |
|-------|-------|
| Priority | Critical |
| Feature | F-TASK-01 |
| User input | Yes |

## Story

**As a** team member
**I want** to add a task with a title
**So that** the team can see what needs to be done

## Acceptance Criteria

1. On the Tasks page, Admins and Editors see the "New Task" button; it opens a form above the list.
2. Clicking "Create Task" with the Title field empty keeps the form open and the Title field shows "Enter a title."

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | No |

## Business Rules

1. A new task always starts with status "Todo".

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The user's role was changed to Viewer while the form was open | "Your role does not allow this change." | Ask an admin of the organization for the Editor role |

## Non-Goals

- Choosing who the task is assigned to.

## Questions users ask

### en

1. How do I add something new to the team's to-do list?
2. Why won't the form let me save when the name is blank?
3. I can't find the button to add work. What am I missing?
4. Can I set a deadline when I make a new item?

### it

1. Come aggiungo un lavoro da fare per il mio team?
2. Perché non mi salva se non scrivo il titolo?
3. Non vedo il pulsante per inserire una nuova attività, perché?
