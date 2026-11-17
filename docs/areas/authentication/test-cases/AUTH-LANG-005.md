# AUTH-LANG-005: Saving the language when the service does not answer shows a message

| Field | Value |
|-------|-------|
| User Story | US-AUTH-08 |
| Type | Error handling |
| Priority | Medium |
| Automated by | e2e/AuthLang005Tests, integration/AuthenticationOutcomeCodesTests |

## Description

When the service does not answer while the chosen language is saved, the user is told so; the app still switches for the current session.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".
- The language of the user is English.
- The service does not answer when the language choice is saved.

## Test Data

None.

## Steps

1. Open the Dashboard page.
2. Click the "Change language" button.
3. Click the "Italiano" button.

## Expected Results

- The message appears in Italian, the language the app has just switched to: "Il servizio non ha risposto. Riprova tra qualche minuto." (in English: "The service did not answer. Try again in a few minutes.").
- The language button shows "IT".

## Observable trace

| Field | Value |
|-------|-------|
| Message | "The service did not answer. Try again in a few minutes." |
| Log key | `auth.lang.unavailable` |
| Severity | Error |
| Runbook | [auth-lang-unavailable](../operations.md#auth-lang-unavailable) |
