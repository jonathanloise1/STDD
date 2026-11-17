# AUTH-INVITE-001: Signing in joins the organization one was invited to

| Field | Value |
|-------|-------|
| User Story | US-AUTH-05 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/AuthInvite001Tests |

## Description

A person with a pending invitation signs in and is a member of the organization without accepting anything and without the setup page.

## Preconditions

- Not signed in.
- "Test UserC" has a pending invitation to "E2E Invited Org" as Editor and belongs to no other organization.

## Test Data

None.

## Steps

1. Open the home page.
2. Sign in as "Test UserC".

## Expected Results

- The Dashboard page opens.
- The header shows "E2E Invited Org".
- The "Set Up Your Organization" page is not shown.
