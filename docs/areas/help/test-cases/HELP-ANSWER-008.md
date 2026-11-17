# HELP-ANSWER-008: A visitor cannot reach the assistant

| Field | Value |
|-------|-------|
| User Story | US-HELP-05 |
| Type | Security |
| Priority | High |
| Automated by | e2e/HelpAnswer008Tests |

## Description

A person who is not signed in is asked to sign in and gets no answers.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The Sign in page opens.
- The page shows no "Help" button.
