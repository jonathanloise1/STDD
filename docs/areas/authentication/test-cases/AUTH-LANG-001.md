# AUTH-LANG-001: Choosing a language switches the app at once

| Field | Value |
|-------|-------|
| User Story | US-AUTH-08 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/AuthLang001Tests |

## Description

A user switches the app to Italian and back to English, and each choice changes the text and shows a message.

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

- After step 3, the menu entry "Tasks" is shown in Italian and a message confirms the change in Italian.
- After step 5, the message "Language changed to English" appears.
- After step 5, the menu entry "Tasks" is shown in English again.
- The page was not reloaded between the steps.
