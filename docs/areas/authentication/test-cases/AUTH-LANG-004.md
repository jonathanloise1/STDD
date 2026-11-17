# AUTH-LANG-004: Only English and Italiano are offered

| Field | Value |
|-------|-------|
| User Story | US-AUTH-08 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/AuthLang004Tests |

## Description

The language menu lists exactly two languages and a user with no choice sees English.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The language of the user is English.

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Change language" button.

## Expected Results

- The language button shows "EN".
- The language menu lists "English" and "Italiano" and no other language.
