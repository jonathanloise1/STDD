# US-ONBOARD-02: Create my organization in the wizard

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-ONBOARD-01 |
| User input | Yes |

## Story

**As a** new user
**I want** to enter the basic details of my organization in the wizard
**So that** I have an organization to work in

## Acceptance Criteria

1. The wizard asks for "Organization Name", "Legal Name", "VAT Number" and "Billing Email"; only "Organization Name" is required.
2. Clicking "Submit" with an empty "Organization Name" shows the message "Organization name is required" and nothing is created.
3. While the organization is being created the page reads "Creating your organization...".
4. When it is done the message "Organization created successfully!" appears and the Dashboard page of the new organization opens, with its name in the header.
5. The creator is the Admin of the new organization and sees the "Settings" menu item.
6. A "Legal Name" left empty is filled in with the organization name, and the Country starts as "IT".

## Access Control

| Role | Allowed |
|------|---------|
| Signed-in user | Yes |

## Business Rules

1. An organization name made only of spaces counts as empty.
2. The person who creates the organization becomes its Admin.
3. The remaining fiscal and billing details are completed later in Settings.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the organization was created | "The service did not answer. Try again in a few minutes." | Click "Submit" again later; the wizard keeps what was typed |

## Non-Goals

- Choosing an industry or template.
- Setting a language or fiscal year.
- Creating a second organization: the wizard is only for people who have none.

## Questions users ask

### en

1. How do I add my company when I first start using the app?
2. Which details are mandatory when setting up the organization?
3. It tells me the name is required but I typed something. Why?
4. Can I fill in the VAT number later?

### it

1. Come aggiungo la mia azienda quando comincio a usare l'applicazione?
2. Quali dati sono obbligatori nella configurazione iniziale?
3. Dice che il nome è obbligatorio ma ho scritto degli spazi, perché?
4. La partita IVA posso inserirla più tardi?
