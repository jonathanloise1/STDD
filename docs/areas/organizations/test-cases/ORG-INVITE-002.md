# ORG-INVITE-002: Sending an empty invitation shows field messages

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | Validation |
| Priority | High |
| Automated by | e2e/OrgInvite002Tests |

## Description

Leaving Full Name and Email empty keeps the dialog open and shows a message under each field.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.
3. Click the "Send Invitation" button.

## Expected Results

- The "Full Name" field shows "Full name is required".
- The "Email" field shows "Email address is required".
- The dialog stays open.
