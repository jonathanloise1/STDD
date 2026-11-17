# HELP-BROWSE-003: The manual follows the language of the application

| Field | Value |
|-------|-------|
| User Story | US-HELP-01 |
| Type | Business rule |
| Priority | Medium |
| Automated by | e2e/HelpBrowse003Tests |

## Description

After the language of the application is changed to Italian, the manual is shown in Italian.

## Preconditions

- Signed in as Editor of "MyApp Dev Org".
- The language of the user is English.

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Change language" button.
3. Click the "Italiano" button.
4. Open the Manual page.

## Expected Results

- The list of areas shows "Attività" instead of "Tasks".
- The Manual page shows no English page titles.
