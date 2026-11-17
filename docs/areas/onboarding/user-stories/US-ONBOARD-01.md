# US-ONBOARD-01: Get asked to set up an organization

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-ONBOARD-01 |
| User input | No |

## Story

**As a** new user who belongs to no organization
**I want** to be guided to set up my organization when I open the app
**So that** I know what to do before I start working

## Acceptance Criteria

1. A signed-in user who belongs to no organization sees the "Set Up Your Organization" page, whichever page of the app they open.
2. The page has one step, "Organization Details", and a "Submit" button.
3. A user who belongs to at least one organization never sees the page; this includes invited people the first time they sign in.
4. The page and its texts are shown in the user's language, English or Italian.

## Access Control

| Role | Allowed |
|------|---------|
| Visitor | No |
| Signed-in user | Yes |

## Business Rules

1. The page is shown as long as, and only while, the user belongs to no organization.

## Error States

None.

## Non-Goals

- Skipping the setup: without an organization there is nothing else to use.
- Choosing a language on the page; the "Change language" button in the header does that.
- Inviting team members on the page.

## Questions users ask

### en

1. Why does the app ask me to set up something when I sign in?
2. What happens the first time I open the app?
3. Can I skip this screen and look around first?
4. I signed in and my colleagues do not get this screen. Why?

### it

1. Perché al primo accesso mi chiede di configurare qualcosa?
2. Cosa succede la prima volta che apro l'applicazione?
3. Posso saltare questa schermata e guardare prima l'app?
4. Ho fatto l'accesso ma ai miei colleghi non compare questa schermata, come mai?
