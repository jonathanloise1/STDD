# DASH-GREET-001: The dashboard greets the user by name

| Field | Value |
|-------|-------|
| User Story | US-DASH-04 |
| Type | Happy path |
| Priority | Low |
| Automated by | e2e/DashGreet001Tests |

## Description

The page starts with a time-of-day greeting, the user name and the overview line.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The Admin is "Dev User" and the language of the user is English.

## Test Data

None.

## Steps

1. Open the Dashboard page.

## Expected Results

- The page starts with "Good morning", "Good afternoon" or "Good evening", followed by a comma and "Dev User".
- Under the greeting the page reads "Here is your task overview".
