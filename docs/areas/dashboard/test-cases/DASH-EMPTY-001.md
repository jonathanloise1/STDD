# DASH-EMPTY-001: An organization without tasks shows a hint

| Field | Value |
|-------|-------|
| User Story | US-DASH-02 |
| Type | UX |
| Priority | Medium |
| Automated by | e2e/DashEmpty001Tests |

## Description

With no tasks, the Recent Tasks section tells an admin where to create the first one.

## Preconditions

- Signed in as Admin of "Empty Org".
- "Empty Org" has no tasks.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The "Recent Tasks" section shows "No tasks yet. Go to Tasks to create your first one."
- No table is shown in the section.
