# AUTH-LANG-002: Menu entries change language with the app

| Field | Value |
|-------|-------|
| User Story | US-AUTH-08 |
| Type | UX |
| Priority | High |
| Automated by | e2e/AuthLang002Tests |

## Description

The menu entries and the language button follow the language chosen.

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

- After step 3, the menu entries "Dashboard", "Tasks", "Team", "Audit Log" and "Settings" are shown in Italian, and the language button shows "IT".
- After step 5, the menu entries are "Dashboard", "Tasks", "Team", "Audit Log" and "Settings", and the language button shows "EN".
