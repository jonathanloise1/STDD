# AUTH-LANG-003: The chosen language is kept at the next sign-in

| Field | Value |
|-------|-------|
| User Story | US-AUTH-08 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AuthLang003Tests |

## Description

A user who chose Italian and signs in again finds the app in Italian.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The language of the user is English.

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Change language" button.
3. Click the "Italiano" button.
4. Click the "Dev User" button.
5. Click the "Logout" button.
6. Sign in as Admin.
7. Click the "Change language" button.
8. Click the "English" button.

## Expected Results

- After step 6, the Dashboard page opens and the language button shows "IT".
- After step 6, the menu entry "Tasks" is shown in Italian.
- After step 8, the language button shows "EN".
