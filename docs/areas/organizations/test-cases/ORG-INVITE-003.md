# ORG-INVITE-003: An invalid email shows a field message

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | Validation |
| Priority | Medium |
| Automated by | e2e/OrgInvite003Tests |

## Description

An email that is not a valid address keeps the dialog open and shows a message.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Full Name | "Jamie Rivera" |
| Email | "jamie.rivera" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.
3. Type "Jamie Rivera" in the "Full Name" field.
4. Type "jamie.rivera" in the "Email" field.
5. Click the "Send Invitation" button.

## Expected Results

- The "Email" field shows "Please enter a valid email address".
- The dialog stays open.
