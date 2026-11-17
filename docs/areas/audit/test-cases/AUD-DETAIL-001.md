# AUD-DETAIL-001: An edited task shows its old and new title

| Field | Value |
|-------|-------|
| User Story | US-AUD-03 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/AudDetail001Tests |

## Description

After renaming a task, the change shows the old and the new title under the label of the field.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Audit rename check" |
| Title (new) | "Audit rename check Q4" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Audit rename check" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Edit" button on the row of "Audit rename check".
6. Clear the "Title" field.
7. Type "Audit rename check Q4" in the "Title" field.
8. Click the "Update" button.
9. Click the "Audit Log" link in the menu.
10. Click the "Details" button on the first row of the table.

## Expected Results

- The "Changes" column of the first row lists "Title".
- The "Details" dialog shows the action "Update", the type "Task" and the user "Dev User".
- "Old Values" shows "Title" with "Audit rename check".
- "New Values" shows "Title" with "Audit rename check Q4".
