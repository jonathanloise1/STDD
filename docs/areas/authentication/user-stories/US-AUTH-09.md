# US-AUTH-09: Move between pages with the keyboard

| Field | Value |
|-------|-------|
| Priority | Low |
| Feature | F-AUTH-03 |
| User input | No |

## Story

**As a** signed-in user who prefers the keyboard
**I want** to open the main pages with a few key presses
**So that** I can move around the app without the mouse

## Acceptance Criteria

1. Pressing G and then D opens the Dashboard page; pressing G and then T opens the Tasks page.
2. For Admins, G and then M opens the Team page, G and then A the Audit Log page, and G and then S the Organization page.
3. For Editors and Viewers, G followed by M, A or S does nothing.
4. Pressing ? opens the "Keyboard Shortcuts" dialog, which lists only the shortcuts the user can use.
5. The shortcuts do nothing while the user is typing in a field or while a dialog is open.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | Yes |
| Viewer | Yes |

## Business Rules

1. Each user has the shortcuts of the pages their role allows: Editors and Viewers only those of the Dashboard and Tasks pages.
2. Keys typed in a field are text, never shortcuts.

## Error States

None.

## Non-Goals

- Choosing other keys for the shortcuts.
- Shortcuts for actions inside a page, such as creating a task.

## Questions users ask

### en

1. Is there a quick way to jump to the task list without clicking?
2. Which keys can I press to move around?
3. Why does pressing G and M do nothing for me?

### it

1. C'è un modo veloce per andare all'elenco attività senza cliccare?
2. Quali tasti posso usare per spostarmi tra le pagine?
3. Perché premendo G e poi M non succede niente?
