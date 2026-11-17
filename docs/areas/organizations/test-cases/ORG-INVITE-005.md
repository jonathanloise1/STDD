# ORG-INVITE-005: Inviting an existing member shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgInvite005Tests, integration/OrganizationsOutcomeCodesTests |

## Description

An admin who invites the email of a current member is told that the person is already a member.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- "member@myapp.local" is the email of the member "Dev Member".

## Test Data

| Field | Value |
|-------|-------|
| Full Name | "Dev Member" |
| Email | "member@myapp.local" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.
3. Type "Dev Member" in the "Full Name" field.
4. Type "member@myapp.local" in the "Email" field.
5. Click the "Send Invitation" button.

## Expected Results

- The message "User is already a member of this organization." appears.
- "Pending Invitations" does not list "Dev Member".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "User is already a member of this organization." |
| Log key | `org.invite.already_member` |
| Severity | Information |
| Runbook | [org-invite-already-member](../operations.md#org-invite-already-member) |
