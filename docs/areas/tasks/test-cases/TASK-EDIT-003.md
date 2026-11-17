# TASK-EDIT-003: Choosing Cancelled in the form changes the row status

| Field | Value |
|-------|-------|
| User Story | US-TASK-02 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/TaskEdit003Tests |

## Description

The status chosen in the edit form is the status shown on the row after updating.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Title | "Order new monitors" |
| Status | "Cancelled" |

## Steps

1. Open the Tasks page.
2. Click the "New Task" button.
3. Type "Order new monitors" in the "Title" field.
4. Click the "Create Task" button.
5. Click the "Edit" button on the row of "Order new monitors".
6. Select "Cancelled" in the "Status" field.
7. Click the "Update" button.

## Expected Results

- The row of "Order new monitors" shows the status "Cancelled".
