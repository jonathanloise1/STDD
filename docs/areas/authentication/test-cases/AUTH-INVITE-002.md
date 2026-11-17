# AUTH-INVITE-002: An invited member has the invited role

| Field | Value |
|-------|-------|
| User Story | US-AUTH-05 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AuthInvite002Tests |

## Description

A person invited as Editor can create tasks after joining but sees no management pages.

## Preconditions

- Not signed in.
- "Test UserC" has a pending invitation to "E2E Invited Org" as Editor and belongs to no other organization.

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as "Test UserC".
3. Click the "Tasks" link in the menu.

## Expected Results

- The menu shows "Dashboard" and "Tasks" and no "Settings".
- The Tasks page shows the "New Task" button.
