# HELP-RATE-005: A visitor cannot rate an answer

| Field | Value |
|-------|-------|
| User Story | US-HELP-06 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpRate005Tests |

## Description

A person who is not signed in cannot reach the question Did this help.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Tasks page.

## Expected Results

- The Sign in page opens.
- The page does not show "Did this help?".
