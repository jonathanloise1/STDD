# US-ORG-01: See the organization details

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-ORG-01 |
| User input | No |

## Story

**As an** organization admin
**I want** to see the profile and billing details of my organization
**So that** I can check that the company data is correct

## Acceptance Criteria

1. For Admins, the "Settings" menu item opens the Organization page of the current organization.
2. The page is titled "Billing Information" and shows the logo, or the first two letters of the name when there is no logo, with the organization name.
3. Under "Fiscal Information" the page shows the VAT Number and the Fiscal Code.
4. Under "Billing Address" the page shows Email, Address, City with the province in brackets, ZIP Code and Country.
5. A detail that has not been filled in shows "-".
6. The "Edit" button opens the Update Organization page.
7. Editors and Viewers do not see the "Settings" menu item; opening the Organization page by its address takes them to the Dashboard page with the message "You don't have access to this page."

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |
| Editor | No |
| Viewer | No |

## Business Rules

None.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the details were loaded | "The service did not answer. Try again in a few minutes." | Reload the page later |

## Non-Goals

- Deleting the organization.
- Seeing the members of the organization; the Team page does that.
- Billing plans or invoices.

## Questions users ask

### en

1. Where can I check our company VAT number?
2. How do I see the address that is used for billing?
3. Why is there no settings entry in my menu?
4. Where do I find the company details I entered when I signed up?

### it

1. Dove controllo la partita IVA della nostra azienda?
2. Come vedo l'indirizzo usato per la fatturazione?
3. Perché nel mio menu non c'è la voce delle impostazioni?
4. Dove trovo i dati dell'azienda che ho inserito all'inizio?
