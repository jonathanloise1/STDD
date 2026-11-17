# US-ORG-02: Edit the organization details

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-ORG-01 |
| User input | Yes |

## Story

**As an** organization admin
**I want** to change the name and billing details of my organization
**So that** contracts and invoices carry the correct company data

## Acceptance Criteria

1. The "Edit" button of the Organization page opens the Update Organization page with the current values.
2. The page has the fields Organization Name, Legal Name, VAT Number, Fiscal Code, Billing Email, Street Address, City, Province, ZIP / Postal Code and Country; all are required.
3. Clicking "Save Changes" with a required field empty shows under that field its message, for example "Organization name is required", and the message "Please fix validation errors before submitting".
4. A Billing Email that is not a valid address shows "Invalid email format".
5. A ZIP / Postal Code that is not exactly five digits shows "ZIP code must be 5 digits".
6. After saving, the message "Organization has been updated successfully" appears and the Organization page shows the new values.
7. Once a value differs from the saved one, the badge "Unsaved Changes" appears next to the page title.
8. Editors and Viewers who open the Update Organization page by its address are taken to the Dashboard page with the message "You don't have access to this page."

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Business Rules

1. The badge "Unsaved Changes" is shown only while the values differ from the saved ones.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the details were saved | "The service did not answer. Try again in a few minutes." | Click "Save Changes" again later; the page keeps the values typed |

## Non-Goals

- Choosing an industry template.
- Checking that the VAT number or fiscal code really exists.

## Questions users ask

### en

1. How do I correct our company name?
2. The page will not let me store the new address, what is wrong?
3. Why is the postcode refused?
4. Can I change the email where invoices are sent?

### it

1. Come correggo la ragione sociale della mia azienda?
2. Non riesco a salvare il nuovo indirizzo, cosa sbaglio?
3. Perché il CAP viene rifiutato?
4. Posso cambiare l'email a cui arrivano le fatture?
