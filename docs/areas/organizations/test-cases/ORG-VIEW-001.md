# ORG-VIEW-001: Settings shows the profile and billing details

| Field | Value |
|-------|-------|
| User Story | US-ORG-01 |
| Type | Happy path |
| Priority | High |
| Automated by | e2e/OrgView001Tests |

## Description

An admin opens Settings and sees the details of the organization.

## Preconditions

- Signed in as Admin of "MyApp Dev Org".

## Test Data

None.

## Steps

1. Click the "Settings" link in the menu.

## Expected Results

- The Organization page opens with the title "Billing Information" and the name "MyApp Dev Org".
- "VAT Number" shows "IT00000000000" and "Fiscal Code" shows "00000000000".
- "Email" shows "dev@myapp.local" and "Address" shows "Via dello Sviluppo 1".
- "City" shows "Milano (MI)", "ZIP Code" shows "20100" and "Country" shows "IT".
- An "Edit" button is shown.
