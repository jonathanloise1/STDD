# ORG-INVITE-006: Inviting an email twice shows a message

| Field | Value |
|-------|-------|
| User Story | US-ORG-05 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/OrgInvite006Tests, integration/OrganizationsOutcomeCodesTests |

## Description

An admin who invites the same email a second time is told that an invitation is already pending.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

| Field | Value |
|-------|-------|
| Full Name | "Sam Okafor" |
| Email | "sam.okafor@example.com" |

## Steps

1. Click the "Team" link in the menu.
2. Click the "Invite" button.
3. Type "Sam Okafor" in the "Full Name" field.
4. Type "sam.okafor@example.com" in the "Email" field.
5. Click the "Send Invitation" button.
6. Click the "Invite" button.
7. Type "Sam Okafor" in the "Full Name" field.
8. Type "sam.okafor@example.com" in the "Email" field.
9. Click the "Send Invitation" button.

## Expected Results

- The message "A pending invitation for this email already exists." appears.
- "Pending Invitations" lists "Sam Okafor" once.

## Observable trace

| Field | Value |
|-------|-------|
| Message | "A pending invitation for this email already exists." |
| Log key | `org.invite.already_invited` |
| Severity | Information |
| Runbook | [org-invite-already-invited](../operations.md#org-invite-already-invited) |
