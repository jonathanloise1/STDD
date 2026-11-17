# AUTH-LOGIN-005: A visitor sees no part of the app before signing in

| Field | Value |
|-------|-------|
| User Story | US-AUTH-02 |
| Type | Business rule |
| Priority | High |
| Automated by | e2e/AuthLogin005Tests |

## Description

Before signing in, a visitor sees only the sign-in page: no menu, no organization name and no data.

## Preconditions

- Not signed in.

## Test Data

None.

## Steps

1. Open the home page.

## Expected Results

- The sign-in page opens.
- The menu entries "Dashboard" and "Tasks" are not shown.
- No organization name is shown.
