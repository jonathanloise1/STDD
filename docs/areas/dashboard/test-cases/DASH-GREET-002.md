# DASH-GREET-002: Italian users read the greeting in Italian

| Field | Value |
|-------|-------|
| User Story | US-DASH-04 |
| Type | UX |
| Priority | Low |
| Automated by | e2e/DashGreet002Tests |

## Description

The greeting follows the language chosen by the user.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The language of the user is English.

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Change language" button.
3. Click the "Italiano" button.
4. Click the "Change language" button.
5. Click the "English" button.

## Expected Results

- After step 3, the greeting and the line "Here is your task overview" are shown in Italian, and the greeting is still followed by a comma and "Dev User".
- After step 5, the line "Here is your task overview" is shown in English again.
