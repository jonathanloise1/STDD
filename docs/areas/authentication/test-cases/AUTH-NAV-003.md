# AUTH-NAV-003: An unknown address shows Page not found

| Field | Value |
|-------|-------|
| User Story | US-AUTH-06 |
| Type | UX |
| Priority | Medium |
| Automated by | e2e/AuthNav003Tests |

## Description

A signed-in user who opens an address that matches no page is told so and can go back to the app.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Open the "Does not exist" page.
2. Click the "Homepage" link.

## Expected Results

- After step 1, "Page not found" is shown, with a "Homepage" link.
- After step 2, the Dashboard page opens.
