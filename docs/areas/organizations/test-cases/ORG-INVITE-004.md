# ORG-INVITE-004: An invited person is not listed as a member yet

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/OrgInvite004Tests |

## Description

An invited person appears under Pending Invitations and not in the Members table.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Full Name | "Robin Chen" |
| Email | "robin.chen@example.com" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.
3. Type "Robin Chen" in the "Full Name" field.
4. Type "robin.chen@example.com" in the "Email" field.
5. Click the "Send Invitation" button.

## Expected Results

- "Pending Invitations" lists a row "Robin Chen".
- The "Members" table does not show a row "Robin Chen".
