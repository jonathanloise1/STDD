# ORG-INVITE-001: Invite a colleague and see the invitation as pending

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | Happy path |
| Priority | Critical |
| Automated by | e2e/OrgInvite001Tests |

## Description

An admin invites a person as Editor and finds the invitation under Pending Invitations.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Full Name | "Jamie Rivera" |
| Email | "jamie.rivera@example.com" |
| Role | "Editor" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.
3. Type "Jamie Rivera" in the "Full Name" field.
4. Type "jamie.rivera@example.com" in the "Email" field.
5. Check the "Editor" field.
6. Click the "Send Invitation" button.

## Expected Results

- The dialog closes and the message "Invitation sent successfully" appears.
- "Pending Invitations" lists a row "Jamie Rivera", "jamie.rivera@example.com".
