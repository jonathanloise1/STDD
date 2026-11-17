# US-AUTH-03: Sign out of the app

| Field | Value |
|-------|-------|
| Priority | High |
| Feature | F-AUTH-01 |
| User input | No |

## Story

**As a** signed-in user
**I want** to sign out
**So that** nobody else can use my account on this device

## Acceptance Criteria

1. The avatar button at the top right opens a menu with "Logout"; for Admins the menu also offers "Settings".
2. Clicking "Logout" signs the user out and the sign-in page opens.
3. After signing out, opening any page of the app asks the user to sign in again.
4. Signing out does not delete the account or the organization's data.

## Access Control

| Role | Allowed |
|------|---------|
| Signed-in user | Yes |

## Business Rules

1. Signing out ends the session on this device: the app cannot be used again until the user signs in.

## Error States

None.

## Non-Goals

- Signing out on all devices at once.
- Asking for confirmation before signing out.

## Questions users ask

### en

1. How do I leave my account on a shared computer?
2. I can't find the button to log out.
3. If I sign out, do I lose my tasks?

### it

1. Come faccio a uscire dal mio account su un computer condiviso?
2. Non trovo il pulsante per disconnettermi.
3. Se esco dall'account perdo le mie attività?
