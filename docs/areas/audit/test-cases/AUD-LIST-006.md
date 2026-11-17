# AUD-LIST-006: An editor cannot open the audit trail

| Field | Value |
|-------|-------|
| User Story | US-AUD-01 |
| Type | Security |
| Priority | High |
| Automated by | e2e/AudList006Tests |

## Description

An editor has no menu link to the audit trail and is sent to the dashboard, with a message, when opening the page by its address.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the Audit Log page.

## Expected Results

- The Dashboard page opens with the message "You don't have access to this page."
- The menu has no "Audit Log" link.
